using System;
using System.Windows;
using System.Windows.Controls;
using BulkCropAndResizeTool.Helpers;
using BulkCropAndResizeTool.Models;
using Xceed.Wpf.Toolkit;

namespace BulkCropAndResizeTool.Controls
{
    public class CropDimensionsController(
        TextBox widthSourceBox,
        TextBox heightSourceBox,
        IntegerUpDown widthBox,
        IntegerUpDown heightBox,
        IntegerUpDown marginLeftBox,
        IntegerUpDown marginTopBox,
        RadioButton unitPixels,
        RadioButton unitMM,
        RadioButton unitPercent,
        RadioButton modeResize,
        CheckBox aspectRatio,
        GroupBox aspectRatioGroup,
        GroupBox marginsSettings,
        Border cropOverlay,
        ImageState imageState,
        Action onCropOverlayUpdateNeeded,
        Action setActionBtnText)
    {
        private readonly TextBox _widthSourceBox = widthSourceBox;
        private readonly TextBox _heightSourceBox = heightSourceBox;
        private readonly IntegerUpDown _widthBox = widthBox;
        private readonly IntegerUpDown _heightBox = heightBox;
        private readonly IntegerUpDown _marginLeftBox = marginLeftBox;
        private readonly IntegerUpDown _marginTopBox = marginTopBox;
        private readonly RadioButton _unitPixels = unitPixels;
        private readonly RadioButton _unitMM = unitMM;
        private readonly RadioButton _unitPercent = unitPercent;
        private readonly RadioButton _modeResize = modeResize;
        private readonly CheckBox _aspectRatio = aspectRatio;
        private readonly GroupBox _aspectRatioGroup = aspectRatioGroup;
        private readonly GroupBox _marginsSettings = marginsSettings;
        private readonly Border _cropOverlay = cropOverlay;
        private readonly ImageState _imageState = imageState;
        private readonly Action _onCropOverlayUpdateNeeded = onCropOverlayUpdateNeeded;
        private readonly Action _setActionBtnText = setActionBtnText;
        private IntegerUpDown? _activeOutputBox;
        private IntegerUpDown? _activeMarginBox;

        private bool _isSyncingUI;

        #region Unit Management

        public string GetCurrentUnit()
        {
            return UnitConverter.GetCurrentUnit(
                _unitPixels.IsChecked == true,
                _unitMM.IsChecked == true,
                _unitPercent.IsChecked == true);
        }

        public void UpdateUnitAvailability()
        {
            bool isResize = _modeResize.IsChecked == true;
            _unitPercent.IsEnabled = isResize;
            _unitPercent.IsChecked = isResize;
            _aspectRatio.IsChecked = isResize;
            _aspectRatioGroup.Visibility = isResize ? Visibility.Visible : Visibility.Collapsed;
            _marginsSettings.Visibility = !isResize ? Visibility.Visible : Visibility.Collapsed;
            _setActionBtnText();
            _imageState.IsCropMode = !isResize;
            _cropOverlay.Visibility = !isResize ? Visibility.Visible : Visibility.Hidden; 
            
            if (_imageState.OriginalImage == null)
            {
                _cropOverlay.Visibility = Visibility.Hidden;
            }            

            if (!isResize && _unitMM.IsChecked == false)
                _unitPixels.IsChecked = true;
        }

        public void OnUnitChanged()
        {
            if (_aspectRatio.IsChecked == true)
                AdjustAspectRatio(anchorIsWidth: true);
            UpdateDimensionTextBoxes();
        }

        #endregion

        #region Refresh / Sync

       private void ClampCropRectangle()
       {
            _imageState.OutputWidthPx =
                Math.Max(1, _imageState.OutputWidthPx);

            _imageState.OutputHeightPx =
                Math.Max(1, _imageState.OutputHeightPx);

            _imageState.MarginLeftPx =
                Math.Max(0, _imageState.MarginLeftPx);

            _imageState.MarginTopPx =
                Math.Max(0, _imageState.MarginTopPx);

            if (_imageState.IsCropMode)
            {
                _imageState.OutputWidthPx =
                    Math.Min(
                        _imageState.OutputWidthPx,
                        _imageState.SourceWidthPx);

                _imageState.OutputHeightPx =
                    Math.Min(
                        _imageState.OutputHeightPx,
                        _imageState.SourceHeightPx);

                _imageState.MarginLeftPx =
                    Math.Min(
                        _imageState.MarginLeftPx,
                        _imageState.SourceWidthPx - 1);

                _imageState.MarginTopPx =
                    Math.Min(
                        _imageState.MarginTopPx,
                        _imageState.SourceHeightPx - 1);
            }
        }

        #endregion

        #region Aspect Ratio

        public void AdjustAspectRatio(bool anchorIsWidth)
        {
            if (_aspectRatio.IsChecked != true) return;
            if (_imageState.SourceWidthPx <= 0 || _imageState.SourceHeightPx <= 0) return;

            var (w, h) = anchorIsWidth
                ? AspectRatioHelper.FromWidth(_imageState.OutputWidthPx, _imageState.SourceWidthPx, _imageState.SourceHeightPx)
                : AspectRatioHelper.FromHeight(_imageState.OutputHeightPx, _imageState.SourceWidthPx, _imageState.SourceHeightPx);

            if (_imageState.IsCropMode)
            {
                w = Math.Min(w, _imageState.SourceWidthPx);
                h = Math.Min(h, _imageState.SourceHeightPx);
            }

            _imageState.OutputWidthPx = w;
            _imageState.OutputHeightPx = h;
            UpdateDimensionTextBoxes();
        }


        #endregion

        #region Dimension / Margin Box Events

        private int UpdatePixelFromBox(IntegerUpDown box, int sourceDimensionPx, bool clampToMin = true)
        {
            string unit = GetCurrentUnit();
            double displayValue = box.Value ?? 0;

            if (unit == "%")
            {
                int result = (int)Math.Round(displayValue / 100.0 * sourceDimensionPx);
                return clampToMin && result < 1 ? 1 : result;
            }
            else
            {
                return UnitConverter.ConvertUnitToPixels(displayValue, unit, clampToMin);
            }
        }

        public void RefreshUI(IntegerUpDown? skipBox = null)
        {
            ClampCropRectangle();
            UpdateDimensionTextBoxes(skipBox);
        }
        public void UpdateDimensionTextBoxes(IntegerUpDown? skipBox = null)
        {
            if (_isSyncingUI) return;
            _isSyncingUI = true;

            try
            {
                string unit = GetCurrentUnit();

                if (unit == "%")
                {
                    _widthSourceBox.Text = "100";
                    _heightSourceBox.Text = "100";

                    double percentW = (double)_imageState.OutputWidthPx / _imageState.SourceWidthPx * 100;
                    double percentH = (double)_imageState.OutputHeightPx / _imageState.SourceHeightPx * 100;
                    if (skipBox != _widthBox) _widthBox.Value = (int)Math.Round(Math.Max(1, percentW));
                    if (skipBox != _heightBox) _heightBox.Value = (int)Math.Round(Math.Max(1, percentH));
                    if (skipBox != _marginLeftBox) _marginLeftBox.Value = 0;
                    if (skipBox != _marginTopBox) _marginTopBox.Value = 0;
                }
                else
                {              
                    int widthSourceDisplay = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.SourceWidthPx, unit)));
                    int heightSourceDisplay = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.SourceHeightPx, unit)));
                    int widthOutputDisplay = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.OutputWidthPx, unit)));
                    int heightOutputDisplay = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.OutputHeightPx, unit)));

                    _widthSourceBox.Text = widthSourceDisplay.ToString();
                    _heightSourceBox.Text = heightSourceDisplay.ToString();

                    if (skipBox != _widthBox) _widthBox.Value = widthOutputDisplay;
                    if (skipBox != _heightBox) _heightBox.Value = heightOutputDisplay;

                    if (skipBox != _marginLeftBox)
                    {
                        _marginLeftBox.Value =
                            (int)Math.Round(
                                UnitConverter.ConvertPixelsToUnit(
                                    _imageState.MarginLeftPx,
                                    unit));
                    }

                    if (skipBox != _marginTopBox)
                    {
                        _marginTopBox.Value =
                            (int)Math.Round(
                                UnitConverter.ConvertPixelsToUnit(
                                    _imageState.MarginTopPx,
                                    unit));
                    }
                }
            }
            finally
            {
                _isSyncingUI = false;
                _onCropOverlayUpdateNeeded?.Invoke();
            }
        }
        public void OnDimensionBoxLostFocus()
        {
            if (_isSyncingUI) return;
            UpdateDimensionTextBoxes();
        }
        private void RecalculateAfterOutputChanged()
        {
            // Output is authoritative

            _imageState.OutputWidthPx =
                Math.Clamp(_imageState.OutputWidthPx, 1, _imageState.SourceWidthPx);

            _imageState.OutputHeightPx =
                Math.Clamp(_imageState.OutputHeightPx, 1, _imageState.SourceHeightPx);

            _imageState.MarginLeftPx =
                Math.Min(
                    _imageState.MarginLeftPx,
                    _imageState.SourceWidthPx - _imageState.OutputWidthPx);

            _imageState.MarginTopPx =
                Math.Min(
                    _imageState.MarginTopPx,
                    _imageState.SourceHeightPx - _imageState.OutputHeightPx);

            _imageState.MarginLeftPx =
                Math.Max(0, _imageState.MarginLeftPx);

            _imageState.MarginTopPx =
                Math.Max(0, _imageState.MarginTopPx);
        }
        private void RecalculateAfterMarginChanged()
        {
            // Margin is authoritative

            _imageState.MarginLeftPx =
                Math.Clamp(
                    _imageState.MarginLeftPx,
                    0,
                    _imageState.SourceWidthPx - 1);

            _imageState.MarginTopPx =
                Math.Clamp(
                    _imageState.MarginTopPx,
                    0,
                    _imageState.SourceHeightPx - 1);

            _imageState.OutputWidthPx =
                Math.Min(
                    _imageState.OutputWidthPx,
                    _imageState.SourceWidthPx - _imageState.MarginLeftPx);

            _imageState.OutputHeightPx =
                Math.Min(
                    _imageState.OutputHeightPx,
                    _imageState.SourceHeightPx - _imageState.MarginTopPx);

            _imageState.OutputWidthPx =
                Math.Max(1, _imageState.OutputWidthPx);

            _imageState.OutputHeightPx =
                Math.Max(1, _imageState.OutputHeightPx);
        }
        public void OnDimensionBoxValueChanged(object sender, bool windowIsLoaded)
        {
            if (_isSyncingUI || !windowIsLoaded) return;

            if (sender == _widthBox)
                _imageState.OutputWidthPx = UpdatePixelFromBox(_widthBox, _imageState.SourceWidthPx, true);

            else if (sender == _heightBox)
                _imageState.OutputHeightPx = UpdatePixelFromBox(_heightBox, _imageState.SourceHeightPx, true);

            if (_aspectRatio.IsChecked == true)
                AdjustAspectRatio(anchorIsWidth: sender == _widthBox);

            RecalculateAfterOutputChanged();
            RefreshUI();
        }

        public void OnMarginBoxValueChanged(object sender)
        {
            if (sender == _marginLeftBox)
                _imageState.MarginLeftPx = UpdatePixelFromBox(_marginLeftBox, _imageState.SourceWidthPx, false);

            else if (sender == _marginTopBox)
                _imageState.MarginTopPx = UpdatePixelFromBox(_marginTopBox, _imageState.SourceHeightPx, false);

            RecalculateAfterMarginChanged();
            RefreshUI();
        }
        
        #endregion

        #region Preview-Commit Pattern

        public void OnDimensionBoxPreviewChanged(object sender, bool windowIsLoaded)
        {
            if (_isSyncingUI || !windowIsLoaded) return;

            _activeOutputBox = sender as IntegerUpDown;
            _imageState.IsEditingOutput = true;
            _imageState.IsEditingMargin = false;

            // Parse the raw input value (what user typed, even if invalid intermediate)
            double displayValue = _activeOutputBox?.Value ?? 0;
            string unit = GetCurrentUnit();

            // Compute what the output WOULD be (preview)
            int previewOutputW = _imageState.OutputWidthPx;
            int previewOutputH = _imageState.OutputHeightPx;

            if (sender == _widthBox)
            {
                previewOutputW = unit == "%"
                    ? (int)Math.Round(displayValue / 100.0 * _imageState.SourceWidthPx)
                    : UnitConverter.ConvertUnitToPixels(displayValue, unit, true);
            }
            else if (sender == _heightBox)
            {
                previewOutputH = unit == "%"
                    ? (int)Math.Round(displayValue / 100.0 * _imageState.SourceHeightPx)
                    : UnitConverter.ConvertUnitToPixels(displayValue, unit, true);
            }

            // Apply aspect ratio to preview if needed
            if (_aspectRatio.IsChecked == true)
            {
                if (sender == _widthBox)
                    previewOutputH = AspectRatioHelper.FromWidth(previewOutputW,
                        _imageState.SourceWidthPx, _imageState.SourceHeightPx).h;
                else
                    previewOutputW = AspectRatioHelper.FromHeight(previewOutputH,
                        _imageState.SourceWidthPx, _imageState.SourceHeightPx).w;
            }

            // Clamp preview output to source bounds
            previewOutputW = Math.Clamp(previewOutputW, 1, _imageState.SourceWidthPx);
            previewOutputH = Math.Clamp(previewOutputH, 1, _imageState.SourceHeightPx);

            // Compute PREVIEW margins based on committed margins + preview output
            // This is the key: margins are derived from preview output, but 
            // we DON'T touch the committed margin values
            int previewMarginLeft = Math.Min(_imageState.MarginLeftPx,
                _imageState.SourceWidthPx - previewOutputW);
            int previewMarginTop = Math.Min(_imageState.MarginTopPx,
                _imageState.SourceHeightPx - previewOutputH);

            // Store previews
            _imageState.PreviewOutputWidthPx = previewOutputW;
            _imageState.PreviewOutputHeightPx = previewOutputH;
            _imageState.PreviewMarginLeftPx = previewMarginLeft;
            _imageState.PreviewMarginTopPx = previewMarginTop;

            // Refresh UI showing preview values
            RefreshUIWithPreviews(skipBox: _activeOutputBox);
            _onCropOverlayUpdateNeeded?.Invoke();
        }

        public void OnMarginBoxPreviewChanged(object sender)
        {
            if (_isSyncingUI) return;

            _activeMarginBox = sender as IntegerUpDown;
            _imageState.IsEditingMargin = true;
            _imageState.IsEditingOutput = false;

            double displayValue = _activeMarginBox?.Value ?? 0;
            string unit = GetCurrentUnit();

            // Compute preview margins
            int previewMarginL = _imageState.MarginLeftPx;
            int previewMarginT = _imageState.MarginTopPx;

            if (sender == _marginLeftBox)
            {
                previewMarginL = unit == "%"
                    ? (int)Math.Round(displayValue / 100.0 * _imageState.SourceWidthPx)
                    : UnitConverter.ConvertUnitToPixels(displayValue, unit, false);
            }
            else if (sender == _marginTopBox)
            {
                previewMarginT = unit == "%"
                    ? (int)Math.Round(displayValue / 100.0 * _imageState.SourceHeightPx)
                    : UnitConverter.ConvertUnitToPixels(displayValue, unit, false);
            }

            // Clamp preview margins
            previewMarginL = Math.Clamp(previewMarginL, 0, _imageState.SourceWidthPx - 1);
            previewMarginT = Math.Clamp(previewMarginT, 0, _imageState.SourceHeightPx - 1);

            // Compute PREVIEW output based on committed output + preview margins
            int previewOutputW = Math.Min(_imageState.OutputWidthPx,
                _imageState.SourceWidthPx - previewMarginL);
            int previewOutputH = Math.Min(_imageState.OutputHeightPx,
                _imageState.SourceHeightPx - previewMarginT);
            previewOutputW = Math.Max(1, previewOutputW);
            previewOutputH = Math.Max(1, previewOutputH);

            // Store previews
            _imageState.PreviewMarginLeftPx = previewMarginL;
            _imageState.PreviewMarginTopPx = previewMarginT;
            _imageState.PreviewOutputWidthPx = previewOutputW;
            _imageState.PreviewOutputHeightPx = previewOutputH;

            RefreshUIWithPreviews(skipBox: _activeMarginBox);
            _onCropOverlayUpdateNeeded?.Invoke();
        }

        public void CommitOutputChanges()
        {
            if (!_imageState.IsEditingOutput) return;

            // Commit preview values to actual state
            if (_imageState.PreviewOutputWidthPx.HasValue)
                _imageState.OutputWidthPx = _imageState.PreviewOutputWidthPx.Value;
            if (_imageState.PreviewOutputHeightPx.HasValue)
                _imageState.OutputHeightPx = _imageState.PreviewOutputHeightPx.Value;
            if (_imageState.PreviewMarginLeftPx.HasValue)
                _imageState.MarginLeftPx = _imageState.PreviewMarginLeftPx.Value;
            if (_imageState.PreviewMarginTopPx.HasValue)
                _imageState.MarginTopPx = _imageState.PreviewMarginTopPx.Value;

            _imageState.ClearPreviews();
            _activeOutputBox = null;

            // Full refresh with committed values
            RefreshUI();
        }

        public void CommitMarginChanges()
        {
            if (!_imageState.IsEditingMargin) return;

            if (_imageState.PreviewMarginLeftPx.HasValue)
                _imageState.MarginLeftPx = _imageState.PreviewMarginLeftPx.Value;
            if (_imageState.PreviewMarginTopPx.HasValue)
                _imageState.MarginTopPx = _imageState.PreviewMarginTopPx.Value;
            if (_imageState.PreviewOutputWidthPx.HasValue)
                _imageState.OutputWidthPx = _imageState.PreviewOutputWidthPx.Value;
            if (_imageState.PreviewOutputHeightPx.HasValue)
                _imageState.OutputHeightPx = _imageState.PreviewOutputHeightPx.Value;

            _imageState.ClearPreviews();
            _activeMarginBox = null;

            RefreshUI();
        }

        #endregion

        #region UI Refresh with Previews

        private void RefreshUIWithPreviews(IntegerUpDown? skipBox = null)
        {
            if (_isSyncingUI) return;
            _isSyncingUI = true;

            try
            {
                string unit = GetCurrentUnit();

                // Use preview values if available, else committed
                int outputW = _imageState.PreviewOutputWidthPx ?? _imageState.OutputWidthPx;
                int outputH = _imageState.PreviewOutputHeightPx ?? _imageState.OutputHeightPx;
                int marginL = _imageState.PreviewMarginLeftPx ?? _imageState.MarginLeftPx;
                int marginT = _imageState.PreviewMarginTopPx ?? _imageState.MarginTopPx;

                if (unit == "%")
                {
                    _widthSourceBox.Text = "100";
                    _heightSourceBox.Text = "100";

                    double percentW = (double)outputW / _imageState.SourceWidthPx * 100;
                    double percentH = (double)outputH / _imageState.SourceHeightPx * 100;

                    if (skipBox != _widthBox) _widthBox.Value = (int)Math.Round(Math.Max(1, percentW));
                    if (skipBox != _heightBox) _heightBox.Value = (int)Math.Round(Math.Max(1, percentH));
                    if (skipBox != _marginLeftBox) _marginLeftBox.Value = 0;
                    if (skipBox != _marginTopBox) _marginTopBox.Value = 0;
                }
                else
                {
                    int widthSourceDisplay = Math.Max(1, (int)Math.Round(
                        UnitConverter.ConvertPixelsToUnit(_imageState.SourceWidthPx, unit)));
                    int heightSourceDisplay = Math.Max(1, (int)Math.Round(
                        UnitConverter.ConvertPixelsToUnit(_imageState.SourceHeightPx, unit)));
                    int widthOutputDisplay = Math.Max(1, (int)Math.Round(
                        UnitConverter.ConvertPixelsToUnit(outputW, unit)));
                    int heightOutputDisplay = Math.Max(1, (int)Math.Round(
                        UnitConverter.ConvertPixelsToUnit(outputH, unit)));

                    _widthSourceBox.Text = widthSourceDisplay.ToString();
                    _heightSourceBox.Text = heightSourceDisplay.ToString();

                    if (skipBox != _widthBox) _widthBox.Value = widthOutputDisplay;
                    if (skipBox != _heightBox) _heightBox.Value = heightOutputDisplay;

                    if (skipBox != _marginLeftBox)
                        _marginLeftBox.Value = (int)Math.Round(
                            UnitConverter.ConvertPixelsToUnit(marginL, unit));
                    if (skipBox != _marginTopBox)
                        _marginTopBox.Value = (int)Math.Round(
                            UnitConverter.ConvertPixelsToUnit(marginT, unit));
                }
            }
            finally
            {
                _isSyncingUI = false;
                _onCropOverlayUpdateNeeded?.Invoke();
            }
        }

        #endregion

        #region Crop Overlay Nudge

        public void MoveOverlay(int dx, int dy)
        {
            int newLeft = _imageState.MarginLeftPx + dx;
            int newTop = _imageState.MarginTopPx + dy;

            newLeft = Math.Clamp(newLeft, 0, _imageState.SourceWidthPx - _imageState.OutputWidthPx);
            newTop = Math.Clamp(newTop, 0, _imageState.SourceHeightPx - _imageState.OutputHeightPx);

            if (newLeft != _imageState.MarginLeftPx || newTop != _imageState.MarginTopPx)
            {
                _imageState.MarginLeftPx = newLeft;
                _imageState.MarginTopPx = newTop;
                RefreshUI();
            }
        }

        #endregion
    }
}
