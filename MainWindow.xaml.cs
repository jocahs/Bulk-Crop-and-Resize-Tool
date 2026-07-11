using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
<<<<<<< HEAD
using System.IO;
=======
>>>>>>> 54142bc566c12e144141ffe1f628899785c8b9ac

namespace ImageCropTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
<<<<<<< HEAD
=======

>>>>>>> 54142bc566c12e144141ffe1f628899785c8b9ac
            // Set initial states
            UpdateUnitAvailability();
            UpdateMarginAvailability();
            UpdateFilenameAvailability();
            UpdateFilenameLayout();
<<<<<<< HEAD
            UpdateSourceFilename();
=======
>>>>>>> 54142bc566c12e144141ffe1f628899785c8b9ac

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
<<<<<<< HEAD
            UnitPer.IsEnabled = false;
            SrcBox.TextChanged += SrcBox_TextChanged;
        }

=======
            NameBox.TextChanged += NameBox_TextChanged;
            UnitPer.IsEnabled = false;
        }

        private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreviewLabel();
        }
>>>>>>> 54142bc566c12e144141ffe1f628899785c8b9ac

        private void Action_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateUnitAvailability();
            UpdateMarginAvailability();
            UpdateFilenameLayout();
        }

        private void Unit_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateUnitAvailability();
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

            // If UnitPer is selected but now disabled, switch to Pixels
            if (!isResize && UnitPer.IsChecked == true)
            {
                UnitPixels.IsChecked = true;
            }
        }

        private void UpdateMarginAvailability()
        {
            if (MarginsSettings != null)
            {
                MarginsSettings.IsEnabled = ActionCrop.IsChecked == true;
            }
        }

        private void UpdateFilenameAvailability()
        {
            // All filename controls should be enabled only when NoOverwriteChk is checked
            bool isEnabled = NoOverwriteChk.IsChecked == true;
<<<<<<< HEAD
          
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
=======

            ModePrefix.IsEnabled = isEnabled;
            ModeSuffix.IsEnabled = isEnabled;
            NameExample.IsEnabled = isEnabled;
            NameBox.IsEnabled = isEnabled;
            UpdatePreviewLabel();
>>>>>>> 54142bc566c12e144141ffe1f628899785c8b9ac
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

<<<<<<< HEAD
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
        private void UpdateSourceFilename()
        {
            string path = SrcBox.Text;

            // If the text is empty or the placeholder, show "filename"
            if (string.IsNullOrEmpty(path) || path == "Write/paste path...")
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
        private void SrcBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string currentText = SrcBox.Text;

            // Check if it's the default placeholder or empty
            if (string.IsNullOrEmpty(currentText) || currentText == "Write/paste path...")
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
            if (SrcBox.Text == "Write/paste path...")
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
                SrcBox.Text = "Write/paste path...";
                SrcBox.FlowDirection = FlowDirection.LeftToRight;
                SrcBox.TextAlignment = TextAlignment.Left;
                SrcBox.HorizontalContentAlignment = HorizontalAlignment.Left;
            }
        }

=======
>>>>>>> 54142bc566c12e144141ffe1f628899785c8b9ac
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
<<<<<<< HEAD
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
=======
            bool isPrefix = ModePrefix.IsChecked == true;
            bool isResize = ActionResize.IsChecked == true;

            // Determine the text based on action and mode
            string defaultText;
            if (isResize)
            {
                defaultText = isPrefix ? "resized_" : "_resized";
            }
            else
            {
                defaultText = isPrefix ? "cropped_" : "_cropped";
            }

            // Check if the current text is a default value
            string currentText = NameBox.Text;
            bool isDefaultText = currentText == "_cropped" || currentText == "_resized" ||
                                 currentText == "cropped_" || currentText == "resized_";

            // If user has custom text, store it
            if (!isDefaultText && !string.IsNullOrEmpty(currentText))
            {
                userCustomText = currentText;
            }

            if (isPrefix)
            {
                // Prefix mode: TextBox on left, Label on right
                NameBox.Text = userCustomText ?? defaultText;
                NameExample.Content = "filename";

                // Set Grid.Column
                Grid.SetColumn(NameExample, 4);
                Grid.SetColumn(NameBox, 2);

                // Set HorizontalAlignment
                NameBox.HorizontalAlignment = HorizontalAlignment.Right;
                NameBox.HorizontalContentAlignment = HorizontalAlignment.Right;
                NameExample.HorizontalAlignment = HorizontalAlignment.Left;
                NameExample.HorizontalContentAlignment = HorizontalAlignment.Left;

                // Set Margins
                NameBox.Margin = new Thickness(0, 4, 2, 0);
                NameExample.Margin = new Thickness(0, 0, 0, 0);
            }
            else // Suffix mode (default)
            {
                // Suffix mode: Label on left, TextBox on right
                NameBox.Text = userCustomText ?? defaultText;
                NameExample.Content = "filename";

                // Set Grid.Column
                Grid.SetColumn(NameExample, 2);
                Grid.SetColumn(NameBox, 4);

                // Set HorizontalAlignment
                NameBox.HorizontalAlignment = HorizontalAlignment.Left;
                NameBox.HorizontalContentAlignment = HorizontalAlignment.Left;
                NameExample.HorizontalAlignment = HorizontalAlignment.Right;
                NameExample.HorizontalContentAlignment = HorizontalAlignment.Right;

                // Set Margins
                NameBox.Margin = new Thickness(2, 4, 0, 0);
                NameExample.Margin = new Thickness(0, 0, 0, 0);
            }

            UpdatePreviewLabel();
        }
        private void UpdatePreviewLabel()
        {
            string namebox = NameBox.Text;
            string nameexample = NameExample.Content?.ToString() ?? "";
            bool overwriteChk = NoOverwriteChk.IsChecked == false;
            bool isPrefix = ModePrefix.IsChecked == true;

            if (overwriteChk)
            {
                PreviewLabel.Text = $"{nameexample}";
            }
            else if (isPrefix)
            {
                PreviewLabel.Text = $"{namebox}{nameexample}";
            }
            else
            {
                PreviewLabel.Text = $"{nameexample}{namebox}";
>>>>>>> 54142bc566c12e144141ffe1f628899785c8b9ac
            }
        }
    }
}