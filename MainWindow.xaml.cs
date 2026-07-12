using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        }


        private void Action_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ActionResize.IsChecked == true)
            {
                // Set output size equal to source (100%)
                outputWidthPx = sourceWidthPx;
                outputHeightPx = sourceHeightPx;
                // Margins are irrelevant in resize mode; set to 0
                marginLeftPx = 0;
                marginTopPx = 0;

                // Switch to Percentage unit – this will fire Unit_CheckedChanged and update all boxes
                UnitPer.IsChecked = true;
            }
            // If Crop is selected, we do not change the pixel values here.
            // UnitPer will be disabled by UpdateUnitAvailability().
            UpdateUnitAvailability();
            UpdateFilenameLayout();
        }

        private void Unit_CheckedChanged(object sender, RoutedEventArgs e)
        {
            string unit = GetCurrentUnit();
            Resources["UnitText"] = unit == "px" ? "px" : unit == "mm" ? "mm" : "%";
            UpdateAllTextBoxes();
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
            AspectRatio.IsChecked = isResize;
            MarginsSettings.IsEnabled = !isResize;

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
            if (string.IsNullOrEmpty(path) || path == "Write/Paste/Browse the source path of a file or folder ------------->")
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

            if (System.IO.File.Exists(filePath))
            {
                LogTextBox.AppendText($"Selected file: {System.IO.Path.GetFileName(filePath)}\n");
            }
            else if (System.IO.Directory.Exists(filePath))
            {
                LogTextBox.AppendText($"Selected folder: {filePath}\n");
            }
        }
        private void SetOutputPath(string filePath)
        {
            DstBox.Text = filePath;

            DstBox.FlowDirection = FlowDirection.RightToLeft;
            DstBox.TextAlignment = TextAlignment.Right;
            DstBox.HorizontalContentAlignment = HorizontalAlignment.Right;

           if (System.IO.Directory.Exists(filePath))
            {
                LogTextBox.AppendText($"Output folder: {filePath}\n");
            }
        }
        private void SrcBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string currentText = SrcBox.Text;

            // Check if it's the default placeholder or empty
            if (string.IsNullOrEmpty(currentText) || currentText == "Write/Paste/Browse the source path of a file or folder ------------->")
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
            if (SrcBox.Text == "Write/Paste/Browse the source path of a file or folder ------------->")
            {
                SrcBox.Text = "";
                SrcBox.FlowDirection = FlowDirection.LeftToRight;
                SrcBox.TextAlignment = TextAlignment.Left;
                SrcBox.HorizontalContentAlignment = HorizontalAlignment.Left;
            }
        }

        private void SrcBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SrcBox.Text))
            {
                SrcBox.Text = "Write/Paste/Browse the source path of a file or folder ------------->";
                SrcBox.FlowDirection = FlowDirection.LeftToRight;
                SrcBox.TextAlignment = TextAlignment.Left;
                SrcBox.HorizontalContentAlignment = HorizontalAlignment.Left;
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
        private int sourceWidthPx = 2000;
        private int sourceHeightPx = 1000;
        private int outputWidthPx = 1024;
        private int outputHeightPx = 768;
        private int marginLeftPx = 0;
        private int marginTopPx = 0;

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

        private int ConvertUnitToPixels(double value, string unit)
        {
            if (unit == "px") return (int)Math.Round(value);
            if (unit == "mm") return (int)Math.Round(value / MmPerInch * Dpi);
            return (int)value;
        }

        private string FormatValue(double value)
        {
            return ((int)Math.Round(value)).ToString();
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

                    double percentW = (double)outputWidthPx / sourceWidthPx * 100;
                    double percentH = (double)outputHeightPx / sourceHeightPx * 100;
                    // Round to nearest integer
                    WidthBox.Value = (int)Math.Round(percentW);
                    HeightBox.Value = (int)Math.Round(percentH);
                    // Margins are always 0 in percentage mode
                    MarginLeftBox.Value = 0;
                    MargintopBox.Value = 0;
                }
                else
                {
                    // Convert from stored pixel values to the current unit, then round to integer
                    int srcW = (int)Math.Round(ConvertPixelsToUnit(sourceWidthPx, unit));
                    int srcH = (int)Math.Round(ConvertPixelsToUnit(sourceHeightPx, unit));
                    int outW = (int)Math.Round(ConvertPixelsToUnit(outputWidthPx, unit));
                    int outH = (int)Math.Round(ConvertPixelsToUnit(outputHeightPx, unit));
                    int mL = (int)Math.Round(ConvertPixelsToUnit(marginLeftPx, unit));
                    int mT = (int)Math.Round(ConvertPixelsToUnit(marginTopPx, unit));

                    WidthSourceBox.Text = srcW.ToString();
                    HeightSourceBox.Text = srcH.ToString();
                    WidthBox.Value = outW;
                    HeightBox.Value = outH;
                    MarginLeftBox.Value = mL;
                    MargintopBox.Value = mT;
                }
            }
            finally
            {
                _updatingUI = false;
            }
        }

        private void WidthBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updatingUI || !IsLoaded) return;
            UpdatePixelFromBox(WidthBox, ref outputWidthPx, sourceWidthPx);
        }

        private void HeightBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updatingUI || !IsLoaded) return;
            UpdatePixelFromBox(HeightBox, ref outputHeightPx, sourceHeightPx);
        }

        private void MarginLeftBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updatingUI || !IsLoaded) return;
            UpdatePixelFromBox(MarginLeftBox, ref marginLeftPx, sourceWidthPx);
        }

        private void MargintopBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updatingUI || !IsLoaded) return;
            UpdatePixelFromBox(MargintopBox, ref marginTopPx, sourceHeightPx);
        }

        private void UpdatePixelFromBox(IntegerUpDown box, ref int pixelField, int sourceDimensionPx)
        {
            string unit = GetCurrentUnit();
            double displayValue = box.Value ?? 0; // IntegerUpDown.Value is double?

            if (unit == "%")
            {
                // Displayed value is a percentage of the source dimension
                double percent = displayValue;
                pixelField = (int)Math.Round(percent / 100.0 * sourceDimensionPx);
            }
            else
            {
                // Pixel or Millimeter – convert to pixels
                pixelField = ConvertUnitToPixels(displayValue, unit);
            }

            // After updating the field, refresh all text boxes to show the new value
            // in the current unit (avoids rounding inconsistencies)
            UpdateAllTextBoxes();
        }

        private bool _updatingUI = false;


    }
}