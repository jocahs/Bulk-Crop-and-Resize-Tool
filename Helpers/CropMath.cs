using System;

namespace BulkCropAndResizeTool.Helpers
{
    /// <summary>
    /// Pure geometry helpers for crop-rectangle math. No WPF dependencies, so it's
    /// straightforward to unit test independently of MainWindow.
    /// </summary>
    public static class CropMath
    {
        private const double AngleTolerance = 0.1;

        /// <summary>Normalizes an angle in degrees to the [0, 360) range.</summary>
        public static double NormalizeAngle(double angle)
        {
            angle %= 360;
            if (angle < 0) angle += 360;
            return angle;
        }

        /// <summary>True if the (normalized) angle is ~90° or ~270°, i.e. width/height swap.</summary>
        public static bool IsQuarterTurn(double angle)
        {
            double normalized = NormalizeAngle(angle);
            return Math.Abs(normalized - 90) < AngleTolerance || Math.Abs(normalized - 270) < AngleTolerance;
        }

        /// <summary>
        /// Re-maps a crop rectangle (margin + output size, all in source-image pixels) so it stays
        /// anchored to the same region of the image after that image is rotated by deltaAngle degrees.
        /// Only ~0/90/180/270 degree turns are handled; other deltas are a no-op.
        /// </summary>
        /// <param name="deltaAngle">The rotation just applied, in degrees.</param>
        /// <param name="oldImageWidthPx">Image width, in pixels, before this rotation.</param>
        /// <param name="oldImageHeightPx">Image height, in pixels, before this rotation.</param>
        /// <param name="marginLeftPx">Current crop rectangle left margin.</param>
        /// <param name="marginTopPx">Current crop rectangle top margin.</param>
        /// <param name="widthPx">Current crop rectangle width.</param>
        /// <param name="heightPx">Current crop rectangle height.</param>
        public static (int MarginLeftPx, int MarginTopPx, int WidthPx, int HeightPx) RotateCropRect(
            double deltaAngle,
            int oldImageWidthPx,
            int oldImageHeightPx,
            int marginLeftPx,
            int marginTopPx,
            int widthPx,
            int heightPx)
        {
            double angle = NormalizeAngle(deltaAngle);

            if (Math.Abs(angle - 90) < AngleTolerance) // +90°
            {
                return (
                    MarginLeftPx: oldImageHeightPx - marginTopPx - heightPx,
                    MarginTopPx: marginLeftPx,
                    WidthPx: heightPx,
                    HeightPx: widthPx);
            }

            if (Math.Abs(angle - 180) < AngleTolerance) // 180°
            {
                return (
                    MarginLeftPx: oldImageWidthPx - marginLeftPx - widthPx,
                    MarginTopPx: oldImageHeightPx - marginTopPx - heightPx,
                    WidthPx: widthPx,
                    HeightPx: heightPx);
            }

            if (Math.Abs(angle - 270) < AngleTolerance) // -90°
            {
                return (
                    MarginLeftPx: marginTopPx,
                    MarginTopPx: oldImageWidthPx - marginLeftPx - widthPx,
                    WidthPx: heightPx,
                    HeightPx: widthPx);
            }

            // Not a quarter turn (e.g. 0°) - rectangle is unchanged.
            return (marginLeftPx, marginTopPx, widthPx, heightPx);
        }
    }
}
