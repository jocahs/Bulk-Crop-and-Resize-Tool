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
            _imageState.OutputWidthPx = Math.Max(1, _imageState.OutputWidthPx);
            _imageState.OutputHeightPx = Math.Max(1, _imageState.OutputHeightPx);

            if (_imageState.IsCropMode)
            {
                _imageState.OutputWidthPx = Math.Min(_imageState.OutputWidthPx, _imageState.SourceWidthPx);
                _imageState.OutputHeightPx = Math.Min(_imageState.OutputHeightPx, _imageState.SourceHeightPx);
            }

            _imageState.MarginLeftPx = Math.Max(0, _imageState.MarginLeftPx);
            _imageState.MarginTopPx = Math.Max(0, _imageState.MarginTopPx);

            if (_imageState.IsCropMode)
            {
                if (_imageState.MarginLeftPx + _imageState.OutputWidthPx > _imageState.SourceWidthPx)
                    _imageState.OutputWidthPx = _imageState.SourceWidthPx - _imageState.MarginLeftPx;

                if (_imageState.MarginTopPx + _imageState.OutputHeightPx > _imageState.SourceHeightPx)
                    _imageState.OutputHeightPx = _imageState.SourceHeightPx - _imageState.MarginTopPx;

                _imageState.MarginLeftPx = Math.Min(_imageState.MarginLeftPx, _imageState.SourceWidthPx - _imageState.OutputWidthPx);
                _imageState.MarginTopPx = Math.Min(_imageState.MarginTopPx, _imageState.SourceHeightPx - _imageState.OutputHeightPx);
                _imageState.MarginLeftPx = Math.Max(0, _imageState.MarginLeftPx);
                _imageState.MarginTopPx = Math.Max(0, _imageState.MarginTopPx);
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

        private void UpdatePixelFromBox(IntegerUpDown box, ref int pixelField, int sourceDimensionPx, bool clampToMin = true)
        {
            string unit = GetCurrentUnit();
            double displayValue = box.Value ?? 0;

            if (unit == "%")
            {
                pixelField = (int)Math.Round(displayValue / 100.0 * sourceDimensionPx);
                if (clampToMin && pixelField < 1) pixelField = 1;
            }
            else
            {
                pixelField = UnitConverter.ConvertUnitToPixels(displayValue, unit, clampToMin);
            }

            RefreshUI(skipBox: box);
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
                        _marginLeftBox.Value = Math.Min(
                            (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.MarginLeftPx, unit)),
                            Math.Max(0, widthSourceDisplay - widthOutputDisplay));

                    if (skipBox != _marginTopBox)
                        _marginTopBox.Value = Math.Min(
                            (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.MarginTopPx, unit)),
                            Math.Max(0, heightSourceDisplay - heightOutputDisplay));
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

        public void OnDimensionBoxValueChanged(object sender, bool windowIsLoaded)
        {
            if (_isSyncingUI || !windowIsLoaded) return;
            
            if (sender == _widthBox)
                UpdatePixelFromBox(_widthBox, ref _imageState.OutputWidthPx, _imageState.SourceWidthPx, true);
            else if (sender == _heightBox)
                UpdatePixelFromBox(_heightBox, ref _imageState.OutputHeightPx, _imageState.SourceHeightPx, true);

            if (_aspectRatio.IsChecked == true)
                AdjustAspectRatio(anchorIsWidth: sender == _widthBox);
        }
        public void OnMarginBoxValueChanged(object sender, bool windowIsLoaded)
        {
            if (_isSyncingUI || !windowIsLoaded) return;

            if (sender == _marginLeftBox)
                UpdatePixelFromBox(_marginLeftBox, ref _imageState.MarginLeftPx, _imageState.SourceWidthPx, false);
            else if (sender == _marginTopBox)
                UpdatePixelFromBox(_marginTopBox, ref _imageState.MarginTopPx, _imageState.SourceHeightPx, false);
        }
        public void MarginBox_LostFocus()
        {
            if (_isSyncingUI) return;
            UpdateDimensionTextBoxes();
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
