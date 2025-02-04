﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using MessageBox        = System.Windows.MessageBox;
using OpenFileDialog    = Microsoft.Win32.OpenFileDialog;

namespace SensorFilter
{
    /// <summary>
    /// Логика взаимодействия для SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        // Инстансы классов
        private readonly DatabaseHelper  databaseHelper  = new();
        private readonly FileProcessor   fileProcessor   = new();

        private CancellationTokenSource _cancellationTokenSource;   // Токен для отмены продолжительных действий
        private bool taskInProgress = false;

        // Переменная для хранения ссылки на окно ошибок
        private static ErrorLogWindow LogWindow => ErrorLogger.GetLogWindow();

        // Временно не используется
        //bool unsavedChanges = false;

        public SettingsWindow(bool adminRights)
        {
            InitializeComponent();

            Loaded  += SettingsWindow_Loaded;
            Closing += SettingsWindow_Closing;

            // Если есть права админа
            if (adminRights)
            {
                AdminGrBox.Visibility   = Visibility.Visible;   // Показываем доп. настройки
                ArchivePathText.Text    = GetDataPath();        // Показываем путь до архива
                UpdateTooltips();                               // Выводим подсказки для кнопок
            }
            else
                AdminGrBox.Visibility   = Visibility.Collapsed;
        }

        // Получаем путь до ДБ из конфига
        private string GetDbPath()
        {
            string path = Properties.DataBase.Default.DataBasePath;

            // Убедиться, что dbPath не пуст
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show(
                    "Путь до базы данных не задан. Укажите путь в настройках",
                    "Внимание", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                ScanSyncStackPanel.IsEnabled = false;
                return null;
            }
            return path;
        }

        public void InitializeErrorLogWindowPosition()
        {
            if (LogWindow != null)
            {
                LogWindow.Left = Left + Width + 10; // смещение справа от окна + 10 пикселей = координаты левой границы + ширина окна + 10
                LogWindow.Top  = Top;               // смещение по верху
            }
        }

        // Получаем путь до дб_инфо
        private string GetDbInfoPath()
        {
            string dbPath = GetDbPath();

            // Убедиться, что dbPath не равен нулю
            if (string.IsNullOrEmpty(dbPath)) return null;

            // Проверка на возможность получения каталога
            string directory = Path.GetDirectoryName(dbPath);
            if (string.IsNullOrEmpty(directory)) return null;

            // Возвращает полный путь до db_info.txt
            return Path.Combine(directory, "db_info.txt");
        }

        // Получаем путь до архива записей
        private string GetDataPath()
        {
            string path = Properties.DataBase.Default.PendingDataPath;
            return path;
        }

        // Апдейтим подсказки админских кнопок скана и добавления
        private void UpdateTooltips()
        {
            // Проверяем на существование ДБ по указанному в конфиге пути
            if (File.Exists(GetDbInfoPath()))
            {
                var dbInfo = ParseDbInfoFile(GetDbInfoPath());

                // Проверяем, удалось ли прочитать даты
                string lastSyncDate = dbInfo.ContainsKey("LastSyncDate") ? dbInfo["LastSyncDate"] : "Неизвестно";

                // Проверка на последнее сканирование
                if (!(lastSyncDate == "Неизвестно"))
                {
                    // Сравниваем дату последнего сканирования с практически невозможной датой последнего сканирования
                    // Если дата скана раньше невозможной даты, то сканирование для БД никогда не производилось
                    if (DateTime.Compare(Convert.ToDateTime(lastSyncDate), new DateTime(2024, 1, 1, 0, 0, 0)) < 0)
                        lastSyncDate = "Никогда";
                }

                ScanDirectoryButton.ToolTip = $"Дата последней синхронизации: {lastSyncDate}";
            }
        }

        // Доп. свойства закрытия окна программы
        private async void SettingsWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            /* Есть несохраненные изменения
            if (unsavedChanges)
            {
                // Показать сообщение пользователю с выбором
                MessageBoxResult result = MessageBox.Show(
                    "У вас есть несохраненные изменения. Вы уверены, что хотите выйти без сохранения?",
                    "Внимание",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                // Обработка выбора пользователя
                if      (result == MessageBoxResult.No)
                    e.Cancel = true;
                else if (result == MessageBoxResult.Cancel)
                    e.Cancel = true;
            }*/

            // Если зафризили окно
            if (taskInProgress)
            {
                MessageBox.Show(
                    "Дождитесь завершения операции",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                e.Cancel = true;
            }
            else
            {
                // Ресетаем поля в главном окне программы
                if (Owner is MainWindow mainWindow)
                {
                    if (GetDbPath().Contains(".db") && File.Exists(GetDbPath()))
                        mainWindow.SearchTools.IsEnabled = true;
                    else
                        mainWindow.SearchTools.IsEnabled = true;
                    mainWindow.ResetFields();
                }
                LogWindow?.Close();
            }
        }

