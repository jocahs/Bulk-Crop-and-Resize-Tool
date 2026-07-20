using System.Windows;

namespace BulkCropAndResizeTool.Helpers
{
    public static class UIHelpers
    {
        public static MessageBoxResult ShowMessage(string messageBoxText, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            var app = Application.Current;
            if (app == null)
            {
                // No application context; fall back to returning default
                return defaultResult;
            }

        public static void OpenFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;

            var app = Application.Current;
            if (app != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.Invoke(() => OpenFolder(folderPath));
                return;
            }

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true,
                    Verb = "open"
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch
            {
                // best-effort; swallow exceptions and return
            }
        }

            if (app.Dispatcher.CheckAccess())
            {
                return MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
            }

            return (MessageBoxResult)app.Dispatcher.Invoke(() => MessageBox.Show(messageBoxText, caption, button, icon, defaultResult));
        }
    }
}
