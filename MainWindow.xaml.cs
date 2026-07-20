using BulkCropAndResizeTool.Controls;
using BulkCropAndResizeTool.Dialogs;
using BulkCropAndResizeTool.Helpers;
using BulkCropAndResizeTool.Models;
using BulkCropAndResizeTool.Services;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using Path = System.IO.Path;
using MessageBox = System.Windows.MessageBox;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BulkCropAndResizeTool
{
    public partial class MainWindow : Window
    {
        #region Fields

        private readonly ImageState _imageState = new();
        private readonly ViewportState _viewportState = new();
        private readonly FileService _fileService;
        private readonly LoggingService _logger;
        private readonly BatchProcessor _batchProcessor;
        private readonly CropOverlayController _cropController;
        private readonly ViewportController _viewportController;
        private readonly DimensionsController _dimensionsController;

        private string _lastValidSourcePath = AppConstants.DefaultSrcBoxText;
        private string _lastValidOutputPath = AppConstants.DefaultDstBoxText;
        private string? _userCustomText = null;
        private string? _lastAppliedDefaultText = null;
        private CancellationTokenSource? _cancellationTokenSource;
        private OverwriteAction? _batchAction = null;
        private int CurrentFileCount = 0;

        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            _fileService = new FileService();
            _logger = new LoggingService(LogTextBox, AppendLog);
            _batchProcessor = new BatchProcessor(_logger);
            _cropController = new CropOverlayController(CropOverlay, _imageState, _viewportState);
            _viewportController = new ViewportController( PreviewCanvas, PreviewImage, CropOverlay, HScrollBar, VScrollBar, ZoomLabel, PanModeBtn, _imageState, _viewportState, UpdateCropOverlay);
            _dimensionsController = new DimensionsController(new DimensionControls(WidthSourceBox, HeightSourceBox, WidthBox, HeightBox, MarginLeftBox, MargintopBox, UnitPixels, UnitMM, UnitPer, ModeResize, AspectRatio, AspectRatioGroup, MarginsSettings, CropOverlay), _imageState, UpdateCropOverlay, SetActionBtnText ); InitializeApplication();
        }
        #endregion

        #region Initialization
        private void InitializeApplication()
        {
            CropOverlay.Visibility = Visibility.Hidden;
            RightPanelGrid.IsEnabled = false;
            UnitPer.IsEnabled = false;
            CurrentFileCount = 0;
            _dimensionsController.RefreshUI();
            _dimensionsController.UpdateUnitAvailability();
            UpdateFilenameUI();

            RegisterEvents();
            Loaded += (s, e) => Width = MinWidth;
        }

        private void RegisterEvents()
        {
            // Action buttons
            ModeCrop.Checked += Mode_CheckedChanged;
            ModeResize.Checked += Mode_CheckedChanged;

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
            MarginLeftBox.LostFocus += MarginBox_LostFocus;
            MargintopBox.LostFocus += MarginBox_LostFocus;

            // Filename
            ModePrefix.Checked += (s, e) => UpdateFilenameUI();
            ModeSuffix.Checked += (s, e) => UpdateFilenameUI();
            PreSufBox.LostFocus += PreSufBox_LostFocus;
            OverwriteChk.Checked += (s, e) => UpdateFilenameAvailability();
            OverwriteChk.Unchecked += (s, e) => UpdateFilenameAvailability();

            // Image interactions
            PreviewImage.MouseDown += PreviewImage_MouseDown;
            PreviewImage.MouseMove += PreviewImage_MouseMove;
            PreviewImage.MouseUp += PreviewImage_MouseUp;
            PreviewImage.SizeChanged += (s, e) => UpdateCropOverlay();
            PreviewCanvas.MouseWheel += PreviewArea_MouseWheel;
            PreviewCanvas.MouseLeave += (s, e) => Cursor = Cursors.Arrow;
            PreviewCanvas.PreviewMouseDown += PreviewCanvas_PreviewMouseDown;
            PreviewCanvas.MouseMove += PreviewCanvas_MouseMove;

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
            CancelBtn.Click += CancelBtn_Click;

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

        #endregion

        #region Image Loading
        private void LoadPreviewImage(string filePath)
        {
            try
            {
                string unit = _dimensionsController.GetCurrentUnit();
                bool hasPreviousImage = _imageState.OriginalImage != null;
                double previousWidthDisplay = WidthBox.Value ?? 0;
                double previousHeightDisplay = HeightBox.Value ?? 0;

                var image = ImageProcessingService.LoadImageFromFile(filePath) ?? throw new Exception("Failed to load image.");
                _imageState.OriginalImage = image;
                _imageState.CurrentRotation = 0;
                _imageState.PreviousRotation = 0;
                _imageState.SetSourceDimensions(image.PixelWidth, image.PixelHeight);

                if (hasPreviousImage)
                {
                    if (unit == "%")
                    {
                        _imageState.OutputWidthPx = (int)Math.Round(previousWidthDisplay / 100.0 * _imageState.SourceWidthPx);
                        _imageState.OutputHeightPx = (int)Math.Round(previousHeightDisplay / 100.0 * _imageState.SourceHeightPx);
                    }
                    else
                    {
                        _imageState.OutputWidthPx = UnitConverter.ConvertUnits(previousWidthDisplay, unit, true);
                        _imageState.OutputHeightPx = UnitConverter.ConvertUnits(previousHeightDisplay, unit, true);
                    }
                }
                else
                {
                    _imageState.OutputWidthPx = _imageState.SourceWidthPx / 2;
                    _imageState.OutputHeightPx = _imageState.SourceHeightPx / 2;
                }

                PreviewImage.Width = _imageState.SourceWidthPx;
                PreviewImage.Height = _imageState.SourceHeightPx;

                UpdateDisplayedImage();
                _imageState.CurrentImagePath = filePath;
                UpdateSourceFilename();

                _dimensionsController.RefreshUI();

                CropOverlay.Visibility = Visibility.Visible;
                RightPanelGrid.IsEnabled = true;
                ActionBtn.IsEnabled = true;

                _logger.Log($"Loaded: {Path.GetFileName(filePath)}");
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

            if (CropMath.IsQuarterTurn(_imageState.CurrentRotation))
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
            _viewportController.UpdateTransform();

            // Apply rotation to image
            var display = ImageProcessingService.RotateImage(_imageState.OriginalImage, _imageState.CurrentRotation);
            PreviewImage.Source = display ?? _imageState.OriginalImage;
        }
        private void RotateImage(double degrees)
        {
            _imageState.CurrentRotation += degrees;
            UpdateDisplayedImage();
            _dimensionsController.RefreshUI();
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

            // Use preview values during editing for live feedback
            int marginLeft = _imageState.PreviewMarginLeftPx ?? _imageState.MarginLeftPx;
            int marginTop = _imageState.PreviewMarginTopPx ?? _imageState.MarginTopPx;
            int outputW = _imageState.PreviewOutputWidthPx ?? _imageState.OutputWidthPx;
            int outputH = _imageState.PreviewOutputHeightPx ?? _imageState.OutputHeightPx;

            Canvas.SetLeft(CropOverlay, marginLeft);
            Canvas.SetTop(CropOverlay, marginTop);
            CropOverlay.Width = Math.Max(1, outputW);
            CropOverlay.Height = Math.Max(1, outputH);

            CropOverlay.Visibility = _imageState.IsCropMode ? Visibility.Visible : Visibility.Hidden;
        }

        private void TransformCropRectangle(double deltaAngle, int oldW, int oldH)
        {
            var (marginLeftPx, marginTopPx, widthPx, heightPx) = CropMath.RotateCropRect(
                deltaAngle, oldW, oldH,
                _imageState.MarginLeftPx, _imageState.MarginTopPx,
                _imageState.OutputWidthPx, _imageState.OutputHeightPx);

            _imageState.MarginLeftPx = marginLeftPx;
            _imageState.MarginTopPx = marginTopPx;
            _imageState.OutputWidthPx = widthPx;
            _imageState.OutputHeightPx = heightPx;
        }
        #endregion

        #region Filename Management

        private void UpdateFilenameUI()
        {
            UpdateSourceFilename();
            bool isResize = ModeResize.IsChecked == true;
            bool isPrefix = ModePrefix.IsChecked == true;

            string defaultText = GetDefaultFilenameText(isResize, isPrefix);
            string currentText = PreSufBox.Text;

            bool isStillDefault = _lastAppliedDefaultText == null || currentText == _lastAppliedDefaultText;
            if (!isStillDefault)
                _userCustomText = currentText;

            PreSufBox.Text = _userCustomText ?? defaultText;
            _lastAppliedDefaultText = defaultText;

            LayoutPositioning();
        }
        private string GetDefaultFilenameText(bool isResize, bool isPrefix)
        {
            if (isResize)
            {
                int w, h;
                if (UnitPer.IsChecked == true)
                {
                    w = _imageState.OutputWidthPx;
                    h = _imageState.OutputHeightPx;
                }
                else
                {
                    w = WidthBox.Value ?? 0;
                    h = HeightBox.Value ?? 0;
                }
                return isPrefix ? $"{w}x{h}_" : $"_{w}x{h}";
            }
            return isPrefix ? AppConstants.DefaultCropPrefix : AppConstants.DefaultCropSuffix;
        }
        private void UpdateFilenameAvailability()
        {
            bool isEnabled = OverwriteChk.IsChecked == true;
            ModePrefix.IsEnabled = !isEnabled;
            ModeSuffix.IsEnabled = !isEnabled;
            PreSufBox.Visibility = isEnabled ? Visibility.Collapsed : Visibility.Visible;

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

            if (System.IO.File.Exists(path))
            {
                baseName = Path.GetFileNameWithoutExtension(path);
                fileExtension = Path.GetExtension(path);
            }
            else if (Directory.Exists(path))
            {
                foreach (var ext in AppConstants.ImageExtensions)
                {
                    var files = Directory.GetFiles(path, ext, SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        baseName = Path.GetFileNameWithoutExtension(files[0]);
                        fileExtension = Path.GetExtension(files[0]);
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
                    outputFolder = Path.GetDirectoryName(SrcBox.Text) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
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

            // Check if source is a folder or file
            if (Directory.Exists(SrcBox.Text))
            {
                await ProcessBatchAsync(SrcBox.Text, outputFolder);
                return;
            }

            // Check if a single file is selected
            if (System.IO.File.Exists(SrcBox.Text))
            {
                // Load the image if not already loaded
                if (_imageState.OriginalImage == null || _imageState.CurrentImagePath != SrcBox.Text)
                {
                    LoadPreviewImage(SrcBox.Text);
                }

                // Process the single image
                await ProcessSingleImageAsync(outputFolder);
                return;
            }

            _logger.Log("No valid source selected. Please select an image file or folder.");
        }
        private async Task ProcessSingleImageAsync(string outputFolder)
        {
            if (_imageState.OriginalImage == null)
            {
                _logger.Log("No image loaded.");
                return;
            }

            bool isResize = ModeResize.IsChecked == true;
            string unit = _dimensionsController.GetCurrentUnit();

            var processedImage = ImageProcessingService.ProcessImage(
                _imageState.OriginalImage!,
                isResize,
                unit,
                _imageState.CurrentRotation,
                _imageState.OutputWidthPx,
                _imageState.OutputHeightPx,
                _imageState.MarginLeftPx,
                _imageState.MarginTopPx,
                _imageState.SourceWidthPx,
                _imageState.SourceHeightPx);

            if (processedImage == null)
            {
                _logger.Log("Failed to process image.");
                return;
            }

            string originalName = string.IsNullOrEmpty(_imageState.CurrentImagePath) ? "image" : Path.GetFileName(_imageState.CurrentImagePath);
            string ext = Path.GetExtension(originalName);
            if (string.IsNullOrWhiteSpace(ext)) ext = AppConstants.DefaultExtension;

            string finalBase;
            string preSuf = (PreSufBox.Text ?? "").Trim();
            string sourcePart = (NameSource.Text ?? "").Trim();

            if (OverwriteChk.IsChecked == true)
                finalBase = Path.GetFileNameWithoutExtension(originalName);
            else if (ModePrefix.IsChecked == true)
                finalBase = $"{preSuf}{sourcePart}";
            else
                finalBase = $"{sourcePart}{preSuf}";

            string saveFileName = finalBase + ext;
            string savePath = Path.Combine(outputFolder, saveFileName);

            if (System.IO.File.Exists(savePath) && OverwriteChk.IsChecked == false)
            {
                var res = UIHelpers.ShowMessage($"File already exists:\n{savePath}\nOverwrite?",
                                         "Overwrite?",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning,
                                         MessageBoxResult.No);
                if (res != MessageBoxResult.Yes)
                {
                    int count = 1;
                    string baseName = Path.GetFileNameWithoutExtension(saveFileName);
                    while (System.IO.File.Exists(savePath))
                    {
                        saveFileName = $"{baseName}_{count}{ext}";
                        savePath = Path.Combine(outputFolder, saveFileName);
                        count++;
                    }
                }
            }

            try
            {
                ImageProcessingService.SaveImage(processedImage, savePath, ext);
                _logger.Log($"Image saved to: {savePath}");

                if (OpenAfterChk.IsChecked == true)
                    Process.Start("explorer.exe", outputFolder);
            }
            catch (Exception ex)
            {
                _logger.Log($"Processing failed: {ex.Message}");
            }
        }
        private async Task ProcessBatchAsync(string folderPath, string outputFolder)
        {
            var files = _fileService.GetImageFiles(folderPath);
            if (files.Count == 0)
            {
                _logger.Log("No image files found in the source folder.");
                return;
            }
            else CurrentFileCount = files.Count;

            _logger.Log($"Starting batch processing for {files.Count} images...");

            bool isResize = ModeResize.IsChecked == true;
            string unit = _dimensionsController.GetCurrentUnit();
            double angle = _imageState.CurrentRotation % 360;

            Progress.Maximum = files.Count;
            Progress.Value = 0;
            CancelBtn.IsEnabled = true;
            _cancellationTokenSource = new CancellationTokenSource();

            var progressReporter = new Progress<int>(value =>
            {
                Progress.Value = value;
            });

            try
            {
                await _batchProcessor.ProcessBatchAsync(
                    files,
                    outputFolder,
                    isResize,
                    unit,
                    angle,
                    _imageState.OutputWidthPx,
                    _imageState.OutputHeightPx,
                    _imageState.MarginLeftPx,
                    _imageState.MarginTopPx,
                    _imageState.SourceWidthPx,
                    _imageState.SourceHeightPx,
                    PreSufBox.Text ?? "",
                    ModePrefix.IsChecked == true,
                    OverwriteChk.IsChecked == true,
                    progressReporter,
                    AppendLog,
                    ShouldSkipFile,
                    _cancellationTokenSource.Token);

                if (OpenAfterChk.IsChecked == true)
                    Process.Start("explorer.exe", outputFolder);
            }
            catch (OperationCanceledException)
            {
                _logger.Log("Batch processing cancelled.");
            }
            finally
            {
                CancelBtn.IsEnabled = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }
        private bool ShouldSkipFile(string saveFileName, string savePath)
        {
            if (!Dispatcher.CheckAccess())
                return Dispatcher.Invoke(() => ShouldSkipFile(saveFileName, savePath));
            if (OverwriteChk.IsChecked == false && System.IO.File.Exists(savePath))
            {
                if (_batchAction.HasValue)
                    return _batchAction.Value == OverwriteAction.SkipAll;

                var dialog = new OverwritePromptDialog { Owner = this };
                bool? result = dialog.ShowDialog();
                if (result == true)
                {
                    var action = dialog.Result;
                    if (action == OverwriteAction.Skip) return true;
                    if (action == OverwriteAction.SkipAll)
                    {
                        _batchAction = OverwriteAction.SkipAll;
                        return true;
                    }
                    if (action == OverwriteAction.OverwriteAll)
                    {
                        _batchAction = OverwriteAction.OverwriteAll;
                    }
                }
                else
                {
                    _logger.Log($"Skipped: {saveFileName}");
                    return true;
                }
            }
            return false;
        }
        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }
        #endregion

        #region Event Handlers

        private void Mode_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _dimensionsController.UpdateUnitAvailability();
            UpdateFilenameUI();
        }

        private void Unit_CheckedChanged(object sender, RoutedEventArgs e)
        {
            string unit = _dimensionsController.GetCurrentUnit();
            Resources["UnitText"] = unit == "px" ? "px" : unit == "mm" ? "mm" : "%";
            _dimensionsController.OnUnitChanged();
            UpdateFilenameUI();
        }

        #endregion

        #region Reset
        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            _imageState.Reset();
            _viewportState.Reset();

            AspectRatio.IsChecked = false;
            ModeCrop.IsChecked = true;
            UnitPixels.IsChecked = true;
            ModePrefix.IsChecked = true;
            OverwriteChk.IsChecked = false;
            OpenAfterChk.IsChecked = true;
            Progress.Value = 0;
            CancelBtn.IsEnabled = false;
            _userCustomText = null;

            _dimensionsController.RefreshUI();
            UpdateFilenameUI();
            _logger.Log("Settings reset to default.");
        }

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            var result = UIHelpers.ShowMessage(
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
        private void AppendLog(string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AppendLog(text));
                return;
            }
            LogTextBox.AppendText(text + "\n");
        }

        #endregion

        #region Zoom Event Handlers
        private void FitBtn_Click(object sender, RoutedEventArgs e) => _viewportController.ZoomToFit();

        private void ActualSizeBtn_Click(object sender, RoutedEventArgs e) => _viewportController.ZoomToActual();

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e) => _viewportController.ZoomIn();

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e) => _viewportController.ZoomOut();

        private void PanModeBtn_Click(object sender, RoutedEventArgs e) => _viewportController.TogglePanMode();
        #endregion

        #region Image Interaction Event Handlers
        private void ScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            _viewportController.OnScrollBarValueChanged(sender, e);
        private void PreviewImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_viewportState.IsPanMode || e.LeftButton != MouseButtonState.Pressed) return;

            _viewportController.BeginPan(e.GetPosition(this));
            e.Handled = true;
        }

        private void PreviewImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_viewportState.IsPanning) return;

            _viewportController.UpdatePan(e.GetPosition(this));
            e.Handled = true;
        }

        private void PreviewImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_viewportState.IsPanning)
            {
                _viewportController.EndPan();
                e.Handled = true;
            }
        }

        private void PreviewArea_MouseWheel(object sender, MouseWheelEventArgs e) =>
            _viewportController.HandleMouseWheel(e);

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
            double tolerance = 10.0 / _viewportState.CurrentScale;

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

            _cropController.StartManipulation(pos, resizeMode);
            e.Handled = true;
        }
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
            double tolerance = 10.0 / _viewportState.CurrentScale;

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
            if (_cropController.IsManipulating)
            {
                Point pos = e.GetPosition(PreviewCanvas);
                _cropController.UpdateManipulation(pos, () => _dimensionsController.UpdateDimensionTextBoxes());
                e.Handled = true;
            }
        }

        private void CropOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_cropController.IsManipulating)
            {
                _cropController.EndManipulation();
                Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        #endregion

        #region Dimension Event Handlers
        private void DimensionBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _dimensionsController.OnDimensionBoxPreviewChanged(sender, IsLoaded);
        }

        private void DimensionBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _dimensionsController.CommitOutputChanges();
            UpdateFilenameUI();
        }
        private void MarginBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Use preview method instead of commit
            _dimensionsController.OnMarginBoxPreviewChanged(sender);
        }
        private void MarginBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Commit the preview values now
            _dimensionsController.CommitMarginChanges();
        }
        #endregion

        #region Filename Event Handlers

        private void PreSufBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text = PreSufBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                var result = UIHelpers.ShowMessage(
                    "Prefix/Suffix additional name is empty. Overwrite?",
                    "Overwrite?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                _userCustomText = null; // clear any stale custom text before reapplying the default
                UpdateFilenameUI();

                if (result == MessageBoxResult.Yes)
                {
                    OverwriteChk.IsChecked = true;
                }
            }
        }

        #endregion

        #region Aspect Ratio Event Handler
        private void AspectRatio_Checked(object sender, RoutedEventArgs e)
        {
            if (AspectRatio.IsChecked == true)
            {
                _dimensionsController.AdjustAspectRatio(anchorIsWidth: true);
            }
        }
        #endregion

        #region Window Keyboard Handler
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_imageState.IsEditingOutput || _imageState.IsEditingMargin)
                {
                    // Cancel preview, revert to committed values
                    _imageState.ClearPreviews();
                    _dimensionsController.RefreshUI();
                    // Move focus away to cancel editing
                    Keyboard.ClearFocus();
                    e.Handled = true;
                    return;
                }
            }
            if (_imageState.IsResizeMode || CropOverlay.Visibility != Visibility.Visible)
                return;
            if (_cropController.IsManipulating) return;

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

            _dimensionsController.MoveOverlay(dx, dy);
            e.Handled = true;
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
                    dialog.InitialDirectory = Path.GetDirectoryName(initialPath);
                    dialog.FileName = Path.GetFileName(initialPath);
                }
                catch { }
            }

            if (dialog.ShowDialog() == true)
            {
                SrcBox.Text = dialog.FileName;
                _lastValidSourcePath = dialog.FileName;
                OutputLabel.Content = "Output";
                LoadPath(dialog.FileName);
            }
        }

        private void BrowseForFolder(string initialPath = "")
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select a folder containing images";
            dialog.ShowNewFolderButton = false;

            if (!string.IsNullOrEmpty(initialPath) && System.IO.Directory.Exists(initialPath))
            {
                dialog.SelectedPath = initialPath;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedFolder = dialog.SelectedPath;
                ValidateSourceFolder(selectedFolder);
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
        private void LoadPath(string path)
        {
            if (System.IO.File.Exists(path))
            {
                string? validPath = _fileService.GetValidDirectoryPath(path);
                if (DstBox.Text == AppConstants.DefaultDstBoxText || DstBox.Text == _fileService.GetValidDirectoryPath(_lastValidSourcePath))
                {
                    if (!string.IsNullOrWhiteSpace(validPath))
                        SetDirectoryPath(validPath);
                }
                _logger.Log($"Selected file: {Path.GetFileName(path)}");
                ActionBtn.IsEnabled = true;
                _lastValidSourcePath = path;
                LoadPreviewImage(path);
            }
            else if (Directory.Exists(path))
            {
                var files = _fileService.GetImageFiles(path);
                if (files.Count > 0)
                {
                    CurrentFileCount = files.Count;
                    string? oldSourceDir = _fileService.GetValidDirectoryPath(_lastValidSourcePath);
                    bool shouldUpdateDst = DstBox.Text == AppConstants.DefaultDstBoxText || DstBox.Text == oldSourceDir;

                    LoadPreviewImage(files[0]);
                    _lastValidSourcePath = path;
                    _logger.Log($"Selected folder: {path}");
                    _logger.Log($"Using first image: {Path.GetFileName(files[0])}");
                    ActionBtn.IsEnabled = true;

                    if (shouldUpdateDst)
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
                UIHelpers.ShowMessage(
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
            OutputLabel.Content = (files.Count == 0) ? "Output" : "Max Output";
            SetActionBtnText();
            LoadPath(selectedFolder);
        }
        private void SetActionBtnText()
        {
            if (string.IsNullOrWhiteSpace(SrcBox.Text) || SrcBox.Text == AppConstants.DefaultSrcBoxText)
            {
                ActionBtn.Content = (ModeCrop.IsChecked == true) ? AppConstants.DefaultSingleCrop : AppConstants.DefaultSingleResize;
                return;
            }

            ActionBtn.Content = ModeCrop.IsChecked == true
                ? (CurrentFileCount <= 1 ? AppConstants.DefaultSingleCrop : AppConstants.DefaultMultiCrop)
                : (CurrentFileCount <= 1 ? AppConstants.DefaultSingleResize : AppConstants.DefaultMultiResize);
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
            if (System.IO.File.Exists(path) || Directory.Exists(path))
            {
                if (_lastValidSourcePath != SrcBox.Text)
                {
                    ValidateSourceFolder(path);
                }
            }
            else
            {
                UIHelpers.ShowMessage(
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
                SetDirectoryPath(AppConstants.DefaultDstBoxText);
                DstBox.Foreground = System.Windows.Media.Brushes.MediumPurple;
                DstBox.FontStyle = FontStyles.Italic;
                return;
            }

            string? validPath = _fileService.GetValidDirectoryPath(path);
            if (!string.IsNullOrWhiteSpace(validPath))
            {
                SetDirectoryPath(validPath);
            }
            else
            {
                UIHelpers.ShowMessage(
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