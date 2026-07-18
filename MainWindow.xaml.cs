using BulkCropAndResizeTool.Dialogs;
using BulkCropAndResizeTool.Helpers;
using BulkCropAndResizeTool.Models;
using BulkCropAndResizeTool.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;

namespace BulkCropAndResizeTool
{
    public partial class MainWindow : Window
    {
        #region Fields

        private readonly ImageState _imageState = new();
        private readonly ViewportState _viewportState = new();
        private readonly FilenameOptions _filenameOptions = new();
        private readonly ImageProcessingService _imageService;
        private readonly FileService _fileService;
        private readonly LoggingService _logger;
        private bool _isUpdatingUI = false;
        private string _lastValidSourcePath = AppConstants.DefaultSrcBoxText;
        private string _lastValidOutputPath = AppConstants.DefaultDstBoxText;
        private string? _userCustomText = null;
        private bool _isManipulating = false;
        private string _resizeMode = string.Empty;
        private Point _startMousePos;
        private int _startMarginLeftPx, _startMarginTopPx, _startWidthPx, _startHeightPx;
        
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            _imageService = new ImageProcessingService();
            _fileService = new FileService();
            _logger = new LoggingService(LogTextBox, AppendLog);

            InitializeApplication();
        }
        #endregion

        #region Initialization
        private void InitializeApplication()
        {
            CropOverlay.Visibility = Visibility.Hidden;
            RightPanelGrid.IsEnabled = false;
            UnitPer.IsEnabled = false;

            RefreshUI();
            UpdateUnitAvailability();
            UpdateFilenameUI();

            RegisterEvents();
            Loaded += MainWindow_Loaded;
        }

        private void RegisterEvents()
        {
            // Action buttons
            ActionCrop.Checked += Action_CheckedChanged;
            ActionResize.Checked += Action_CheckedChanged;

            // Zoom controls
            FitBtn.Click += FitBtn_Click;
            ActualSizeBtn.Click += ActualSizeBtn_Click;
            ZoomInBtn.Click += ZoomInBtn_Click;
            ZoomOutBtn.Click += ZoomOutBtn_Click;
            PanModeBtn.Click += PanModeBtn_Click;

            // Unit selection
            UnitPixels.Checked += Unit_CheckedChanged;
            UnitMM.Checked += Unit_CheckedChanged;
            UnitPer.Checked += Unit_CheckedChanged;

            // Dimension boxes
            WidthBox.LostFocus += DimensionBox_LostFocus;
            HeightBox.LostFocus += DimensionBox_LostFocus;
            WidthBox.ValueChanged += DimensionBox_ValueChanged;
            HeightBox.ValueChanged += DimensionBox_ValueChanged;
            MarginLeftBox.ValueChanged += MarginBox_ValueChanged;
            MargintopBox.ValueChanged += MarginBox_ValueChanged;

            // Filename
            ModePrefix.Checked += ModePrefix_Checked;
            ModeSuffix.Checked += ModeSuffix_Checked;
            PreSufBox.LostFocus += PreSufBox_LostFocus;
            OverwriteChk.Checked += Overwrite_CheckedChanged;
            OverwriteChk.Unchecked += Overwrite_CheckedChanged;

            // Image interactions
            PreviewImage.MouseDown += PreviewImage_MouseDown;
            PreviewImage.MouseMove += PreviewImage_MouseMove;
            PreviewImage.MouseUp += PreviewImage_MouseUp;
            PreviewImage.SizeChanged += (s, e) => UpdateCropOverlay();
            PreviewCanvas.MouseWheel += PreviewArea_MouseWheel;
            PreviewCanvas.MouseLeave += (s, e) => Cursor = Cursors.Arrow;
            PreviewCanvas.PreviewMouseDown += PreviewCanvas_PreviewMouseDown;

            // Crop overlay
            CropOverlay.MouseMove += CropOverlay_MouseMove;
            CropOverlay.MouseUp += CropOverlay_MouseUp;

            // Scrolling
            HScrollBar.ValueChanged += ScrollBar_ValueChanged;
            VScrollBar.ValueChanged += ScrollBar_ValueChanged;

            // Actions
            ActionBtn.Click += ActionBtn_Click;
            ResetBtn.Click += ResetBtn_Click;
            ResetAll.Click += ResetAll_Click;

            // Rotation
            Rotate180.Click += (s, e) => RotateImage(180);
            RotateMinus90.Click += (s, e) => RotateImage(-90);
            RotateMore90.Click += (s, e) => RotateImage(90);

            // Browsing
            SrcBrowse.Click += SrcBrowse_Click;
            DstBrowse.Click += DstBrowse_Click;

            // Keyboard
            PreviewKeyDown += Window_PreviewKeyDown;

            // Aspect Ratio
            AspectRatio.Checked += AspectRatio_Checked;
            AspectRatio.Unchecked += AspectRatio_Checked;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Width = MinWidth;
        }
        #endregion

