using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using BulkCropAndResizeTool.Models;

namespace BulkCropAndResizeTool.Controls
{
    /// <summary>
    /// Owns all zoom, pan, and scroll-bar logic for the image preview.
    /// MainWindow forwards the relevant XAML events here and stays out of the geometry.
    /// </summary>
    public class ViewportController
    {
        private readonly Canvas _previewCanvas;
        private readonly Image _previewImage;
        private readonly Border _cropOverlay;
        private readonly ScrollBar _hScrollBar;
        private readonly ScrollBar _vScrollBar;
        private readonly Label _zoomLabel;
        private readonly Button _panModeBtn;
        private readonly ImageState _imageState;
        private readonly ViewportState _viewportState;
        private readonly Action _onTransformUpdated;

        private bool _isSyncingScrollBars;

        public ViewportController(
            Canvas previewCanvas,
            Image previewImage,
            Border cropOverlay,
            ScrollBar hScrollBar,
            ScrollBar vScrollBar,
            Label zoomLabel,
            Button panModeBtn,
            ImageState imageState,
            ViewportState viewportState,
            Action onTransformUpdated)
        {
            _previewCanvas = previewCanvas;
            _previewImage = previewImage;
            _cropOverlay = cropOverlay;
            _hScrollBar = hScrollBar;
            _vScrollBar = vScrollBar;
            _zoomLabel = zoomLabel;
            _panModeBtn = panModeBtn;
            _imageState = imageState;
            _viewportState = viewportState;
            _onTransformUpdated = onTransformUpdated;
        }

        /// <summary>True while scroll bar values are being set programmatically (suppresses feedback loops).</summary>
        public bool IsSyncingScrollBars => _isSyncingScrollBars;

        #region Transform

        public void UpdateTransform()
        {
            if (_imageState.OriginalImage == null || _imageState.SourceWidthPx <= 0)
            {
                _previewCanvas.RenderTransform = null;
                _viewportState.CurrentScale = 1.0;
                UpdateZoomLabel();
                ResetScrollBars();
                return;
            }

            double canvasW = _previewCanvas.ActualWidth;
            double canvasH = _previewCanvas.ActualHeight;
            double scale = CalculateScale(canvasW, canvasH);

            _viewportState.CurrentScale = Math.Clamp(scale, 0.01, 100);

            double scaledW = _imageState.SourceWidthPx * _viewportState.CurrentScale;
            double scaledH = _imageState.SourceHeightPx * _viewportState.CurrentScale;

            _viewportState.MinPanX = scaledW > canvasW ? (canvasW - scaledW) : 0;
            _viewportState.MinPanY = scaledH > canvasH ? (canvasH - scaledH) : 0;
            _viewportState.MaxPanX = 0;
            _viewportState.MaxPanY = 0;

            _viewportState.PanX = Math.Clamp(_viewportState.PanX, _viewportState.MinPanX, _viewportState.MaxPanX);
            _viewportState.PanY = Math.Clamp(_viewportState.PanY, _viewportState.MinPanY, _viewportState.MaxPanY);

            SyncScrollBars(canvasW, canvasH);

            var group = new TransformGroup();
            group.Children.Add(new ScaleTransform(scale, scale));
            group.Children.Add(new TranslateTransform(_viewportState.PanX, _viewportState.PanY));
            _previewCanvas.RenderTransform = group;

            UpdateZoomLabel();
            _onTransformUpdated?.Invoke();
        }

        private double CalculateScale(double canvasW, double canvasH)
        {
            return _viewportState.ZoomMode switch
            {
                ZoomMode.Fit => Math.Min(canvasW / _imageState.SourceWidthPx, canvasH / _imageState.SourceHeightPx),
                ZoomMode.Actual => 1.0,
                ZoomMode.Custom => _viewportState.CustomZoom,
                _ => 1.0
            };
        }

        private void SyncScrollBars(double canvasW, double canvasH)
        {
            _isSyncingScrollBars = true;
            try
            {
                double hRange = -_viewportState.MinPanX;
                _hScrollBar.Minimum = 0;
                _hScrollBar.Maximum = hRange;
                _hScrollBar.ViewportSize = canvasW;
                _hScrollBar.Value = Math.Clamp(-_viewportState.PanX, 0, hRange);

                double vRange = -_viewportState.MinPanY;
                _vScrollBar.Minimum = 0;
                _vScrollBar.Maximum = vRange;
                _vScrollBar.ViewportSize = canvasH;
                _vScrollBar.Value = Math.Clamp(-_viewportState.PanY, 0, vRange);
            }
            finally
            {
                _isSyncingScrollBars = false;
            }
        }

