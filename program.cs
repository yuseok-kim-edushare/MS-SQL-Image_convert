using System;
using System.Data.SqlTypes;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.SqlServer.Server;

namespace MS_SQL_Image_convert
{
    /// <summary>
    /// SQL CLR Assembly for image format conversion, resizing, and encryption
    /// </summary>
    public class ImageFunctions
    {
        /// <summary>
        /// Converts an image from any format to JPG
        /// </summary>
        [SqlFunction(IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static SqlBytes ConvertToJpg(SqlBytes imageData, SqlInt32 quality)
        {
            if (imageData.IsNull || imageData.Value.Length == 0)
                return SqlBytes.Null;

            int jpegQuality = quality.IsNull ? 85 : quality.Value;
            jpegQuality = Math.Max(1, Math.Min(100, jpegQuality));

            try
            {
                using (MemoryStream inputStream = new MemoryStream(imageData.Value))
                using (Image originalImage = Image.FromStream(inputStream))
                using (MemoryStream outputStream = new MemoryStream())
                {
                    EncoderParameters encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)jpegQuality);
                    
                    ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");
                    originalImage.Save(outputStream, jpegCodec, encoderParams);
                    
                    return new SqlBytes(outputStream.ToArray());
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error converting image to JPG: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts an image from any format to PNG
        /// </summary>
        [SqlFunction(IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static SqlBytes ConvertToPng(SqlBytes imageData)
        {
            if (imageData.IsNull || imageData.Value.Length == 0)
                return SqlBytes.Null;

            try
            {
                using (MemoryStream inputStream = new MemoryStream(imageData.Value))
                using (Image originalImage = Image.FromStream(inputStream))
                using (MemoryStream outputStream = new MemoryStream())
                {
                    originalImage.Save(outputStream, ImageFormat.Png);
                    return new SqlBytes(outputStream.ToArray());
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error converting image to PNG: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resizes an image to specified dimensions
        /// </summary>
        [SqlFunction(IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static SqlBytes ResizeImage(SqlBytes imageData, SqlInt32 width, SqlInt32 height, SqlBoolean maintainAspectRatio)
        {
            if (imageData.IsNull || imageData.Value.Length == 0)
                return SqlBytes.Null;

            if (width.IsNull || height.IsNull)
                throw new ArgumentException("Width and height must be specified");

            int targetWidth = width.Value;
            int targetHeight = height.Value;
            bool keepAspectRatio = !maintainAspectRatio.IsNull && maintainAspectRatio.Value;

            try
            {
                using (MemoryStream inputStream = new MemoryStream(imageData.Value))
                using (Image originalImage = Image.FromStream(inputStream))
                {
                    if (keepAspectRatio)
                    {
                        double ratioX = (double)targetWidth / originalImage.Width;
                        double ratioY = (double)targetHeight / originalImage.Height;
                        double ratio = Math.Min(ratioX, ratioY);
                        
                        targetWidth = (int)(originalImage.Width * ratio);
                        targetHeight = (int)(originalImage.Height * ratio);
                    }

                    using (Bitmap resizedImage = new Bitmap(targetWidth, targetHeight))
                    using (Graphics graphics = Graphics.FromImage(resizedImage))
                    {
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.DrawImage(originalImage, 0, 0, targetWidth, targetHeight);

                        using (MemoryStream outputStream = new MemoryStream())
                        {
                            // Preserve the original format if possible
                            ImageFormat format = originalImage.RawFormat;
                            if (format.Equals(ImageFormat.MemoryBmp))
                                format = ImageFormat.Png;
                            
                            resizedImage.Save(outputStream, format);
                            return new SqlBytes(outputStream.ToArray());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error resizing image: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reduces image size by applying compression and optionally resizing
        /// </summary>
        [SqlFunction(IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static SqlBytes ReduceImageSize(SqlBytes imageData, SqlInt32 maxSizeKB, SqlInt32 jpegQuality)
        {
            if (imageData.IsNull || imageData.Value.Length == 0)
                return SqlBytes.Null;

            int targetSizeKB = maxSizeKB.IsNull ? 100 : maxSizeKB.Value;
            int quality = jpegQuality.IsNull ? 85 : jpegQuality.Value;
            quality = Math.Max(1, Math.Min(100, quality));

            try
            {
                byte[] currentData = imageData.Value;

                // If already smaller than target, return as-is
                if (currentData.Length <= targetSizeKB * 1024)
                {
                    // Check if the image is already JPEG. If not, convert it.
                    using (var ms = new MemoryStream(currentData))
                    using (var img = Image.FromStream(ms))
                    {
                        if (!img.RawFormat.Equals(ImageFormat.Jpeg))
                        {
                            return new SqlBytes(CompressImage(img, quality));
                        }
                    }
                    return imageData;
                }

                using (MemoryStream inputStream = new MemoryStream(currentData))
                using (Image originalImage = Image.FromStream(inputStream))
                {
                    int currentWidth = originalImage.Width;
                    int currentHeight = originalImage.Height;

                    // Try compression first (converts to JPEG)
                    byte[] compressedData = CompressImage(originalImage, quality);

                    // If still too large, progressively reduce dimensions
                    while (compressedData.Length > targetSizeKB * 1024 && currentWidth > 100 && currentHeight > 100)
                    {
                        currentWidth = (int)(currentWidth * 0.9);
                        currentHeight = (int)(currentHeight * 0.9);

                        using (Bitmap resizedImage = new Bitmap(currentWidth, currentHeight))
                        using (Graphics graphics = Graphics.FromImage(resizedImage))
                        {
                            graphics.CompositingQuality = CompositingQuality.HighQuality;
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.SmoothingMode = SmoothingMode.HighQuality;
                            graphics.DrawImage(originalImage, 0, 0, currentWidth, currentHeight);

                            compressedData = CompressImage(resizedImage, quality);
                        }
                    }

                    return new SqlBytes(compressedData);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reducing image size: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Encrypts image data using AES-GCM encryption
        /// </summary>
        [SqlFunction(IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static SqlBytes EncryptImage(SqlBytes imageData, SqlString password)
        {
            if (imageData.IsNull || imageData.Value.Length == 0)
                return SqlBytes.Null;

            if (password.IsNull || string.IsNullOrEmpty(password.Value))
                throw new ArgumentException("Password cannot be null or empty");

            try
            {
                // Use AES-GCM encryption for better security and performance
                byte[] encryptedData = BcryptInterop.EncryptAesGcmBytes(imageData.Value, password.Value);
                return new SqlBytes(encryptedData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error encrypting image: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Decrypts image data using AES-GCM decryption
        /// </summary>
        [SqlFunction(IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static SqlBytes DecryptImage(SqlBytes encryptedData, SqlString password)
        {
            if (encryptedData.IsNull || encryptedData.Value.Length == 0)
                return SqlBytes.Null;

            if (password.IsNull || string.IsNullOrEmpty(password.Value))
                throw new ArgumentException("Password cannot be null or empty");

            try
            {
                // Use AES-GCM decryption for better security and performance
                byte[] decryptedData = BcryptInterop.DecryptAesGcmBytes(encryptedData.Value, password.Value);
                return new SqlBytes(decryptedData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error decrypting image: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets image information
        /// </summary>
        [SqlFunction(IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static SqlString GetImageInfo(SqlBytes imageData)
        {
            if (imageData.IsNull || imageData.Value.Length == 0)
                return SqlString.Null;

            try
            {
                using (MemoryStream stream = new MemoryStream(imageData.Value))
                using (Image image = Image.FromStream(stream))
                {
                    StringBuilder info = new StringBuilder();
                    info.AppendLine($"Format: {GetImageFormatName(image.RawFormat)}");
                    info.AppendLine($"Width: {image.Width}px");
                    info.AppendLine($"Height: {image.Height}px");
                    info.AppendLine($"Size: {imageData.Value.Length:N0} bytes");
                    info.AppendLine($"Horizontal Resolution: {image.HorizontalResolution} dpi");
                    info.AppendLine($"Vertical Resolution: {image.VerticalResolution} dpi");
                    info.AppendLine($"Pixel Format: {image.PixelFormat}");
                    
                    return new SqlString(info.ToString());
                }
            }
            catch (Exception ex)
            {
                return new SqlString($"Error reading image: {ex.Message}");
            }
        }

        #region Helper Methods

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == mimeType)
                    return codec;
            }
            return null;
        }

        private static byte[] CompressImage(Image image, int quality)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                EncoderParameters encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                
                ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");
                image.Save(stream, jpegCodec, encoderParams);
                
                return stream.ToArray();
            }
        }

        private static string GetImageFormatName(ImageFormat format)
        {
            if (format.Equals(ImageFormat.Bmp)) return "BMP";
            if (format.Equals(ImageFormat.Emf)) return "EMF";
            if (format.Equals(ImageFormat.Exif)) return "EXIF";
            if (format.Equals(ImageFormat.Gif)) return "GIF";
            if (format.Equals(ImageFormat.Icon)) return "ICO";
            if (format.Equals(ImageFormat.Jpeg)) return "JPEG";
            if (format.Equals(ImageFormat.MemoryBmp)) return "MemoryBMP";
            if (format.Equals(ImageFormat.Png)) return "PNG";
            if (format.Equals(ImageFormat.Tiff)) return "TIFF";
            if (format.Equals(ImageFormat.Wmf)) return "WMF";
            return "Unknown";
        }

        #endregion
    }
}
