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

            if (app.Dispatcher.CheckAccess())
            {
                return MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
            }

            return (MessageBoxResult)app.Dispatcher.Invoke(() => MessageBox.Show(messageBoxText, caption, button, icon, defaultResult));
        }
    }
}
