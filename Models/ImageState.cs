using System.Windows.Media.Imaging;

namespace BulkCropAndResizeTool.Models
{
    public class ImageState
    {
        public BitmapSource? OriginalImage { get; set; }
        public string? CurrentImagePath { get; set; }
        public double CurrentRotation { get; set; }
        public double PreviousRotation { get; set; }
        public int SourceWidthPx = 1000;
        public int SourceHeightPx = 2000;
        public int OutputWidthPx = 500;
        public int OutputHeightPx = 1000;
        public int MarginLeftPx = 0;
        public int MarginTopPx = 0;

        public bool IsImageLoaded => OriginalImage != null;
        public bool IsCropMode { get; set; } = true;
        public bool IsResizeMode => !IsCropMode;

        public void Reset()
        {
            OutputWidthPx = SourceWidthPx / 2;
            OutputHeightPx = SourceHeightPx / 2;
            MarginLeftPx = 0;
            MarginTopPx = 0;
            CurrentRotation = 0;
            PreviousRotation = 0;
        }

        public void SetSourceDimensions(int width, int height)
        {
            SourceWidthPx = width;
            SourceHeightPx = height;
        }
    }
}