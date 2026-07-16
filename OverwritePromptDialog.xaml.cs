using System.Windows;

namespace ImageCropTool
{
    public enum OverwriteAction
    {
        Overwrite,
        Skip,
        OverwriteAll,
        SkipAll
    }

    public partial class OverwritePromptDialog : Window
    {
        public OverwriteAction Result { get; private set; }

        public OverwritePromptDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void OverwriteBtn_Click(object sender, RoutedEventArgs e)
        {
            Result = OverwriteAction.Overwrite;
            DialogResult = true;
        }

        private void SkipBtn_Click(object sender, RoutedEventArgs e)
        {
            Result = OverwriteAction.Skip;
            DialogResult = true;
        }

        private void OverwriteAllBtn_Click(object sender, RoutedEventArgs e)
        {
            Result = OverwriteAction.OverwriteAll;
            DialogResult = true;
        }

        private void SkipAllBtn_Click(object sender, RoutedEventArgs e)
        {
            Result = OverwriteAction.SkipAll;
            DialogResult = true;
        }
    }
}