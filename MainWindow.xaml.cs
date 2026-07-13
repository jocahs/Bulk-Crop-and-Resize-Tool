using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        }


        private void Action_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ActionResize.IsChecked == true)
            {
                marginLeftPx = 0;
                marginTopPx = 0;
            }
            else
            {
                AspectRatio.IsChecked = false;
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
            if (isResize)
            {
                marginLeftPx = 0;
                marginTopPx = 0;
            }

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
                else if (unit == "mm")
                {
                    var (rw, rh) = GetReducedRatio();

                    // ----- Source dimensions -----
                    if (AspectRatio.IsChecked == true)
                    {
                        double actualSrc_mmW = ConvertPixelsToUnit(sourceWidthPx, "mm");
                        double actualSrc_mmH = ConvertPixelsToUnit(sourceHeightPx, "mm");

                        int k1 = (int)Math.Round(actualSrc_mmW / rw);
                        int k2 = (int)Math.Round(actualSrc_mmH / rh);
                        if (k1 < 1) k1 = 1;
                        if (k2 < 1) k2 = 1;

                        double err1 = Math.Abs(actualSrc_mmW - k1 * rw) + Math.Abs(actualSrc_mmH - k1 * rh);
                        double err2 = Math.Abs(actualSrc_mmW - k2 * rw) + Math.Abs(actualSrc_mmH - k2 * rh);
                        int kSrc = err1 <= err2 ? k1 : k2;

                        int srcW_mm = kSrc * rw;
                        int srcH_mm = kSrc * rh;
                        WidthSourceBox.Text = Math.Max(1, srcW_mm).ToString();
                        HeightSourceBox.Text = Math.Max(1, srcH_mm).ToString();
                    }
                    else
                    {
                        int srcW_mm = (int)Math.Round(ConvertPixelsToUnit(sourceWidthPx, "mm"));
                        int srcH_mm = (int)Math.Round(ConvertPixelsToUnit(sourceHeightPx, "mm"));
                        WidthSourceBox.Text = Math.Max(1, srcW_mm).ToString();
                        HeightSourceBox.Text = Math.Max(1, srcH_mm).ToString();
                    }

                    // ----- Output dimensions -----
                    if (AspectRatio.IsChecked == true)
                    {
                        double actual_mmW = ConvertPixelsToUnit(outputWidthPx, "mm");
                        double actual_mmH = ConvertPixelsToUnit(outputHeightPx, "mm");

                        int k1_out = (int)Math.Round(actual_mmW / rw);
                        int k2_out = (int)Math.Round(actual_mmH / rh);
                        if (k1_out < 1) k1_out = 1;
                        if (k2_out < 1) k2_out = 1;

                        double err1_out = Math.Abs(actual_mmW - k1_out * rw) + Math.Abs(actual_mmH - k1_out * rh);
                        double err2_out = Math.Abs(actual_mmW - k2_out * rw) + Math.Abs(actual_mmH - k2_out * rh);
                        int kOut = err1_out <= err2_out ? k1_out : k2_out;

                        int outW_mm = kOut * rw;
                        int outH_mm = kOut * rh;
                        WidthBox.Value = outW_mm;
                        HeightBox.Value = outH_mm;
                    }
                    else
                    {
                        int outW_mm = (int)Math.Round(ConvertPixelsToUnit(outputWidthPx, "mm"));
                        int outH_mm = (int)Math.Round(ConvertPixelsToUnit(outputHeightPx, "mm"));
                        WidthBox.Value = Math.Max(1, outW_mm);
                        HeightBox.Value = Math.Max(1, outH_mm);
                    }

                    // ----- Margins (always independent) -----
                    int mL_mm = (int)Math.Round(ConvertPixelsToUnit(marginLeftPx, "mm"));
                    int mT_mm = (int)Math.Round(ConvertPixelsToUnit(marginTopPx, "mm"));
                    MarginLeftBox.Value = mL_mm;
                    MargintopBox.Value = mT_mm;
                }
                else // pixels
                {
                    int srcW = Math.Max(1, (int)Math.Round(ConvertPixelsToUnit(sourceWidthPx, "px")));
                    int srcH = Math.Max(1, (int)Math.Round(ConvertPixelsToUnit(sourceHeightPx, "px")));
                    int outW = Math.Max(1, (int)Math.Round(ConvertPixelsToUnit(outputWidthPx, "px")));
                    int outH = Math.Max(1, (int)Math.Round(ConvertPixelsToUnit(outputHeightPx, "px")));
                    int mL = (int)Math.Round(ConvertPixelsToUnit(marginLeftPx, "px"));
                    int mT = (int)Math.Round(ConvertPixelsToUnit(marginTopPx, "px"));

                    WidthSourceBox.Text = srcW.ToString();
                    HeightSourceBox.Text = srcH.ToString();
                    WidthBox.Value = outW;
                    HeightBox.Value = outH;
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

                // Percentage mode: both percentages are equal (uniform scaling)
                if (unit == "%")
                {
                    double newWidthPercent = newDisplayValue;
                    double newHeightPercent = newWidthPercent;

                    int newHeightDisplay = (int)Math.Round(newHeightPercent);
                    if (newHeightDisplay < 1) newHeightDisplay = 1;

                    outputWidthPx = (int)Math.Round(newWidthPercent / 100.0 * sourceWidthPx);
                    outputHeightPx = (int)Math.Round(newHeightPercent / 100.0 * sourceHeightPx);
                    if (outputWidthPx < 1) outputWidthPx = 1;
                    if (outputHeightPx < 1) outputHeightPx = 1;

                    UpdateAllTextBoxes();
                    return;
                }

                var (rw, rh) = GetReducedRatio();
                double rwUnit = ConvertPixelsToUnit(rw, unit); // size of one reduced unit in current unit

                int oldDisplayValue = _lastDisplayWidth;
                bool increasing = newDisplayValue > oldDisplayValue;
                bool decreasing = newDisplayValue < oldDisplayValue;

                int k;
                if (increasing)
                {
                    // Find the smallest k such that rounded(k * rwUnit) >= newDisplayValue
                    k = 1;
                    while ((int)Math.Round(k * rwUnit) < newDisplayValue)
                        k++;
                }
                else if (decreasing)
                {
                    // Find the largest k such that rounded(k * rwUnit) <= newDisplayValue
                    // Start from a reasonable high estimate (based on the entered value)
                    k = (int)Math.Floor((newDisplayValue + 0.5) / rwUnit);
                    if (k < 1) k = 1;
                    // Adjust downward if necessary
                    while ((int)Math.Round(k * rwUnit) > newDisplayValue)
                        k--;
                    if (k < 1) k = 1;
                }
                else // no change – keep current
                {
                    k = (int)Math.Round((double)outputWidthPx / rw);
                    if (k < 1) k = 1;
                }

                outputWidthPx = k * rw;
                outputHeightPx = k * rh;
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
                    int newWidthDisplay = (int)Math.Round(newWidthPercent);
                    if (newWidthDisplay < 1) newWidthDisplay = 1;

                    outputWidthPx = (int)Math.Round(newWidthPercent / 100.0 * sourceWidthPx);
                    outputHeightPx = (int)Math.Round(newHeightPercent / 100.0 * sourceHeightPx);
                    if (outputWidthPx < 1) outputWidthPx = 1;
                    if (outputHeightPx < 1) outputHeightPx = 1;

                    UpdateAllTextBoxes();
                    return;
                }

                var (rw, rh) = GetReducedRatio();
                double rhUnit = ConvertPixelsToUnit(rh, unit);

                int oldDisplayValue = _lastDisplayHeight;
                bool increasing = newDisplayValue > oldDisplayValue;
                bool decreasing = newDisplayValue < oldDisplayValue;

                int k;
                if (increasing)
                {
                    k = 1;
                    while ((int)Math.Round(k * rhUnit) < newDisplayValue)
                        k++;
                }
                else if (decreasing)
                {
                    k = (int)Math.Floor((newDisplayValue + 0.5) / rhUnit);
                    if (k < 1) k = 1;
                    while ((int)Math.Round(k * rhUnit) > newDisplayValue)
                        k--;
                    if (k < 1) k = 1;
                }
                else
                {
                    k = (int)Math.Round((double)outputHeightPx / rh);
                    if (k < 1) k = 1;
                }

                outputWidthPx = k * rw;
                outputHeightPx = k * rh;
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

        private (int rw, int rh) GetReducedRatio()
        {
            int g = GCD(sourceWidthPx, sourceHeightPx);
            return (sourceWidthPx / g, sourceHeightPx / g);
        }

        private void AdjustAspectRatio()
        {
            if (!AspectRatio.IsChecked == true) return;
            if (sourceWidthPx <= 0 || sourceHeightPx <= 0) return;

            var (rw, rh) = GetReducedRatio();
            int k;

            if (sourceWidthPx <= sourceHeightPx)
            {
                // Keep width – compute k from current outputWidthPx
                k = (int)Math.Round((double)outputWidthPx / rw);
                if (k < 1) k = 1;
            }
            else
            {
                // Keep height – compute k from current outputHeightPx
                k = (int)Math.Round((double)outputHeightPx / rh);
                if (k < 1) k = 1;
            }

            outputWidthPx = k * rw;
            outputHeightPx = k * rh;
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

        private int ComputeKWithDirection(int targetPx, int reducedDim, int currentPx, int currentOutputDim)
        {
            // targetPx: the pixel value the user typed (converted)
            // reducedDim: the reduced ratio dimension (rw or rh)
            // currentPx: the previous pixel value for the changed dimension
            // currentOutputDim: the current output dimension (width or height) before change

            double kDouble = (double)targetPx / reducedDim;
            int k;
            if (targetPx > currentPx) // user increased the value
                k = (int)Math.Ceiling(kDouble);
            else if (targetPx < currentPx) // user decreased the value
                k = (int)Math.Floor(kDouble);
            else // no change, keep current
                k = (int)Math.Round((double)currentOutputDim / reducedDim);
            return Math.Max(1, k);
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

            double imgWidth = PreviewImage.ActualWidth;
            double imgHeight = PreviewImage.ActualHeight;

            double scaleX, scaleY, scale;
            double offsetX = 0, offsetY = 0;

            if (imgWidth > 0 && imgHeight > 0)
            {
                // Image is loaded – use its actual size
                scaleX = imgWidth / sourceWidthPx;
                scaleY = imgHeight / sourceHeightPx;
                scale = Math.Min(scaleX, scaleY);
                // Image is centered, so offset is 0 because the image's top-left is (0,0) in the Canvas
                // (the Canvas contains only the image, so it's at (0,0))
            }
            else
            {
                // No image loaded – fall back to Canvas size and default source dimensions
                double canvasWidth = PreviewCanvas.ActualWidth;
                double canvasHeight = PreviewCanvas.ActualHeight;
                if (canvasWidth <= 0 || canvasHeight <= 0)
                {
                    // If Canvas size is also zero (e.g., not yet rendered), just use scale=1 and no offset
                    scale = 1.0;
                    offsetX = 0;
                    offsetY = 0;
                }
                else
                {
                    scaleX = canvasWidth / sourceWidthPx;
                    scaleY = canvasHeight / sourceHeightPx;
                    scale = Math.Min(scaleX, scaleY);
                    // Center the overlay in the canvas
                    offsetX = (canvasWidth - sourceWidthPx * scale) / 2;
                    offsetY = (canvasHeight - sourceHeightPx * scale) / 2;
                }
            }

            if (scale <= 0 || double.IsInfinity(scale) || double.IsNaN(scale))
                scale = 1.0;

            // Apply scale and offset to the overlay
            CropOverlay.Width = Math.Max(1, outputWidthPx * scale);
            CropOverlay.Height = Math.Max(1, outputHeightPx * scale);
            Canvas.SetLeft(CropOverlay, marginLeftPx * scale + offsetX);
            Canvas.SetTop(CropOverlay, marginTopPx * scale + offsetY);
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
        private void ClampOutputToSource()
        {
            if (ActionCrop.IsChecked == true)
            {
                if (outputWidthPx > sourceWidthPx) outputWidthPx = sourceWidthPx;
                if (outputHeightPx > sourceHeightPx) outputHeightPx = sourceHeightPx;
                UpdateAllTextBoxes(); // refresh UI
            }
        }
        private void AppendLog(string text)
        {
            LogTextBox.AppendText(text);
            LogTextBox.ScrollToEnd();
        }
    }
}