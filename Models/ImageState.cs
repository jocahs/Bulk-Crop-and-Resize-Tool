using System.Windows.Media.Imaging;

namespace BulkCropAndResizeTool.Models
{
    public class ImageState
    {
        // Committed values
        public int SourceWidthPx { get; set; } = 1000;
        public int SourceHeightPx { get; set; } = 2000;
        public int OutputWidthPx { get; set; } = 500;
        public int OutputHeightPx { get; set; } = 1000;
        public int MarginLeftPx { get; set; } = 0;
        public int MarginTopPx { get; set; } = 0;

        // Preview values (transient during editing)
        public int? PreviewOutputWidthPx { get; set; }
        public int? PreviewOutputHeightPx { get; set; }
        public int? PreviewMarginLeftPx { get; set; }
        public int? PreviewMarginTopPx { get; set; }

        public bool IsEditingOutput { get; set; }
        public bool IsEditingMargin { get; set; }

        public void ClearPreviews()
        {
            PreviewOutputWidthPx = null;
            PreviewOutputHeightPx = null;
            PreviewMarginLeftPx = null;
            PreviewMarginTopPx = null;
            IsEditingOutput = false;
            IsEditingMargin = false;
        }
        public BitmapSource? OriginalImage { get; set; }
        public string? CurrentImagePath { get; set; }
        public double CurrentRotation { get; set; }
        public double PreviousRotation { get; set; }

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