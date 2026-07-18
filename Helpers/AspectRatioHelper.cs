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

    }
}
