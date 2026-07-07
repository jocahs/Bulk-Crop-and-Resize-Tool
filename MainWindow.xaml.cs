using Microsoft.Win32;
using System.Windows;

namespace ImageCropTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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

        private void NoOverwriteChk_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void ModePrefix_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}