        #region Image Loading
        private void LoadPreviewImage(string filePath)
        {
            try
            {
                var image = ImageProcessingService.LoadImageFromFile(filePath) ?? throw new Exception("Failed to load image.");
                _imageState.OriginalImage = image;
                _imageState.CurrentRotation = 0;
                _imageState.PreviousRotation = 0;
                _imageState.SetSourceDimensions(image.PixelWidth, image.PixelHeight);

                // Reset output to half size if this is the first load
                bool wasDefaultHalfSize = _imageState.OutputWidthPx == _imageState.SourceWidthPx / 2 &&
                                          _imageState.OutputHeightPx == _imageState.SourceHeightPx / 2;
                if (wasDefaultHalfSize)
                {
                    _imageState.OutputWidthPx = _imageState.SourceWidthPx / 2;
                    _imageState.OutputHeightPx = _imageState.SourceHeightPx / 2;
                }

                PreviewImage.Width = _imageState.SourceWidthPx;
                PreviewImage.Height = _imageState.SourceHeightPx;

                UpdateDisplayedImage();
                _imageState.CurrentImagePath = filePath;
                UpdateSourceFilename();

                CropOverlay.Visibility = Visibility.Visible;
                RightPanelGrid.IsEnabled = true;
                ActionBtn.IsEnabled = true;

                _logger.Log($"Loaded: {System.IO.Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                ClearPreview();
                _logger.Log($"Failed to load preview: {ex.Message}");
            }
        }

        private void ClearPreview()
        {
            PreviewImage.Source = null;
            CropOverlay.Visibility = Visibility.Hidden;
            RightPanelGrid.IsEnabled = false;
            ActionBtn.IsEnabled = false;
            _imageState.OriginalImage = null;
        }

        private void UpdateDisplayedImage()
        {
            if (_imageState.OriginalImage == null) return;

            int oldW = _imageState.SourceWidthPx;
            int oldH = _imageState.SourceHeightPx;

            // Compute dimensions after rotation
            int w = _imageState.OriginalImage.PixelWidth;
            int h = _imageState.OriginalImage.PixelHeight;
            double angle = NormalizeAngle(_imageState.CurrentRotation);

            bool shouldSwap = Math.Abs(angle - 90) < 0.1 || Math.Abs(angle - 270) < 0.1;
            if (shouldSwap)
            {
                w = _imageState.OriginalImage.PixelHeight;
                h = _imageState.OriginalImage.PixelWidth;
            }

            // Apply rotation to crop rectangle if angle changed
            double deltaAngle = _imageState.CurrentRotation - _imageState.PreviousRotation;
            if (Math.Abs(deltaAngle) > 0.1)
            {
                TransformCropRectangle(deltaAngle, oldW, oldH);
                _imageState.PreviousRotation = _imageState.CurrentRotation;
            }

            _imageState.SetSourceDimensions(w, h);

            PreviewImage.Width = w;
            PreviewImage.Height = h;
            UpdatePreviewTransform();

            // Apply rotation to image
            var display = ImageProcessingService.RotateImage(_imageState.OriginalImage, _imageState.CurrentRotation);
            PreviewImage.Source = display ?? _imageState.OriginalImage;
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 360;
            if (angle < 0) angle += 360;
            return angle;
        }

        private void RotateImage(double degrees)
        {
            _imageState.CurrentRotation += degrees;
            UpdateDisplayedImage();
        }
        #endregion

        #region Preview Transform
        private void UpdatePreviewTransform()
        {
            if (_imageState.OriginalImage == null || _imageState.SourceWidthPx <= 0)
            {
                PreviewCanvas.RenderTransform = null;
                _viewportState.CurrentScale = 1.0;
                UpdateZoomLabel();
                ResetScrollBars();
                return;
            }

            double canvasW = PreviewCanvas.ActualWidth;
            double canvasH = PreviewCanvas.ActualHeight;
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

            UpdateScrollBars(canvasW, canvasH);

            var group = new TransformGroup();
            group.Children.Add(new ScaleTransform(scale, scale));
            group.Children.Add(new TranslateTransform(_viewportState.PanX, _viewportState.PanY));
            PreviewCanvas.RenderTransform = group;

            UpdateZoomLabel();
            UpdateCropOverlay();
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

        private void UpdateScrollBars(double canvasW, double canvasH)
        {
            _isUpdatingUI = true;
            try
            {
                double hRange = -_viewportState.MinPanX;
                HScrollBar.Minimum = 0;
                HScrollBar.Maximum = hRange;
                HScrollBar.ViewportSize = canvasW;
                HScrollBar.Value = Math.Clamp(-_viewportState.PanX, 0, hRange);

                double vRange = -_viewportState.MinPanY;
                VScrollBar.Minimum = 0;
                VScrollBar.Maximum = vRange;
                VScrollBar.ViewportSize = canvasH;
                VScrollBar.Value = Math.Clamp(-_viewportState.PanY, 0, vRange);
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }

        private void ResetScrollBars()
        {
            _isUpdatingUI = true;
            try
            {
                HScrollBar.Minimum = 0;
                HScrollBar.Maximum = 0;
                HScrollBar.Value = 0;
                VScrollBar.Minimum = 0;
                VScrollBar.Maximum = 0;
                VScrollBar.Value = 0;
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }

        private void UpdateZoomLabel()
        {
            if (_imageState.OriginalImage == null)
            {
                ZoomLabel.Content = "No image";
                return;
            }

            ZoomLabel.Content = _viewportState.ZoomMode switch
            {
                ZoomMode.Fit => "Fit",
                ZoomMode.Actual => "1:1",
                ZoomMode.Custom => $"{_viewportState.CurrentScale * 100:F0}%",
                _ => ""
            };
        }
        #endregion

        #region Crop Overlay
        private void UpdateCropOverlay()
        {
            if (_imageState.OriginalImage == null || _imageState.SourceWidthPx <= 0)
            {
                CropOverlay.Visibility = Visibility.Hidden;
                return;
            }

            Canvas.SetLeft(CropOverlay, _imageState.MarginLeftPx);
            Canvas.SetTop(CropOverlay, _imageState.MarginTopPx);
            CropOverlay.Width = Math.Max(1, _imageState.OutputWidthPx);
            CropOverlay.Height = Math.Max(1, _imageState.OutputHeightPx);
            CropOverlay.RenderTransform = null;

            CropOverlay.Visibility = _imageState.IsCropMode ? Visibility.Visible : Visibility.Hidden;
        }

        private void TransformCropRectangle(double deltaAngle, int oldW, int oldH)
        {
            double angle = NormalizeAngle(deltaAngle);
            double left = _imageState.MarginLeftPx;
            double top = _imageState.MarginTopPx;
            double width = _imageState.OutputWidthPx;
            double height = _imageState.OutputHeightPx;

            if (Math.Abs(angle - 90) < 0.1) // +90°
            {
                _imageState.MarginLeftPx = (int)Math.Round(oldH - top - height);
                _imageState.MarginTopPx = (int)Math.Round(left);
                _imageState.OutputWidthPx = (int)Math.Round(height);
                _imageState.OutputHeightPx = (int)Math.Round(width);
            }
            else if (Math.Abs(angle - 180) < 0.1) // 180°
            {
                _imageState.MarginLeftPx = (int)Math.Round(oldW - left - width);
                _imageState.MarginTopPx = (int)Math.Round(oldH - top - height);
            }
            else if (Math.Abs(angle - 270) < 0.1) // -90°
            {
                _imageState.MarginLeftPx = (int)Math.Round(top);
                _imageState.MarginTopPx = (int)Math.Round(oldW - left - width);
                _imageState.OutputWidthPx = (int)Math.Round(height);
                _imageState.OutputHeightPx = (int)Math.Round(width);
            }
        }
        #endregion

        #region UI Refresh
        private void RefreshUI()
        {
            ClampCropRectangle();
            UpdateMaxValues();
            UpdateDimensionTextBoxes();
        }

        private void UpdateDimensionTextBoxes()
        {
            if (_isUpdatingUI) return;
            _isUpdatingUI = true;

            try
            {
                string unit = GetCurrentUnit();

                if (unit == "%")
                {
                    WidthSourceBox.Text = "100";
                    HeightSourceBox.Text = "100";

                    double percentW = (double)_imageState.OutputWidthPx / _imageState.SourceWidthPx * 100;
                    double percentH = (double)_imageState.OutputHeightPx / _imageState.SourceHeightPx * 100;
                    WidthBox.Value = (int)Math.Round(Math.Max(1, percentW));
                    HeightBox.Value = (int)Math.Round(Math.Max(1, percentH));
                    MarginLeftBox.Value = 0;
                    MargintopBox.Value = 0;
                }
                else
                {
                    WidthSourceBox.Text = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.SourceWidthPx, unit))).ToString();
                    HeightSourceBox.Text = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.SourceHeightPx, unit))).ToString();

                    WidthBox.Value = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.OutputWidthPx, unit)));
                    HeightBox.Value = Math.Max(1, (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.OutputHeightPx, unit)));
                    MarginLeftBox.Value = (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.MarginLeftPx, unit));
                    MargintopBox.Value = (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.MarginTopPx, unit));
                }
            }
            finally
            {
                _isUpdatingUI = false;
                UpdateCropOverlay();
            }
        }

        private void UpdateMaxValues()
        {
            string unit = GetCurrentUnit();

            if (_imageState.IsCropMode)
            {
                int maxWidthPx = _imageState.SourceWidthPx - _imageState.MarginLeftPx;
                int maxHeightPx = _imageState.SourceHeightPx - _imageState.MarginTopPx;

                WidthBox.Maximum = (int)Math.Ceiling(UnitConverter.ConvertPixelsToUnit(maxWidthPx, unit));
                HeightBox.Maximum = (int)Math.Ceiling(UnitConverter.ConvertPixelsToUnit(maxHeightPx, unit));
            }
            else
            {
                WidthBox.Maximum = 99999;
                HeightBox.Maximum = 99999;
            }
        }

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

        #region Unit Management
        private string GetCurrentUnit()
        {
            return UnitConverter.GetCurrentUnit(
                UnitPixels.IsChecked == true,
                UnitMM.IsChecked == true,
                UnitPer.IsChecked == true);
        }

        private void UpdateUnitAvailability()
        {
            bool isResize = ActionResize.IsChecked == true;
            UnitPer.IsEnabled = isResize;
            UnitPer.IsChecked = isResize;
            StackRatio.IsEnabled = isResize;
            AspectRatio.IsChecked = isResize;
            MarginsSettings.IsEnabled = !isResize;
            ActionBtn.Content = isResize ? "RESIZE IMAGE(S)" : "CROP IMAGE(S)";

            if (isResize) CropOverlay.Visibility = Visibility.Hidden;
            _imageState.IsCropMode = !isResize;

            if (!isResize && UnitPer.IsChecked == true)
                UnitPixels.IsChecked = true;
        }
        #endregion

        #region Filename Management
        private void UpdateFilenameUI()
        {
            UpdateSourceFilename();
            bool isResize = ActionResize.IsChecked == true;
            bool isPrefix = ModePrefix.IsChecked == true;

            string defaultText = GetDefaultFilenameText(isResize, isPrefix);
            string currentText = PreSufBox.Text;
            bool isDefaultText = currentText == AppConstants.DefaultCropSuffix ||
                               currentText == AppConstants.DefaultCropPrefix ||
                               currentText == AppConstants.DefaultResizeSuffix ||
                               currentText == AppConstants.DefaultResizePrefix;

            if (!isDefaultText)
                _userCustomText = currentText;

            PreSufBox.Text = _userCustomText ?? defaultText;
            LayoutPositioning();
        }

        private static string GetDefaultFilenameText(bool isResize, bool isPrefix)
        {
            if (isResize)
                return isPrefix ? AppConstants.DefaultResizePrefix : AppConstants.DefaultResizeSuffix;
            return isPrefix ? AppConstants.DefaultCropPrefix : AppConstants.DefaultCropSuffix;
        }

        private void UpdateFilenameAvailability()
        {
            bool isEnabled = OverwriteChk.IsChecked == true;
            ModePrefix.IsEnabled = !isEnabled;
            ModeSuffix.IsEnabled = !isEnabled;
            PreSufBox.IsEnabled = !isEnabled;

            if (!isEnabled)
            {
                LayoutPositioning();
            }
            else
            {
                NameStackPanel.Children.Clear();
                NameStackPanel.Children.Add(NameSource);
                NameStackPanel.Children.Add(NameExtension);
            }
        }

        private void UpdateSourceFilename()
        {
            string path = SrcBox.Text;

            if (string.IsNullOrEmpty(path) || path == AppConstants.DefaultSrcBoxText)
            {
                NameSource.Text = AppConstants.DefaultFilename;
                return;
            }

            string baseName = AppConstants.DefaultFilename;
            string fileExtension = AppConstants.DefaultExtension;

            if (File.Exists(path))
            {
                baseName = System.IO.Path.GetFileNameWithoutExtension(path);
                fileExtension = System.IO.Path.GetExtension(path);
            }
            else if (Directory.Exists(path))
            {
                foreach (var ext in AppConstants.ImageExtensions)
                {
                    var files = Directory.GetFiles(path, ext, SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        baseName = System.IO.Path.GetFileNameWithoutExtension(files[0]);
                        fileExtension = System.IO.Path.GetExtension(files[0]);
                        break;
                    }
                }
                if (baseName == AppConstants.DefaultFilename)
                    fileExtension = AppConstants.DefaultExtension;
            }

            NameSource.Text = baseName;
            NameExtension.Text = fileExtension;
        }

        private void LayoutPositioning()
        {
            bool isPrefix = ModePrefix.IsChecked == true;
            NameStackPanel.Children.Clear();

            if (isPrefix)
            {
                NameStackPanel.Children.Add(PreSufBox);
                NameStackPanel.Children.Add(NameSource);
                NameStackPanel.Children.Add(NameExtension);
            }
            else
            {
                NameStackPanel.Children.Add(NameSource);
                NameStackPanel.Children.Add(PreSufBox);
                NameStackPanel.Children.Add(NameExtension);
            }
        }
        #endregion

        #region Batch Processing
        private async void ActionBtn_Click(object sender, RoutedEventArgs e)
        {
            string? outputFolder = _fileService.GetValidDirectoryPath(DstBox.Text);
            if (outputFolder == null)
            {
                if (Directory.Exists(SrcBox.Text))
                    outputFolder = SrcBox.Text;
                else
                    outputFolder = System.IO.Path.GetDirectoryName(SrcBox.Text) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }

            try
            {
                if (!Directory.Exists(outputFolder))
                    Directory.CreateDirectory(outputFolder);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to create output folder: {ex.Message}");
                return;
            }

            if (Directory.Exists(SrcBox.Text))
            {
                await ProcessBatchAsync(SrcBox.Text, outputFolder);
                return;
            }

            await ProcessSingleImageAsync(outputFolder);
        }

        private async Task ProcessBatchAsync(string folderPath, string outputFolder)
        {
            var files = _fileService.GetImageFiles(folderPath);
            if (files.Count == 0)
            {
                _logger.Log("No image files found in the source folder.");
                return;
            }

            _logger.Log($"Starting batch processing for {files.Count} images...");
            bool isResize = ActionResize.IsChecked == true;
            string unit = GetCurrentUnit();

            int total = files.Count;
            int processed = 0;
            Progress.Maximum = total;
            Progress.Value = 0;
            CancelBtn.IsEnabled = true;

            double angle = _imageState.CurrentRotation % 360;
            OverwriteAction? batchAction = null;

            double? percentW = null, percentH = null;
            if (unit == "%")
            {
                percentW = _imageState.OutputWidthPx * 100.0 / _imageState.SourceWidthPx;
                percentH = _imageState.OutputHeightPx * 100.0 / _imageState.SourceHeightPx;
            }

            foreach (string filePath in files)
            {
                var image = ImageProcessingService.LoadImageFromFile(filePath);
                if (image == null)
                {
                    _logger.Log($"Failed to load: {System.IO.Path.GetFileName(filePath)}");
                    processed++;
                    Progress.Value = processed;
                    continue;
                }

                var processedImage = ImageProcessingService.ProcessImage( image, isResize, unit, angle, _imageState.OutputWidthPx, _imageState.OutputHeightPx, _imageState.MarginLeftPx, _imageState.MarginTopPx, percentW, percentH);

                if (processedImage == null)
                {
                    _logger.Log($"Failed to process: {System.IO.Path.GetFileName(filePath)}");
                    processed++;
                    Progress.Value = processed;
                    continue;
                }
                var (saveFileName, savePath, ext) = FilenameGenerator.GetOutputFileInfo(
                    filePath, outputFolder, PreSufBox.Text ?? "",
                    OverwriteChk.IsChecked == true,
                    ModePrefix.IsChecked == true);

                if (ShouldSkipFile(saveFileName, savePath, ref batchAction))
                {
                    _logger.Log($"Skipped: {saveFileName}");
                    processed++;
                    Progress.Value = processed;
                    continue;
                }

                try
                {
                    ImageProcessingService.SaveImage(processedImage, savePath, ext);
                    _logger.Log($"Saved: {saveFileName}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error saving {saveFileName}: {ex.Message}");
                }

                processed++;
                Progress.Value = processed;
                await Task.Delay(1);
            }

            Progress.Value = total;
            CancelBtn.IsEnabled = false;
            _logger.Log($"Batch processing completed. {processed} files processed.");

            if (OpenAfterChk.IsChecked == true)
                Process.Start("explorer.exe", outputFolder);
        }

        private async Task ProcessSingleImageAsync(string outputFolder)
        {
            bool isResize = ActionResize.IsChecked == true;
            string unit = GetCurrentUnit();

            var processedImage = ImageProcessingService.ProcessImage(
                _imageState.OriginalImage!, isResize, unit, _imageState.CurrentRotation,
                _imageState.OutputWidthPx, _imageState.OutputHeightPx,
                _imageState.MarginLeftPx, _imageState.MarginTopPx,
                _imageState.SourceWidthPx, _imageState.SourceHeightPx);

            string originalName = string.IsNullOrEmpty(_imageState.CurrentImagePath) ? "image" : System.IO.Path.GetFileName(_imageState.CurrentImagePath);
            string ext = System.IO.Path.GetExtension(originalName);
            if (string.IsNullOrWhiteSpace(ext)) ext = AppConstants.DefaultExtension;

            string finalBase;
            string preSuf = (PreSufBox.Text ?? "").Trim();
            string sourcePart = (NameSource.Text ?? "").Trim();

            if (OverwriteChk.IsChecked == true)
                finalBase = System.IO.Path.GetFileNameWithoutExtension(originalName);
            else if (ModePrefix.IsChecked == true)
                finalBase = $"{preSuf}{sourcePart}";
            else
                finalBase = $"{sourcePart}{preSuf}";

            string saveFileName = finalBase + ext;
            string savePath = System.IO.Path.Combine(outputFolder, saveFileName);

            if (File.Exists(savePath) && OverwriteChk.IsChecked == false)
            {
                var res = System.Windows.MessageBox.Show($"File already exists:\n{savePath}\nOverwrite?",
                                         "Overwrite?",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning,
                                         MessageBoxResult.No);
                if (res != MessageBoxResult.Yes)
                {
                    int count = 1;
                    string baseName = System.IO.Path.GetFileNameWithoutExtension(saveFileName);
                    while (File.Exists(savePath))
                    {
                        saveFileName = $"{baseName}_{count}{ext}";
                        savePath = System.IO.Path.Combine(outputFolder, saveFileName);
                        count++;
                    }
                }
            }

            try
            {
                ImageProcessingService.SaveImage(processedImage!, savePath, ext);
                _logger.Log($"Image saved to: {savePath}");

                if (OpenAfterChk.IsChecked == true)
                    Process.Start("explorer.exe", outputFolder);
            }
            catch (Exception ex)
            {
                _logger.Log($"Processing failed: {ex.Message}");
            }
        }

        private bool ShouldSkipFile(string saveFileName, string savePath, ref OverwriteAction? batchAction)
        {
            if (OverwriteChk.IsChecked == false && File.Exists(savePath))
            {
                if (batchAction.HasValue)
                    return batchAction.Value == OverwriteAction.SkipAll;

                var dialog = new OverwritePromptDialog { Owner = this };
                bool? result = dialog.ShowDialog();
                if (result == true)
                {
                    var action = dialog.Result;
                    if (action == OverwriteAction.Skip) return true;
                    if (action == OverwriteAction.SkipAll) { batchAction = OverwriteAction.SkipAll; return true; }
                    if (action == OverwriteAction.OverwriteAll) { batchAction = OverwriteAction.OverwriteAll; }
                }
                else
                {
                    _logger.Log($"Skipped: {saveFileName}");
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Event Handlers
        // ... (keep all your existing event handlers but delegate to helper methods)

        // These are just examples - you'll need to wire them up
        private void Action_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateUnitAvailability();
            UpdateFilenameUI();
        }

        private void Unit_CheckedChanged(object sender, RoutedEventArgs e)
        {
            string unit = GetCurrentUnit();
            Resources["UnitText"] = unit == "px" ? "px" : unit == "mm" ? "mm" : "%";
            UpdateMaxValues();
            if (AspectRatio.IsChecked == true)
                AdjustAspectRatio();
            UpdateDimensionTextBoxes();
        }

        // ... (all other event handlers follow the same pattern)
        #endregion

        #region Reset
        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            _imageState.Reset();
            _viewportState.Reset();
            _userCustomText = null;

            AspectRatio.IsChecked = false;
            ActionCrop.IsChecked = true;
            UnitPixels.IsChecked = true;
            ModePrefix.IsChecked = true;
            OverwriteChk.IsChecked = false;
            PreSufBox.Text = AppConstants.DefaultCropPrefix;
            OpenAfterChk.IsChecked = true;

            Progress.Value = 0;
            CancelBtn.IsEnabled = false;

            RefreshUI();
            UpdateFilenameUI();
            _logger.Log("Settings reset to default.");
        }

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "This will clear the loaded image and reset all settings to their defaults. Continue?",
                "Reset Everything",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes) return;

            SrcBox.Text = AppConstants.DefaultSrcBoxText;
            DstBox.Text = AppConstants.DefaultDstBoxText;
            ClearPreview();
            _imageState.Reset();
            _viewportState.Reset();

            ResetBtn_Click(this, new RoutedEventArgs());
            _logger.Clear();
            _logger.Log("Everything reset to default.");
        }
        #endregion

        #region Logging (delegated to service)
        private void AppendLog(string text) => LogTextBox.AppendText(text + "\n");
        #endregion

        #region Zoom Event Handlers
        private void FitBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_imageState.OriginalImage == null) return;
            _viewportState.ZoomMode = ZoomMode.Fit;
            _viewportState.PanX = 0;
            _viewportState.PanY = 0;
            UpdatePreviewTransform();
        }

        private void ActualSizeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_imageState.OriginalImage == null) return;
            _viewportState.ZoomMode = ZoomMode.Actual;
            _viewportState.PanX = 0;
            _viewportState.PanY = 0;
            UpdatePreviewTransform();
        }

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_imageState.OriginalImage == null) return;
            _viewportState.CustomZoom = _viewportState.CurrentScale * 1.1;
            _viewportState.ZoomMode = ZoomMode.Custom;
            UpdatePreviewTransform();
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_imageState.OriginalImage == null) return;
            _viewportState.CustomZoom = _viewportState.CurrentScale / 1.1;
            if (_viewportState.CustomZoom < 0.01) _viewportState.CustomZoom = 0.01;
            _viewportState.ZoomMode = ZoomMode.Custom;
            UpdatePreviewTransform();
        }

        private void PanModeBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewportState.IsPanMode = !_viewportState.IsPanMode;
            PanModeBtn.Background = _viewportState.IsPanMode ? Brushes.LightBlue : Brushes.Transparent;

            if (_viewportState.IsPanMode)
            {
                PreviewImage.Cursor = Cursors.Hand;
                CropOverlay.IsHitTestVisible = false;
            }
            else
            {
                PreviewImage.Cursor = Cursors.Arrow;
                CropOverlay.IsHitTestVisible = true;
            }
        }
        #endregion

        #region Scroll Event Handlers
        private void ScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI || _imageState.OriginalImage == null) return;

            if (sender == HScrollBar)
                _viewportState.PanX = -HScrollBar.Value;
            else if (sender == VScrollBar)
                _viewportState.PanY = -VScrollBar.Value;

            UpdatePreviewTransform();
        }
        #endregion

        #region Image Interaction Event Handlers
        private void PreviewImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_viewportState.IsPanMode || e.LeftButton != MouseButtonState.Pressed) return;

            _viewportState.IsPanning = true;
            _viewportState.PanStartMouse = e.GetPosition(this);
            _viewportState.PanStartX = _viewportState.PanX;
            _viewportState.PanStartY = _viewportState.PanY;
            PreviewImage.CaptureMouse();
            e.Handled = true;
        }

        private void PreviewImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_viewportState.IsPanning) return;

            Point current = e.GetPosition(this);
            double deltaX = current.X - _viewportState.PanStartMouse.X;
            double deltaY = current.Y - _viewportState.PanStartMouse.Y;
            _viewportState.PanX = _viewportState.PanStartX + deltaX;
            _viewportState.PanY = _viewportState.PanStartY + deltaY;
            UpdatePreviewTransform();
            e.Handled = true;
        }

        private void PreviewImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_viewportState.IsPanning)
            {
                _viewportState.IsPanning = false;
                PreviewImage.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void PreviewArea_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_imageState.OriginalImage == null) return;

            double step = e.Delta;

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                HScrollBar.Value = Math.Clamp(HScrollBar.Value - step, HScrollBar.Minimum, HScrollBar.Maximum);
            }
            else
            {
                VScrollBar.Value = Math.Clamp(VScrollBar.Value - step, VScrollBar.Minimum, VScrollBar.Maximum);
            }

            e.Handled = true;
        }

        private void PreviewCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewportState.IsPanMode) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (_imageState.IsResizeMode) return;

            Point pos = e.GetPosition(PreviewCanvas);
            double left = Canvas.GetLeft(CropOverlay);
            double top = Canvas.GetTop(CropOverlay);
            double right = left + CropOverlay.Width;
            double bottom = top + CropOverlay.Height;
            double tolerance = 20.0;

            bool nearLeft = Math.Abs(pos.X - left) < tolerance;
            bool nearRight = Math.Abs(pos.X - right) < tolerance;
            bool nearTop = Math.Abs(pos.Y - top) < tolerance;
            bool nearBottom = Math.Abs(pos.Y - bottom) < tolerance;

            // Determine resize mode
            string resizeMode;
            if (nearLeft && nearTop)
                resizeMode = "ResizeTopLeft";
            else if (nearRight && nearTop)
                resizeMode = "ResizeTopRight";
            else if (nearLeft && nearBottom)
                resizeMode = "ResizeBottomLeft";
            else if (nearRight && nearBottom)
                resizeMode = "ResizeBottomRight";
            else if (nearLeft)
                resizeMode = "ResizeLeft";
            else if (nearRight)
                resizeMode = "ResizeRight";
            else if (nearTop)
                resizeMode = "ResizeTop";
            else if (nearBottom)
                resizeMode = "ResizeBottom";
            else
                resizeMode = "Drag";

            bool inside = (pos.X >= left && pos.X <= right && pos.Y >= top && pos.Y <= bottom);
            if (resizeMode == "Drag" && !inside)
                return;

            // You need to add these fields to your class (see below)
            _isManipulating = true;
            _resizeMode = resizeMode;
            _startMousePos = pos;
            _startMarginLeftPx = _imageState.MarginLeftPx;
            _startMarginTopPx = _imageState.MarginTopPx;
            _startWidthPx = _imageState.OutputWidthPx;
            _startHeightPx = _imageState.OutputHeightPx;

            CropOverlay.CaptureMouse();
            e.Handled = true;
        }

        // Optional: Mouse move for cursor changes on crop overlay
        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_imageState.OriginalImage == null || CropOverlay.Visibility != Visibility.Visible || _imageState.IsResizeMode)
            {
                Cursor = Cursors.Arrow;
                return;
            }

            if (_viewportState.IsPanMode)
            {
                Cursor = Cursors.Hand;
                return;
            }

            Point pos = e.GetPosition(PreviewCanvas);
            double left = Canvas.GetLeft(CropOverlay);
            double top = Canvas.GetTop(CropOverlay);
            double right = left + CropOverlay.Width;
            double bottom = top + CropOverlay.Height;
            double tolerance = 20.0;

            bool nearLeft = Math.Abs(pos.X - left) < tolerance;
            bool nearRight = Math.Abs(pos.X - right) < tolerance;
            bool nearTop = Math.Abs(pos.Y - top) < tolerance;
            bool nearBottom = Math.Abs(pos.Y - bottom) < tolerance;

            Cursor cursor = Cursors.Arrow;

            if (nearLeft && nearTop)
                cursor = Cursors.SizeNWSE;
            else if (nearRight && nearTop)
                cursor = Cursors.SizeNESW;
            else if (nearLeft && nearBottom)
                cursor = Cursors.SizeNESW;
            else if (nearRight && nearBottom)
                cursor = Cursors.SizeNWSE;
            else if (nearLeft || nearRight)
                cursor = Cursors.SizeWE;
            else if (nearTop || nearBottom)
                cursor = Cursors.SizeNS;

            Cursor = cursor;
        }
        #endregion

        #region Crop Overlay Event Handlers
        private void CropOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (_viewportState.IsPanMode) return;
            if (!_isManipulating) return;
            if (_imageState.IsResizeMode) return;

            Point pos = e.GetPosition(PreviewCanvas);
            double deltaX = pos.X - _startMousePos.X;
            double deltaY = pos.Y - _startMousePos.Y;

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

            UpdateDimensionTextBoxes();
            e.Handled = true;
        }

        private void CropOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isManipulating)
            {
                _isManipulating = false;
                _resizeMode = string.Empty;
                CropOverlay.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }
        #endregion

        #region Dimension Event Handlers
        private void DimensionBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_isUpdatingUI || !IsLoaded) return;

            if (sender == WidthBox)
            {
                UpdatePixelFromBox(WidthBox, ref _imageState.OutputWidthPx, _imageState.SourceWidthPx, true);
            }
            else if (sender == HeightBox)
            {
                UpdatePixelFromBox(HeightBox, ref _imageState.OutputHeightPx, _imageState.SourceHeightPx, true);
            }
        }

        private void DimensionBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(AspectRatio.IsChecked == true)) return;
            if (_isUpdatingUI) return;

            string unit = GetCurrentUnit();

            if (sender == WidthBox)
            {
                int currentDisplay = WidthBox.Value ?? 0;
                int expectedDisplay = (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.OutputWidthPx, unit));
                if (currentDisplay != expectedDisplay)
                {
                    _isUpdatingUI = true;
                    WidthBox.Value = expectedDisplay;
                    _isUpdatingUI = false;
                    UpdateDimensionTextBoxes();
                }
            }
            else if (sender == HeightBox)
            {
                int currentDisplay = HeightBox.Value ?? 0;
                int expectedDisplay = (int)Math.Round(UnitConverter.ConvertPixelsToUnit(_imageState.OutputHeightPx, unit));
                if (currentDisplay != expectedDisplay)
                {
                    _isUpdatingUI = true;
                    HeightBox.Value = expectedDisplay;
                    _isUpdatingUI = false;
                    UpdateDimensionTextBoxes();
                }
            }
        }

        private void MarginBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_isUpdatingUI || !IsLoaded) return;

            if (sender == MarginLeftBox)
                UpdatePixelFromBox(MarginLeftBox, ref _imageState.MarginLeftPx, _imageState.SourceWidthPx, false);
            else if (sender == MargintopBox)
                UpdatePixelFromBox(MargintopBox, ref _imageState.MarginTopPx, _imageState.SourceHeightPx, false);

            RefreshUI();
        }
        #endregion

        #region Filename Event Handlers
        private void ModePrefix_Checked(object sender, RoutedEventArgs e)
        {
            UpdateFilenameUI();
        }

        private void ModeSuffix_Checked(object sender, RoutedEventArgs e)
        {
            UpdateFilenameUI();
        }

        private void PreSufBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text = PreSufBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                var result = System.Windows.MessageBox.Show(
                    "Prefix/Suffix additional name is empty. Overwrite?",
                    "Overwrite?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                bool isResize = _imageState.IsResizeMode;
                bool isPrefix = ModePrefix.IsChecked == true;
                string defaultText = GetDefaultFilenameText(isResize, isPrefix);

                PreSufBox.Text = defaultText;
                UpdateFilenameUI();

                if (result == MessageBoxResult.Yes)
                {
                    OverwriteChk.IsChecked = true;
                }
            }
        }

        private void Overwrite_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateFilenameAvailability();
        }
        #endregion

        #region Aspect Ratio Event Handler
        private void AspectRatio_Checked(object sender, RoutedEventArgs e)
        {
            if (AspectRatio.IsChecked == true)
            {
                AdjustAspectRatio();
            }
        }
        #endregion

        #region Window Keyboard Handler
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_imageState.IsResizeMode || CropOverlay.Visibility != Visibility.Visible)
                return;
            if (_isManipulating) return;

            int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            int dx = 0, dy = 0;

            switch (e.Key)
            {
                case Key.Left: dx = -step; break;
                case Key.Right: dx = step; break;
                case Key.Up: dy = -step; break;
                case Key.Down: dy = step; break;
                default: return;
            }

            MoveOverlay(dx, dy);
            e.Handled = true;
        }

        private void MoveOverlay(int dx, int dy)
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

        #region Browsing Event Handlers
        private void SrcBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FileFolderDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                if (dialog.IsFileSelected)
                {
                    BrowseForFile();
                }
                else
                {
                    BrowseForFolder();
                }
            }
        }

        private void BrowseForFile(string initialPath = "")
        {
            var dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                ValidateNames = true,
                Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff|All files|*.*"
            };

            if (!string.IsNullOrEmpty(initialPath))
            {
                try
                {
                    dialog.InitialDirectory = System.IO.Path.GetDirectoryName(initialPath);
                    dialog.FileName = System.IO.Path.GetFileName(initialPath);
                }
                catch { }
            }

            if (dialog.ShowDialog() == true)
            {
                SrcBox.Text = dialog.FileName;
                _lastValidSourcePath = dialog.FileName;
                LoadPath(dialog.FileName);
            }
        }

        private void BrowseForFolder(string initialPath = "")
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select a folder containing images";
            dialog.ShowNewFolderButton = false;

            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
            {
                dialog.SelectedPath = initialPath;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedFolder = dialog.SelectedPath;
                if (selectedFolder != _lastValidSourcePath)
                {
                    ValidateSourceFolder(selectedFolder);
                }
            }
        }

        private void DstBrowse_Click(object sender, RoutedEventArgs e)
        {
            BrowseOutputFolder();
        }

        private void BrowseOutputFolder(string initialPath = "")
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select a folder to save the image(s)";
            dialog.ShowNewFolderButton = true;

            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
            {
                dialog.SelectedPath = initialPath;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedPath = dialog.SelectedPath;
                _lastValidOutputPath = selectedPath;
                DstBox.Text = selectedPath;
                _logger.Log($"Output folder: {selectedPath}");
            }
        }
        #endregion

        #region Helper Methods
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

            RefreshUI();
        }

        private void AdjustAspectRatio()
        {
            if (!AspectRatio.IsChecked == true) return;
            if (_imageState.SourceWidthPx <= 0 || _imageState.SourceHeightPx <= 0) return;

            int targetW = _imageState.OutputWidthPx;
            int targetH = _imageState.OutputHeightPx;
            var (w, h) = AspectRatioHelper.FindBestAspectRatioPair(targetW, targetH,
                _imageState.SourceWidthPx, _imageState.SourceHeightPx);

            if (_imageState.IsCropMode)
            {
                w = Math.Min(w, _imageState.SourceWidthPx);
                h = Math.Min(h, _imageState.SourceHeightPx);
            }

            _imageState.OutputWidthPx = w;
            _imageState.OutputHeightPx = h;
            UpdateDimensionTextBoxes();
        }

        private void LoadPath(string path)
        {
            if (File.Exists(path))
            {
                string? validPath = _fileService.GetValidDirectoryPath(path);
                if (DstBox.Text == AppConstants.DefaultDstBoxText || DstBox.Text == _fileService.GetValidDirectoryPath(_lastValidSourcePath))
                {
                    if (validPath != null) SetDirectoryPath(validPath);
                }
                _logger.Log($"Selected file: {System.IO.Path.GetFileName(path)}");
                ActionBtn.IsEnabled = true;
                _lastValidSourcePath = path;
                LoadPreviewImage(path);
            }
            else if (Directory.Exists(path))
            {
                var files = _fileService.GetImageFiles(path);
                if (files.Count > 0)
                {
                    LoadPreviewImage(files[0]);
                    _logger.Log($"Using first image: {System.IO.Path.GetFileName(files[0])}");
                    _lastValidSourcePath = path;
                    _logger.Log($"Selected folder: {path}");
                    ActionBtn.IsEnabled = true;
                    SetDirectoryPath(path);
                }
                else
                {
                    _logger.Log($"No image files found in: {path}");
                    ActionBtn.IsEnabled = false;
                }
            }
            else
            {
                _logger.Log($"Path not found: {path}");
                ActionBtn.IsEnabled = false;
            }
        }

        private void ValidateSourceFolder(string selectedFolder)
        {
            var files = _fileService.GetImageFiles(selectedFolder);

            if (files.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "No image files found in the selected folder.",
                    "Invalid Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ActionBtn.IsEnabled = false;
                SrcBox.Text = _lastValidSourcePath;
                return;
            }

            SrcBox.Text = selectedFolder;
            _lastValidSourcePath = selectedFolder;
            LoadPath(selectedFolder);
        }

        private void SetDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                DstBox.Text = _lastValidOutputPath;
            }
            else
            {
                _lastValidOutputPath = DstBox.Text = path;
            }
        }
        #endregion

        #region Source/Destination Path Handlers

        private void SrcBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SrcBox.Text == AppConstants.DefaultSrcBoxText)
            {
                SrcBox.Text = "";
                SrcBox.Foreground = System.Windows.Media.Brushes.Black;
                SrcBox.FontStyle = FontStyles.Normal;
            }
        }

        private void SrcBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string path = SrcBox.Text;
            if (string.IsNullOrWhiteSpace(path) || path == AppConstants.DefaultSrcBoxText)
            {
                // Restore placeholder
                SrcBox.Text = AppConstants.DefaultSrcBoxText;
                SrcBox.Foreground = System.Windows.Media.Brushes.DarkOrange;
                SrcBox.FontStyle = FontStyles.Italic;
                ClearPreview();
                return;
            }

            // If it's a valid file or folder, load dimensions
            if (File.Exists(path) || Directory.Exists(path))
            {
                if (_lastValidSourcePath != SrcBox.Text)
                {
                    ValidateSourceFolder(path);
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "The path is not valid. Please check for invalid characters, reserved names, or missing drive.",
                    "Invalid Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                SrcBox.Text = _lastValidSourcePath;
                SrcBox.Foreground = System.Windows.Media.Brushes.DarkOrange;
                SrcBox.FontStyle = FontStyles.Italic;
            }
        }

        private void DstBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (DstBox.Text == AppConstants.DefaultDstBoxText)
            {
                DstBox.Text = "";
                DstBox.Foreground = System.Windows.Media.Brushes.Black;
                DstBox.FontStyle = FontStyles.Normal;
            }
        }

        private void DstBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string path = DstBox.Text;

            if (string.IsNullOrWhiteSpace(path) || path == AppConstants.DefaultDstBoxText)
            {
                SetDirectoryPath(path);
                DstBox.Foreground = System.Windows.Media.Brushes.MediumPurple;
                DstBox.FontStyle = FontStyles.Italic;
                return;
            }

            string? validPath = _fileService.GetValidDirectoryPath(path);
            if (validPath != null)
            {
                SetDirectoryPath(validPath);
                DstBox.Foreground = System.Windows.Media.Brushes.Black;
                DstBox.FontStyle = FontStyles.Normal;
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "The entered path is not valid for a folder. Please check for invalid characters, reserved names, or missing drive.",
                    "Invalid Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DstBox.Text = _lastValidOutputPath;
                DstBox.Foreground = System.Windows.Media.Brushes.MediumPurple;
                DstBox.FontStyle = FontStyles.Italic;
            }
        }

        #endregion

    }
}