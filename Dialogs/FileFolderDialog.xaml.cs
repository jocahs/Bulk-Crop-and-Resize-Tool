using System.Windows;

namespace BulkCropAndResizeTool.Dialogs
{
    public partial class FileFolderDialog : Window
    {
        public bool IsFileSelected { get; private set; }

        public FileFolderDialog()
        {
            InitializeComponent();
        }

        private void FileButton_Click(object sender, RoutedEventArgs e)
        {
            IsFileSelected = true;
            DialogResult = true;
            Close();
        }

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            IsFileSelected = false;
            DialogResult = true;
            Close();
        }
    }
}