        // Доп. действия происходящие после окончательной инициализации окна
        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e) => CheckDatabase();

        // Валидация пути до ДБ
        private void CheckDatabase()
        {
            if (GetDbPath().Contains(".db") && File.Exists(GetDbPath()))
            {
                DbPathText.Text = GetDbPath();
                if (!File.Exists(GetDbInfoPath()))
                {
                    // Создаем новый конфиг дб_инфо, если он по какой-то причине отсутствует
                    File.WriteAllText(GetDbInfoPath(), "LastScanDate=01.01.2000\nLastSyncDate=01.01.2000");
                    MessageBox.Show(
                        "Файл db_info.txt не найден, создан новый файл с значениями по умолчанию.",
                        "Информация",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            else if (GetDbPath().Contains(".db"))
            {
                DbPathText.Text = GetDbPath();
                ScanSyncStackPanel.IsEnabled = false;
                MessageBox.Show(
                    "База данных по указанному пути отсутствует или недоступна. " +
                    "Проверьте правильность указанного пути",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                DbPathText.Text = "Не задано";
                ScanSyncStackPanel.IsEnabled = false;
            }
        }

        // Чтение дат из файла дбинфо
        private Dictionary<string, string> ParseDbInfoFile(string filePath)
        {
            var infoData = new Dictionary<string, string>();

            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("LastScanDate"))
                    {
                        // Извлекаем дату последнего сканирования
                        string lastScanDate = line.Split('=')[1].Trim();
                        infoData["LastScanDate"] = lastScanDate;
                    }
                    else if (line.StartsWith("LastSyncDate"))
                    {
                        // Извлекаем дату последней синхронизации
                        string lastSyncDate = line.Split('=')[1].Trim();
                        infoData["LastSyncDate"] = lastSyncDate;
                    }
                }
            }
            return infoData;
        }

        // Кнопка выбора ДБ
        private void SelectDbButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем диалоговое окно выбора ДБ
            OpenFileDialog openFileDialog = new() { Filter = "Database files (*.db)|*.db" };
            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;
                DbPathText.Text = selectedPath;

                // Обновляем путь до дбинфо
                string newDbInfoPath = Path.Combine(Path.GetDirectoryName(selectedPath), "db_info.txt");
                if (!File.Exists(newDbInfoPath))
                {
                    // Пишем новый дбинфо, если не был найден
                    File.WriteAllText(newDbInfoPath, "LastScanDate=01.01.2000\nLastSyncDate=01.01.2000");
                    MessageBox.Show(
                        "Файл db_info.txt не найден, создан новый файл с значениями по умолчанию.", 
                        "Информация", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
                // Сохраняем новые пути в настройках и локальных переменных
                Properties.DataBase.Default.DataBasePath = selectedPath;
                Properties.DataBase.Default.Save();

                // Включаем скан и добавление если блокировали
                ScanSyncStackPanel.IsEnabled = true;
                UpdateTooltips();
            }
        }

        // Кнопка выбора пути до архива
        private void SelectArchiveButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем диалоговое окно выбора пути до архива
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                DialogResult result = folderBrowserDialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    Properties.DataBase.Default.PendingDataPath = folderBrowserDialog.SelectedPath;
                    Properties.DataBase.Default.Save();
                    ArchivePathText.Text = GetDataPath();
                }
            }
        }

        // Кнопка сохранения изменений в настройках
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // На данный момент такой же крестик по сути
            MessageBox.Show(
                "Изменения успешно сохранены", 
                "Информация", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
            Close();
        }

        // Кнопка создания ДБ
        private void CreateDbButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем диалоговое окно выбора пути до архива
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                DialogResult result = folderBrowserDialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    string selectedPath = folderBrowserDialog.SelectedPath;

                    // Задаём путь для новой базы данных и файла db_info.txt
                    string crDbPath     = Path.Combine(selectedPath, "sensor_data.db");
                    string crDbInfoPath = Path.Combine(selectedPath, "db_info.txt");
                    DbPathText.Text = crDbPath;

                    // Создание базы данных
                    bool dbExists = databaseHelper.CreateDatabaseIfNotExists(crDbPath);
                    if (!dbExists)
                    {
                        // Создание db_info
                        File.WriteAllText(crDbInfoPath, "LastScanDate=01.01.2000\nLastSyncDate=01.01.2000");

                        MessageBox.Show(
                            "База данных успешно создана",
                            "Информация",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        Properties.DataBase.Default.DataBasePath = crDbPath;
                        Properties.DataBase.Default.Save();
                    }
                    else
                    {
                        Properties.DataBase.Default.DataBasePath = crDbPath;
                        Properties.DataBase.Default.Save();
                    }
                }
            }
            ScanSyncStackPanel.IsEnabled = true;
            UpdateTooltips();
        }

        // Кнопка скана архива
        private async void ScanDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем токен на отмену скана и записи, если необходимо
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                // Получаем дату синхронизации
                DateTime lastSyncDate = LoadLastSyncDate();

                // Если сканим корень
                if (GetDataPath().EndsWith(":\\"))
                {
                    MessageBoxResult result = MessageBox.Show(
                        "Задан корневой каталог диска для сканирования.\n" +
                        "Вы уверены, что хотите продолжить?",
                        "Внимание",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                        return;
                }

                // Получаем ДБ
                string dataPath = GetDataPath();

                // Ловим отсутствие ДБ
                if (!Directory.Exists(dataPath))
                {
                    MessageBox.Show(
                        "Каталог не существует или недоступен.", 
                        "Ошибка", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                    return;
                }

                // Показываем кнопки остановки
                ScanDirectoryButton.Visibility  = Visibility.Hidden;
                StopScanButton.     Visibility  = Visibility.Visible;
                AddFilesButton.     IsEnabled   = false;
                ToggleElements(false);

                // Воспроизводим скан, ловим отмену и получаем список файлов на загрузку
                var (wasScanCancelled, newFilesFound) = await ScanDirectory(dataPath, lastSyncDate, token);
                
                // Словили отмену скана
                if (wasScanCancelled)
                {
                    MessageBox.Show(
                        "Операция сканирования была остановлена.",
                        "Информация", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                    IsEnabled       = true;
                    taskInProgress  = false;
                    ToggleElements(true);
                    return;
                }

                // Обработка результатов сканирования
                else
                {
                    // Файлов нет
                    if (newFilesFound == null || !newFilesFound.Any())
                    {
                        MessageBox.Show(
                            "Новых файлов не обнаружено",
                            "Информация",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        SaveLastScanDate(DateTime.Now);
                        taskInProgress = false;
                        ToggleElements(true);
                    }
                    // Файлы есть
                    else
                    {

                        string found    = "Обнаружено";
                        string file     = "файлов";
                        if (newFilesFound.Count() % 10  ==  1   &&
                            newFilesFound.Count() % 100 !=  11  )  { found = "Обнаружен"; file = "файл"; }  //  1;      21;     31
                        if (newFilesFound.Count() % 10  >=  2   &&
                            newFilesFound.Count() % 10  <=  4   && (
                            newFilesFound.Count() % 100 <   10  ||
                            newFilesFound.Count() % 100 >=  20  )) file = "файла";                          //  2-4;    22-24;  32-34

                            MessageBoxResult result = MessageBox.Show(
                            $"{found} {newFilesFound.Count()} {file} для синхронизации\n" +
                            "Синхронизировать данные?",
                            "Результаты сканирования",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        // Не хотим добавлять - конец метода
                        if (result == MessageBoxResult.No)
                        {
                            taskInProgress = false;
                            ToggleElements(true);
                            return;
                        }

                        ToggleElements(false);

                        // Очищаем лог ошибок
                        ErrorLogger.Clear();
                        
                        // Добавляем
                        await SynchronizeFiles(newFilesFound, token);
                    }
                }
            }
            // Словили ошибку
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        ex.Message, 
                        "Ошибка", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                    taskInProgress = false;
                    ToggleElements(true);
                });
            }
            // По окончанию работы возвраащем кнопки
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    ScanDirectoryButton.Visibility  = Visibility.Visible;
                    StopScanButton.Visibility       = Visibility.Hidden;
                    AddFilesButton.IsEnabled        = true;
                    taskInProgress                  = false;
                    ToggleElements(true);
                });
            }
        }

        // Синхронизация архива и ДБ
        private async Task SynchronizeFiles(IEnumerable<FileInfo> newFiles, CancellationToken token)
        {
            // Получаем список на синхронизацию
            int filesWritten    = 0;
            int filesPassed     = 0;
            int filesTotal      = newFiles.Count();

            // Показываем прогрессбар
            Dispatcher.Invoke(() =>
            {
                SyncProgress.       Visibility = Visibility.Visible;
                ScanSyncStackPanel. Visibility = Visibility.Hidden;
            });

            // Считаем дубликаты
            (bool, bool, bool) skippedRows = (false, false, false);
            int skippedCharacterisation = 0,
                skippedVerification     = 0;

            bool fullSync = true;

            // Начало синхронизации файлов
            await Task.Run(() =>
            {
                foreach (var fileInfo in newFiles)
                {
                    // Проверка отмены задачи
                    if (token.IsCancellationRequested)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                "Операция синхронизации была остановлена",
                                "Информация",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            IsEnabled = true;
                        });
                        fullSync = false;
                        break;
                    }

                    // Парсим файл из списка
                    try
                    {
                        // При парсинге ловим возврат по дубликатам файлов
                        skippedRows = ParseAndInsertFileData(fileInfo.FullName, false);
                        if (    skippedRows.Item1) skippedCharacterisation++;
                        if (    skippedRows.Item2) skippedVerification++;
                        if (!   skippedRows.Item3) filesWritten++;
                        
                        // Обновляем прогрессбар
                        Dispatcher.Invoke(() =>
                        {
                            SyncProgressBar.Value = (double)filesPassed / filesTotal * 100;
                            ProgressText.   Text = $"{filesPassed}/{filesTotal}";
                        });
                    }
                    // Ловим исключение-отмену
                    catch (OperationCanceledException)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                "Операция была отменена.",
                                "Информация",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        });

                        ToggleElements(true);

                        fullSync = false;
                    }
                    // Ловим остальные исключения
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => { ErrorLogger.LogErrorAsync(fileInfo.Name, "ОШИБКА", ex.Message); });

                        fullSync = false;
                    }

                    filesPassed++;
                }
            }, token);

            // Показываем кол-во пропущенных дубликатов
            if (skippedCharacterisation != 0 || skippedVerification != 0)
            {
                string file = "файлов";
                if (filesWritten % 10 == 1 &&
                filesWritten % 100 != 11) file = "файла"; // 1; 21; 31

                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine(
                    $"Успешно занесены сведения из {filesWritten} {file}\n" +
                    $"Предотвращено занесение файлов:");

                if (skippedCharacterisation != 0)
                    messageBuilder.AppendLine($"Файлы характеризации:\t{skippedCharacterisation}");

                if (skippedVerification != 0)
                    messageBuilder.AppendLine($"Файлы верификации:\t{skippedVerification}");

                MessageBox.Show(
                    messageBuilder.ToString(),
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
                
            else
            {
                string file = "файлов";
                if (filesWritten % 10 == 1 &&
                filesWritten % 100 != 11) file = "файла"; // 1; 21; 31

                MessageBox.Show(
                    $"Занесены сведения из {filesWritten} {file}",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
                
            // Сохраняем дату синхронизации
            if (fullSync) SaveLastSyncDate(DateTime.Now);
            UpdateTooltips();

            // Возвращаем кнопки
            SyncProgress.       Visibility = Visibility.Hidden;
            ScanSyncStackPanel. Visibility = Visibility.Visible;
            ToggleElements(true);
        }

        // Кнопка ручного добавления в ДБ
        private void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Открываем проводник, указываем на файл
                OpenFileDialog openFileDialog = new()
                {
                    InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pending Data"),
                    Filter = "Text files (*.txt)|*.txt"
                };

                if (openFileDialog.ShowDialog() == true)
                    // Заносим данные в таблицу
                    ParseAndInsertFileData(openFileDialog.FileName, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message, 
                    "Ошибка", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        // Универсальный метод занесения данных из файла в ДБ
        private (bool, bool, bool) ParseAndInsertFileData(string filePath, bool needsOutput)
        {
            // Переменные для подсчета дубликатов строк
            bool skippedCharacterisation    = false;
            bool skippedVerification        = false;
            bool skippedFile                = false;

            // Читаем содержимое файла
            var lines = File.ReadAllLines(filePath);

            // Определяем тип файла по его названию
            string fileName = Path.GetFileName(filePath);

            // Читаем первую строку и на этой основе подбираем нужный парсинг
            if (fileName.StartsWith("CH_FN_"))      // Если это файл характеризации
            {
                skippedCharacterisation = fileProcessor.ParseCharacterisationData(lines, GetDbPath(), fileName, filePath);
                skippedFile = skippedCharacterisation;
            }
            else if (fileName.StartsWith("VR_FN_")) // Если это файл верификации
            {
                skippedVerification = fileProcessor.ParseVerificationData(lines, GetDbPath(), fileName, filePath);
                skippedFile = skippedVerification;
            }

            // Если надо, докладываем о занесении
            if (
                needsOutput
                && !skippedCharacterisation
                && !skippedVerification)
                MessageBox.Show(
                    "Данные успешно добавлены в базу данных",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            else if (
                needsOutput
                && (skippedCharacterisation
                ||  skippedVerification))
                MessageBox.Show(
                    "Файл содержит дублирующиеся сведения\n" +
                    "Операция была отменена",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

            // Возвращаем пропущенные дубликаты строк
            return (skippedCharacterisation, skippedVerification, skippedFile);
        }

        // Метод сканирования указанной директории на наличие файлов характеризации/верификации
        private async Task<(bool wasScanCanceled, IEnumerable<FileInfo> newFilesFound)> 
            ScanDirectory(string dataPath, DateTime lastSyncDate, CancellationToken token)
        {
            taskInProgress = true;
            return await Task.Run(() =>
            {
                var validFiles = new List<FileInfo>();  // Перечисляем уместные файлы
                
                try
                {
                    // Получаем все тхт из директории
                    var files = Directory.GetFiles(dataPath, "*.txt", SearchOption.AllDirectories);

                    for (int i = 0; i < files.Length; i++)
                    {
                        // Если запрошена отмена - прерываем всё
                        if (token.IsCancellationRequested) return (wasScanCancelled: true, newFilesFound: new List<FileInfo>());
                        try
                        {
                            // Открываем и смотрим сведения о файле из списка файлов
                            var fileInfo = new FileInfo(files[i]);
                            if (fileInfo.CreationTime <= lastSyncDate) continue;

                            // Читаем первую строку и отсеиваем невалидные тхт
                            var fileName = fileInfo.Name;
                            if( fileName.StartsWith("CH_FN_") ||
                                fileName.StartsWith("VR_FN_"))
                                validFiles.Add(fileInfo);
                        }
                        catch (UnauthorizedAccessException ex)  { Console.WriteLine($"Ошибка/Доступ: {ex.Message}"); }
                        catch (Exception ex)                    { Console.WriteLine($"Ошибка: {ex.Message}");        }
                    }
                }
                catch
                {
                    MessageBox.Show(
                        "Выполнить сканирование по указанному пути невозможно",
                        "Ошибка", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
                return (wasScanCancelled: false, newFilesFound: validFiles);
            });
        }

        // Метод загрузки даты синхронизации
        private DateTime LoadLastSyncDate()
        {
            // Получаем дбинфо, если он существует
            if (File.Exists(GetDbInfoPath()))
            {
                var lines = File.ReadAllLines(GetDbInfoPath());
                foreach (var line in lines)
                    // Получаем строку синхронизации
                    if (line.StartsWith("LastSyncDate=") && 
                        DateTime.TryParse(line.Split('=')[1], out DateTime lastSyncDate)) 
                        return lastSyncDate;
            }
            return DateTime.MinValue;
        }

        // Метод сохранения даты синхронизации
        private void SaveLastSyncDate(DateTime date)
        {
            // Получаем дбинфо, если он существует
            if (File.Exists(GetDbInfoPath()))
            {
                // Ищем и апдейтим строку синхронизации
                var lines       = File.ReadAllLines(GetDbInfoPath());
                bool updated    = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("LastSyncDate="))
                    {
                        lines[i]    = $"LastSyncDate={date}";
                        updated     = true;
                    }
                }
                if (!updated)
                {
                    var newLines    = lines.ToList();
                    newLines.Add($"LastSyncDate={date}");
                    lines           = newLines.ToArray();
                }
                File.WriteAllLines(GetDbInfoPath(), lines);
            }
        }

        // Метод сохранения даты сканирования
        private void SaveLastScanDate(DateTime date)
        {
            // Получаем дбинфо, если он существует
            if (File.Exists(GetDbInfoPath()))
            {
                // Ищем и апдейтим строку сканирования
                var lines       = File.ReadAllLines(GetDbInfoPath());
                bool updated    = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("LastScanDate="))
                    {
                        lines[i]    = $"LastScanDate={date}";
                        updated     = true;
                    }
                }
                if (!updated)
                {
                    var newLines    = lines.ToList();
                    newLines.Add($"LastScanDate={date}");
                    lines           = newLines.ToArray();
                }
                File.WriteAllLines(GetDbInfoPath(), lines);
            }
        }

        // Переключатель для элементов на время скана / синхронизации
        private void ToggleElements(bool enable)
        {
            SelectDbButton.      IsEnabled  = enable;
            CreateDbButton.      IsEnabled  = enable;
            SelectArchiveButton. IsEnabled  = enable;
            SaveButton.          IsEnabled  = enable;
        }

        // Кнопка остановки синхронизации
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                IsEnabled = false;
            }
        }

        // Кнопка остановки сканирования
        private void StopScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                IsEnabled = false;
            }
        }
    }
}
