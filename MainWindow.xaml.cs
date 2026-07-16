using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xceed.Wpf.Toolkit;

namespace ImageCropTool
{
    public static class AppConstants
    {
        public const string DefaultSrcBoxText = "Write/Paste or Browse the source path of a file/folder ----->";
        public const string DefaultDstBoxText = "Write/Paste or Browse if different from Source folder ------>";
    }
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Set initial states

            RefreshCropUI();
            UpdateUnitAvailability();
            UpdateFilenameAvailability();
            UpdateFilenameLayout();
            UpdateSourceFilename();
            UpdateAllTextBoxes();


            // Attach event handlers
            ActionCrop.Checked += Action_CheckedChanged;
            ActionResize.Checked += Action_CheckedChanged;
            ActualSizebtn.Click += ActualSizeBtn_Click;
            AspectRatio.Checked += AspectRatio_Checked;
            AspectRatio.Unchecked += AspectRatio_Checked;
            CropBtn.Click += CropBtn_Click;
            CropOverlay.MouseDown += CropOverlay_MouseDown;
            CropOverlay.MouseMove += CropOverlay_MouseMove;
            CropOverlay.MouseUp += CropOverlay_MouseUp;
            CropOverlay.Visibility = Visibility.Hidden;
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
            SrcBox.TextChanged += SrcBox_TextChanged;
            this.PreviewKeyDown += Window_PreviewKeyDown;
            UnitMM.Checked += Unit_CheckedChanged;
            UnitPer.Checked += Unit_CheckedChanged;
            UnitPer.IsEnabled = false;
            UnitPixels.Checked += Unit_CheckedChanged;
            VScrollBar.ValueChanged += VScrollBar_ValueChanged;
            WidthBox.LostFocus += WidthBox_LostFocus;
            WidthBox.ValueChanged += WidthBox_ValueChanged;
            ZoomInBtn.Click += ZoomInBtn_Click;
            ZoomOutBtn.Click += ZoomOutBtn_Click;
        }

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

            // --- Clamp panning so the image can't be dragged out of view ---
            // Content can only be panned when it's larger than the viewport on that
            // axis; if it already fits entirely, pan is locked to 0 (top-left aligned,
            // nothing to scroll) - just like a normal scrollbar disables itself when
            // there's nothing to scroll.
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

            UpdateScrollBars(canvasW, canvasH, scaledW, scaledH);

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

        private void UpdateScrollBars(double canvasW, double canvasH, double scaledW, double scaledH)
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
        private BitmapSource? _originalImage = null;   // the raw image (no rotation)
        private double _currentRotation = 0;
        private void Action_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ActionResize.IsChecked == false)
            {
                // When switching to Crop, ensure output doesn't exceed source
                if (outputWidthPx > sourceWidthPx) outputWidthPx = sourceWidthPx;
                if (outputHeightPx > sourceHeightPx) outputHeightPx = sourceHeightPx;
                AspectRatio.IsChecked = false; // disable aspect ratio when switching to crop
                RefreshCropUI(); // refresh UI after clamping
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

        private void NoOverwrite_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateFilenameAvailability();
        }

        private void UpdateUnitAvailability()
        {
            // UnitPer should only be available when ActionResize is selected
            bool isResize = ActionResize.IsChecked == true;
            UnitPer.IsEnabled = isResize;
            StackRatio.IsEnabled = isResize;
            MarginsSettings.IsEnabled = !isResize;
            if (isResize) { CropOverlay.Visibility = Visibility.Hidden; }

            // If UnitPer is selected but now disabled, switch to Pixels
            if (!isResize && UnitPer.IsChecked == true)
            {
                UnitPixels.IsChecked = true;
            }
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
                SetFilePath(dialog.FileName);
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
                SetFilePath(dialog.SelectedPath);
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
                SetOutputPath(dialog.SelectedPath);
            }
        }
        private void UpdateSourceFilename()
        {
            string path = SrcBox.Text;

            // If the text is empty or the placeholder, show "filename"
            if (string.IsNullOrEmpty(path) || path == (string)AppConstants.DefaultSrcBoxText)
            {
                NameSource.Text = "filename";
                return;
            }

            string baseName = "filename";
            string fileExtension = ".jpg";

            if (System.IO.File.Exists(path))
            {
                // It's a file – get name without extension
                baseName = System.IO.Path.GetFileNameWithoutExtension(path);
                fileExtension = System.IO.Path.GetExtension(path);
            }
            else if (System.IO.Directory.Exists(path))
            {
                // It's a folder – find the first image file
                string[] extensions = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff"];
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
            }

            NameSource.Text = baseName;
            NameExtension.Text = fileExtension;
        }
        private void SetFilePath(string filePath)
        {
            SrcBox.Text = filePath;
            SrcBox.FlowDirection = FlowDirection.RightToLeft;
            SrcBox.TextAlignment = TextAlignment.Right;
            SrcBox.HorizontalContentAlignment = HorizontalAlignment.Right;

            LoadPath(filePath);
        }
        private void SetOutputPath(string filePath)
        {
            DstBox.Text = filePath;

            DstBox.FlowDirection = FlowDirection.RightToLeft;
            DstBox.TextAlignment = TextAlignment.Right;
            DstBox.HorizontalContentAlignment = HorizontalAlignment.Right;

            if (System.IO.Directory.Exists(filePath))
            {
                AppendLog($"Output folder: {filePath}\n");
            }
        }
        private void SrcBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string currentText = SrcBox.Text;

            // Check if it's the default placeholder or empty
            if (string.IsNullOrEmpty(currentText) || currentText == (string)AppConstants.DefaultSrcBoxText)
            {
                // Left align for default text
                SrcBox.FlowDirection = FlowDirection.LeftToRight;
                SrcBox.TextAlignment = TextAlignment.Left;
                SrcBox.HorizontalContentAlignment = HorizontalAlignment.Left;
            }
            else
            {
                // Check if it looks like a file path (contains backslashes or slashes)
                if (currentText.Contains('\\'))
                {
                    // Right align for file paths
                    SrcBox.FlowDirection = FlowDirection.RightToLeft;
                    SrcBox.TextAlignment = TextAlignment.Right;
                    SrcBox.HorizontalContentAlignment = HorizontalAlignment.Right;
                }
                else
                {
                    // Keep left alignment for other text
                    SrcBox.FlowDirection = FlowDirection.LeftToRight;
                    SrcBox.TextAlignment = TextAlignment.Left;
                    SrcBox.HorizontalContentAlignment = HorizontalAlignment.Left;
                }
                UpdateSourceFilename();
            }
        }

        private void SrcBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SrcBox.Text == (string)AppConstants.DefaultSrcBoxText)
            {
                SrcBox.Text = "";
                SrcBox.FlowDirection = FlowDirection.LeftToRight;
                SrcBox.TextAlignment = TextAlignment.Left;
                SrcBox.HorizontalContentAlignment = HorizontalAlignment.Left;
            }
        }

        private void SrcBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string path = SrcBox.Text;
            if (string.IsNullOrWhiteSpace(path) || path == (string)AppConstants.DefaultSrcBoxText)
            {
                // restore placeholder
                SrcBox.Text = (string)AppConstants.DefaultSrcBoxText;
                SrcBox.FlowDirection = FlowDirection.LeftToRight;
                SrcBox.TextAlignment = TextAlignment.Left;
                SrcBox.HorizontalContentAlignment = HorizontalAlignment.Left;
                PreviewImage.Source = null;   // <-- clear preview
                PreviewImage.Width = 0;
                PreviewImage.Height = 0;
                _originalImage = null;
                CropOverlay.Visibility = Visibility.Hidden;   // <-- hide overlay
                return;
            }

            // If it's a valid file or folder, load dimensions
            if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path))
            {
                LoadPath(path);
            }
        }
        private void ModePrefix_Checked(object sender, RoutedEventArgs e)
        {
            UpdateFilenameLayout();
        }

        private void ModeSuffix_Checked(object sender, RoutedEventArgs e)
        {
            UpdateFilenameLayout();
        }

        private string? userCustomText = null; // Store user custom text

        private string? _currentImagePath = null;

        private void UpdateFilenameLayout()
        {
            // Update the source filename first
            UpdateSourceFilename();

            bool isResize = ActionResize.IsChecked == true;
            bool isPrefix = ModePrefix.IsChecked == true;

            // Determine default text for PreSufBox
            string defaultText;
            if (isResize)
                defaultText = isPrefix ? "resized_" : "_resized";
            else
                defaultText = isPrefix ? "cropped_" : "_cropped";

            // Preserve user custom text
            string currentText = PreSufBox.Text;
            bool isDefaultText = currentText == "_cropped" || currentText == "_resized" ||
                                 currentText == "cropped_" || currentText == "resized_";
            if (!isDefaultText)
                userCustomText = currentText;

            // Set PreSufBox text
            PreSufBox.Text = userCustomText ?? defaultText;

            LayoutPositioning();

        }
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

        private const double Dpi = 96.0;               // Standard screen DPI
        private const double MmPerInch = 25.4;

        // These store the *real* pixel values of the source and current settings.
        // Initially filled with example values – you'll update these when an image is loaded.
        private int sourceWidthPx = 1000;
        private int sourceHeightPx = 2000;
        private int outputWidthPx = 500;
        private int outputHeightPx = 1000;
        private int marginLeftPx = 0;
        private int marginTopPx = 0;

        private bool _isManipulating = false;
        private string _resizeMode = "";
        private Point _startMousePos;
        private int _startMarginLeftPx, _startMarginTopPx, _startWidthPx, _startHeightPx;

        private string GetCurrentUnit()
        {
            if (UnitPixels.IsChecked == true) return "px";
            if (UnitMM.IsChecked == true) return "mm";
            if (UnitPer.IsChecked == true) return "%";
            return "px";
        }

        private double ConvertPixelsToUnit(int pixels, string unit)
        {
            if (unit == "px") return pixels;
            if (unit == "mm") return pixels / Dpi * MmPerInch;
            return pixels; // fallback
        }

        private int ConvertUnitToPixels(double value, string unit, bool clampToMin = true)
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
                var (w, h) = FindBestAspectRatioPair(targetW, targetH, sourceWidthPx, sourceHeightPx);

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

                var (w, h) = FindBestAspectRatioPair(targetW, targetH, sourceWidthPx, sourceHeightPx);

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

        private bool _updatingUI = false;

        private void AdjustAspectRatio()
        {
            if (!AspectRatio.IsChecked == true) return;
            if (sourceWidthPx <= 0 || sourceHeightPx <= 0) return;

            int targetW = outputWidthPx;
            int targetH = outputHeightPx;
            var (w, h) = FindBestAspectRatioPair(targetW, targetH, sourceWidthPx, sourceHeightPx);

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
        private void AspectRatio_Checked(object sender, RoutedEventArgs e)
        {
            if (AspectRatio.IsChecked == true)
            {
                AdjustAspectRatio();
            }
            // If unchecked, do nothing – allow free editing
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
        private void LoadPath(string path)
        {
            if (File.Exists(path))
            {
                AppendLog($"Selected file: {Path.GetFileName(path)}\n");
                LoadPreviewImage(path);   // ✅ loads once, sets dimensions internally
            }
            else if (Directory.Exists(path))
            {
                AppendLog($"Selected folder: {path}\n");
                string[] extensions = { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff" };
                bool found = false;
                foreach (var ext in extensions)
                {
                    var files = Directory.GetFiles(path, ext, SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        LoadPreviewImage(files[0]);   // ✅ loads once, sets dimensions internally
                        AppendLog($"Using first image: {Path.GetFileName(files[0])}\n");
                        found = true;
                        break;
                    }
                }
                if (!found)
                    AppendLog("No image files found in the folder.\n");
            }
            else
            {
                AppendLog($"Path not found: {path}\n");
            }
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
        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            // Reset output dimensions to the current source dimensions
            outputWidthPx = sourceWidthPx;
            outputHeightPx = sourceHeightPx;

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
            PreSufBox.Text = "cropped_"; 

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

        private void AppendLog(string text)
        {
            LogTextBox.AppendText(text);
            LogTextBox.ScrollToEnd();
        }

        private void LoadPreviewImage(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 0;
                bitmap.EndInit();
                bitmap.Freeze();

                _originalImage = bitmap;
                _currentRotation = 0;
                _previousRotation = 0;

                int w = bitmap.PixelWidth;
                int h = bitmap.PixelHeight;

                // Force image to render at pixel size (ignores DPI)
                PreviewImage.Width = w;
                PreviewImage.Height = h;

                SetSourceDimensions(w, h);
                UpdatePreviewTransform();
                UpdateDisplayedImage();
                _currentImagePath = filePath;
                CropOverlay.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                PreviewImage.Source = null;
                CropOverlay.Visibility = Visibility.Hidden;
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
        private (int w, int h) FindBestAspectRatioPair(int targetW, int targetH, int sourceW, int sourceH, int searchRadius = 5)
        {
            double bestCost = double.MaxValue;
            int bestW = Math.Max(1, targetW);
            int bestH = Math.Max(1, targetH);

            int startW = Math.Max(1, targetW - searchRadius);
            int endW = targetW + searchRadius;

            for (int w = startW; w <= endW; w++)
            {
                int h = (int)Math.Round((double)w * sourceH / sourceW);
                if (h < 1) h = 1;

                // Cost: sum of absolute differences from targets
                double cost = Math.Abs(w - targetW) + Math.Abs(h - targetH);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestW = w;
                    bestH = h;
                }
            }

            // Clamp to source if Crop mode (optional, but will be handled later)
            return (bestW, bestH);
        }
        private double GetScale() => _currentScale;

        private void CropOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isPanMode) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (ActionResize.IsChecked == true) return;

            CropOverlay.CaptureMouse();
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

            // Determine resize mode – corners take priority over edges
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

            _isManipulating = true;
            _startMousePos = pos;
            _startMarginLeftPx = marginLeftPx;
            _startMarginTopPx = marginTopPx;
            _startWidthPx = outputWidthPx;
            _startHeightPx = outputHeightPx;

            e.Handled = true;
        }
        private void CropOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanMode) return;
            Point pos = e.GetPosition(PreviewCanvas);

            if (!_isManipulating)
            {
                // Update cursor based on position
                double left = Canvas.GetLeft(CropOverlay);
                double top = Canvas.GetTop(CropOverlay);
                double right = left + CropOverlay.Width;
                double bottom = top + CropOverlay.Height;
                double tolerance = EdgeTolerance;

                bool nearLeft = Math.Abs(pos.X - left) < tolerance;
                bool nearRight = Math.Abs(pos.X - right) < tolerance;
                bool nearTop = Math.Abs(pos.Y - top) < tolerance;
                bool nearBottom = Math.Abs(pos.Y - bottom) < tolerance;

                if (nearLeft && nearTop)
                    Cursor = Cursors.SizeNWSE;
                else if (nearRight && nearTop)
                    Cursor = Cursors.SizeNESW;
                else if (nearLeft && nearBottom)
                    Cursor = Cursors.SizeNESW;
                else if (nearRight && nearBottom)
                    Cursor = Cursors.SizeNWSE;
                else if (nearLeft || nearRight)
                    Cursor = Cursors.SizeWE;
                else if (nearTop || nearBottom)
                    Cursor = Cursors.SizeNS;
                else
                    Cursor = Cursors.Hand;
                return;
            }

            // --- Manipulation ---
            if (ActionResize.IsChecked == true) return;

            // pos is already in PreviewCanvas's local (unscaled) coordinate space,
            // since GetPosition(PreviewCanvas) accounts for the canvas's own RenderTransform.
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
        private int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);
        private double Clamp(double value, double min, double max) => value < min ? min : (value > max ? max : value);

        private const double EdgeTolerance = 12.0;

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
        private void RefreshCropUI()
        {
            ClampCropRectangle();
            UpdateMaxValues();
            UpdateAllTextBoxes();
        }
        private double _previousRotation = 0;
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
        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_originalImage == null) return;
            if (_zoomMode == ZoomMode.Fit)
            {
                // Start from the current fit scale
                _customZoom = _currentScale * 1.1;
            }
            else
            {
                _customZoom *= 1.1;
            }
            _zoomMode = ZoomMode.Custom;
            UpdatePreviewTransform();
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_originalImage == null) return;
            if (_zoomMode == ZoomMode.Fit)
            {
                _customZoom = _currentScale / 1.1;
            }
            else
            {
                _customZoom /= 1.1;
            }
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
                string defaultText = isResize
                    ? (isPrefix ? "resized_" : "_resized")
                    : (isPrefix ? "cropped_" : "_cropped");

                // Update text without re-entrancy issues
                textBox.Text = defaultText;
                UpdateFilenameLayout();

                if (result != MessageBoxResult.Yes)
                    return;

                OverwriteChk.IsChecked = true;
            }
        }



        private async void CropBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_originalImage == null)
            {
                AppendLog("No image loaded.\n");
                return;
            }

            // Determine source path
            string srcPath = SrcBox.Text;
            if (string.IsNullOrWhiteSpace(srcPath) || srcPath == AppConstants.DefaultSrcBoxText)
            {
                AppendLog("Please specify a source file or folder.\n");
                return;
            }

            // Determine output folder
            string dstPath = DstBox.Text;
            string outputFolder;
            if (!string.IsNullOrWhiteSpace(dstPath) && Directory.Exists(dstPath))
            {
                outputFolder = dstPath;
            }
            else if (Directory.Exists(srcPath))
            {
                outputFolder = srcPath; // use source folder if no output specified
            }
            else
            {
                outputFolder = Path.GetDirectoryName(srcPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }

            // Check if source is a folder
            if (Directory.Exists(srcPath))
            {
                // Batch processing
                string[] extensions = { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff" };
                var files = new List<string>();
                foreach (var ext in extensions)
                {
                    files.AddRange(Directory.GetFiles(srcPath, ext, SearchOption.TopDirectoryOnly));
                }
                if (files.Count == 0)
                {
                    AppendLog("No image files found in the selected folder.\n");
                    return;
                }

                AppendLog($"Starting batch crop for {files.Count} images...\n");
                await ProcessBatchAsync(srcPath, outputFolder, files);
                return;
            }

            // --- Single file processing (existing code) ---
            // Ensure the crop rectangle is valid
            ClampCropRectangle();

            int x = marginLeftPx;
            int y = marginTopPx;
            int w = outputWidthPx;
            int h = outputHeightPx;

            // Clamp to image bounds (should already be clamped, but safety)
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x + w > sourceWidthPx) w = Math.Max(0, sourceWidthPx - x);
            if (y + h > sourceHeightPx) h = Math.Max(0, sourceHeightPx - y);
            if (w <= 0 || h <= 0) return;

            try
            {
                BitmapSource source = _originalImage;
                if (Math.Abs(_currentRotation % 360) > 0.001)
                {
                    var transform = new RotateTransform(_currentRotation);
                    source = new TransformedBitmap(_originalImage, transform);
                    source.Freeze();
                }

                var cropRect = new Int32Rect(x, y, w, h);
                var cropped = new CroppedBitmap(source, cropRect);
                cropped.Freeze();

                // Determine filename (original code)
                string originalName = string.Empty;
                if (!string.IsNullOrWhiteSpace(_currentImagePath))
                    originalName = Path.GetFileName(_currentImagePath);
                string ext = Path.GetExtension(originalName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

                string finalBase = string.Empty;
                string PreSufFromUI = (PreSufBox.Text ?? "").Trim();
                string SourcePart = (NameSource.Text ?? "").Trim();

                if (OverwriteChk.IsChecked == true)
                {
                    finalBase = Path.GetFileNameWithoutExtension(originalName);
                }
                else if (ModePrefix.IsChecked == true)
                {
                    finalBase = $"{PreSufFromUI}{SourcePart}";
                }
                else if (ModeSuffix.IsChecked == true)
                {
                    finalBase = $"{SourcePart}{PreSufFromUI}";
                }

                string saveFileName = finalBase + ext;
                string savePath = Path.Combine(outputFolder, saveFileName);

                if (File.Exists(savePath) && OverwriteChk.IsChecked == false)
                {
                    var res = System.Windows.MessageBox.Show(
                        $"File already exists:\n{savePath}\nOverwrite?",
                        "Overwrite?",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.No);

                    if (res != MessageBoxResult.Yes)
                    {
                        int count = 1;
                        string baseName = Path.GetFileNameWithoutExtension(saveFileName);
                        while (File.Exists(savePath))
                        {
                            saveFileName = $"{baseName}_{count}{ext}";
                            savePath = Path.Combine(outputFolder, saveFileName);
                            count++;
                        }
                    }
                }

                BitmapEncoder encoder;
                string lowerExt = ext.ToLower();
                if (lowerExt == ".png") encoder = new PngBitmapEncoder();
                else if (lowerExt == ".bmp") encoder = new BmpBitmapEncoder();
                else if (lowerExt == ".gif") encoder = new GifBitmapEncoder();
                else encoder = new JpegBitmapEncoder();

                encoder.Frames.Add(BitmapFrame.Create(cropped));
                if (encoder is JpegBitmapEncoder jpeg) jpeg.QualityLevel = 90;

                using (var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(stream);
                }

                AppendLog($"Cropped image saved to: {savePath}\n");

                if (OpenAfterChk.IsChecked == true)
                {
                    System.Diagnostics.Process.Start("explorer.exe", outputFolder);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Crop failed: {ex.Message}\n");
            }
        }
        private BitmapSource? LoadImageFromFile(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 0;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
        private BitmapSource CropSingleImage(BitmapSource source, double rotationAngle,
                                     int cropX, int cropY, int cropW, int cropH)
        {
            BitmapSource rotated = source;
            if (Math.Abs(rotationAngle % 360) > 0.001)
            {
                var transform = new RotateTransform(rotationAngle);
                rotated = new TransformedBitmap(source, transform);
                rotated.Freeze();
            }

            // Ensure crop rectangle is within bounds
            int rw = rotated.PixelWidth;
            int rh = rotated.PixelHeight;
            cropX = Math.Max(0, Math.Min(cropX, rw - 1));
            cropY = Math.Max(0, Math.Min(cropY, rh - 1));
            cropW = Math.Max(1, Math.Min(cropW, rw - cropX));
            cropH = Math.Max(1, Math.Min(cropH, rh - cropY));

            var cropRect = new Int32Rect(cropX, cropY, cropW, cropH);
            var cropped = new CroppedBitmap(rotated, cropRect);
            cropped.Freeze();
            return cropped;
        }
        private async System.Threading.Tasks.Task ProcessBatchAsync(string folderPath, string outputFolder, List<string> files)
        {
            // Compute relative crop ratios from the current (first image) dimensions
            double leftRatio = (double)marginLeftPx / sourceWidthPx;
            double topRatio = (double)marginTopPx / sourceHeightPx;
            double widthRatio = (double)outputWidthPx / sourceWidthPx;
            double heightRatio = (double)outputHeightPx / sourceHeightPx;

            int total = files.Count;
            int processed = 0;
            Progress.Maximum = total;
            Progress.Value = 0;
            CancelBtn.IsEnabled = true; // for future cancellation

            // Precompute rotation angle
            double angle = _currentRotation % 360;

            foreach (string filePath in files)
            {
                // Check for cancellation (you can implement later)
                // if (cancellationToken.IsCancellationRequested) break;

                // Load the image
                var image = LoadImageFromFile(filePath);
                if (image == null)
                {
                    AppendLog($"Failed to load: {Path.GetFileName(filePath)}\n");
                    processed++;
                    Progress.Value = processed;
                    continue;
                }

                // Determine rotated dimensions
                int origW = image.PixelWidth;
                int origH = image.PixelHeight;
                int rotW = origW, rotH = origH;
                double angleMod = angle;
                if (angleMod < 0) angleMod += 360;
                if (Math.Abs(angleMod - 90) < 0.1 || Math.Abs(angleMod - 270) < 0.1)
                {
                    rotW = origH;
                    rotH = origW;
                }

                // Compute crop rectangle in rotated coordinates
                int cropX = (int)Math.Round(leftRatio * rotW);
                int cropY = (int)Math.Round(topRatio * rotH);
                int cropW = (int)Math.Round(widthRatio * rotW);
                int cropH = (int)Math.Round(heightRatio * rotH);

                // Clamp to image bounds
                cropX = Math.Max(0, Math.Min(cropX, rotW - 1));
                cropY = Math.Max(0, Math.Min(cropY, rotH - 1));
                cropW = Math.Max(1, Math.Min(cropW, rotW - cropX));
                cropH = Math.Max(1, Math.Min(cropH, rotH - cropY));

                // Perform crop
                var cropped = CropSingleImage(image, angle, cropX, cropY, cropW, cropH);

                // Determine output filename
                string originalName = Path.GetFileName(filePath);
                string baseName = Path.GetFileNameWithoutExtension(originalName);
                string ext = Path.GetExtension(originalName);
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";

                string finalBase;
                string PreSufFromUI = (PreSufBox.Text ?? "").Trim();
                if (OverwriteChk.IsChecked == true)
                {
                    finalBase = baseName;
                }
                else if (ModePrefix.IsChecked == true)
                {
                    finalBase = $"{PreSufFromUI}{baseName}";
                }
                else // suffix
                {
                    finalBase = $"{baseName}{PreSufFromUI}";
                }

                string saveFileName = finalBase + ext;
                string savePath = Path.Combine(outputFolder, saveFileName);

                // Handle overwrite conflicts if not overwriting
                if (!OverwriteChk.IsChecked == true && File.Exists(savePath))
                {
                    int count = 1;
                    while (File.Exists(savePath))
                    {
                        saveFileName = $"{finalBase}_{count}{ext}";
                        savePath = Path.Combine(outputFolder, saveFileName);
                        count++;
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

                    encoder.Frames.Add(BitmapFrame.Create(cropped));
                    if (encoder is JpegBitmapEncoder jpeg) jpeg.QualityLevel = 90;

                    using (var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                    {
                        encoder.Save(stream);
                    }
                    AppendLog($"Saved: {saveFileName}\n");
                }
                catch (Exception ex)
                {
                    AppendLog($"Error saving {saveFileName}: {ex.Message}\n");
                }

                processed++;
                Progress.Value = processed;
                // Allow UI to update
                await System.Threading.Tasks.Task.Delay(1);
            }

            Progress.Value = total;
            CancelBtn.IsEnabled = false;
            AppendLog($"Batch processing completed. {processed} files processed.\n");

            // Open folder if checked
            if (OpenAfterChk.IsChecked == true)
            {
                System.Diagnostics.Process.Start("explorer.exe", outputFolder);
            }
        }

    }

}