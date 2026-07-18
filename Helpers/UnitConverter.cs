using System;

namespace BulkCropAndResizeTool.Helpers
{
    public static class UnitConverter
    {
        private const double Dpi = 96.0;
        private const double MmPerInch = 25.4;

        public static double ConvertPixelsToUnit(int pixels, string unit)
        {
            return unit switch
            {
                "px" => pixels,
                "mm" => pixels / Dpi * MmPerInch,
                "%" => pixels,
                _ => pixels
            };
        }

        public static int ConvertUnitToPixels(double value, string unit, bool clampToMin = true)
        {
            int result = unit switch
            {
                "px" => (int)Math.Round(value),
                "mm" => (int)Math.Round(value / MmPerInch * Dpi),
                "%" => (int)Math.Round(value),
                _ => (int)Math.Round(value)
            };

            return clampToMin ? Math.Max(1, result) : result;
        }

        public static string GetCurrentUnit(bool isPixels, bool isMM, bool isPercent)
        {
            if (isPercent) return "%";
            if (isMM) return "mm";
            return "px";
        }
    }
}