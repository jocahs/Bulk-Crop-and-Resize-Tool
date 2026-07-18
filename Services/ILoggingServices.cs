using System;
using System.Windows.Controls;

namespace BulkCropAndResizeTool.Services
{
    public interface ILoggingServices
    {
        void Log(string message);
        void Clear();
    }

    // Primary constructor - parameters become capture variables
    public class LoggingService(TextBox logTextBox, Action<string> appendAction) : ILoggingServices
    {
        public void Log(string message)
        {
            appendAction?.Invoke(message);

            if (logTextBox != null)
            {
                if (logTextBox.Dispatcher.CheckAccess())
                    logTextBox.ScrollToEnd();
                else
                    logTextBox.Dispatcher.Invoke(() => logTextBox.ScrollToEnd());
            }
        }

        public void Clear()
        {
            if (logTextBox != null)
            {
                if (logTextBox.Dispatcher.CheckAccess())
                    logTextBox.Clear();
                else
                    logTextBox.Dispatcher.Invoke(() => logTextBox.Clear());
            }
        }
    }
}