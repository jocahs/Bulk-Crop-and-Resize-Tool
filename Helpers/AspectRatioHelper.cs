using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkCropAndResizeTool.Helpers
{
    public class AspectRatioHelper
    {
        public static (int w, int h) FindBestAspectRatioPair(int targetW, int targetH, int sourceW, int sourceH, int searchRadius = 5)
        {
            double bestCost = double.MaxValue;
            int bestW = Math.Max(1, targetW);
            int bestH = Math.Max(1, targetH);

            int startW = Math.Max(1, targetW - searchRadius);
            int endW = targetW + searchRadius;

            for (int w = startW; w <= endW; w++)
            {
                int h = (int)Math.Round((double)w * sourceH / sourceW);
                if (h < 1) h = 1;

                // Cost: sum of absolute differences from targets
                double cost = Math.Abs(w - targetW) + Math.Abs(h - targetH);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestW = w;
                    bestH = h;
                }
            }

            // Clamp to source if Crop mode (optional, but will be handled later)
            return (bestW, bestH);
        }
        public static (int w, int h) FromWidth(int w, int sourceW, int sourceH)
        {
            w = Math.Max(1, w);
            int h = Math.Max(1, (int)Math.Round((double)w * sourceH / sourceW));
            // round-trip: recompute w from h so the pair is mutually exact
            int w2 = Math.Max(1, (int)Math.Round((double)h * sourceW / sourceH));
            return (w2, h);
        }

        public static (int w, int h) FromHeight(int h, int sourceW, int sourceH)
        {
            h = Math.Max(1, h);
            int w = Math.Max(1, (int)Math.Round((double)h * sourceW / sourceH));
            // round-trip: recompute h from w so the pair is mutually exact
            int h2 = Math.Max(1, (int)Math.Round((double)w * sourceH / sourceW));
            return (w, h2);
        }

    }
}
