using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ImageCropTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Set initial states
            UpdateUnitAvailability();
            UpdateMarginAvailability();
            UpdateFilenameAvailability();
            UpdateFilenameLayout();

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
            NameBox.TextChanged += NameBox_TextChanged;
            UnitPer.IsEnabled = false;
        }

        private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreviewLabel();
        }

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

            ModePrefix.IsEnabled = isEnabled;
            ModeSuffix.IsEnabled = isEnabled;
            NameExample.IsEnabled = isEnabled;
            NameBox.IsEnabled = isEnabled;
            UpdatePreviewLabel();
        }

        private void SrcBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                CheckFileExists = false,
                ValidateNames = false,
                FileName = " ",
                Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
            };
            if (dialog.ShowDialog() == true)
            {
                SrcBox.Text = dialog.FileName;
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
            }
        }
    }
}