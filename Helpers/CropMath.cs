using System;

namespace BulkCropAndResizeTool.Helpers
{
    public static class CropMath
    {
        private const double AngleTolerance = 0.1;
        public static double NormalizeAngle(double angle)
        {
            angle %= 360;
            if (angle < 0) angle += 360;
            return angle;
        }
        public static bool IsQuarterTurn(double angle)
        {
            double normalized = NormalizeAngle(angle);
            return Math.Abs(normalized - 90) < AngleTolerance || Math.Abs(normalized - 270) < AngleTolerance;
        }
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
