using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Security;
using System.Threading;

namespace MS_SQL_Image_convert
{
    [SecuritySafeCritical]
    public class BcryptInterop
    {
        private const string BCRYPT_AES_ALGORITHM = "AES";
        private const string BCRYPT_CHAINING_MODE = "ChainingMode";
        private const string BCRYPT_CHAIN_MODE_GCM = "ChainingModeGCM";
        private const int STATUS_SUCCESS = 0;

        /// <summary>
        /// Cached key entry with expiration time
        /// </summary>
        private class CachedKeyEntry
        {
            public byte[] Key { get; set; }
            public DateTime ExpiresAt { get; set; }
            
            public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        }

        /// <summary>
        /// Thread-safe cache for derived keys
        /// </summary>
        private static readonly ConcurrentDictionary<string, CachedKeyEntry> _keyCache = 
            new ConcurrentDictionary<string, CachedKeyEntry>();
        
        /// <summary>
        /// Default cache expiration time in minutes
        /// </summary>
        private static readonly int _cacheExpirationMinutes = 30;
        
        /// <summary>
        /// Timer for cache cleanup
        /// </summary>
        private static readonly Timer _cleanupTimer = new Timer(CleanupExpiredKeys, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        [DllImport("bcrypt.dll")]
        private static extern int BCryptOpenAlgorithmProvider(
            out IntPtr phAlgorithm,
            [MarshalAs(UnmanagedType.LPWStr)] string pszAlgId,
            [MarshalAs(UnmanagedType.LPWStr)] string pszImplementation,
            uint dwFlags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptSetProperty(
            IntPtr hObject,
            [MarshalAs(UnmanagedType.LPWStr)] string pszProperty,
            byte[] pbInput,
            int cbInput,
            int dwFlags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptGenerateSymmetricKey(
            IntPtr hAlgorithm,
            out IntPtr phKey,
            IntPtr pbKeyObject,
            int cbKeyObject,
            byte[] pbSecret,
            int cbSecret,
            int dwFlags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptEncrypt(
            IntPtr hKey,
            byte[] pbInput,
            int cbInput,
            ref BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO pPaddingInfo,
            byte[] pbIV,
            int cbIV,
            byte[] pbOutput,
            int cbOutput,
            out int pcbResult,
            int dwFlags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptDecrypt(
            IntPtr hKey,
            byte[] pbInput,
            int cbInput,
            ref BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO pPaddingInfo,
            byte[] pbIV,
            int cbIV,
            byte[] pbOutput,
            int cbOutput,
            out int pcbResult,
            int dwFlags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptDestroyKey(IntPtr hKey);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptCloseAlgorithmProvider(IntPtr hAlgorithm, int dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO
        {
            public int cbSize;
            public int dwInfoVersion;
            public IntPtr pbNonce;
            public int cbNonce;
            public IntPtr pbAuthData;
            public int cbAuthData;
            public IntPtr pbTag;
            public int cbTag;
            public IntPtr pbMacContext;
            public int cbMacContext;
            public int cbAAD;
            public long cbData;
            public int dwFlags;

            public static BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO Initialize()
            {
                return new BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO
                {
                    cbSize = Marshal.SizeOf(typeof(BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO)),
                    dwInfoVersion = 1
                };
            }
        }

        /// <summary>
        /// Cleanup expired keys from cache
        /// </summary>
        private static void CleanupExpiredKeys(object state)
        {
            var expiredKeys = new List<string>();
            
            foreach (var kvp in _keyCache)
            {
                if (kvp.Value.IsExpired)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
            
            foreach (var key in expiredKeys)
            {
                if (_keyCache.TryRemove(key, out var entry))
                {
                    // Clear the key data for security
                    if (entry.Key != null)
                    {
                        Array.Clear(entry.Key, 0, entry.Key.Length);
                    }
                }
            }
        }

        /// <summary>
        /// Derives a key from password and caches it for reuse
        /// </summary>
        /// <param name="password">Password for key derivation</param>
        /// <param name="salt">Salt for key derivation (optional, will generate if null)</param>
        /// <param name="iterations">PBKDF2 iteration count (default: 2000)</param>
        /// <returns>Base64 encoded derived key with salt information</returns>
        public static string DeriveAndCacheKey(string password, byte[] salt = null, int iterations = 2000)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException("password");

            // Generate salt if not provided
            if (salt == null)
            {
                salt = new byte[16];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }
            }

            // Create cache key from password hash and salt
            string cacheKey = CreateCacheKey(password, salt, iterations);
            
            // Check if key is already cached
            if (_keyCache.TryGetValue(cacheKey, out var cachedEntry) && !cachedEntry.IsExpired)
            {
                // Return cached key with salt information
                return EncodeCachedKeyWithSalt(cachedEntry.Key, salt);
            }

            // Derive new key
            byte[] derivedKey;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                derivedKey = pbkdf2.GetBytes(32);
            }

            // Cache the key
            var newEntry = new CachedKeyEntry
            {
                Key = new byte[derivedKey.Length],
                ExpiresAt = DateTime.UtcNow.AddMinutes(_cacheExpirationMinutes)
            };
            Buffer.BlockCopy(derivedKey, 0, newEntry.Key, 0, derivedKey.Length);
            
            _keyCache.AddOrUpdate(cacheKey, newEntry, (key, oldEntry) => 
            {
                // Clear old key for security
                if (oldEntry.Key != null)
                {
                    Array.Clear(oldEntry.Key, 0, oldEntry.Key.Length);
                }
                return newEntry;
            });

            string result = EncodeCachedKeyWithSalt(derivedKey, salt);
            
            // Clear local key copy
            Array.Clear(derivedKey, 0, derivedKey.Length);
            
            return result;
        }

        /// <summary>
        /// Encrypts data using a cached key
        /// </summary>
        /// <param name="plainData">Data to encrypt</param>
        /// <param name="cachedKeyWithSalt">Base64 encoded cached key with salt information</param>
        /// <returns>Encrypted data</returns>
        public static byte[] EncryptWithCachedKey(byte[] plainData, string cachedKeyWithSalt)
        {
            if (plainData == null) throw new ArgumentNullException(nameof(plainData));
            if (cachedKeyWithSalt == null) throw new ArgumentNullException(nameof(cachedKeyWithSalt));
            if (cachedKeyWithSalt == string.Empty) throw new ArgumentException("Parameter cannot be an empty string.", nameof(cachedKeyWithSalt));

            var keyInfo = DecodeCachedKeyWithSalt(cachedKeyWithSalt);
            
            // Generate 12-byte nonce
            byte[] nonce = new byte[12];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(nonce);
            }

            byte[] encryptedData = EncryptAesGcmBytes(plainData, keyInfo.Key, nonce);

            // Combine salt length (4 bytes) + salt + nonce + encrypted data for output
            byte[] result = new byte[4 + keyInfo.Salt.Length + nonce.Length + encryptedData.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(keyInfo.Salt.Length), 0, result, 0, 4);
            Buffer.BlockCopy(keyInfo.Salt, 0, result, 4, keyInfo.Salt.Length);
            Buffer.BlockCopy(nonce, 0, result, 4 + keyInfo.Salt.Length, nonce.Length);
            Buffer.BlockCopy(encryptedData, 0, result, 4 + keyInfo.Salt.Length + nonce.Length, encryptedData.Length);

            // Clear sensitive data
            Array.Clear(keyInfo.Key, 0, keyInfo.Key.Length);
            Array.Clear(nonce, 0, nonce.Length);
            Array.Clear(encryptedData, 0, encryptedData.Length);

            return result;
        }

        /// <summary>
        /// Decrypts data using a cached key
        /// </summary>
        /// <param name="encryptedData">Encrypted data with salt, nonce, and tag</param>
        /// <param name="cachedKeyWithSalt">Base64 encoded cached key with salt information</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptWithCachedKey(byte[] encryptedData, string cachedKeyWithSalt)
        {
            if (encryptedData == null) throw new ArgumentNullException("encryptedData");
            if (string.IsNullOrEmpty(cachedKeyWithSalt)) throw new ArgumentNullException("cachedKeyWithSalt");

            var keyInfo = DecodeCachedKeyWithSalt(cachedKeyWithSalt);

            const int nonceLength = 12;
            const int tagLength = 16;
            const int headerLength = 4; // 4 bytes to store salt length
            
            if (encryptedData.Length < headerLength + nonceLength + tagLength)
                throw new ArgumentException("Encrypted data too short", "encryptedData");

            // Extract salt length from the header
            int saltLength = BitConverter.ToInt32(encryptedData, 0);
            if (saltLength <= 0 || encryptedData.Length < headerLength + saltLength + nonceLength + tagLength)
                throw new ArgumentException("Invalid salt length in encrypted data", "encryptedData");

            // Verify salt matches
            byte[] dataSalt = new byte[saltLength];
            Buffer.BlockCopy(encryptedData, headerLength, dataSalt, 0, saltLength);
            
            bool saltMatches = dataSalt.Length == keyInfo.Salt.Length;
            if (saltMatches)
            {
                for (int i = 0; i < dataSalt.Length; i++)
                {
                    if (dataSalt[i] != keyInfo.Salt[i])
                    {
                        saltMatches = false;
                        break;
                    }
                }
            }
            
            if (!saltMatches)
                throw new ArgumentException("Salt in encrypted data does not match cached key salt", "encryptedData");

            byte[] nonce = new byte[nonceLength];
            byte[] cipherWithTag = new byte[encryptedData.Length - headerLength - saltLength - nonceLength];

            Buffer.BlockCopy(encryptedData, headerLength + saltLength, nonce, 0, nonceLength);
            Buffer.BlockCopy(encryptedData, headerLength + saltLength + nonceLength, cipherWithTag, 0, cipherWithTag.Length);

            byte[] result = DecryptAesGcmBytes(cipherWithTag, keyInfo.Key, nonce);

            // Clear sensitive data
            Array.Clear(keyInfo.Key, 0, keyInfo.Key.Length);
            Array.Clear(dataSalt, 0, dataSalt.Length);
            Array.Clear(nonce, 0, nonce.Length);
            Array.Clear(cipherWithTag, 0, cipherWithTag.Length);

            return result;
        }

        #region Helper Methods for Key Caching

        /// <summary>
        /// Information about cached key and salt
        /// </summary>
        private class CachedKeyInfo
        {
            public byte[] Key { get; set; }
            public byte[] Salt { get; set; }
        }

        /// <summary>
        /// Creates a cache key from password, salt and iterations
        /// </summary>
        private static string CreateCacheKey(string password, byte[] salt, int iterations)
        {
            using (var sha256 = SHA256.Create())
            {
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var iterationBytes = BitConverter.GetBytes(iterations);
                var combined = new byte[passwordBytes.Length + salt.Length + iterationBytes.Length];
                
                Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
                Buffer.BlockCopy(salt, 0, combined, passwordBytes.Length, salt.Length);
                Buffer.BlockCopy(iterationBytes, 0, combined, passwordBytes.Length + salt.Length, iterationBytes.Length);
                
                var hash = sha256.ComputeHash(combined);
                
                // Clear sensitive data
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
                Array.Clear(combined, 0, combined.Length);
                
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Encodes cached key with salt information for returning to caller
        /// </summary>
        private static string EncodeCachedKeyWithSalt(byte[] key, byte[] salt)
        {
            var combined = new byte[4 + salt.Length + key.Length]; // 4 bytes for salt length
            Buffer.BlockCopy(BitConverter.GetBytes(salt.Length), 0, combined, 0, 4);
            Buffer.BlockCopy(salt, 0, combined, 4, salt.Length);
            Buffer.BlockCopy(key, 0, combined, 4 + salt.Length, key.Length);
            
            return Convert.ToBase64String(combined);
        }

        /// <summary>
        /// Decodes cached key with salt information
        /// </summary>
        private static CachedKeyInfo DecodeCachedKeyWithSalt(string cachedKeyWithSalt)
        {
            var combined = Convert.FromBase64String(cachedKeyWithSalt);
            
            if (combined.Length < 4)
                throw new ArgumentException("Invalid cached key format", "cachedKeyWithSalt");
            
            int saltLength = BitConverter.ToInt32(combined, 0);
            if (saltLength <= 0 || combined.Length < 4 + saltLength + 32) // 32 bytes for AES-256 key
                throw new ArgumentException("Invalid cached key format", "cachedKeyWithSalt");
            
            var salt = new byte[saltLength];
            var key = new byte[32]; // AES-256 key
            
            Buffer.BlockCopy(combined, 4, salt, 0, saltLength);
            Buffer.BlockCopy(combined, 4 + saltLength, key, 0, 32);
            
            // Clear the combined array for security
            Array.Clear(combined, 0, combined.Length);
            
            return new CachedKeyInfo { Key = key, Salt = salt };
        }

        #endregion

        /// <summary>
        /// Encrypts string using AES-GCM with password-based key derivation
        /// </summary>
        /// <param name="plainText">Text to encrypt</param>
        /// <param name="password">Password for key derivation</param>
        /// <param name="salt">Salt for key derivation (optional, will generate if null)</param>
        /// <param name="iterations">PBKDF2 iteration count (default: 2000)</param>
        /// <returns>Base64 encoded encrypted data with salt, nonce, and tag</returns>
        public static string EncryptAesGcmWithPassword(string plainText, string password, byte[] salt = null, int iterations = 2000)
        {
            if (plainText == null) throw new ArgumentNullException("plainText");
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException("password");

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = EncryptAesGcmBytes(plainBytes, password, salt, iterations);
            
            // Clear sensitive data
            Array.Clear(plainBytes, 0, plainBytes.Length);
            
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Decrypts string using AES-GCM with password-based key derivation
        /// </summary>
        /// <param name="base64EncryptedData">Base64 encoded encrypted data</param>
        /// <param name="password">Password for key derivation</param>
        /// <param name="iterations">PBKDF2 iteration count (default: 2000)</param>
        /// <returns>Decrypted text</returns>
        public static string DecryptAesGcmWithPassword(string base64EncryptedData, string password, int iterations = 2000)
        {
            if (string.IsNullOrEmpty(base64EncryptedData)) throw new ArgumentNullException("base64EncryptedData");
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException("password");

            byte[] encryptedBytes = Convert.FromBase64String(base64EncryptedData);
            byte[] decryptedBytes = DecryptAesGcmBytes(encryptedBytes, password, iterations);
            
            string result = Encoding.UTF8.GetString(decryptedBytes);
            
            // Clear sensitive data
            Array.Clear(encryptedBytes, 0, encryptedBytes.Length);
            Array.Clear(decryptedBytes, 0, decryptedBytes.Length);
            
            return result;
        }

        /// <summary>
        /// Encrypts byte array using AES-GCM with password-based key derivation
        /// </summary>
        /// <param name="plainData">Data to encrypt</param>
        /// <param name="password">Password for key derivation</param>
        /// <param name="salt">Salt for key derivation (optional, will generate if null)</param>
        /// <param name="iterations">PBKDF2 iteration count (default: 2000)</param>
        /// <returns>Encrypted data with salt, nonce, and tag</returns>
        public static byte[] EncryptAesGcmBytes(byte[] plainData, string password, byte[] salt = null, int iterations = 2000)
        {
            if (plainData == null) throw new ArgumentNullException("plainData");
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException("password");

            // Generate salt if not provided
            if (salt == null)
            {
                salt = new byte[16];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }
            }

            // Derive 32-byte key from password
            byte[] key;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                key = pbkdf2.GetBytes(32);
            }

            // Generate 12-byte nonce
            byte[] nonce = new byte[12];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(nonce);
            }

            byte[] encryptedData = EncryptAesGcmBytes(plainData, key, nonce);

            // Combine salt length (4 bytes) + salt + nonce + encrypted data for output
            byte[] result = new byte[4 + salt.Length + nonce.Length + encryptedData.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(salt.Length), 0, result, 0, 4);
            Buffer.BlockCopy(salt, 0, result, 4, salt.Length);
            Buffer.BlockCopy(nonce, 0, result, 4 + salt.Length, nonce.Length);
            Buffer.BlockCopy(encryptedData, 0, result, 4 + salt.Length + nonce.Length, encryptedData.Length);

            // Clear sensitive data
            Array.Clear(key, 0, key.Length);
            Array.Clear(nonce, 0, nonce.Length);
            Array.Clear(encryptedData, 0, encryptedData.Length);

            return result;
        }

        /// <summary>
        /// Decrypts byte array using AES-GCM with password-based key derivation
        /// </summary>
        /// <param name="encryptedData">Encrypted data with salt, nonce, and tag</param>
        /// <param name="password">Password for key derivation</param>
        /// <param name="iterations">PBKDF2 iteration count (default: 2000)</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptAesGcmBytes(byte[] encryptedData, string password, int iterations = 2000)
        {
            if (encryptedData == null) throw new ArgumentNullException("encryptedData");
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException("password");

            // Check if this is the new format (has salt length header)
            if (encryptedData.Length >= 4)
            {
                int saltLength = BitConverter.ToInt32(encryptedData, 0);
                // If salt length is reasonable (between 8 and 64 bytes), assume new format
                if (saltLength >= 8 && saltLength <= 64)
                {
                    return DecryptAesGcmBytesNewFormat(encryptedData, password, iterations);
                }
            }

            // Fall back to old format (fixed 16-byte salt)
            return DecryptAesGcmBytesOldFormat(encryptedData, password, iterations);
        }

        /// <summary>
        /// Decrypts byte array using AES-GCM with password-based key derivation (new format with variable salt)
        /// </summary>
        /// <param name="encryptedData">Encrypted data with salt length header, salt, nonce, and tag</param>
        /// <param name="password">Password for key derivation</param>
        /// <param name="iterations">PBKDF2 iteration count (default: 2000)</param>
        /// <returns>Decrypted data</returns>
        private static byte[] DecryptAesGcmBytesNewFormat(byte[] encryptedData, string password, int iterations = 2000)
        {
            const int nonceLength = 12;
            const int tagLength = 16;
            const int headerLength = 4; // 4 bytes to store salt length
            if (encryptedData.Length < headerLength + nonceLength + tagLength)
                throw new ArgumentException("Encrypted data too short", "encryptedData");
            // Extract salt length from the header
            int saltLength = BitConverter.ToInt32(encryptedData, 0);
            if (saltLength <= 0 || encryptedData.Length < headerLength + saltLength + nonceLength + tagLength)
                throw new ArgumentException("Invalid salt length in encrypted data", "encryptedData");
            byte[] salt = new byte[saltLength];
            byte[] nonce = new byte[nonceLength];
            byte[] cipherWithTag = new byte[encryptedData.Length - headerLength - saltLength - nonceLength];
            byte[] key = null;
            Buffer.BlockCopy(encryptedData, headerLength, salt, 0, saltLength);
            Buffer.BlockCopy(encryptedData, headerLength + saltLength, nonce, 0, nonceLength);
            Buffer.BlockCopy(encryptedData, headerLength + saltLength + nonceLength, cipherWithTag, 0, cipherWithTag.Length);
            // Derive key and decrypt
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                key = pbkdf2.GetBytes(32);
            }
            byte[] result = DecryptAesGcmBytes(cipherWithTag, key, nonce);
            // Clear sensitive data
            Array.Clear(salt, 0, salt.Length);
            Array.Clear(nonce, 0, nonce.Length);
            Array.Clear(cipherWithTag, 0, cipherWithTag.Length);
            Array.Clear(key, 0, key.Length);
            return result;
        }

        /// <summary>
        /// Decrypts byte array using AES-GCM with password-based key derivation (old format with fixed 16-byte salt)
        /// </summary>
        /// <param name="encryptedData">Encrypted data with fixed 16-byte salt, nonce, and tag</param>
        /// <param name="password">Password for key derivation</param>
        /// <param name="iterations">PBKDF2 iteration count (default: 2000)</param>
        /// <returns>Decrypted data</returns>
        private static byte[] DecryptAesGcmBytesOldFormat(byte[] encryptedData, string password, int iterations = 2000)
        {
            const int saltLength = 16;
            const int nonceLength = 12;
            const int tagLength = 16;

            if (encryptedData.Length < saltLength + nonceLength + tagLength)
                throw new ArgumentException("Encrypted data too short", "encryptedData");

            // Extract salt, nonce, and encrypted data
            byte[] salt = new byte[saltLength];
            byte[] nonce = new byte[nonceLength];
            byte[] cipherWithTag = new byte[encryptedData.Length - saltLength - nonceLength];

            Buffer.BlockCopy(encryptedData, 0, salt, 0, saltLength);
            Buffer.BlockCopy(encryptedData, saltLength, nonce, 0, nonceLength);
            Buffer.BlockCopy(encryptedData, saltLength + nonceLength, cipherWithTag, 0, cipherWithTag.Length);

            // Derive key from password and salt (using old SHA1 for compatibility)
            byte[] key;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA1))
            {
                key = pbkdf2.GetBytes(32);
            }

            byte[] result = DecryptAesGcmBytes(cipherWithTag, key, nonce);
            
            // Clear sensitive data
            Array.Clear(salt, 0, salt.Length);
            Array.Clear(nonce, 0, nonce.Length);
            Array.Clear(cipherWithTag, 0, cipherWithTag.Length);
            Array.Clear(key, 0, key.Length);
            
            return result;
        }

        /// <summary>
        /// Encrypts byte array using AES-GCM with provided key and nonce
        /// </summary>
        /// <param name="plainData">Data to encrypt</param>
        /// <param name="key">32-byte encryption key</param>
        /// <param name="nonce">12-byte nonce</param>
        /// <returns>Encrypted data with authentication tag</returns>
        public static byte[] EncryptAesGcmBytes(byte[] plainData, byte[] key, byte[] nonce)
        {
            if (plainData == null) throw new ArgumentNullException("plainData");
            if (key == null) throw new ArgumentNullException("key");
            if (nonce == null) throw new ArgumentNullException("nonce");

            if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes", "key");
            if (nonce.Length != 12) throw new ArgumentException("Nonce must be 12 bytes", "nonce");

            IntPtr hAlg = IntPtr.Zero;
            IntPtr hKey = IntPtr.Zero;

            try
            {
                // Initialize algorithm provider
                int status = BCryptOpenAlgorithmProvider(out hAlg, BCRYPT_AES_ALGORITHM, null, 0);
                if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptOpenAlgorithmProvider failed with status " + status);

                // Set GCM mode
                status = BCryptSetProperty(hAlg, BCRYPT_CHAINING_MODE, 
                    Encoding.Unicode.GetBytes(BCRYPT_CHAIN_MODE_GCM), 
                    Encoding.Unicode.GetBytes(BCRYPT_CHAIN_MODE_GCM).Length, 0);
                if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptSetProperty failed with status " + status);

                // Generate key
                status = BCryptGenerateSymmetricKey(hAlg, out hKey, IntPtr.Zero, 0, key, key.Length, 0);
                if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptGenerateSymmetricKey failed with status " + status);

                const int tagLength = 16;  // GCM tag length

                var authInfo = BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO.Initialize();
                var nonceHandle = GCHandle.Alloc(nonce, GCHandleType.Pinned);
                var tagBuffer = new byte[tagLength];
                var tagHandle = GCHandle.Alloc(tagBuffer, GCHandleType.Pinned);

                try
                {
                    authInfo.pbNonce = nonceHandle.AddrOfPinnedObject();
                    authInfo.cbNonce = nonce.Length;
                    authInfo.pbTag = tagHandle.AddrOfPinnedObject();
                    authInfo.cbTag = tagLength;

                    // Get required size
                    int cipherLength;
                    status = BCryptEncrypt(hKey, plainData, plainData.Length, ref authInfo,
                        null, 0, null, 0, out cipherLength, 0);
                    if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptEncrypt size failed with status " + status);

                    byte[] cipherText = new byte[cipherLength];

                    // Encrypt
                    int bytesWritten;
                    status = BCryptEncrypt(hKey, plainData, plainData.Length, ref authInfo,
                        null, 0, cipherText, cipherText.Length, out bytesWritten, 0);
                    if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptEncrypt failed with status " + status);

                    // Combine ciphertext and tag
                    byte[] result = new byte[bytesWritten + tagLength];
                    Buffer.BlockCopy(cipherText, 0, result, 0, bytesWritten);
                    Buffer.BlockCopy(tagBuffer, 0, result, bytesWritten, tagLength);

                    // Clear sensitive data
                    Array.Clear(cipherText, 0, cipherText.Length);
                    Array.Clear(tagBuffer, 0, tagBuffer.Length);

                    return result;
                }
                finally
                {
                    if (nonceHandle.IsAllocated) nonceHandle.Free();
                    if (tagHandle.IsAllocated) tagHandle.Free();
                }
            }
            finally
            {
                if (hKey != IntPtr.Zero) BCryptDestroyKey(hKey);
                if (hAlg != IntPtr.Zero) BCryptCloseAlgorithmProvider(hAlg, 0);
            }
        }

        /// <summary>
        /// Decrypts byte array using AES-GCM with provided key and nonce
        /// </summary>
        /// <param name="cipherWithTag">Encrypted data with authentication tag</param>
        /// <param name="key">32-byte decryption key</param>
        /// <param name="nonce">12-byte nonce</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptAesGcmBytes(byte[] cipherWithTag, byte[] key, byte[] nonce)
        {
            if (cipherWithTag == null) throw new ArgumentNullException("cipherWithTag");
            if (key == null) throw new ArgumentNullException("key");
            if (nonce == null) throw new ArgumentNullException("nonce");

            if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes", "key");
            if (nonce.Length != 12) throw new ArgumentException("Nonce must be 12 bytes", "nonce");

            const int tagLength = 16;
            if (cipherWithTag.Length < tagLength)
                throw new ArgumentException("Encrypted data too short", "cipherWithTag");

            IntPtr hAlg = IntPtr.Zero;
            IntPtr hKey = IntPtr.Zero;

            try
            {
                // Initialize algorithm provider
                int status = BCryptOpenAlgorithmProvider(out hAlg, BCRYPT_AES_ALGORITHM, null, 0);
                if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptOpenAlgorithmProvider failed with status " + status);

                // Set GCM mode
                status = BCryptSetProperty(hAlg, BCRYPT_CHAINING_MODE,
                    Encoding.Unicode.GetBytes(BCRYPT_CHAIN_MODE_GCM),
                    Encoding.Unicode.GetBytes(BCRYPT_CHAIN_MODE_GCM).Length, 0);
                if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptSetProperty failed with status " + status);

                // Generate key
                status = BCryptGenerateSymmetricKey(hAlg, out hKey, IntPtr.Zero, 0, key, key.Length, 0);
                if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptGenerateSymmetricKey failed with status " + status);

                // Separate ciphertext and tag
                int encryptedDataLength = cipherWithTag.Length - tagLength;
                byte[] encryptedData = new byte[encryptedDataLength];
                byte[] tag = new byte[tagLength];
                Buffer.BlockCopy(cipherWithTag, 0, encryptedData, 0, encryptedDataLength);
                Buffer.BlockCopy(cipherWithTag, encryptedDataLength, tag, 0, tagLength);

                var authInfo = BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO.Initialize();
                var nonceHandle = GCHandle.Alloc(nonce, GCHandleType.Pinned);
                var tagHandle = GCHandle.Alloc(tag, GCHandleType.Pinned);

                try
                {
                    authInfo.pbNonce = nonceHandle.AddrOfPinnedObject();
                    authInfo.cbNonce = nonce.Length;
                    authInfo.pbTag = tagHandle.AddrOfPinnedObject();
                    authInfo.cbTag = tagLength;

                    // Get required size
                    int plainTextLength;
                    status = BCryptDecrypt(hKey, encryptedData, encryptedData.Length, ref authInfo,
                        null, 0, null, 0, out plainTextLength, 0);
                    if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptDecrypt size failed with status " + status);

                    byte[] plainText = new byte[plainTextLength];

                    // Decrypt
                    int bytesWritten;
                    status = BCryptDecrypt(hKey, encryptedData, encryptedData.Length, ref authInfo,
                        null, 0, plainText, plainText.Length, out bytesWritten, 0);
                    if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptDecrypt failed with status " + status);

                    byte[] result = new byte[bytesWritten];
                    Buffer.BlockCopy(plainText, 0, result, 0, bytesWritten);

                    // Clear sensitive data
                    Array.Clear(encryptedData, 0, encryptedData.Length);
                    Array.Clear(tag, 0, tag.Length);
                    Array.Clear(plainText, 0, plainText.Length);

                    return result;
                }
                finally
                {
                    if (nonceHandle.IsAllocated) nonceHandle.Free();
                    if (tagHandle.IsAllocated) tagHandle.Free();
                }
            }
            finally
            {
                if (hKey != IntPtr.Zero) BCryptDestroyKey(hKey);
                if (hAlg != IntPtr.Zero) BCryptCloseAlgorithmProvider(hAlg, 0);
            }
        }

        /// <summary>
        /// Encrypts string using AES-GCM with provided key and nonce (legacy method)
        /// </summary>
        /// <param name="plainText">Text to encrypt</param>
        /// <param name="base64Key">Base64 encoded 32-byte key</param>
        /// <param name="base64Nonce">Base64 encoded 12-byte nonce</param>
        /// <returns>Base64 encoded encrypted data with authentication tag</returns>
        public static string EncryptAesGcm(string plainText, string base64Key, string base64Nonce)
        {
            if (plainText == null) throw new ArgumentNullException("plainText");
            if (string.IsNullOrEmpty(base64Key)) throw new ArgumentNullException("base64Key");
            if (string.IsNullOrEmpty(base64Nonce)) throw new ArgumentNullException("base64Nonce");

            // Convert Base64 strings to byte arrays
            byte[] key = Convert.FromBase64String(base64Key);
            byte[] nonce = Convert.FromBase64String(base64Nonce);

            if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes", "base64Key");
            if (nonce.Length != 12) throw new ArgumentException("Nonce must be 12 bytes", "base64Nonce");

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = EncryptAesGcmBytes(plainBytes, key, nonce);
            
            // Clear sensitive data
            Array.Clear(key, 0, key.Length);
            Array.Clear(nonce, 0, nonce.Length);
            Array.Clear(plainBytes, 0, plainBytes.Length);
            
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Decrypts string using AES-GCM with provided key and nonce (legacy method)
        /// </summary>
        /// <param name="base64CipherText">Base64 encoded encrypted data</param>
        /// <param name="base64Key">Base64 encoded 32-byte key</param>
        /// <param name="base64Nonce">Base64 encoded 12-byte nonce</param>
        /// <returns>Decrypted text</returns>
        public static string DecryptAesGcm(string base64CipherText, string base64Key, string base64Nonce)
        {
            if (string.IsNullOrEmpty(base64CipherText)) throw new ArgumentNullException("base64CipherText");
            if (string.IsNullOrEmpty(base64Key)) throw new ArgumentNullException("base64Key");
            if (string.IsNullOrEmpty(base64Nonce)) throw new ArgumentNullException("base64Nonce");

            // Convert Base64 strings to byte arrays
            byte[] cipherText = Convert.FromBase64String(base64CipherText);
            byte[] key = Convert.FromBase64String(base64Key);
            byte[] nonce = Convert.FromBase64String(base64Nonce);

            if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes", "base64Key");
            if (nonce.Length != 12) throw new ArgumentException("Nonce must be 12 bytes", "base64Nonce");

            const int tagLength = 16;
            if (cipherText.Length < tagLength)
                throw new ArgumentException("Encrypted data too short", "base64CipherText");

            byte[] decryptedBytes = DecryptAesGcmBytes(cipherText, key, nonce);
            string result = Encoding.UTF8.GetString(decryptedBytes);
            
            // Clear sensitive data
            Array.Clear(cipherText, 0, cipherText.Length);
            Array.Clear(key, 0, key.Length);
            Array.Clear(nonce, 0, nonce.Length);
            Array.Clear(decryptedBytes, 0, decryptedBytes.Length);
            
            return result;
        }
    }
}
