using BulkCropAndResizeTool.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BulkCropAndResizeTool.Controls
{
    public class CropOverlayController
    {
        private readonly Border _overlay;
        private readonly ImageState _imageState;
        private readonly ViewportState _viewportState;
        private bool _isManipulating;
        private string _resizeMode;
        private Point _startMousePos;
        private int _startMarginLeftPx, _startMarginTopPx, _startWidthPx, _startHeightPx;

        public CropOverlayController(Border overlay, ImageState imageState, ViewportState viewportState)
        {
            _overlay = overlay;
            _imageState = imageState;
            _viewportState = viewportState;
            _isManipulating = false;
            _resizeMode = string.Empty;
        }

        public bool IsManipulating => _isManipulating;

        public void StartManipulation(Point mousePos, string mode)
        {
            _isManipulating = true;
            _resizeMode = mode;
            _startMousePos = mousePos;
            _startMarginLeftPx = _imageState.MarginLeftPx;
            _startMarginTopPx = _imageState.MarginTopPx;
            _startWidthPx = _imageState.OutputWidthPx;
            _startHeightPx = _imageState.OutputHeightPx;
            _overlay.CaptureMouse();
        }

        public void UpdateManipulation(Point currentPos, Action refreshUI)
        {
            if (_viewportState.IsPanMode) return;
            if (!_isManipulating) return;
            if (_imageState.IsResizeMode) return;

            double deltaX = currentPos.X - _startMousePos.X;
            double deltaY = currentPos.Y - _startMousePos.Y;

            int newMarginLeft = _startMarginLeftPx;
            int newMarginTop = _startMarginTopPx;
            int newWidth = _startWidthPx;
            int newHeight = _startHeightPx;

            switch (_resizeMode)
            {
                case "Drag":
                    newMarginLeft = (int)Math.Round(_startMarginLeftPx + deltaX);
                    newMarginTop = (int)Math.Round(_startMarginTopPx + deltaY);
                    newMarginLeft = Math.Clamp(newMarginLeft, 0, _imageState.SourceWidthPx - newWidth);
                    newMarginTop = Math.Clamp(newMarginTop, 0, _imageState.SourceHeightPx - newHeight);
                    break;

                case "ResizeLeft":
                    newMarginLeft = (int)Math.Round(_startMarginLeftPx + deltaX);
                    newWidth = _startWidthPx - (newMarginLeft - _startMarginLeftPx);
                    if (newMarginLeft < 0) { newWidth += newMarginLeft; newMarginLeft = 0; }
                    if (newWidth < 1) { newWidth = 1; newMarginLeft = Math.Min(newMarginLeft, _imageState.SourceWidthPx - 1); }
                    if (newMarginLeft + newWidth > _imageState.SourceWidthPx) newWidth = _imageState.SourceWidthPx - newMarginLeft;
                    break;

                case "ResizeRight":
                    newWidth = (int)Math.Round(_startWidthPx + deltaX);
                    if (newWidth < 1) newWidth = 1;
                    if (newMarginLeft + newWidth > _imageState.SourceWidthPx) newWidth = _imageState.SourceWidthPx - newMarginLeft;
                    break;

                case "ResizeTop":
                    newMarginTop = (int)Math.Round(_startMarginTopPx + deltaY);
                    newHeight = _startHeightPx - (newMarginTop - _startMarginTopPx);
                    if (newMarginTop < 0) { newHeight += newMarginTop; newMarginTop = 0; }
                    if (newHeight < 1) { newHeight = 1; newMarginTop = Math.Min(newMarginTop, _imageState.SourceHeightPx - 1); }
                    if (newMarginTop + newHeight > _imageState.SourceHeightPx) newHeight = _imageState.SourceHeightPx - newMarginTop;
                    break;

                case "ResizeBottom":
                    newHeight = (int)Math.Round(_startHeightPx + deltaY);
                    if (newHeight < 1) newHeight = 1;
                    if (newMarginTop + newHeight > _imageState.SourceHeightPx) newHeight = _imageState.SourceHeightPx - newMarginTop;
                    break;

                case "ResizeTopLeft":
                    newMarginLeft = (int)Math.Round(_startMarginLeftPx + deltaX);
                    newMarginTop = (int)Math.Round(_startMarginTopPx + deltaY);
                    newWidth = _startWidthPx - (newMarginLeft - _startMarginLeftPx);
                    newHeight = _startHeightPx - (newMarginTop - _startMarginTopPx);

                    if (newMarginLeft < 0) { newWidth += newMarginLeft; newMarginLeft = 0; }
                    if (newMarginTop < 0) { newHeight += newMarginTop; newMarginTop = 0; }
                    if (newWidth < 1) { newWidth = 1; newMarginLeft = Math.Min(newMarginLeft, _imageState.SourceWidthPx - 1); }
                    if (newHeight < 1) { newHeight = 1; newMarginTop = Math.Min(newMarginTop, _imageState.SourceHeightPx - 1); }
                    if (newMarginLeft + newWidth > _imageState.SourceWidthPx) newWidth = _imageState.SourceWidthPx - newMarginLeft;
                    if (newMarginTop + newHeight > _imageState.SourceHeightPx) newHeight = _imageState.SourceHeightPx - newMarginTop;
                    break;

                case "ResizeTopRight":
                    newMarginTop = (int)Math.Round(_startMarginTopPx + deltaY);
                    newWidth = (int)Math.Round(_startWidthPx + deltaX);
                    newHeight = _startHeightPx - (newMarginTop - _startMarginTopPx);

                    if (newMarginTop < 0) { newHeight += newMarginTop; newMarginTop = 0; }
                    if (newWidth < 1) newWidth = 1;
                    if (newHeight < 1) { newHeight = 1; newMarginTop = Math.Min(newMarginTop, _imageState.SourceHeightPx - 1); }
                    if (newMarginLeft + newWidth > _imageState.SourceWidthPx) newWidth = _imageState.SourceWidthPx - newMarginLeft;
                    if (newMarginTop + newHeight > _imageState.SourceHeightPx) newHeight = _imageState.SourceHeightPx - newMarginTop;
                    break;

                case "ResizeBottomLeft":
                    newMarginLeft = (int)Math.Round(_startMarginLeftPx + deltaX);
                    newHeight = (int)Math.Round(_startHeightPx + deltaY);
                    newWidth = _startWidthPx - (newMarginLeft - _startMarginLeftPx);

                    if (newMarginLeft < 0) { newWidth += newMarginLeft; newMarginLeft = 0; }
                    if (newWidth < 1) { newWidth = 1; newMarginLeft = Math.Min(newMarginLeft, _imageState.SourceWidthPx - 1); }
                    if (newHeight < 1) newHeight = 1;
                    if (newMarginLeft + newWidth > _imageState.SourceWidthPx) newWidth = _imageState.SourceWidthPx - newMarginLeft;
                    if (newMarginTop + newHeight > _imageState.SourceHeightPx) newHeight = _imageState.SourceHeightPx - newMarginTop;
                    break;

                case "ResizeBottomRight":
                    newWidth = (int)Math.Round(_startWidthPx + deltaX);
                    newHeight = (int)Math.Round(_startHeightPx + deltaY);
                    if (newWidth < 1) newWidth = 1;
                    if (newHeight < 1) newHeight = 1;
                    if (newMarginLeft + newWidth > _imageState.SourceWidthPx) newWidth = _imageState.SourceWidthPx - newMarginLeft;
                    if (newMarginTop + newHeight > _imageState.SourceHeightPx) newHeight = _imageState.SourceHeightPx - newMarginTop;
                    break;
            }

            _imageState.MarginLeftPx = newMarginLeft;
            _imageState.MarginTopPx = newMarginTop;
            _imageState.OutputWidthPx = newWidth;
            _imageState.OutputHeightPx = newHeight;

            refreshUI?.Invoke();
        }

        public void EndManipulation()
        {
            _isManipulating = false;
            _resizeMode = string.Empty;
            _overlay.ReleaseMouseCapture();
        }
    }
}