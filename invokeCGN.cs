using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Security;

namespace MS_SQL_Image_convert
{
    [SecuritySafeCritical]
    public class BcryptInterop
    {
        private const string BCRYPT_AES_ALGORITHM = "AES";
        private const string BCRYPT_CHAINING_MODE = "ChainingMode";
        private const string BCRYPT_CHAIN_MODE_GCM = "ChainingModeGCM";
        private const int STATUS_SUCCESS = 0;

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
        /// Encrypts byte array using AES-GCM with derived key from password
        /// </summary>
        /// <param name="plainData">Data to encrypt</param>
        /// <param name="password">Password for key derivation</param>
        /// <param name="salt">Salt for key derivation (optional, will generate if null)</param>
        /// <returns>Encrypted data with nonce, salt, and tag</returns>
        public static byte[] EncryptAesGcmBytes(byte[] plainData, string password, byte[] salt = null)
        {
            if (plainData == null) throw new ArgumentNullException("plainData");
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException("password");

            // Generate salt if not provided
            if (salt == null)
            {
                salt = new byte[16];
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(salt);
                }
            }

            // Derive 32-byte key from password
            byte[] key;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000))
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

            // Combine salt + nonce + encrypted data for output
            byte[] result = new byte[salt.Length + nonce.Length + encryptedData.Length];
            Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
            Buffer.BlockCopy(nonce, 0, result, salt.Length, nonce.Length);
            Buffer.BlockCopy(encryptedData, 0, result, salt.Length + nonce.Length, encryptedData.Length);

            return result;
        }

        /// <summary>
        /// Decrypts byte array using AES-GCM with derived key from password
        /// </summary>
        /// <param name="encryptedData">Encrypted data with salt, nonce, and tag</param>
        /// <param name="password">Password for key derivation</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptAesGcmBytes(byte[] encryptedData, string password)
        {
            if (encryptedData == null) throw new ArgumentNullException("encryptedData");
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException("password");

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

            // Derive key from password and salt
            byte[] key;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000))
            {
                key = pbkdf2.GetBytes(32);
            }

            return DecryptAesGcmBytes(cipherWithTag, key, nonce);
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

                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
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
                    status = BCryptEncrypt(hKey, plainBytes, plainBytes.Length, ref authInfo,
                        null, 0, null, 0, out cipherLength, 0);
                    if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptEncrypt size failed with status " + status);

                    byte[] cipherText = new byte[cipherLength];

                    // Encrypt
                    int bytesWritten;
                    status = BCryptEncrypt(hKey, plainBytes, plainBytes.Length, ref authInfo,
                        null, 0, cipherText, cipherText.Length, out bytesWritten, 0);
                    if (status != STATUS_SUCCESS) throw new CryptographicException("BCryptEncrypt failed with status " + status);

                    // Combine ciphertext and tag
                    byte[] result = new byte[bytesWritten + tagLength];
                    Buffer.BlockCopy(cipherText, 0, result, 0, bytesWritten);
                    Buffer.BlockCopy(tagBuffer, 0, result, bytesWritten, tagLength);

                    // Convert final result to Base64
                    return Convert.ToBase64String(result);
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
                int encryptedDataLength = cipherText.Length - tagLength;
                byte[] encryptedData = new byte[encryptedDataLength];
                byte[] tag = new byte[tagLength];
                Buffer.BlockCopy(cipherText, 0, encryptedData, 0, encryptedDataLength);
                Buffer.BlockCopy(cipherText, encryptedDataLength, tag, 0, tagLength);

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

                    return Encoding.UTF8.GetString(plainText, 0, bytesWritten);
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
    }
}
