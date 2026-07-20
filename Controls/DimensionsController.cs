using System;
using System.Windows;
using System.Windows.Controls;
using BulkCropAndResizeTool.Helpers;
using BulkCropAndResizeTool.Models;
using Xceed.Wpf.Toolkit;

namespace BulkCropAndResizeTool.Controls
{
    public record DimensionControls(
    TextBox WidthSourceBox,
    TextBox HeightSourceBox,
    IntegerUpDown WidthBox,
    IntegerUpDown HeightBox,
    IntegerUpDown MarginLeftBox,
    IntegerUpDown MarginTopBox,
    RadioButton UnitPixels,
    RadioButton UnitMM,
    RadioButton UnitPercent,
    RadioButton ModeResize,
    CheckBox AspectRatio,
    GroupBox AspectRatioGroup,
    GroupBox MarginsSettings,
    Border CropOverlay
    );

    public class DimensionsController(
        DimensionControls controls,
        ImageState imageState,
        Action onCropOverlayUpdateNeeded,
        Action setActionBtnText)
    {
        private readonly TextBox _widthSourceBox = controls.WidthSourceBox;
        private readonly TextBox _heightSourceBox = controls.HeightSourceBox;
        private readonly IntegerUpDown _widthBox = controls.WidthBox;
        private readonly IntegerUpDown _heightBox = controls.HeightBox;
        private readonly IntegerUpDown _marginLeftBox = controls.MarginLeftBox;
        private readonly IntegerUpDown _marginTopBox = controls.MarginTopBox;
        private readonly RadioButton _unitPixels = controls.UnitPixels;
        private readonly RadioButton _unitMM = controls.UnitMM;
        private readonly RadioButton _unitPercent = controls.UnitPercent;
        private readonly RadioButton _modeResize = controls.ModeResize;
        private readonly CheckBox _aspectRatio = controls.AspectRatio;
        private readonly GroupBox _aspectRatioGroup = controls.AspectRatioGroup;
        private readonly GroupBox _marginsSettings = controls.MarginsSettings;
        private readonly Border _cropOverlay = controls.CropOverlay;
        private readonly ImageState _imageState = imageState;
        private readonly Action _onCropOverlayUpdateNeeded = onCropOverlayUpdateNeeded;
        private readonly Action _setActionBtnText = setActionBtnText;
        private IntegerUpDown? _activeOutputBox;
        private IntegerUpDown? _activeMarginBox;

        private bool _isSyncingUI;

        #region Unit Management

        public string GetCurrentUnit()
        {
            return UnitConverter.GetCurrentUnit(_unitMM.IsChecked == true, _unitPercent.IsChecked == true);
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
            _imageState.OutputWidthPx = Math.Max(1, _imageState.OutputWidthPx);
            _imageState.OutputHeightPx = Math.Max(1, _imageState.OutputHeightPx);

            _imageState.MarginLeftPx = Math.Max(0, _imageState.MarginLeftPx);
            _imageState.MarginTopPx = Math.Max(0, _imageState.MarginTopPx);

            if (_imageState.IsCropMode)
            {
                _imageState.OutputWidthPx = Math.Min(_imageState.OutputWidthPx, _imageState.SourceWidthPx);
                _imageState.OutputHeightPx = Math.Min(_imageState.OutputHeightPx, _imageState.SourceHeightPx);

                _imageState.MarginLeftPx = Math.Min(_imageState.MarginLeftPx, _imageState.SourceWidthPx - 1);
                _imageState.MarginTopPx = Math.Min(_imageState.MarginTopPx, _imageState.SourceHeightPx - 1);

                if (_imageState.MarginLeftPx + _imageState.OutputWidthPx > _imageState.SourceWidthPx)
                    _imageState.OutputWidthPx = _imageState.SourceWidthPx - _imageState.MarginLeftPx;

                if (_imageState.MarginTopPx + _imageState.OutputHeightPx > _imageState.SourceHeightPx)
                    _imageState.OutputHeightPx = _imageState.SourceHeightPx - _imageState.MarginTopPx;
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
            RefreshUI();
        }

        #endregion

        #region Dimension / Margin Box Events

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
                    int widthSourceDisplay = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.SourceWidthPx, unit)));
                    int heightSourceDisplay = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.SourceHeightPx, unit)));
                    int widthOutputDisplay = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(outputW, unit)));
                    int heightOutputDisplay = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(outputH, unit)));

                    _widthSourceBox.Text = widthSourceDisplay.ToString();
                    _heightSourceBox.Text = heightSourceDisplay.ToString();

                    if (skipBox != _widthBox) _widthBox.Value = widthOutputDisplay;
                    if (skipBox != _heightBox) _heightBox.Value = heightOutputDisplay;

                    if (skipBox != _marginLeftBox)
                        _marginLeftBox.Value = Math.Min(
                            (int)Math.Round(UnitConverter.ConvertPixelsToUnit(marginL, unit)),
                            Math.Max(0, widthSourceDisplay - widthOutputDisplay));

                    if (skipBox != _marginTopBox)
                        _marginTopBox.Value = Math.Min(
                            (int)Math.Round(UnitConverter.ConvertPixelsToUnit(marginT, unit)),
                            Math.Max(0, heightSourceDisplay - heightOutputDisplay));
                }
            }
            finally
            {
                _isSyncingUI = false;
                _onCropOverlayUpdateNeeded?.Invoke();
            }
        }
        #endregion

        #region Preview-Commit Pattern

        public void OnDimensionBoxPreviewChanged(object sender, bool windowIsLoaded)
        {
            if (_isSyncingUI || !windowIsLoaded) return;

            _activeOutputBox = sender as IntegerUpDown;
            _imageState.IsEditingOutput = true;
            _imageState.IsEditingMargin = false;

            int? boxValue = _activeOutputBox?.Value;
            if (boxValue == null)
            {
                // Box cleared mid-edit — don't treat blank as 0. Leave everything else as-is.
                UpdateDimensionTextBoxes(skipBox: _activeOutputBox);
                return;
            }

            double displayValue = boxValue.Value;
            string unit = GetCurrentUnit();

            // Compute what the output WOULD be (preview)
            int previewOutputW = _imageState.OutputWidthPx;
            int previewOutputH = _imageState.OutputHeightPx;

            if (sender == _widthBox)
            {
                previewOutputW = unit == "%"
                    ? (int)Math.Round(displayValue / 100.0 * _imageState.SourceWidthPx)
                    : UnitConverter.ConvertUnits(displayValue, unit, true);
            }
            else if (sender == _heightBox)
            {
                previewOutputH = unit == "%"
                    ? (int)Math.Round(displayValue / 100.0 * _imageState.SourceHeightPx)
                    : UnitConverter.ConvertUnits(displayValue, unit, true);
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
            UpdateDimensionTextBoxes(skipBox: _activeOutputBox);
            _onCropOverlayUpdateNeeded?.Invoke();
        }

        public void OnMarginBoxPreviewChanged(object sender)
        {
            if (_isSyncingUI) return;

            _activeMarginBox = sender as IntegerUpDown;
            _imageState.IsEditingMargin = true;
            _imageState.IsEditingOutput = false;

            int? boxValue = _activeMarginBox?.Value;
            if (boxValue == null)
            {
                UpdateDimensionTextBoxes(skipBox: _activeMarginBox);
                return;
            }

            double displayValue = boxValue.Value;
            string unit = GetCurrentUnit();

            // Compute preview margins
            int previewMarginL = _imageState.MarginLeftPx;
            int previewMarginT = _imageState.MarginTopPx;

            if (sender == _marginLeftBox)
            {
                previewMarginL = unit == "%"
                    ? (int)Math.Round(displayValue / 100.0 * _imageState.SourceWidthPx)
                    : UnitConverter.ConvertUnits(displayValue, unit, false);
            }
            else if (sender == _marginTopBox)
            {
                previewMarginT = unit == "%"
                    ? (int)Math.Round(displayValue / 100.0 * _imageState.SourceHeightPx)
                    : UnitConverter.ConvertUnits(displayValue, unit, false);
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

            UpdateDimensionTextBoxes(skipBox: _activeMarginBox);
            _onCropOverlayUpdateNeeded?.Invoke();
        }

        public void CommitOutputChanges()
        {
            if (!_imageState.IsEditingOutput) return;

            bool boxIsEmpty = _activeOutputBox?.Value == null;

            if (!boxIsEmpty)
            {
                if (_imageState.PreviewOutputWidthPx.HasValue)
                    _imageState.OutputWidthPx = _imageState.PreviewOutputWidthPx.Value;
                if (_imageState.PreviewOutputHeightPx.HasValue)
                    _imageState.OutputHeightPx = _imageState.PreviewOutputHeightPx.Value;
                if (_imageState.PreviewMarginLeftPx.HasValue)
                    _imageState.MarginLeftPx = _imageState.PreviewMarginLeftPx.Value;
                if (_imageState.PreviewMarginTopPx.HasValue)
                    _imageState.MarginTopPx = _imageState.PreviewMarginTopPx.Value;
            }
            _imageState.ClearPreviews();
            _activeOutputBox = null;

            // Full refresh with committed values
            RefreshUI();
        }

        public void CommitMarginChanges()
        {
            if (!_imageState.IsEditingMargin) return;

            bool boxIsEmpty = _activeMarginBox?.Value == null;

            if (!boxIsEmpty)
            {
                if (_imageState.PreviewMarginLeftPx.HasValue)
                    _imageState.MarginLeftPx = _imageState.PreviewMarginLeftPx.Value;
                if (_imageState.PreviewMarginTopPx.HasValue)
                    _imageState.MarginTopPx = _imageState.PreviewMarginTopPx.Value;
                if (_imageState.PreviewOutputWidthPx.HasValue)
                    _imageState.OutputWidthPx = _imageState.PreviewOutputWidthPx.Value;
                if (_imageState.PreviewOutputHeightPx.HasValue)
                    _imageState.OutputHeightPx = _imageState.PreviewOutputHeightPx.Value;
            }

            _imageState.ClearPreviews();
            _activeMarginBox = null;

            RefreshUI();
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
