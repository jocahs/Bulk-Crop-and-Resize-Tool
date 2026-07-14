using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xceed.Wpf.Toolkit;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace ImageCropTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Set initial states

            outputWidthPx = sourceWidthPx;
            outputHeightPx = sourceHeightPx;
            UpdateMaxValues();
            
            UpdateUnitAvailability();
            UpdateFilenameAvailability();
            UpdateFilenameLayout();
            UpdateSourceFilename();
            UpdateAllTextBoxes();


            // Attach event handlers
            ActionCrop.Checked += Action_CheckedChanged;
            ActionResize.Checked += Action_CheckedChanged;
            UnitPixels.Checked += Unit_CheckedChanged;
            UnitMM.Checked += Unit_CheckedChanged;
            UnitPer.Checked += Unit_CheckedChanged;
            NoOverwriteChk.Checked += NoOverwrite_CheckedChanged;
            NoOverwriteChk.Unchecked += NoOverwrite_CheckedChanged;
            ModePrefix.Checked += ModePrefix_Checked;
            ModeSuffix.Checked += ModeSuffix_Checked;
            UnitPer.IsEnabled = false;
            SrcBox.TextChanged += SrcBox_TextChanged;
            WidthBox.ValueChanged += WidthBox_ValueChanged;
            HeightBox.ValueChanged += HeightBox_ValueChanged;
            MarginLeftBox.ValueChanged += MarginLeftBox_ValueChanged;
            MargintopBox.ValueChanged += MargintopBox_ValueChanged;
            AspectRatio.Checked += AspectRatio_Checked;
            AspectRatio.Unchecked += AspectRatio_Checked;
            WidthBox.LostFocus += WidthBox_LostFocus;
            HeightBox.LostFocus += HeightBox_LostFocus;
            ResetBtn.Click += ResetBtn_Click;
            PreviewImage.SizeChanged += (s, e) => UpdateCropOverlay();
            PreviewImage.Loaded += (s, e) => UpdateCropOverlay();
            PreviewImage.Loaded += (s, e) => UpdateCropOverlay();
            CropOverlay.MouseDown += CropOverlay_MouseDown;
            CropOverlay.MouseMove += CropOverlay_MouseMove;
            CropOverlay.MouseUp += CropOverlay_MouseUp;
            PreviewCanvas.MouseLeave += (s, e) => Cursor = Cursors.Arrow;
        }


        private void Action_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ActionResize.IsChecked == false)            
            {
                // When switching to Crop, ensure output doesn't exceed source
                if (outputWidthPx > sourceWidthPx) outputWidthPx = sourceWidthPx;
                if (outputHeightPx > sourceHeightPx) outputHeightPx = sourceHeightPx;
                UpdateAllTextBoxes(); // refresh UI after clamping
            }
            UpdateUnitAvailability();
            UpdateMaxValues();
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
          
            ModePrefix.IsEnabled = isEnabled;
            ModeSuffix.IsEnabled = isEnabled;
            NameBox.IsEnabled = isEnabled;

            if (isEnabled)
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
            if (string.IsNullOrEmpty(path) || path == "Write/Paste or Browse the source path of a file/folder ------->")
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
            if (string.IsNullOrEmpty(currentText) || currentText == "Write/Paste or Browse the source path of a file/folder ------->")
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
            if (SrcBox.Text == "Write/Paste or Browse the source path of a file/folder ------->")
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
            if (string.IsNullOrWhiteSpace(path) || path == "Write/Paste or Browse the source path of a file/folder ------->")
            {
                // restore placeholder
                SrcBox.Text = "Write/Paste or Browse the source path of a file/folder ------->";
                SrcBox.FlowDirection = FlowDirection.LeftToRight;
                SrcBox.TextAlignment = TextAlignment.Left;
                SrcBox.HorizontalContentAlignment = HorizontalAlignment.Left;
                PreviewImage.Source = null;   // <-- clear preview
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
        private int outputWidthPx;
        private int outputHeightPx;
        private int marginLeftPx = 0;
        private int marginTopPx = 0;
        private int _lastDisplayWidth = 1;
        private int _lastDisplayHeight = 1;

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
                _lastDisplayWidth = WidthBox.Value ?? 0;
                _lastDisplayHeight = HeightBox.Value ?? 0;
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
                UpdateAllTextBoxes();
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
                UpdateAllTextBoxes();
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
        }

        private void MargintopBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updatingUI || !IsLoaded) return;
            UpdatePixelFromBox(MargintopBox, ref marginTopPx, sourceHeightPx, false);
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

            UpdateAllTextBoxes();
        }

        private bool _updatingUI = false;

        private int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

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

        private (int width, int height)? GetImageDimensions(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.DecodePixelWidth = 0; // forces full resolution
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // optional, for better performance

                return (bitmap.PixelWidth, bitmap.PixelHeight);
            }
            catch
            {
                return null;
            }
        }

        private void LoadPath(string path)
        {
            if (System.IO.File.Exists(path))
            {
                AppendLog($"Selected file: {System.IO.Path.GetFileName(path)}\n");
                var dims = GetImageDimensions(path);
                if (dims.HasValue)
                {
                    SetSourceDimensions(dims.Value.width, dims.Value.height);
                    AppendLog($"Loaded dimensions: {dims.Value.width} × {dims.Value.height} px\n");
                    LoadPreviewImage(path);
                }
                else
                {
                    AppendLog("Warning: Could not read image dimensions.\n");
                }
            }
            else if (System.IO.Directory.Exists(path))
            {
                AppendLog($"Selected folder: {path}\n");
                // Find first image file
                string[] extensions = { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff" };
                bool found = false;
                foreach (var ext in extensions)
                {
                    var files = System.IO.Directory.GetFiles(path, ext, System.IO.SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        var dims = GetImageDimensions(files[0]);
                        if (dims.HasValue)
                        {
                            SetSourceDimensions(dims.Value.width, dims.Value.height);
                            AppendLog($"Using first image: {System.IO.Path.GetFileName(files[0])} ({dims.Value.width} × {dims.Value.height} px)\n");
                            LoadPreviewImage(files[0]);
                        }
                        else
                        {
                            AppendLog($"Warning: Could not read dimensions from {System.IO.Path.GetFileName(files[0])}\n");
                        }
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
            if (sourceWidthPx <= 0 || sourceHeightPx <= 0) return;

            // Get scale 
            var scale = GetScale();

            // Apply scale and offset
            CropOverlay.Width = Math.Max(1, outputWidthPx * scale);
            CropOverlay.Height = Math.Max(1, outputHeightPx * scale);
            Canvas.SetLeft(CropOverlay, marginLeftPx * scale);
            Canvas.SetTop(CropOverlay, marginTopPx * scale);
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
            NoOverwriteChk.IsChecked = true;

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
                // In Crop mode, the output cannot exceed the source dimensions
                double maxW = ConvertPixelsToUnit(sourceWidthPx, unit);
                double maxH = ConvertPixelsToUnit(sourceHeightPx, unit);
                WidthBox.Maximum = (int)Math.Ceiling(maxW);
                HeightBox.Maximum = (int)Math.Ceiling(maxH);
            }
            else
            {
                // In Resize mode, allow any reasonable value (e.g., 99999)
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
                bitmap.DecodePixelWidth = 0; // full resolution
                bitmap.EndInit();
                bitmap.Freeze(); // makes it cross-thread safe
                PreviewImage.Source = bitmap;
                _currentImagePath = filePath;
            }
            catch (Exception ex)
            {
                PreviewImage.Source = null;
                AppendLog($"Failed to load preview: {ex.Message}\n");
            }
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
            if (ActionResize.IsChecked == true) return; // disable in Resize mode
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

            // Determine resize mode: only edges, no corners
            if (nearLeft) _resizeMode = "ResizeLeft";
            else if (nearRight) _resizeMode = "ResizeRight";
            else if (nearTop) _resizeMode = "ResizeTop";
            else if (nearBottom) _resizeMode = "ResizeBottom";
            else _resizeMode = "Drag";

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

                if (nearLeft) Cursor = Cursors.SizeWE;
                else if (nearRight) Cursor = Cursors.SizeWE;
                else if (nearTop) Cursor = Cursors.SizeNS;
                else if (nearBottom) Cursor = Cursors.SizeNS;
                else Cursor = Cursors.Hand;
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
                    // Move overlay: only margins change; size unchanged
                    newMarginLeft = (int)Math.Round(_startMarginLeftPx + deltaX);
                    newMarginTop = (int)Math.Round(_startMarginTopPx + deltaY);
                    // Clamp margins so the overlay stays inside the source image
                    newMarginLeft = Clamp(newMarginLeft, 0, sourceWidthPx - newWidth);
                    newMarginTop = Clamp(newMarginTop, 0, sourceHeightPx - newHeight);
                    break;

                case "ResizeLeft":
                    // Only horizontal movement matters
                    newMarginLeft = (int)Math.Round(_startMarginLeftPx + deltaX);
                    // Width changes inversely: newWidth = _startWidthPx - (newMarginLeft - _startMarginLeftPx)
                    newWidth = _startWidthPx - (newMarginLeft - _startMarginLeftPx);
                    // Clamp
                    if (newMarginLeft < 0) { newWidth += newMarginLeft; newMarginLeft = 0; }
                    if (newWidth < 1) { newWidth = 1; newMarginLeft = Math.Min(newMarginLeft, sourceWidthPx - 1); }
                    if (newMarginLeft + newWidth > sourceWidthPx) newWidth = sourceWidthPx - newMarginLeft;
                    break;

                case "ResizeRight":
                    // Only horizontal movement matters
                    newWidth = (int)Math.Round(_startWidthPx + deltaX);
                    if (newWidth < 1) newWidth = 1;
                    if (newMarginLeft + newWidth > sourceWidthPx) newWidth = sourceWidthPx - newMarginLeft;
                    break;

                case "ResizeTop":
                    // Only vertical movement matters
                    newMarginTop = (int)Math.Round(_startMarginTopPx + deltaY);
                    newHeight = _startHeightPx - (newMarginTop - _startMarginTopPx);
                    if (newMarginTop < 0) { newHeight += newMarginTop; newMarginTop = 0; }
                    if (newHeight < 1) { newHeight = 1; newMarginTop = Math.Min(newMarginTop, sourceHeightPx - 1); }
                    if (newMarginTop + newHeight > sourceHeightPx) newHeight = sourceHeightPx - newMarginTop;
                    break;

                case "ResizeBottom":
                    // Only vertical movement matters
                    newHeight = (int)Math.Round(_startHeightPx + deltaY);
                    if (newHeight < 1) newHeight = 1;
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
        private int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

        private const double EdgeTolerance = 12.0;
    }

}