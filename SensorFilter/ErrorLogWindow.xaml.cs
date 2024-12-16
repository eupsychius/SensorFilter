using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;

namespace SensorFilter
{
    /// <summary>
    /// Логика взаимодействия для ErrorLogWindow.xaml
    /// </summary>
    public partial class ErrorLogWindow : Window
    {
        private void CloseLog(object sender, RoutedEventArgs e) => Close();

        public ErrorLogWindow(ObservableCollection<ErrorLogEntry> logEntries)
        {
            InitializeComponent();
            DataContext = logEntries;
            Closing += ErrorLogWindow_Closing;
        }

        private void ErrorLogWindow_Closing(object sender, CancelEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                 "Несохраненные данные могут быть утеряны\n" +
                 "Вы уверены, что хотите зыкрыть окно?",
                 "Внимание",
                 MessageBoxButton.YesNo,
                 MessageBoxImage.Warning);
            if (result == MessageBoxResult.No)
                e.Cancel = true;
        }

        private void SaveLog(object sender, RoutedEventArgs e)
        {
            // Формируем имя файла с текущей датой и временем
            string timeStamp        = DateTime.Now.ToString("dd_MM_yyyy_HH_mm");
            string defaultFileName  = $"sync_log_{timeStamp}.csv";

            // Открываем диалог для выбора пути и имени файла
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName    = defaultFileName,          // Имя по умолчанию
                DefaultExt  = ".csv",                   // Расширение по умолчанию
                Filter      = "CSV files (*.csv)|*.csv" // Фильтр файлов
            };

            // Если пользователь выбрал файл
            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;

                // Формируем данные в формате CSV
                var csvLines = new List<string> { "Дата;Файл;Критичность;Ошибка" }; // Заголовок CSV
                foreach (var entry in ErrorLogger.LogEntries)
                {
                    // Экранируем запятые и двойные кавычки
                    string date         = entry.Date.           Replace("\"", "\"\"");
                    string fileName     = entry.FileName.       Replace("\"", "\"\"");
                    string criticality  = entry.Criticality.    Replace("\"", "\"\"");
                    string errorMessage = entry.ErrorMessage.   Replace("\"", "\"\"");

                    // Добавляем строку
                    csvLines.Add($"\"{date}\";\"{fileName}\";\"{criticality}\";\"{errorMessage}\"");
                }

                // Записываем строки в файл
                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);

                MessageBox.Show(
                    "Лог успешно сохранен",
                    "Информация", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            string approximatePath = Properties.DataBase.Default.PendingDataPath;
            string fileName = Path.GetFileName(e.Uri.ToString()); // Извлекаем имя файла из ссылки

            OpenFileInExplorer(approximatePath, fileName);

            e.Handled = true;
        }

        private static void OpenFileInExplorer(string approximatePath, string fileName)
        {
            try
            {
                // Проверяем, существует ли каталог
                if (!Directory.Exists(approximatePath))
                {
                    MessageBox.Show(
                        $"Каталог \"{approximatePath}\" не существует.", 
                        "Ошибка", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                    return;
                }

                // Ищем файл в указанной директории и ее подкаталогах
                string[] files = Directory.GetFiles(approximatePath, fileName, SearchOption.AllDirectories);

                if (files.Length > 0)
                {
                    // Берем первый найденный файл (если их несколько)
                    string fullPath = files[0];

                    // Открываем Проводник и выделяем файл
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer",
                        Arguments = $"/select,\"{fullPath}\"",
                        UseShellExecute = true
                    });
                }
                else
                    MessageBox.Show(
                        $"Файл \"{fileName}\" не найден в каталоге \"{approximatePath}\" и его подкаталогах.", 
                        "Файл не найден", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
            }
            catch (UnauthorizedAccessException ex) { MessageBox.Show(
                $"Нет доступа к одному из подкаталогов: {ex.Message}", 
                "Ошибка доступа", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error); }
            catch (Exception ex) { MessageBox.Show(
                $"Произошла ошибка при поиске файла: {ex.Message}", 
                "Ошибка", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error); }
        }
    }
}
