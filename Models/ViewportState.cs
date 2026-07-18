using System.Windows;  // ← Add this for Point
using System.Windows.Media; // ← Add this if you use other WPF types

namespace BulkCropAndResizeTool.Models
{
    public enum ZoomMode
    {
        Fit,
        Actual,
        Custom
    }

    public class ViewportState
    {
        public ZoomMode ZoomMode { get; set; } = ZoomMode.Fit;
        public double CustomZoom { get; set; } = 1.0;
        public double CurrentScale { get; set; } = 1.0;
        public double PanX { get; set; } = 0;
        public double PanY { get; set; } = 0;
        public double MinPanX { get; set; } = 0;
        public double MaxPanX { get; set; } = 0;
        public double MinPanY { get; set; } = 0;
        public double MaxPanY { get; set; } = 0;
        public bool IsPanMode { get; set; } = false;
        public bool IsPanning { get; set; } = false;
        public Point PanStartMouse { get; set; }  // ← Now works with using System.Windows;
        public double PanStartX { get; set; }
        public double PanStartY { get; set; }

        public void Reset()
        {
            ZoomMode = ZoomMode.Fit;
            CustomZoom = 1.0;
            CurrentScale = 1.0;
            PanX = 0;
            PanY = 0;
            IsPanMode = false;
            IsPanning = false;
            PanStartMouse = new Point(0, 0);
            PanStartX = 0;
            PanStartY = 0;
        }
    }
}