        private void ResetScrollBars()
        {
            _isSyncingScrollBars = true;
            try
            {
                _hScrollBar.Minimum = 0;
                _hScrollBar.Maximum = 0;
                _hScrollBar.Value = 0;
                _vScrollBar.Minimum = 0;
                _vScrollBar.Maximum = 0;
                _vScrollBar.Value = 0;
            }
            finally
            {
                _isSyncingScrollBars = false;
            }
        }

        private void UpdateZoomLabel()
        {
            if (_imageState.OriginalImage == null)
            {
                _zoomLabel.Content = "No image";
                return;
            }

            _zoomLabel.Content = _viewportState.ZoomMode switch
            {
                ZoomMode.Fit => "Fit",
                ZoomMode.Actual => "1:1",
                ZoomMode.Custom => $"{_viewportState.CurrentScale * 100:F0}%",
                _ => ""
            };
        }

        #endregion

        #region Zoom Actions

        public void ZoomToFit()
        {
            if (_imageState.OriginalImage == null) return;
            _viewportState.ZoomMode = ZoomMode.Fit;
            _viewportState.PanX = 0;
            _viewportState.PanY = 0;
            UpdateTransform();
        }

        public void ZoomToActual()
        {
            if (_imageState.OriginalImage == null) return;
            _viewportState.ZoomMode = ZoomMode.Actual;
            _viewportState.PanX = 0;
            _viewportState.PanY = 0;
            UpdateTransform();
        }

        public void ZoomIn()
        {
            if (_imageState.OriginalImage == null) return;
            _viewportState.CustomZoom = _viewportState.CurrentScale * 1.1;
            _viewportState.ZoomMode = ZoomMode.Custom;
            UpdateTransform();
        }

        public void ZoomOut()
        {
            if (_imageState.OriginalImage == null) return;
            _viewportState.CustomZoom = _viewportState.CurrentScale / 1.1;
            if (_viewportState.CustomZoom < 0.01) _viewportState.CustomZoom = 0.01;
            _viewportState.ZoomMode = ZoomMode.Custom;
            UpdateTransform();
        }

        public void TogglePanMode()
        {
            _viewportState.IsPanMode = !_viewportState.IsPanMode;
            _panModeBtn.Background = _viewportState.IsPanMode ? Brushes.LightBlue : Brushes.Transparent;

            if (_viewportState.IsPanMode)
            {
                _previewImage.Cursor = Cursors.Hand;
                _cropOverlay.IsHitTestVisible = false;
            }
            else
            {
                _previewImage.Cursor = Cursors.Arrow;
                _cropOverlay.IsHitTestVisible = true;
            }
        }

        #endregion

        #region Scroll Bars

        public void OnScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingScrollBars || _imageState.OriginalImage == null) return;

            if (sender == _hScrollBar)
                _viewportState.PanX = -_hScrollBar.Value;
            else if (sender == _vScrollBar)
                _viewportState.PanY = -_vScrollBar.Value;

            UpdateTransform();
        }

        public void HandleMouseWheel(MouseWheelEventArgs e)
        {
            if (_imageState.OriginalImage == null) return;

            double step = e.Delta;

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                _hScrollBar.Value = Math.Clamp(_hScrollBar.Value - step, _hScrollBar.Minimum, _hScrollBar.Maximum);
            }
            else
            {
                _vScrollBar.Value = Math.Clamp(_vScrollBar.Value - step, _vScrollBar.Minimum, _vScrollBar.Maximum);
            }

            e.Handled = true;
        }

        #endregion

        #region Panning

        public void BeginPan(Point mousePos)
        {
            if (!_viewportState.IsPanMode) return;

            _viewportState.IsPanning = true;
            _viewportState.PanStartMouse = mousePos;
            _viewportState.PanStartX = _viewportState.PanX;
            _viewportState.PanStartY = _viewportState.PanY;
            _previewImage.CaptureMouse();
        }

        public void UpdatePan(Point mousePos)
        {
            if (!_viewportState.IsPanning) return;

            double deltaX = mousePos.X - _viewportState.PanStartMouse.X;
            double deltaY = mousePos.Y - _viewportState.PanStartMouse.Y;
            _viewportState.PanX = _viewportState.PanStartX + deltaX;
            _viewportState.PanY = _viewportState.PanStartY + deltaY;
            UpdateTransform();
        }

        public void EndPan()
        {
            if (!_viewportState.IsPanning) return;

            _viewportState.IsPanning = false;
            _previewImage.ReleaseMouseCapture();
        }

        #endregion
    }
}
