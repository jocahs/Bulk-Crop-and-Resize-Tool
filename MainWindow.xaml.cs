using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xceed.Wpf.Toolkit;

namespace ImageCropTool
{
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
            AspectRatio.Checked += AspectRatio_Checked;
            AspectRatio.Unchecked += AspectRatio_Checked;
            CropOverlay.Visibility = Visibility.Hidden;
            CropOverlay.MouseDown += CropOverlay_MouseDown;
            CropOverlay.MouseMove += CropOverlay_MouseMove;
            CropOverlay.MouseUp += CropOverlay_MouseUp;
            HeightBox.LostFocus += HeightBox_LostFocus;
            HeightBox.ValueChanged += HeightBox_ValueChanged;
            MarginLeftBox.ValueChanged += MarginLeftBox_ValueChanged;
            MargintopBox.ValueChanged += MargintopBox_ValueChanged;
            ModePrefix.Checked += ModePrefix_Checked;
            ModeSuffix.Checked += ModeSuffix_Checked;
            NoOverwriteChk.Checked += NoOverwrite_CheckedChanged;
            NoOverwriteChk.Unchecked += NoOverwrite_CheckedChanged;
            PreviewCanvas.MouseLeave += (s, e) => Cursor = Cursors.Arrow;
            PreviewImage.Loaded += (s, e) => UpdateCropOverlay();
            PreviewImage.SizeChanged += (s, e) => UpdateCropOverlay();
            this.PreviewKeyDown += Window_PreviewKeyDown; 
            ResetBtn.Click += ResetBtn_Click;
            Rotate180.Click += (s, e) => { _currentRotation += 180; UpdateDisplayedImage(); };
            RotateMinus90.Click += (s, e) => { _currentRotation -= 90; UpdateDisplayedImage(); };
            RotateMore90.Click += (s, e) => { _currentRotation += 90; UpdateDisplayedImage(); };
            SrcBox.TextChanged += SrcBox_TextChanged;
            UnitMM.Checked += Unit_CheckedChanged;
            UnitPer.Checked += Unit_CheckedChanged;
            UnitPer.IsEnabled = false;
            UnitPixels.Checked += Unit_CheckedChanged;
            WidthBox.LostFocus += WidthBox_LostFocus;
            WidthBox.ValueChanged += WidthBox_ValueChanged;

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
            CropOverlay.Visibility = isResize ? Visibility.Collapsed : Visibility.Visible;

            // If UnitPer is selected but now disabled, switch to Pixels
            if (!isResize && UnitPer.IsChecked == true)
            {
                UnitPixels.IsChecked = true;
            }
        }

        private void UpdateFilenameAvailability()
        {
            // All filename controls should be enabled only when NoOverwriteChk is checked
            bool isEnabled = NoOverwriteChk.IsChecked == true;
          
            ModePrefix.IsEnabled = !isEnabled;
            ModeSuffix.IsEnabled = !isEnabled;
            NameBox.IsEnabled = !isEnabled;

            if (!isEnabled)
            {
                LayoutPositioning();
            }
            else
            {
                NameStackPanel.Children.Clear();
                NameStackPanel.Children.Add(NameExample);
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
            if (string.IsNullOrEmpty(path) || path == "Write/Paste or Browse the source path of a file/folder ------>")
            {
                NameExample.Text = "filename";
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

            NameExample.Text = baseName;
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
            if (string.IsNullOrEmpty(currentText) || currentText == "Write/Paste or Browse the source path of a file/folder ------>")
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
            if (SrcBox.Text == "Write/Paste or Browse the source path of a file/folder ------>")
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
            if (string.IsNullOrWhiteSpace(path) || path == "Write/Paste or Browse the source path of a file/folder ------>")
            {
                // restore placeholder
                SrcBox.Text = "Write/Paste or Browse the source path of a file/folder ------>";
                SrcBox.FlowDirection = FlowDirection.LeftToRight;
                SrcBox.TextAlignment = TextAlignment.Left;
                SrcBox.HorizontalContentAlignment = HorizontalAlignment.Left;
                PreviewImage.Source = null;   // <-- clear preview
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

            // Determine default text for NameBox
            string defaultText;
            if (isResize)
                defaultText = isPrefix ? "resized_" : "_resized";
            else
                defaultText = isPrefix ? "cropped_" : "_cropped";

            // Preserve user custom text
            string currentText = NameBox.Text;
            bool isDefaultText = currentText == "_cropped" || currentText == "_resized" ||
                                 currentText == "cropped_" || currentText == "resized_";
            if (!isDefaultText)
                userCustomText = currentText;

            // Set NameBox text
            NameBox.Text = userCustomText ?? defaultText;

            LayoutPositioning();
            
        }
        private void LayoutPositioning()
        {
            bool isPrefix = ModePrefix.IsChecked == true;
            
            if (isPrefix)
            {
                NameStackPanel.Children.Clear();
                NameStackPanel.Children.Add(NameBox);
                NameStackPanel.Children.Add(NameExample);
                NameStackPanel.Children.Add(NameExtension);
            }
            else
            {
                NameStackPanel.Children.Clear();
                NameStackPanel.Children.Add(NameExample);
                NameStackPanel.Children.Add(NameBox);
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
                CropOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            double scale = GetScale();

            // Overlay is already in the current image pixel space (rotated if needed)
            CropOverlay.Width = Math.Max(1, outputWidthPx * scale);
            CropOverlay.Height = Math.Max(1, outputHeightPx * scale);
            Canvas.SetLeft(CropOverlay, marginLeftPx * scale);
            Canvas.SetTop(CropOverlay, marginTopPx * scale);

            // Do NOT rotate the overlay border – it stays axis‑aligned (bounding box)
            CropOverlay.RenderTransform = null;

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
            NoOverwriteChk.IsChecked = false;

            // Force UI refresh (this also updates the crop overlay)
            UpdateAllTextBoxes();
            UpdateFilenameLayout();

            // Log the action
            AppendLog("Settings reset to default.\n");
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
                SetSourceDimensions(w, h);
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
        private double GetScale()
        {
            if (sourceWidthPx <= 0 || sourceHeightPx <= 0) return (1);

            double displayWidth = PreviewCanvas.ActualWidth;
            double displayHeight = PreviewCanvas.ActualHeight;
            if (displayWidth <= 0 || displayHeight <= 0)
            {
                displayWidth = 600;
                displayHeight = 600;
            }

            double scaleX = displayWidth / sourceWidthPx;
            double scaleY = displayHeight / sourceHeightPx;
            double scale = Math.Min(scaleX, scaleY);
            if (scale <= 0 || double.IsInfinity(scale) || double.IsNaN(scale))
                scale = 1.0;

            return (scale);
        }
        private void CropOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
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

            var scale = GetScale();
            double invScale = 1.0 / scale;
            double deltaX = (pos.X - _startMousePos.X) * invScale;
            double deltaY = (pos.Y - _startMousePos.Y) * invScale;

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
    }

}