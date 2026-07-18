using BulkCropAndResizeTool.Dialogs;
using BulkCropAndResizeTool.Helpers;
using BulkCropAndResizeTool.Models;
//using BulkCropAndResizeTool.Resources;
using BulkCropAndResizeTool.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;
using static System.Net.Mime.MediaTypeNames;

namespace BulkCropAndResizeTool
{
    public partial class MainWindow : Window
    {
        #region Constructor / Initialization
        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
            RegisterEvents();
            Loaded += MainWindow_Loaded;
        }
        private void InitializeApplication()
        {
            CropOverlay.Visibility = Visibility.Hidden;
            RightPanelGrid.IsEnabled = false;
            UnitPer.IsEnabled = false;

            RefreshCropUI();
            UpdateUnitAvailability();
            UpdateFilenameAvailability();
            UpdateFilenameLayout();
            UpdateSourceFilename();
            UpdateAllTextBoxes();
        }
        private void RegisterEvents()
        {
            ActionCrop.Checked += Action_CheckedChanged;
            ActionResize.Checked += Action_CheckedChanged;
            ActualSizebtn.Click += ActualSizeBtn_Click;
            AspectRatio.Checked += AspectRatio_Checked;
            AspectRatio.Unchecked += AspectRatio_Checked;
            ActionBtn.Click += ActionBtn_Click;
            CropOverlay.MouseMove += CropOverlay_MouseMove;
            CropOverlay.MouseUp += CropOverlay_MouseUp;
            FitBtn.Click += FitBtn_Click;
            HeightBox.LostFocus += HeightBox_LostFocus;
            HeightBox.ValueChanged += HeightBox_ValueChanged;
            HScrollBar.ValueChanged += HScrollBar_ValueChanged;
            MarginLeftBox.ValueChanged += MarginLeftBox_ValueChanged;
            MargintopBox.ValueChanged += MargintopBox_ValueChanged;
            ModePrefix.Checked += ModePrefix_Checked;
            ModeSuffix.Checked += ModeSuffix_Checked;
            OverwriteChk.Checked += NoOverwrite_CheckedChanged;
            OverwriteChk.Unchecked += NoOverwrite_CheckedChanged;
            PanModeBtn.Click += PanModeBtn_Click;
            PreSufBox.LostFocus += PreSufBox_LostFocus;
            PreviewBorder.MouseWheel += PreviewArea_MouseWheel;
            PreviewCanvas.MouseLeave += (s, e) => Cursor = Cursors.Arrow;
            PreviewCanvas.PreviewMouseDown += PreviewCanvas_PreviewMouseDown;
            PreviewImage.Loaded += (s, e) => UpdateCropOverlay();
            PreviewImage.MouseDown += PreviewImage_MouseDown;
            PreviewImage.MouseMove += PreviewImage_MouseMove;
            PreviewImage.MouseUp += PreviewImage_MouseUp;
            PreviewImage.SizeChanged += (s, e) => UpdateCropOverlay();
            ResetAll.Click += ResetAll_Click;
            ResetBtn.Click += ResetBtn_Click;
            Rotate180.Click += (s, e) => { _currentRotation += 180; UpdateDisplayedImage(); };
            RotateMinus90.Click += (s, e) => { _currentRotation -= 90; UpdateDisplayedImage(); };
            RotateMore90.Click += (s, e) => { _currentRotation += 90; UpdateDisplayedImage(); };
            this.PreviewKeyDown += Window_PreviewKeyDown;
            UnitMM.Checked += Unit_CheckedChanged;
            UnitPer.Checked += Unit_CheckedChanged;
            UnitPixels.Checked += Unit_CheckedChanged;
            VScrollBar.ValueChanged += VScrollBar_ValueChanged;
            WidthBox.LostFocus += WidthBox_LostFocus;
            WidthBox.ValueChanged += WidthBox_ValueChanged;
            ZoomInBtn.Click += ZoomInBtn_Click;
            ZoomOutBtn.Click += ZoomOutBtn_Click;
            PreviewCanvas.MouseMove += PreviewCanvas_MouseMove;
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
                var normalizedImage = ImageProcessor.LoadImageFromFile(filePath) ?? throw new Exception("Failed to load image.");
                _originalImage = normalizedImage;
                _currentRotation = 0;
                _previousRotation = 0;

                int w = normalizedImage.PixelWidth;
                int h = normalizedImage.PixelHeight;

                bool wasDefaultHalfSize =
                    outputWidthPx == sourceWidthPx / 2 &&
                    outputHeightPx == sourceHeightPx / 2;

                if (wasDefaultHalfSize)
                {
                    outputWidthPx = w / 2;
                    outputHeightPx = h / 2;
                }

                PreviewImage.Width = w;
                PreviewImage.Height = h;

                SetSourceDimensions(w, h);
                UpdatePreviewTransform();
                UpdateDisplayedImage();
                _currentImagePath = filePath;
                UpdateSourceFilename();
                CropOverlay.Visibility = Visibility.Visible;
                RightPanelGrid.IsEnabled = true;
                ActionBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                PreviewImage.Source = null;
                CropOverlay.Visibility = Visibility.Hidden;
                RightPanelGrid.IsEnabled = false;
                ActionBtn.IsEnabled = false;
                AppendLog($"Failed to load preview: {ex.Message}\n");
            }
        }
        private void UpdateDisplayedImage()
        {
            if (_originalImage == null) return;

            // Store the old dimensions (before rotation)
            int oldW = sourceWidthPx;
            int oldH = sourceHeightPx;

            // Compute new dimensions after rotation
            int w = _originalImage.PixelWidth;
            int h = _originalImage.PixelHeight;
            double angle = _currentRotation % 360;
            if (angle < 0) angle += 360;

            bool shouldSwap = Math.Abs(angle - 90) < 0.1 || Math.Abs(angle - 270) < 0.1;
            if (shouldSwap)
            {
                w = _originalImage.PixelHeight;
                h = _originalImage.PixelWidth;
            }

            // Apply the rotation to the crop rectangle (if the angle changed)
            double deltaAngle = _currentRotation - _previousRotation;
            if (Math.Abs(deltaAngle) > 0.1)
            {
                TransformCropRectangle(deltaAngle, oldW, oldH);
                _previousRotation = _currentRotation;
            }

            // Update source dimensions (used for scaling and max values)
            SetSourceDimensions(w, h);

            PreviewImage.Width = w;
            PreviewImage.Height = h;
            UpdatePreviewTransform();

            // Rotate the image itself (TransformedBitmap)
            BitmapSource display = _originalImage;
            if (Math.Abs(_currentRotation % 360) > 0.001)
            {
                var transform = new RotateTransform(_currentRotation);
                display = new TransformedBitmap(_originalImage, transform);
                display.Freeze();
            }
            PreviewImage.Source = display;
        }
        private BitmapSource ProcessImage(BitmapSource image, bool isResize, string unit, double angle, double? percentW, double? percentH)
        {
            if (isResize)
            {
                // Apply rotation
                BitmapSource rotated = image;
                if (Math.Abs(angle % 360) > 0.001)
                {
                    var transform = new RotateTransform(angle);
                    rotated = new TransformedBitmap(image, transform);
                    rotated.Freeze();
                }

                int targetW, targetH;
                if (unit == "%")
                {
                    targetW = (int)Math.Round(rotated.PixelWidth * percentW!.Value / 100.0);
                    targetH = (int)Math.Round(rotated.PixelHeight * percentH!.Value / 100.0);
                }
                else
                {
                    if (AspectRatio.IsChecked == true)
                    {
                        int targetSize;
                        if (rotated.PixelWidth >= rotated.PixelHeight)
                            targetSize = outputWidthPx;
                        else
                            targetSize = outputHeightPx;

                        double scale = targetSize / (double)Math.Max(rotated.PixelWidth, rotated.PixelHeight);
                        targetW = (int)Math.Round(rotated.PixelWidth * scale);
                        targetH = (int)Math.Round(rotated.PixelHeight * scale);
                    }
                    else
                    {
                        targetW = outputWidthPx;
                        targetH = outputHeightPx;
                    }
                }

                if (targetW < 1) targetW = 1;
                if (targetH < 1) targetH = 1;

                return ResizeImage(rotated, targetW, targetH)!;
            }
            else
            {
                // --- CROP (using absolute pixel values) ---
                int cropX = marginLeftPx;
                int cropY = marginTopPx;
                int cropW = outputWidthPx;
                int cropH = outputHeightPx;

                // The CropSingleImage method applies rotation and clamps
                return ImageProcessor.CropSingleImage(image, angle, cropX, cropY, cropW, cropH);
            }


        }
        private static BitmapSource? ResizeImage(BitmapSource source, int targetWidth, int targetHeight)
        {
            if (source == null) return null;
            if (targetWidth <= 0 || targetHeight <= 0) return source;
            double scaleX = targetWidth / (double)source.PixelWidth;
            double scaleY = targetHeight / (double)source.PixelHeight;
            var transform = new ScaleTransform(scaleX, scaleY);
            var resized = new TransformedBitmap(source, transform);
            resized.Freeze();
            return resized;
        }
        private void UpdatePreviewTransform()
        {
            if (_originalImage == null || sourceWidthPx <= 0 || sourceHeightPx <= 0)
            {
                PreviewCanvas.RenderTransform = null;
                _currentScale = 1.0;
                UpdateZoomLabel();
                ResetScrollBars();
                return;
            }

            double canvasW = PreviewCanvas.ActualWidth;   // 600
            double canvasH = PreviewCanvas.ActualHeight;  // 600
            double scale = 1.0;
            switch (_zoomMode)
            {
                case ZoomMode.Fit:
                    double scaleX = canvasW / sourceWidthPx;
                    double scaleY = canvasH / sourceHeightPx;
                    scale = Math.Min(scaleX, scaleY);
                    break;
                case ZoomMode.Actual:
                    scale = 1.0;
                    break;
                case ZoomMode.Custom:
                    scale = _customZoom;
                    break;
            }

            // Clamp scale to avoid extreme values
            if (scale < 0.01) scale = 0.01;
            if (scale > 100) scale = 100;

            _currentScale = scale;

            double scaledW = sourceWidthPx * scale;
            double scaledH = sourceHeightPx * scale;

            double minPanX = scaledW > canvasW ? (canvasW - scaledW) : 0;
            double maxPanX = 0;
            double minPanY = scaledH > canvasH ? (canvasH - scaledH) : 0;
            double maxPanY = 0;

            _minPanX = minPanX; _maxPanX = maxPanX;
            _minPanY = minPanY; _maxPanY = maxPanY;

            _panX = Clamp(_panX, minPanX, maxPanX);
            _panY = Clamp(_panY, minPanY, maxPanY);

            UpdateScrollBars(canvasW, canvasH);

            // --- CHANGE: No centering – image is top-left aligned ---
            double totalX = _panX;   // pan offsets only (initialized to 0)
            double totalY = _panY;

            var group = new TransformGroup();
            group.Children.Add(new ScaleTransform(scale, scale));
            group.Children.Add(new TranslateTransform(totalX, totalY));
            PreviewCanvas.RenderTransform = group;

            UpdateZoomLabel();
            UpdateCropOverlay();
        }

        #endregion

        #region Batch Processing
        private async void ActionBtn_Click(object sender, RoutedEventArgs e)
        {
            string? outputFolder = GetValidDirectoryPath(DstBox.Text);
            if (outputFolder == null)
            {
                // Fallback to source folder or default
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
                AppendLog($"Failed to create output folder: {ex.Message}\n");
                return;
            }

            // --- BATCH PROCESSING if source is a folder ---
            if (Directory.Exists(SrcBox.Text))
            {
                string[] extensions = (string[])AppConstants.ImageExtensions.Clone();
                var files = new List<string>();
                foreach (var pattern in extensions)   // <-- changed 'ext' to 'pattern'
                    files.AddRange(Directory.GetFiles(SrcBox.Text, pattern, SearchOption.TopDirectoryOnly));

                if (files.Count == 0)
                {
                    AppendLog("No image files found in the source folder.\n");
                    return;
                }

                AppendLog($"Starting batch processing for {files.Count} images...\n");
                bool isResize = ActionResize.IsChecked == true;
                string unit = GetCurrentUnit();
                await ProcessBatchAsync(SrcBox.Text, outputFolder, files, isResize, unit, sourceWidthPx, sourceHeightPx);
                return;
            }

            // --- SINGLE FILE PROCESSING ---
            bool resizeMode = ActionResize.IsChecked == true;

            // Apply rotation if any
            BitmapSource source = _originalImage!;
            if (Math.Abs(_currentRotation % 360) > 0.001)
            {
                var transform = new RotateTransform(_currentRotation);
                source = new TransformedBitmap(_originalImage!, transform);
                source.Freeze();
            }

            BitmapSource? finalImage;
            if (resizeMode)
            {
                string unit = GetCurrentUnit();
                int targetW, targetH;

                if (unit == "%")
                {
                    double percentW = outputWidthPx * 100.0 / sourceWidthPx;
                    double percentH = outputHeightPx * 100.0 / sourceHeightPx;
                    targetW = (int)Math.Round(source.PixelWidth * percentW / 100.0);
                    targetH = (int)Math.Round(source.PixelHeight * percentH / 100.0);
                }
                else // px or mm
                {
                    if (AspectRatio.IsChecked == true)
                    {
                        int targetSize;
                        if (source.PixelWidth >= source.PixelHeight)
                            targetSize = outputWidthPx;
                        else
                            targetSize = outputHeightPx;

                        double scale = targetSize / (double)Math.Max(source.PixelWidth, source.PixelHeight);
                        targetW = (int)Math.Round(source.PixelWidth * scale);
                        targetH = (int)Math.Round(source.PixelHeight * scale);
                    }
                    else
                    {
                        targetW = outputWidthPx;
                        targetH = outputHeightPx;
                    }
                }

                if (targetW < 1) targetW = 1;
                if (targetH < 1) targetH = 1;

                finalImage = ResizeImage(source, targetW, targetH)!;
            }
            else
            {
                // --- CROP SINGLE IMAGE (existing code) ---
                int x = marginLeftPx;
                int y = marginTopPx;
                int w = outputWidthPx;
                int h = outputHeightPx;

                // Clamp to image bounds (in rotated coordinates)
                int rw = source.PixelWidth;
                int rh = source.PixelHeight;
                x = Math.Max(0, Math.Min(x, rw - 1));
                y = Math.Max(0, Math.Min(y, rh - 1));
                w = Math.Max(1, Math.Min(w, rw - x));
                h = Math.Max(1, Math.Min(h, rh - y));

                var cropRect = new Int32Rect(x, y, w, h);
                finalImage = new CroppedBitmap(source, cropRect);
                finalImage.Freeze();
            }

            // --- Save the processed image (common for both) ---
            string originalName = string.IsNullOrEmpty(_currentImagePath) ? "image" : System.IO.Path.GetFileName(_currentImagePath);
            string ext = System.IO.Path.GetExtension(originalName);
            if (string.IsNullOrWhiteSpace(ext)) ext = AppConstants.DefaultExtension;

            string finalBase;
            string PreSufFromUI = (PreSufBox.Text ?? "").Trim();
            string SourcePart = (NameSource.Text ?? "").Trim();

            if (OverwriteChk.IsChecked == true)
                finalBase = System.IO.Path.GetFileNameWithoutExtension(originalName);
            else if (ModePrefix.IsChecked == true)
                finalBase = $"{PreSufFromUI}{SourcePart}";
            else
                finalBase = $"{SourcePart}{PreSufFromUI}";

            string saveFileName = finalBase + ext;
            string savePath = System.IO.Path.Combine(outputFolder, saveFileName);

            // Handle overwrite
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

            // Save
            try
            {
                BitmapEncoder encoder;
                string lowerExt = ext.ToLower();
                if (lowerExt == ".png") encoder = new PngBitmapEncoder();
                else if (lowerExt == ".bmp") encoder = new BmpBitmapEncoder();
                else if (lowerExt == ".gif") encoder = new GifBitmapEncoder();
                else encoder = new JpegBitmapEncoder();

                encoder.Frames.Add(BitmapFrame.Create(finalImage));
                if (encoder is JpegBitmapEncoder jpeg) jpeg.QualityLevel = 90;

                using (var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                    encoder.Save(stream);

                AppendLog($"Image saved to: {savePath}\n");

                if (OpenAfterChk.IsChecked == true)
                    Process.Start("explorer.exe", outputFolder);
            }
            catch (Exception ex)
            {
                AppendLog($"Processing failed: {ex.Message}\n");
            }
        }
        private async System.Threading.Tasks.Task ProcessBatchAsync(string folderPath, string outputFolder, List<string> files, bool isResize, string unit, int previewW, int previewH)
        {
            ArgumentNullException.ThrowIfNull(folderPath);

            int total = files.Count;
            int processed = 0;
            Progress.Maximum = total;
            Progress.Value = 0;
            CancelBtn.IsEnabled = true;

            double angle = _currentRotation % 360;
            OverwriteAction? batchAction = null;

            // Precompute percentages if unit is "%"
            double? percentW = null, percentH = null;
            if (unit == "%")
            {
                percentW = outputWidthPx * 100.0 / previewW;
                percentH = outputHeightPx * 100.0 / previewH;
            }

            foreach (string filePath in files)
            {
                var image = ImageProcessor.LoadImageFromFile(filePath);
                if (image == null)
                {
                    AppendLog($"Failed to load: {System.IO.Path.GetFileName(filePath)}\n");
                    processed++;
                    Progress.Value = processed;
                    continue;
                }

                BitmapSource processedImage = ProcessImage(image, isResize, unit, angle, percentW, percentH);

                var (saveFileName, savePath, ext) = FilenameGenerator.GetOutputFileInfo(filePath, outputFolder, PreSufBox.Text ?? "", OverwriteChk.IsChecked == true, ModePrefix.IsChecked == true);

                if (ShouldSkipExistingFile(saveFileName, savePath, ref batchAction))
                {
                    AppendLog($"Skipped: {saveFileName}\n");
                    processed++;
                    Progress.Value = processed;
                    continue;
                }

                // --- Save ---
                try
                {
                    BitmapEncoder encoder;
                    string lowerExt = ext.ToLower();
                    if (lowerExt == ".png") encoder = new PngBitmapEncoder();
                    else if (lowerExt == ".bmp") encoder = new BmpBitmapEncoder();
                    else if (lowerExt == ".gif") encoder = new GifBitmapEncoder();
                    else encoder = new JpegBitmapEncoder();

                    encoder.Frames.Add(BitmapFrame.Create(processedImage));
                    if (encoder is JpegBitmapEncoder jpeg) jpeg.QualityLevel = 90;

                    using (var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                        encoder.Save(stream);

                    AppendLog($"Saved: {saveFileName}\n");
                }
                catch (Exception ex)
                {
                    AppendLog($"Error saving {saveFileName}: {ex.Message}\n");
                }

                processed++;
                Progress.Value = processed;
                await System.Threading.Tasks.Task.Delay(1);
            }

            Progress.Value = total;
            CancelBtn.IsEnabled = false;
            AppendLog($"Batch processing completed. {processed} files processed.\n");

            if (OpenAfterChk.IsChecked == true)
                Process.Start("explorer.exe", outputFolder);
        }
        private bool ShouldSkipExistingFile(string saveFileName, string savePath, ref OverwriteAction? batchAction)
        {
            if (OverwriteChk.IsChecked == false && File.Exists(savePath))
            {
                if (batchAction.HasValue)
                {
                    if (batchAction.Value == OverwriteAction.SkipAll)
                    {
                        return true;
                    }
                    // OverwriteAll: proceed
                }
                else
                {
                    var dialog = new OverwritePromptDialog { Owner = this };
                    bool? result = dialog.ShowDialog();
                    if (result == true)
                    {
                        var action = dialog.Result;
                        if (action == OverwriteAction.Skip)
                        {
                            return true;
                        }
                        else if (action == OverwriteAction.SkipAll)
                        {
                            batchAction = OverwriteAction.SkipAll;
                            return true;
                        }
                        else if (action == OverwriteAction.OverwriteAll)
                        {
                            batchAction = OverwriteAction.OverwriteAll;
                        }
                        // Overwrite: proceed
                    }
                    else
                    {
                        AppendLog($"Skipped: {saveFileName}\n");
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Filename

        private void UpdateFilenameLayout()
        {
            // Update the source filename first
            UpdateSourceFilename();

            bool isResize = ActionResize.IsChecked == true;
            bool isPrefix = ModePrefix.IsChecked == true;

            // Determine default text for PreSufBox
            string defaultText;
            if (isResize)
                defaultText = isPrefix ? AppConstants.DefaultResizePrefix : AppConstants.DefaultResizeSuffix;
            else
                defaultText = isPrefix ? AppConstants.DefaultCropPrefix : AppConstants.DefaultCropSuffix;

            // Preserve user custom text
            string currentText = PreSufBox.Text;
            bool isDefaultText = currentText == AppConstants.DefaultCropSuffix || currentText == AppConstants.DefaultCropPrefix ||
                                 currentText == AppConstants.DefaultResizeSuffix || currentText == AppConstants.DefaultResizePrefix;
            if (!isDefaultText)
                userCustomText = currentText;

            // Set PreSufBox text
            PreSufBox.Text = userCustomText ?? defaultText;

            LayoutPositioning();

        }
        private void UpdateFilenameAvailability()
        {
            // All filename controls should be enabled only when OverwriteChk is checked
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

            // If the text is empty or the placeholder, show "filename"
            if (string.IsNullOrEmpty(path) || path == AppConstants.DefaultSrcBoxText)
            {
                NameSource.Text = AppConstants.DefaultFilename;
                return;
            }

            string baseName = AppConstants.DefaultFilename;
            string fileExtension = AppConstants.DefaultExtension;

            if (System.IO.File.Exists(path))
            {
                // It's a file – get name without extension
                baseName = System.IO.Path.GetFileNameWithoutExtension(path);
                fileExtension = System.IO.Path.GetExtension(path);
            }
            else if (System.IO.Directory.Exists(path))
            {
                // It's a folder – find the first image file
                string[] extensions = (string[])AppConstants.ImageExtensions.Clone();
                foreach (var ext in extensions)
                {
                    var files = System.IO.Directory.GetFiles(path, ext, System.IO.SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        baseName = System.IO.Path.GetFileNameWithoutExtension(files[0]);
                        fileExtension = System.IO.Path.GetExtension(files[0]);
                        break;
                    }
                }
                // If no image is found, baseName stays "filename"
                if (baseName == AppConstants.DefaultFilename)
                {
                    fileExtension = AppConstants.DefaultExtension;
                }
            }

            NameSource.Text = baseName;
            NameExtension.Text = fileExtension;
        }
        private void ModePrefix_Checked(object sender, RoutedEventArgs e) 
        {
            UpdateFilenameLayout();
        }
        private void ModeSuffix_Checked(object sender, RoutedEventArgs e)
        {
            UpdateFilenameLayout();
        }
        private void PreSufBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = PreSufBox;
            if (textBox == null) return;

            var text = textBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                var result = System.Windows.MessageBox.Show(
                    "Prefix/Suffix additional name is empty. Overwrite?",
                    "Overwrite?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                bool isResize = ActionResize.IsChecked == true;
                bool isPrefix = ModePrefix.IsChecked == true;
                string defaultText = isResize ? (isPrefix ? (string)AppConstants.DefaultResizePrefix : AppConstants.DefaultResizeSuffix) : (isPrefix ? AppConstants.DefaultCropPrefix : AppConstants.DefaultCropSuffix);

                // Update text without re-entrancy issues
                textBox.Text = defaultText;
                UpdateFilenameLayout();

                if (result != MessageBoxResult.Yes)
                    return;

                OverwriteChk.IsChecked = true;
            }
        }
        private void NoOverwrite_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateFilenameAvailability();
        }
        private string? userCustomText = null; // Store user custom text
        private void LayoutPositioning()
        {
            bool isPrefix = ModePrefix.IsChecked == true;

            if (isPrefix)
            {
                NameStackPanel.Children.Clear();
                NameStackPanel.Children.Add(PreSufBox);
                NameStackPanel.Children.Add(NameSource);
                NameStackPanel.Children.Add(NameExtension);
            }
            else
            {
                NameStackPanel.Children.Clear();
                NameStackPanel.Children.Add(NameSource);
                NameStackPanel.Children.Add(PreSufBox);
                NameStackPanel.Children.Add(NameExtension);
            }
        }

        #endregion

        #region Crop / Resize

        private void RefreshCropUI()
        {
            ClampCropRectangle();
            UpdateMaxValues();
            UpdateAllTextBoxes();
        }
        private void UpdateCropOverlay()
        {
            if (sourceWidthPx <= 0 || sourceHeightPx <= 0 || _originalImage == null)
            {
                CropOverlay.Visibility = Visibility.Hidden;
                return;
            }

            // Set position and size in raw pixel values (no scale)
            Canvas.SetLeft(CropOverlay, marginLeftPx);
            Canvas.SetTop(CropOverlay, marginTopPx);
            CropOverlay.Width = Math.Max(1, outputWidthPx);
            CropOverlay.Height = Math.Max(1, outputHeightPx);

            // No rotation for the overlay
            CropOverlay.RenderTransform = null;

            // Show/hide based on mode
            if (_originalImage == null || ActionResize.IsChecked == true)
                CropOverlay.Visibility = Visibility.Hidden;
            else
                CropOverlay.Visibility = Visibility.Visible;
        }
        private void UpdateMaxValues()
        {
            string unit = GetCurrentUnit();

            if (ActionCrop.IsChecked == true)
            {
                int maxWidthPx = sourceWidthPx - marginLeftPx;
                int maxHeightPx = sourceHeightPx - marginTopPx;

                WidthBox.Maximum =
                    (int)Math.Ceiling(ConvertPixelsToUnit(maxWidthPx, unit));

                HeightBox.Maximum =
                    (int)Math.Ceiling(ConvertPixelsToUnit(maxHeightPx, unit));
            }
            else
            {
                WidthBox.Maximum = 99999;
                HeightBox.Maximum = 99999;
            }
        }
        public void SetSourceDimensions(int widthPx, int heightPx)
                {
                    sourceWidthPx = widthPx;
                    sourceHeightPx = heightPx;
                    UpdateMaxValues();

                    // Clamp output to source if Crop is active
                    if (ActionCrop.IsChecked == true)
                    {
                        if (outputWidthPx > sourceWidthPx) outputWidthPx = sourceWidthPx;
                        if (outputHeightPx > sourceHeightPx) outputHeightPx = sourceHeightPx;
                    }

                    if (AspectRatio.IsChecked == true)
                        AdjustAspectRatio();
                    else
                        UpdateAllTextBoxes();
                }
        private void AdjustAspectRatio()
                {
                    if (!AspectRatio.IsChecked == true) return;
                    if (sourceWidthPx <= 0 || sourceHeightPx <= 0) return;

                    int targetW = outputWidthPx;
                    int targetH = outputHeightPx;
                    var (w, h) = AspectRatioHelper.FindBestAspectRatioPair(targetW, targetH, sourceWidthPx, sourceHeightPx);

                    // Apply Crop limits if needed
                    if (ActionCrop.IsChecked == true)
                    {
                        w = Math.Min(w, sourceWidthPx);
                        h = Math.Min(h, sourceHeightPx);
                    }

                    outputWidthPx = w;
                    outputHeightPx = h;
                    UpdateAllTextBoxes();
                }
        private void TransformCropRectangle(double deltaAngle, int oldW, int oldH)
        {
            // deltaAngle should be one of: 90, -90, 180 (or 0)
            double angle = deltaAngle % 360;
            if (angle < 0) angle += 360;

            double left = marginLeftPx;
            double top = marginTopPx;
            double width = outputWidthPx;
            double height = outputHeightPx;

            if (Math.Abs(angle - 90) < 0.1) // +90° clockwise
            {
                marginLeftPx = (int)Math.Round(oldH - top - height);
                marginTopPx = (int)Math.Round(left);
                outputWidthPx = (int)Math.Round(height);
                outputHeightPx = (int)Math.Round(width);
            }
            else if (Math.Abs(angle - 180) < 0.1) // 180°
            {
                marginLeftPx = (int)Math.Round(oldW - left - width);
                marginTopPx = (int)Math.Round(oldH - top - height);
                // width and height unchanged
            }
            else if (Math.Abs(angle - 270) < 0.1) // -90° (or 270° clockwise)
            {
                marginLeftPx = (int)Math.Round(top);
                marginTopPx = (int)Math.Round(oldW - left - width);
                outputWidthPx = (int)Math.Round(height);
                outputHeightPx = (int)Math.Round(width);
            }
            // angle 0 → no change
        }
        private void CropOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanMode) return;
            if (!_isManipulating) return;
            if (ActionResize.IsChecked == true) return;

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
                    newMarginLeft = Clamp(newMarginLeft, 0, sourceWidthPx - newWidth);
                    newMarginTop = Clamp(newMarginTop, 0, sourceHeightPx - newHeight);
                    break;

                case "ResizeLeft":
                    newMarginLeft = (int)Math.Round(_startMarginLeftPx + deltaX);
                    newWidth = _startWidthPx - (newMarginLeft - _startMarginLeftPx);
                    if (newMarginLeft < 0) { newWidth += newMarginLeft; newMarginLeft = 0; }
                    if (newWidth < 1) { newWidth = 1; newMarginLeft = Math.Min(newMarginLeft, sourceWidthPx - 1); }
                    if (newMarginLeft + newWidth > sourceWidthPx) newWidth = sourceWidthPx - newMarginLeft;
                    break;

                case "ResizeRight":
                    newWidth = (int)Math.Round(_startWidthPx + deltaX);
                    if (newWidth < 1) newWidth = 1;
                    if (newMarginLeft + newWidth > sourceWidthPx) newWidth = sourceWidthPx - newMarginLeft;
                    break;

                case "ResizeTop":
                    newMarginTop = (int)Math.Round(_startMarginTopPx + deltaY);
                    newHeight = _startHeightPx - (newMarginTop - _startMarginTopPx);
                    if (newMarginTop < 0) { newHeight += newMarginTop; newMarginTop = 0; }
                    if (newHeight < 1) { newHeight = 1; newMarginTop = Math.Min(newMarginTop, sourceHeightPx - 1); }
                    if (newMarginTop + newHeight > sourceHeightPx) newHeight = sourceHeightPx - newMarginTop;
                    break;

                case "ResizeBottom":
                    newHeight = (int)Math.Round(_startHeightPx + deltaY);
                    if (newHeight < 1) newHeight = 1;
                    if (newMarginTop + newHeight > sourceHeightPx) newHeight = sourceHeightPx - newMarginTop;
                    break;

                // ---- NEW CORNER CASES ----
                case "ResizeTopLeft":
                    newMarginLeft = (int)Math.Round(_startMarginLeftPx + deltaX);
                    newMarginTop = (int)Math.Round(_startMarginTopPx + deltaY);
                    newWidth = _startWidthPx - (newMarginLeft - _startMarginLeftPx);
                    newHeight = _startHeightPx - (newMarginTop - _startMarginTopPx);

                    // Clamp margins and ensure minimum size
                    if (newMarginLeft < 0) { newWidth += newMarginLeft; newMarginLeft = 0; }
                    if (newMarginTop < 0) { newHeight += newMarginTop; newMarginTop = 0; }
                    if (newWidth < 1) { newWidth = 1; newMarginLeft = Math.Min(newMarginLeft, sourceWidthPx - 1); }
                    if (newHeight < 1) { newHeight = 1; newMarginTop = Math.Min(newMarginTop, sourceHeightPx - 1); }
                    if (newMarginLeft + newWidth > sourceWidthPx) newWidth = sourceWidthPx - newMarginLeft;
                    if (newMarginTop + newHeight > sourceHeightPx) newHeight = sourceHeightPx - newMarginTop;
                    break;

                case "ResizeTopRight":
                    newMarginTop = (int)Math.Round(_startMarginTopPx + deltaY);
                    newWidth = (int)Math.Round(_startWidthPx + deltaX);
                    newHeight = _startHeightPx - (newMarginTop - _startMarginTopPx);

                    if (newMarginTop < 0) { newHeight += newMarginTop; newMarginTop = 0; }
                    if (newWidth < 1) newWidth = 1;
                    if (newHeight < 1) { newHeight = 1; newMarginTop = Math.Min(newMarginTop, sourceHeightPx - 1); }
                    if (newMarginLeft + newWidth > sourceWidthPx) newWidth = sourceWidthPx - newMarginLeft;
                    if (newMarginTop + newHeight > sourceHeightPx) newHeight = sourceHeightPx - newMarginTop;
                    break;

                case "ResizeBottomLeft":
                    newMarginLeft = (int)Math.Round(_startMarginLeftPx + deltaX);
                    newHeight = (int)Math.Round(_startHeightPx + deltaY);
                    newWidth = _startWidthPx - (newMarginLeft - _startMarginLeftPx);

                    if (newMarginLeft < 0) { newWidth += newMarginLeft; newMarginLeft = 0; }
                    if (newWidth < 1) { newWidth = 1; newMarginLeft = Math.Min(newMarginLeft, sourceWidthPx - 1); }
                    if (newHeight < 1) newHeight = 1;
                    if (newMarginLeft + newWidth > sourceWidthPx) newWidth = sourceWidthPx - newMarginLeft;
                    if (newMarginTop + newHeight > sourceHeightPx) newHeight = sourceHeightPx - newMarginTop;
                    break;

                case "ResizeBottomRight":
                    newWidth = (int)Math.Round(_startWidthPx + deltaX);
                    newHeight = (int)Math.Round(_startHeightPx + deltaY);
                    if (newWidth < 1) newWidth = 1;
                    if (newHeight < 1) newHeight = 1;
                    if (newMarginLeft + newWidth > sourceWidthPx) newWidth = sourceWidthPx - newMarginLeft;
                    if (newMarginTop + newHeight > sourceHeightPx) newHeight = sourceHeightPx - newMarginTop;
                    break;
            }

            // Apply changes
            marginLeftPx = newMarginLeft;
            marginTopPx = newMarginTop;
            outputWidthPx = newWidth;
            outputHeightPx = newHeight;

            UpdateAllTextBoxes();
            e.Handled = true;
        }
        private void CropOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isManipulating)
            {
                _isManipulating = false;
                _resizeMode = "";
                CropOverlay.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }
        private void PreviewCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isPanMode) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (ActionResize.IsChecked == true) return;

            Point pos = e.GetPosition(PreviewCanvas);
            double left = Canvas.GetLeft(CropOverlay);
            double top = Canvas.GetTop(CropOverlay);
            double right = left + CropOverlay.Width;
            double bottom = top + CropOverlay.Height;
            double tolerance = EdgeTolerance;

            bool nearLeft = Math.Abs(pos.X - left) < tolerance;
            bool nearRight = Math.Abs(pos.X - right) < tolerance;
            bool nearTop = Math.Abs(pos.Y - top) < tolerance;
            bool nearBottom = Math.Abs(pos.Y - bottom) < tolerance;

            // Determine resize mode – corners take priority
            if (nearLeft && nearTop)
                _resizeMode = "ResizeTopLeft";
            else if (nearRight && nearTop)
                _resizeMode = "ResizeTopRight";
            else if (nearLeft && nearBottom)
                _resizeMode = "ResizeBottomLeft";
            else if (nearRight && nearBottom)
                _resizeMode = "ResizeBottomRight";
            else if (nearLeft)
                _resizeMode = "ResizeLeft";
            else if (nearRight)
                _resizeMode = "ResizeRight";
            else if (nearTop)
                _resizeMode = "ResizeTop";
            else if (nearBottom)
                _resizeMode = "ResizeBottom";
            else
                _resizeMode = "Drag";

            // For dragging (not resizing), the click must be inside the overlay
            bool inside = (pos.X >= left && pos.X <= right && pos.Y >= top && pos.Y <= bottom);
            if (_resizeMode == "Drag" && !inside)
                return;

            // Start manipulation
            _isManipulating = true;
            _startMousePos = pos;
            _startMarginLeftPx = marginLeftPx;
            _startMarginTopPx = marginTopPx;
            _startWidthPx = outputWidthPx;
            _startHeightPx = outputHeightPx;

            // Capture mouse on the overlay – this ensures we get Move/Up events even if mouse leaves
            CropOverlay.CaptureMouse();
            e.Handled = true;
        }
        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // If no image or overlay hidden, reset to default arrow
            if (_originalImage == null || CropOverlay.Visibility != Visibility.Visible || ActionResize.IsChecked == true)
            {
                Cursor = Cursors.Arrow;
                return;
            }

            // In pan mode, always show the hand cursor
            if (_isPanMode)
            {
                Cursor = Cursors.Hand;
                return;
            }

            Point pos = e.GetPosition(PreviewCanvas);
            double left = Canvas.GetLeft(CropOverlay);
            double top = Canvas.GetTop(CropOverlay);
            double right = left + CropOverlay.Width;
            double bottom = top + CropOverlay.Height;
            double tolerance = EdgeTolerance;

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

        #region Unit Conversion

        private string GetCurrentUnit()
        {
            if (UnitPixels.IsChecked == true) return "px";
            if (UnitMM.IsChecked == true) return "mm";
            if (UnitPer.IsChecked == true) return "%";
            return "px";
        }
        private static double ConvertPixelsToUnit(int pixels, string unit)
                {
                    if (unit == "px") return pixels;
                    if (unit == "mm") return pixels / Dpi * MmPerInch;
                    return pixels; // fallback
                }
        private static int ConvertUnitToPixels(double value, string unit, bool clampToMin = true)
                {
                    int result;
                    if (unit == "px")
                        result = (int)Math.Round(value);
                    else if (unit == "mm")
                        result = (int)Math.Round(value / MmPerInch * Dpi);
                    else
                        result = (int)Math.Round(value);

                    return clampToMin ? Math.Max(1, result) : result;
                }
        private void UpdateAllTextBoxes()
                {
                    if (_updatingUI) return;
                    _updatingUI = true;
                    try
                    {
                        string unit = GetCurrentUnit();

                        if (unit == "%")
                        {
                            WidthSourceBox.Text = "100";
                            HeightSourceBox.Text = "100";

                            double percentW = Math.Max(1, (double)outputWidthPx / sourceWidthPx * 100);
                            double percentH = Math.Max(1, (double)outputHeightPx / sourceHeightPx * 100);
                            WidthBox.Value = (int)Math.Round(percentW);
                            HeightBox.Value = (int)Math.Round(percentH);
                            MarginLeftBox.Value = 0;
                            MargintopBox.Value = 0;
                        }
                        else // pixels or millimeters
                        {
                            // Source dimensions (converted and rounded)
                            int srcW = (int)Math.Round(ConvertPixelsToUnit(sourceWidthPx, unit));
                            int srcH = (int)Math.Round(ConvertPixelsToUnit(sourceHeightPx, unit));
                            WidthSourceBox.Text = Math.Max(1, srcW).ToString();
                            HeightSourceBox.Text = Math.Max(1, srcH).ToString();

                            // Output dimensions (converted and rounded)
                            int outW = (int)Math.Round(ConvertPixelsToUnit(outputWidthPx, unit));
                            int outH = (int)Math.Round(ConvertPixelsToUnit(outputHeightPx, unit));
                            WidthBox.Value = Math.Max(1, outW);
                            HeightBox.Value = Math.Max(1, outH);

                            // Margins
                            int mL = (int)Math.Round(ConvertPixelsToUnit(marginLeftPx, unit));
                            int mT = (int)Math.Round(ConvertPixelsToUnit(marginTopPx, unit));
                            MarginLeftBox.Value = mL;
                            MargintopBox.Value = mT;
                        }

                        // Force text to match value (prevents lost‑focus rollback)
                        WidthBox.Text = WidthBox.Value?.ToString() ?? "0";
                        HeightBox.Text = HeightBox.Value?.ToString() ?? "0";
                    }
                    finally
                    {
                        _updatingUI = false;
                        UpdateCropOverlay();
                    }
                }
        private void UpdatePixelFromBox(IntegerUpDown box, ref int pixelField, int sourceDimensionPx, bool clampToMin = true)
                {
                    string unit = GetCurrentUnit();
                    double displayValue = box.Value ?? 0;

                    if (unit == "%")
                    {
                        pixelField = (int)Math.Round(displayValue / 100.0 * sourceDimensionPx);
                        // For percentage, also clamp if needed? Usually percentage of source can be 0.
                        if (clampToMin && pixelField < 1) pixelField = 1;
                    }
                    else
                    {
                        pixelField = ConvertUnitToPixels(displayValue, unit, clampToMin);
                    }

                    RefreshCropUI();
                }
        private void UpdateUnitAvailability()
        {
            // UnitPer should only be available when ActionResize is selected
            bool isResize = ActionResize.IsChecked == true;
            UnitPer.IsEnabled = isResize;
            UnitPer.IsChecked = isResize;
            StackRatio.IsEnabled = isResize;
            AspectRatio.IsChecked = isResize;
            MarginsSettings.IsEnabled = !isResize;
            ActionBtn.Content = isResize ? "RESIZE IMAGE(S)" : "CROP IMAGE(S)";

            if (isResize) { CropOverlay.Visibility = Visibility.Hidden; }

            // If UnitPer is selected but now disabled, switch to Pixels
            if (!isResize && UnitPer.IsChecked == true)
            {
                UnitPixels.IsChecked = true;
            }
        }

        #endregion
        
        #region Dimension Editing

        private void WidthBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updatingUI || !IsLoaded) return;

            if (AspectRatio.IsChecked == true)
            {
                string unit = GetCurrentUnit();
                int newDisplayValue = WidthBox.Value ?? 0;

                // Percentage mode – keep both percentages equal
                if (unit == "%")
                {
                    double newWidthPercent = newDisplayValue;
                    double newHeightPercent = newWidthPercent;

                    outputWidthPx = (int)Math.Round(newWidthPercent / 100.0 * sourceWidthPx);
                    outputHeightPx = (int)Math.Round(newHeightPercent / 100.0 * sourceHeightPx);
                    if (outputWidthPx < 1) outputWidthPx = 1;
                    if (outputHeightPx < 1) outputHeightPx = 1;

                    UpdateAllTextBoxes();
                    return;
                }

                // Convert the new displayed width to pixels
                int targetW = ConvertUnitToPixels(newDisplayValue, unit, false);
                int targetH = (int)Math.Round((double)targetW * sourceHeightPx / sourceWidthPx);
                if (targetH < 1) targetH = 1;

                // Find the best integer pair preserving the ratio
                var (w, h) = AspectRatioHelper.FindBestAspectRatioPair(targetW, targetH, sourceWidthPx, sourceHeightPx);

                // Apply Crop limits
                if (ActionCrop.IsChecked == true)
                {
                    w = Math.Min(w, sourceWidthPx);
                    h = Math.Min(h, sourceHeightPx);
                }

                outputWidthPx = w;
                outputHeightPx = h;
                RefreshCropUI();
            }
            else
            {
                UpdatePixelFromBox(WidthBox, ref outputWidthPx, sourceWidthPx, true);
            }
        }
        private void HeightBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updatingUI || !IsLoaded) return;

            if (AspectRatio.IsChecked == true)
            {
                string unit = GetCurrentUnit();
                int newDisplayValue = HeightBox.Value ?? 0;

                if (unit == "%")
                {
                    double newHeightPercent = newDisplayValue;
                    double newWidthPercent = newHeightPercent;

                    outputWidthPx = (int)Math.Round(newWidthPercent / 100.0 * sourceWidthPx);
                    outputHeightPx = (int)Math.Round(newHeightPercent / 100.0 * sourceHeightPx);
                    if (outputWidthPx < 1) outputWidthPx = 1;
                    if (outputHeightPx < 1) outputHeightPx = 1;

                    UpdateAllTextBoxes();
                    return;
                }

                int targetH = ConvertUnitToPixels(newDisplayValue, unit, false);
                int targetW = (int)Math.Round((double)targetH * sourceWidthPx / sourceHeightPx);
                if (targetW < 1) targetW = 1;

                var (w, h) = AspectRatioHelper.FindBestAspectRatioPair(targetW, targetH, sourceWidthPx, sourceHeightPx);

                if (ActionCrop.IsChecked == true)
                {
                    w = Math.Min(w, sourceWidthPx);
                    h = Math.Min(h, sourceHeightPx);
                }

                outputWidthPx = w;
                outputHeightPx = h;
                RefreshCropUI();
            }
            else
            {
                UpdatePixelFromBox(HeightBox, ref outputHeightPx, sourceHeightPx, true);
            }
        }
        private void MarginLeftBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updatingUI || !IsLoaded) return;
            UpdatePixelFromBox(MarginLeftBox, ref marginLeftPx, sourceWidthPx, false);
            RefreshCropUI();
        }
        private void MargintopBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updatingUI || !IsLoaded) return;
            UpdatePixelFromBox(MargintopBox, ref marginTopPx, sourceHeightPx, false);
            RefreshCropUI();
        }
        private void WidthBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(AspectRatio.IsChecked == true)) return;
            if (_updatingUI) return;

            string unit = GetCurrentUnit();
            int currentDisplay = WidthBox.Value ?? 0;
            int expectedDisplay = (int)Math.Round(ConvertPixelsToUnit(outputWidthPx, unit));

            if (currentDisplay != expectedDisplay)
            {
                // Force the correct value and refresh the UI
                _updatingUI = true;
                WidthBox.Value = expectedDisplay;
                _updatingUI = false;
                // Calling UpdateAllTextBoxes will also set the HeightBox correctly and sync Text
                UpdateAllTextBoxes();
            }
        }
        private void HeightBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(AspectRatio.IsChecked == true)) return;
            if (_updatingUI) return;

            string unit = GetCurrentUnit();
            int currentDisplay = HeightBox.Value ?? 0;
            int expectedDisplay = (int)Math.Round(ConvertPixelsToUnit(outputHeightPx, unit));

            if (currentDisplay != expectedDisplay)
            {
                _updatingUI = true;
                HeightBox.Value = expectedDisplay;
                _updatingUI = false;
                UpdateAllTextBoxes();
            }
        }
        private void AspectRatio_Checked(object sender, RoutedEventArgs e)
        {
            if (AspectRatio.IsChecked == true)
            {
                AdjustAspectRatio();
            }
            // If unchecked, do nothing – allow free editing
        }
        private void Action_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ActionResize.IsChecked == false)
            {
                // When switching to Crop, ensure output doesn't exceed source
                if (outputWidthPx > sourceWidthPx) outputWidthPx = sourceWidthPx;
                if (outputHeightPx > sourceHeightPx) outputHeightPx = sourceHeightPx;
                AspectRatio.IsChecked = false; // disable aspect ratio when switching to crop
                RefreshCropUI(); // refresh UI after clamping
                if (UnitPer.IsChecked == true)
                {
                    UnitPixels.IsChecked = true;
                }
            }
            UpdateUnitAvailability();
            UpdateFilenameLayout();
        }
        private void Unit_CheckedChanged(object sender, RoutedEventArgs e)
        {
            string unit = GetCurrentUnit();
            Resources["UnitText"] = unit == "px" ? "px" : unit == "mm" ? "mm" : "%";
            UpdateMaxValues();
            if (AspectRatio.IsChecked == true)
                AdjustAspectRatio();
            UpdateAllTextBoxes();

            WidthBox.InvalidateVisual();
            HeightBox.InvalidateVisual();
        }
        #endregion
        
        #region Zoom & Pan
        private enum ZoomMode { Fit, Actual, Custom }
        private ZoomMode _zoomMode = ZoomMode.Fit;
        private double _customZoom = 1.0;
        private double _currentScale = 1.0;
        private double _panX = 0, _panY = 0;
        private double _minPanX = 0, _maxPanX = 0, _minPanY = 0, _maxPanY = 0;
        private bool _suppressScrollSync = false;
        private bool _isPanMode = false;
        private bool _isPanning = false;
        private Point _panStartMouse;
        private double _panStartX, _panStartY;
        private const double EdgeTolerance = 20.0;
        private void UpdateScrollBars(double canvasW, double canvasH)
        {
            _suppressScrollSync = true;

            // maxPanX/maxPanY are always 0 (top-left aligned default), so Value = -pan.
            double hRange = -_minPanX;
            HScrollBar.Minimum = 0;
            HScrollBar.Maximum = hRange;
            HScrollBar.ViewportSize = canvasW;
            HScrollBar.Value = Clamp(-_panX, 0, hRange);

            double vRange = -_minPanY;
            VScrollBar.Minimum = 0;
            VScrollBar.Maximum = vRange;
            VScrollBar.ViewportSize = canvasH;
            VScrollBar.Value = Clamp(-_panY, 0, vRange);

            _suppressScrollSync = false;
        }
        private void ResetScrollBars()
        {
            _suppressScrollSync = true;
            HScrollBar.Minimum = 0; HScrollBar.Maximum = 0; HScrollBar.Value = 0;
            VScrollBar.Minimum = 0; VScrollBar.Maximum = 0; VScrollBar.Value = 0;
            _suppressScrollSync = false;
        }
        private void HScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressScrollSync || _originalImage == null) return;
            _panX = -HScrollBar.Value;
            UpdatePreviewTransform();
        }
        private void VScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressScrollSync || _originalImage == null) return;
            _panY = -VScrollBar.Value;
            UpdatePreviewTransform();
        }
        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_originalImage == null) return;
            // Start from the current scale and multiply by 1.1
            _customZoom = _currentScale * 1.1;
            _zoomMode = ZoomMode.Custom;
            UpdatePreviewTransform();
        }
        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_originalImage == null) return;
            // Start from the current scale and divide by 1.1
            _customZoom = _currentScale / 1.1;
            if (_customZoom < 0.01) _customZoom = 0.01;
            _zoomMode = ZoomMode.Custom;
            UpdatePreviewTransform();
        }
        private void FitBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_originalImage == null) return;
            _zoomMode = ZoomMode.Fit;
            _panX = 0;
            _panY = 0;
            UpdatePreviewTransform();
        }
        private void ActualSizeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_originalImage == null) return;
            _zoomMode = ZoomMode.Actual;
            _panX = 0;
            _panY = 0;
            UpdatePreviewTransform();
        }
        private void PanModeBtn_Click(object sender, RoutedEventArgs e)
        {
            _isPanMode = !_isPanMode;
            // Change button appearance (optional)
            PanModeBtn.Background = _isPanMode ? Brushes.LightBlue : Brushes.Transparent;
            if (_isPanMode)
            {
                PreviewImage.Cursor = Cursors.Hand;
                CropOverlay.IsHitTestVisible = false; // let clicks pass through to PreviewImage for panning
            }
            else
            {
                PreviewImage.Cursor = Cursors.Arrow;
                CropOverlay.IsHitTestVisible = true;  // restore normal crop drag/resize
            }
        }
        private void UpdateZoomLabel()
        {
            if (_originalImage == null)
            {
                ZoomLabel.Content = "No image";
                return;
            }

            string text;
            switch (_zoomMode)
            {
                case ZoomMode.Fit:
                    text = "Fit";
                    break;
                case ZoomMode.Actual:
                    text = "1:1";
                    break;
                case ZoomMode.Custom:
                    int percent = (int)Math.Round(_currentScale * 100);
                    text = $"{percent}%";
                    break;
                default:
                    text = "";
                    break;
            }
            ZoomLabel.Content = text;
        }
        private void PreviewArea_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_originalImage == null) return;

            double step = e.Delta; // typically +/-120 per notch

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                HScrollBar.Value = Clamp(HScrollBar.Value - step, HScrollBar.Minimum, HScrollBar.Maximum);
            }
            else
            {
                VScrollBar.Value = Clamp(VScrollBar.Value - step, VScrollBar.Minimum, VScrollBar.Maximum);
            }

            e.Handled = true;
        }
        private void PreviewImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanMode || e.LeftButton != MouseButtonState.Pressed) return;
            _isPanning = true;
            _panStartMouse = e.GetPosition(this);
            _panStartX = _panX;
            _panStartY = _panY;
            PreviewImage.CaptureMouse();
            e.Handled = true;
        }
        private void PreviewImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning) return;
            Point current = e.GetPosition(this);
            double deltaX = current.X - _panStartMouse.X;
            double deltaY = current.Y - _panStartMouse.Y;
            _panX = _panStartX + deltaX;
            _panY = _panStartY + deltaY;
            UpdatePreviewTransform();
            e.Handled = true;
        }
        private void PreviewImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                PreviewImage.ReleaseMouseCapture();
                e.Handled = true;
            }
        }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Only handle when Crop mode is active and overlay is visible
            if (ActionCrop.IsChecked != true || CropOverlay.Visibility != Visibility.Visible)
                return;

            // Ignore if the user is currently dragging with the mouse
            if (_isManipulating)
                return;

            // Determine step size
            int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;

            int dx = 0, dy = 0;

            switch (e.Key)
            {
                case Key.Left: dx = -step; break;
                case Key.Right: dx = step; break;
                case Key.Up: dy = -step; break;
                case Key.Down: dy = step; break;
                default: return; // not an arrow key
            }

            MoveOverlay(dx, dy);
            e.Handled = true; // prevent other actions
        }
        private void MoveOverlay(int dx, int dy)
        {
            // Calculate new margins
            int newLeft = marginLeftPx + dx;
            int newTop = marginTopPx + dy;

            // Clamp to keep the overlay inside the image
            newLeft = Clamp(newLeft, 0, sourceWidthPx - outputWidthPx);
            newTop = Clamp(newTop, 0, sourceHeightPx - outputHeightPx);

            // Only update if changed
            if (newLeft != marginLeftPx || newTop != marginTopPx)
            {
                marginLeftPx = newLeft;
                marginTopPx = newTop;
                RefreshCropUI(); // updates UI and overlay position
            }
        }
        private void ClampCropRectangle()
        {
            // Prevent invalid sizes
            outputWidthPx = Math.Max(1, outputWidthPx);
            outputHeightPx = Math.Max(1, outputHeightPx);

            // Crop mode cannot exceed source image
            if (ActionCrop.IsChecked == true)
            {
                outputWidthPx = Math.Min(outputWidthPx, sourceWidthPx);
                outputHeightPx = Math.Min(outputHeightPx, sourceHeightPx);
            }

            // Prevent negative margins
            marginLeftPx = Math.Max(0, marginLeftPx);
            marginTopPx = Math.Max(0, marginTopPx);

            // Prevent crop rectangle from leaving the image
            if (ActionCrop.IsChecked == true)
            {
                if (marginLeftPx + outputWidthPx > sourceWidthPx)
                    outputWidthPx = sourceWidthPx - marginLeftPx;

                if (marginTopPx + outputHeightPx > sourceHeightPx)
                    outputHeightPx = sourceHeightPx - marginTopPx;

                // If the width/height changed first,
                // make sure the margins are still valid.
                marginLeftPx = Math.Min(marginLeftPx, sourceWidthPx - outputWidthPx);
                marginTopPx = Math.Min(marginTopPx, sourceHeightPx - outputHeightPx);

                marginLeftPx = Math.Max(0, marginLeftPx);
                marginTopPx = Math.Max(0, marginTopPx);
            }
        }
        
        #endregion

        #region Path Validation

        private void ValidateSourceFolder(string selectedFolder)
        {
            string[] extensions = (string[])AppConstants.ImageExtensions.Clone();

            bool hasImage = extensions.Any(ext =>
                System.IO.Directory.GetFiles(selectedFolder, ext).Length > 0);

            if (!hasImage)
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
            else
            {
                SrcBox.Text = selectedFolder;
                _lastValidSourcePath = selectedFolder;
                LoadPath(selectedFolder);
            }
        }
        private void SrcBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SrcBox.Text == AppConstants.DefaultSrcBoxText)
            {
                SrcBox.Text = "";
            }
        }
        private void DstBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (DstBox.Text == AppConstants.DefaultDstBoxText)
            {
                DstBox.Text = "";
            }
        }
        private void SrcBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string path = SrcBox.Text;
            if (string.IsNullOrWhiteSpace(path) || path == AppConstants.DefaultSrcBoxText)
            {
                // restore placeholder
                SrcBox.Text = AppConstants.DefaultSrcBoxText;
                PreviewImage.Source = null;   // <-- clear preview
                PreviewImage.Width = 0;
                PreviewImage.Height = 0;
                _originalImage = null;
                CropOverlay.Visibility = Visibility.Hidden;
                RightPanelGrid.IsEnabled = false;
                ActionBtn.IsEnabled = false;
                _lastValidSourcePath = AppConstants.DefaultSrcBoxText;
                UpdateSourceFilename();
                return;
            }

            // If it's a valid file or folder, load dimensions
            if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path))
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
                if (SrcBox.Text == _lastValidSourcePath)
                {
                    _lastValidSourcePath = AppConstants.DefaultSrcBoxText;
                }

            }
            SrcBox.Text = _lastValidSourcePath;
        }
        private void DstBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string path = DstBox.Text;

            if (string.IsNullOrWhiteSpace(path) || path == AppConstants.DefaultDstBoxText)
            {
                SetDirectoryPath(path);
                return;
            }

            string? validPath = GetValidDirectoryPath(path);
            if (validPath != null)
            {
                SetDirectoryPath(validPath);
            }

            else
            {
                System.Windows.MessageBox.Show(
                    "The entered path is not valid for a folder. Please check for invalid characters, reserved names, or missing drive.",
                    "Invalid Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DstBox.Text = _lastValidOutputPath;
            }
        }
        private static string? GetValidDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return AppConstants.DefaultDstBoxText;
            }

            // If the path looks like a file (has an extension and no trailing slash), strip the filename.
            if (!path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()) &&
                !string.IsNullOrEmpty(System.IO.Path.GetExtension(path)))
            {
                string? directory = System.IO.Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                {
                    return null;
                }
                path = directory;
            }

            // Now validate the directory part
            if (!System.IO.Path.IsPathRooted(path))
            {
                return null;
            }

            try
            {
                string fullPath = System.IO.Path.GetFullPath(path);

                // Check for reserved device names
                string directoryName = System.IO.Path.GetFileName(fullPath);
                if (!string.IsNullOrEmpty(directoryName))
                {
                    string[] reserved = ["CON", "PRN", "AUX", "NUL"];
                    if (reserved.Contains(directoryName, StringComparer.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    if (_reservedNameRegex.IsMatch(directoryName))
                    {
                        return null;
                    }

                    // Trailing spaces or dots are not allowed
                    if (directoryName.TrimEnd(' ', '.').Length != directoryName.Length)
                    {
                        return null;
                    }
                }

                // Validate drive exists
                string? root = System.IO.Path.GetPathRoot(fullPath);
                if (string.IsNullOrEmpty(root) || !System.IO.Directory.Exists(root))
                {
                    return null;
                }
                return fullPath; // normalized path

            }
            catch
            {

                return null;
            }
        }
        private void SetDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                DstBox.Text = _lastValidOutputPath;
            }
            else _lastValidOutputPath = DstBox.Text = path;
        }

        #endregion

        #region Reset

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            // Reset output dimensions to the current source dimensions
            outputWidthPx = sourceWidthPx / 2;
            outputHeightPx = sourceHeightPx / 2;

            // Reset margins to zero
            marginLeftPx = 0;
            marginTopPx = 0;

            // Reset UI controls
            AspectRatio.IsChecked = false;
            ActionCrop.IsChecked = true;
            UnitPixels.IsChecked = true;

            // Reset filename settings
            userCustomText = null;          // clear any custom text
            ModePrefix.IsChecked = true;    // default to Prefix
            OverwriteChk.IsChecked = false;
            PreSufBox.Text = AppConstants.DefaultCropPrefix;

            // Reset progress, cancel button, "Open folder" checkbox
            Progress.Value = 0;
            CancelBtn.IsEnabled = false;
            OpenAfterChk.IsChecked = true;

            // Force UI refresh (this also updates the crop overlay)
            UpdateAllTextBoxes();
            UpdateFilenameLayout();

            // Log the action
            AppendLog("Settings reset to default.\n");
        }
        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "This will clear the loaded image and reset all settings to their defaults. Continue?",
                "Reset Everything",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;

            // Reset source and destination text boxes
            SrcBox.Text = AppConstants.DefaultSrcBoxText;
            SrcBox.FlowDirection = FlowDirection.LeftToRight;
            SrcBox.TextAlignment = TextAlignment.Left;
            SrcBox.HorizontalContentAlignment = HorizontalAlignment.Left;

            DstBox.Text = AppConstants.DefaultDstBoxText;
            DstBox.FlowDirection = FlowDirection.LeftToRight;
            DstBox.TextAlignment = TextAlignment.Left;
            DstBox.HorizontalContentAlignment = HorizontalAlignment.Left;

            // Clear preview image and hide overlay
            PreviewImage.Source = null;
            _originalImage = null;
            CropOverlay.Visibility = Visibility.Hidden;
            sourceWidthPx = 1000;
            sourceHeightPx = 2000;
            _zoomMode = ZoomMode.Fit;
            _customZoom = 1.0;
            _panX = 0;
            _panY = 0;
            _isPanMode = false;
            PanModeBtn.Background = Brushes.Transparent;
            PreviewImage.Width = 0;
            PreviewImage.Height = 0;
            RightPanelGrid.IsEnabled = false;
            UpdatePreviewTransform();

            // Reset rotation
            _currentRotation = 0;
            _previousRotation = 0;
            UpdateDisplayedImage();

            ResetBtn_Click(this, new RoutedEventArgs());

            // Clear the log
            LogTextBox.Clear();

            // Log the action
            AppendLog("Everything reset to default.\n");
        }

        #endregion

        #region Helpers

        private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);       
        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : (value > max ? max : value);
        }
        
        #endregion

        #region State

        private string _resizeMode = "";
        private string _lastValidSourcePath = AppConstants.DefaultSrcBoxText;
        private string _lastValidOutputPath = AppConstants.DefaultDstBoxText;
        private BitmapSource? _originalImage = null;
        private double _currentRotation = 0;
        private double _previousRotation = 0;
        private string? _currentImagePath = null;
        private const double Dpi = 96.0; // Standard screen DPI
        private const double MmPerInch = 25.4;
        private int sourceWidthPx = 1000;
        private int sourceHeightPx = 2000;
        private int outputWidthPx = 500;
        private int outputHeightPx = 1000;
        private int marginLeftPx = 0;
        private int marginTopPx = 0;
        private bool _isManipulating = false;
        private Point _startMousePos;
        private int _startMarginLeftPx, _startMarginTopPx, _startWidthPx, _startHeightPx;
        private bool _updatingUI = false;


        #endregion

        #region Logging

        private void AppendLog(string text)
        {
            LogTextBox.AppendText(text);
            LogTextBox.ScrollToEnd();
        }

        #endregion

        #region Source/Destination Browsing
        private void SrcBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FileFolderDialog
            {
                Owner = this // Center the dialog over the main window
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
                SrcBox.Text = (dialog.FileName);
                _lastValidSourcePath = (dialog.FileName);
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

            if (!string.IsNullOrEmpty(initialPath) && System.IO.Directory.Exists(initialPath))
            {
                dialog.SelectedPath = initialPath;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedPath = dialog.SelectedPath;
                _lastValidOutputPath = selectedPath;
                DstBox.Text = selectedPath;
                AppendLog($"Output folder: {selectedPath}\n");
            }
        }
        
        #pragma warning disable SYSLIB1045
        private static readonly System.Text.RegularExpressions.Regex _reservedNameRegex = new(@"^(COM|LPT)[1-9]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
        #pragma warning restore SYSLIB1045

        private void LoadPath(string path)
        {
            if (System.IO.File.Exists(path))
            {
                string? validPath = GetValidDirectoryPath(path);
                if (DstBox.Text == AppConstants.DefaultDstBoxText || DstBox.Text == GetValidDirectoryPath(_lastValidSourcePath))
                {
                    SetDirectoryPath(validPath!);
                }
                AppendLog($"Selected file: {System.IO.Path.GetFileName(path)}\n");
                ActionBtn.IsEnabled = true;
                _lastValidSourcePath = path;
                LoadPreviewImage(path);
            }
            else if (System.IO.Directory.Exists(path))
            {
                string[] extensions = (string[])AppConstants.ImageExtensions.Clone();
                bool found = false;
                foreach (var ext in extensions)
                {
                    var files = System.IO.Directory.GetFiles(path, ext, SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        LoadPreviewImage(files[0]);   // ✅ loads once, sets dimensions internally
                        AppendLog($"Using first image: {System.IO.Path.GetFileName(files[0])}\n");
                        found = true;
                        _lastValidSourcePath = path;
                        break;
                    }
                }
                if (found)
                {
                    AppendLog($"Selected folder: {path}\n");
                    ActionBtn.IsEnabled = true;
                    SetDirectoryPath(path);
                }

            }
            else
            {
                AppendLog($"Path not found: {path}\n");
                ActionBtn.IsEnabled = false;
            }
        }

        #endregion

        }

}