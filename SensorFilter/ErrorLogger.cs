using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SensorFilter
{
    public static class ErrorLogger
    {
        private static ErrorLogWindow _logWindow;
        private static ObservableCollection<ErrorLogEntry> _logEntries = new();
        public  static ObservableCollection<ErrorLogEntry> LogEntries => _logEntries;

        public static void Clear()
        {
            _logEntries.Clear();
        }

        public static void LogError(string fileName, string criticality, string errorMessage)
        {
            var newEntry = new ErrorLogEntry
            {
                Date                = DateTime.Now.ToString(),
                FileName            = fileName,
                Criticality         = criticality,
                ErrorMessage        = errorMessage
            };

            Application.Current.Dispatcher.Invoke(() => _logEntries.Add(newEntry));

            if (_logWindow == null)
            {
                _logWindow = new ErrorLogWindow(_logEntries);
                _logWindow.Show();
                _logWindow.Closed += (s, e) => _logWindow = null;

                // Инициализируем позицию сразу после открытия
                var settingsWindow = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
                if (settingsWindow != null)
                {
                    settingsWindow.InitializeErrorLogWindowPosition();
                }
            }
        }

        public static async Task LogErrorAsync(string fileName, string criticality, string errorMessage)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var newEntry = new ErrorLogEntry
                {
                    Date                = DateTime.Now.ToString(),
                    FileName            = fileName,
                    Criticality         = criticality,
                    ErrorMessage        = errorMessage
                };

                // Добавляем ошибку в лог
                _logEntries.Add(newEntry);

                // Открываем окно на первой ошибке
                if (_logWindow == null)
                {
                    _logWindow = new ErrorLogWindow(_logEntries);
                    _logWindow.Show();
                    _logWindow.Closed += (s, e) => _logWindow = null;

                    // Инициализируем позицию сразу после открытия
                    var settingsWindow = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
                    if (settingsWindow != null)
                    {
                        settingsWindow.InitializeErrorLogWindowPosition();
                    }
                }
            });
        }

        // Возвращает текущее окно ошибок
        public static ErrorLogWindow GetLogWindow()
        {
            return _logWindow;
        }

    }

    // Log Entry Model
    public class ErrorLogEntry
    {
        public string Date          { get; set; }
        public string FileName      { get; set; }
        public string Criticality   { get; set; }
        public string ErrorMessage  { get; set; }
    }

}
