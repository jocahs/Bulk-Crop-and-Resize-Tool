using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkCropAndResizeTool.Helpers
{
    public class AspectRatioHelper
    {
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
