using BulkCropAndResizeTool.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BulkCropAndResizeTool.Services
{    
    public class ImageProcessingService
    {
        public static BitmapSource LoadImageFromFile(string filePath)
        {
            try
            {
                int orientation = 1;

                using (var fs = new FileStream(
                           filePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite))
                using (var img = System.Drawing.Image.FromStream(fs, false, false))
                {
                    var prop = img.PropertyItems.FirstOrDefault(p => p.Id == 0x0112);
                    if (prop != null && prop.Value?.Length >= 2)
                        orientation = BitConverter.ToUInt16(prop.Value, 0);
                }

                BitmapImage bitmap;

                using (var stream = new FileStream(
                           filePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite))
                {
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                BitmapSource source = bitmap;

                double w = source.PixelWidth;
                double h = source.PixelHeight;

                // 3. Apply EXIF transformation
                var transform = new TransformGroup();
                switch (orientation)
                {
                    case 2: // Mirror horizontally
                        transform.Children.Add(new ScaleTransform(-1, 1));
                        transform.Children.Add(new TranslateTransform(w, 0));
                        break;
                    case 3: // Rotate 180°
                        transform.Children.Add(new RotateTransform(180));
                        break;
                    case 4: // Mirror vertically
                        transform.Children.Add(new ScaleTransform(1, -1));
                        transform.Children.Add(new TranslateTransform(0, h));
                        break;
                    case 5: // Rotate 90° CW + Mirror horizontally
                        transform.Children.Add(new RotateTransform(90));
                        transform.Children.Add(new ScaleTransform(-1, 1));
                        transform.Children.Add(new TranslateTransform(h, 0));
                        break;
                    case 6: // Rotate 90° CW
                        transform.Children.Add(new RotateTransform(90));
                        break;
                    case 7: // Rotate 90° CW + Mirror vertically
                        transform.Children.Add(new RotateTransform(90));
                        transform.Children.Add(new ScaleTransform(1, -1));
                        transform.Children.Add(new TranslateTransform(0, w));
                        break;
                    case 8: // Rotate 270° CW
                        transform.Children.Add(new RotateTransform(270));
                        break;
                    default:
                        break;
                }

                if (transform.Children.Count > 0)
                {
                    source = new TransformedBitmap(source, transform);
                    source.Freeze();
                }

                return source;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load image '{filePath}'.", ex);
            }
        }
        public static BitmapSource? RotateImage(BitmapSource source, double angle)
        {
            if (source == null || Math.Abs(angle % 360) < 0.001)
                return source;

            var transform = new RotateTransform(angle);
            var rotated = new TransformedBitmap(source, transform);
            rotated.Freeze();
            return rotated;
        }
        public static BitmapSource? ResizeImage(BitmapSource source, int targetWidth, int targetHeight)
        {
            if (source == null) return null;
            if (targetWidth <= 0 || targetHeight <= 0) return source;

            double scaleX = targetWidth / (double)source.PixelWidth;
            double scaleY = targetHeight / (double)source.PixelHeight;
            var transform = new ScaleTransform(scaleX, scaleY);
            var resized = new TransformedBitmap(source, transform);
            resized.Freeze();
            return resized;
        }
        public static CroppedBitmap CropImage(BitmapSource source, double rotationAngle, int cropX, int cropY, int cropW, int cropH)
        {
            BitmapSource rotated = source;
            if (Math.Abs(rotationAngle % 360) > 0.001)
            {
                var transform = new RotateTransform(rotationAngle);
                rotated = new TransformedBitmap(source, transform);
                rotated.Freeze();
            }

            int rw = rotated.PixelWidth;
            int rh = rotated.PixelHeight;
            cropX = Math.Max(0, Math.Min(cropX, rw - 1));
            cropY = Math.Max(0, Math.Min(cropY, rh - 1));
            cropW = Math.Max(1, Math.Min(cropW, rw - cropX));
            cropH = Math.Max(1, Math.Min(cropH, rh - cropY));

            var cropRect = new Int32Rect(cropX, cropY, cropW, cropH);
            var cropped = new CroppedBitmap(rotated, cropRect);
            cropped.Freeze();
            return cropped;
        }
        public static BitmapSource? ProcessImage( BitmapSource source, bool isResize, string unit, double angle, int outputWidth, int outputHeight, int marginLeft, int marginTop,  double? percentW = null, double? percentH = null)
        {
            if (source == null) return null;

            // Apply rotation first
            var rotated = RotateImage(source, angle);
            if (rotated == null) return null;

            if (isResize)
            {
                return ProcessResize(rotated, unit, outputWidth, outputHeight, percentW, percentH);
            }
            else
            {
                return ProcessCrop(rotated, marginLeft, marginTop, outputWidth, outputHeight);
            }
        }
        private static BitmapSource? ProcessResize( BitmapSource rotated, string unit, int outputWidth, int outputHeight, double? percentW, double? percentH)
        {
            int targetW, targetH;

            if (unit == "%")
            {
                targetW = (int)Math.Round(rotated.PixelWidth * (percentW ?? 100) / 100.0);
                targetH = (int)Math.Round(rotated.PixelHeight * (percentH ?? 100) / 100.0);
            }
            else
            {
                // Use aspect ratio or exact dimensions
                targetW = outputWidth;
                targetH = outputHeight;
            }

            targetW = Math.Max(1, targetW);
            targetH = Math.Max(1, targetH);

            return ResizeImage(rotated, targetW, targetH);
        }
        private static CroppedBitmap? ProcessCrop(BitmapSource rotated, int marginLeft, int marginTop, int outputWidth, int outputHeight)
        {
            return CropImage(rotated, 0,marginLeft, marginTop, outputWidth, outputHeight);
        }
        public static void SaveImage (BitmapSource image, string filePath, string format = ".jpg", int quality = 90)
        {
            ArgumentNullException.ThrowIfNull(image);

            BitmapEncoder encoder = CreateEncoder(format, quality);
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            encoder.Save(stream);
        }
        private static BitmapEncoder CreateEncoder(string format, int quality)
        {
            return format.ToLower() switch
            {
                ".png" => new PngBitmapEncoder(),
                ".bmp" => new BmpBitmapEncoder(),
                ".gif" => new GifBitmapEncoder(),
                _ => new JpegBitmapEncoder { QualityLevel = quality }
            };
        }
    }
}