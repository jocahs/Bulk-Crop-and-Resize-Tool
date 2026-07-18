using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace BulkCropAndResizeTool.Services;

public static class ImageProcessor
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
    public static CroppedBitmap CropSingleImage(BitmapSource source, double rotationAngle, int cropX, int cropY, int cropW, int cropH)
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

    
}