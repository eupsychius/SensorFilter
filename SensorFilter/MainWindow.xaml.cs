using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;

namespace SensorFilter
{
    public partial class MainWindow : Window
    {
        // Настройки
        private bool            adminRightsEnabled  = false;
        private SettingsWindow  settings;
        private PasswordWindow  passwordWindow;
        private CreditsWindow   credits;

        // Инстансы классов
        private DatabaseHelper  databaseHelper      = new DatabaseHelper();

        // Список моделей датчиков
        SensorModels sensorModels = new SensorModels();

        public MainWindow()
        {
            InitializeComponent();

            Loaded     += MainWindow_Loaded;
            Closing    += MainWindow_Closing;
        }

        // Доп. действия при закрытии окна
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Завершаем приложение полностью
            Application.Current.Shutdown();
        }

        // Действия при окончательной загрузке окна программы
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CheckDatabase();
        }

        // Проверяем, есть ли возможность взаимодействовать с ДБ
        private void CheckDatabase()
        {
            // Если путь до ДБ указан
            if (GetDbPath().Contains(".db"))
            {
                if (!File.Exists(GetDbPath()))
                {
                    MessageBox.Show(
                        "База данных по указанному в настройках пути не существует или недоступна",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    SearchTools.IsEnabled = false;
                }
            }
            else
            {
                MessageBox.Show(
                    "База данных не указана. Перейдите в настройки и укажите путь вручную",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SearchTools.IsEnabled = false;
            }
        }

        // [Меню док] Открытие формы настроек
        private void GoToSettingsWindow(object sender, RoutedEventArgs e)
        {
            settings = new SettingsWindow(adminRightsEnabled);

            settings.Left   = Left  + 30;
            settings.Top    = Top   + 30;

            settings.Height = 319;
            if (!adminRightsEnabled) settings.Height = 172;

            settings.Owner = this;
            settings.ShowDialog();
        }
        
        // [Меню док] Функция переключения админки
        private void ToggleAdminRights(object sender, RoutedEventArgs e)
        {
            if (adminRightsEnabled)
            {
                // Отключение прав администратора
                adminRightsEnabled = false;
                AdmRights.IsChecked = false;
            }
            else
            {
                // Включение прав администратора через ввод пароля
                if (passwordWindow == null || !passwordWindow.IsVisible)
                {
                    passwordWindow = new PasswordWindow();
                    passwordWindow.Closed += (s, args) => passwordWindow = null; // Освобождаем объект при закрытии
                    bool? dialogResult = passwordWindow.ShowDialog();

                    if (dialogResult == true)
                    {
                        adminRightsEnabled = true;
                        AdmRights.IsChecked = true;
                    }
                    else
                    {
                        adminRightsEnabled = false;
                        AdmRights.IsChecked = false;
                    }
                }
                else passwordWindow.Focus();
            }
        }

        // Меню док - закрытие программы
        private void CloseApp(object sender, RoutedEventArgs e) => Close();

        // Выбираем из списка серийников нужный датчик
        private void SortedByDateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortedByDateComboBox.SelectedItem != null)
            {
                // Контрольная проверка на связь с ДБ
                if (File.Exists(GetDbPath())){
                    try
                    {
                        // Создаем запросы на основе исходных данных
                        string selectedSerialNumber = SortedByDateComboBox.SelectedItem.ToString();

                        int sensorId = DatabaseHelper.GetSensorId(selectedSerialNumber, selectedType, selectedModel) ?? -1;
                        CreateTable(sensorId);

                        ShowSerialsIfPossible();
                    }
                    catch (SQLiteException sql)
                    {
                        MessageBox.Show(
                            "Произошла ошибка обработки SQL-запроса",
                            "Ошибка",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
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
                else
                    MessageBox.Show(
                        "Возникла ошибка связи с базой данных",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                SortedByDateComboBox.SelectedIndex = -1;
            }
        }

        // Поиск датчика по его серийнику
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            // Контрольная проверка на связь с ДБ
            if (GetDbPath().Contains(".db") && File.Exists(GetDbPath()))
            {
                string serialNumberText = SortBySerialIdTextBox.Text;

                if (!string.IsNullOrWhiteSpace(serialNumberText)) TrySerialNumber();

                else
                    MessageBox.Show(
                        "Введите серийный номер или выберите его из списка",
                        "Внимание",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
            }
            else
                MessageBox.Show(
                    "Ошибка соединения с базой данных\n" +
                    "Проверьте правильность указанного пути",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
        }

        // Динамическое получение пути до ДБ из конфига
        private string GetDbPath()
        {
            string path = Properties.DataBase.Default.DataBasePath;

            // Убедиться, что dbPath не пуст
            if (string.IsNullOrEmpty(path)) return null;
            return path;
        }

        // Формируем таблицу по серийнику и модели датчика
        private FilteredTable filteredTable;
        private void CreateTable(int sensorId)
        {
            if (filteredTable == null || !filteredTable.IsVisible)
            {
                filteredTable = new FilteredTable(adminRightsEnabled, sensorId);
                filteredTable.FilterBySerialNumber(sensorId);
                filteredTable.Closed += (s, args) => filteredTable = null;
                filteredTable.ShowDialog();
            }
            else
            {
                filteredTable.Focus();
                MessageBox.Show(
                    "Закройте текущую таблицу, прежде чем открывать новую",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // Сбрасываем поля поиска по дате
        public void ResetFields()
        {
            SortedByDateComboBox.SelectedIndex      = -1;
            SortedByDateComboBox.ItemsSource        = null;
            SortedByDateComboBox.IsEnabled          = false;

            SortBySerialIdTextBox.Text              = "";

            ScannerTypeCombobox.    SelectedIndex   = -1;
            ScannerModelCombobox.   SelectedIndex   = -1;
        }

        // Запуск поиска по ентеру
        private void SortBySerialIdTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) TrySerialNumber(); }

        // Пробуем прописанный серийник на совпадение в ДБ
        private void TrySerialNumber()
        {
            string serialNumberString = SortBySerialIdTextBox.Text;

            if (GetDbPath().Contains(".db") && File.Exists(GetDbPath()))
            {
                if (!(serialNumberString.Contains('(') || serialNumberString.Contains(')')))
                {
                    if (int.TryParse(serialNumberString, out int serialNumber))
                    {
                        if (serialNumber > 16777215) serialNumberString  = serialNumberString.Remove(0, 1);

                        // Получаем данные и количество уникальных моделей
                        List<Sensor> sensorList = DatabaseHelper.GetSensorBySerialNumber(serialNumberString);

                        if (sensorList != null && sensorList.Count > 0)
                        {
                            // Если уникальных записей одна, вызываем CreateTable напрямую
                            if (sensorList.Count == 1) 
                            {
                                CreateTable(sensorList[0].SensorId); // Передаем единственную модель
                            } 
                            else if (sensorList.Count > 1)
                            {
                                // Открываем диалоговое окно для выбора модели
                                SelectModelWindow selectModelWindow = new(sensorList);
                                if (selectModelWindow.ShowDialog() == true)
                                {
                                    // Получаем выбранную модель из окна
                                    string selectedModel    = selectModelWindow.SelectedModel;
                                    string selectedType     = selectModelWindow.SelectedType;

                                    int sensorId = DatabaseHelper.GetSensorId(serialNumberString, selectedType, selectedModel) ?? -1;

                                    try { CreateTable(sensorId); /* Передаем выбранную модель */ }
                                    catch { MessageBox.Show(
                                            "Возникла ошибка при связи с базой данных",
                                            "Ошибка",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Error); }
                                }
                            }
                        }
                        else    MessageBox.Show(
                                "Нет данных для данного серийного номера",
                                "Внимание",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                    }
                    else    MessageBox.Show(
                            "Введите корректный серийный номер",
                            "Внимание",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                }
                else    MessageBox.Show(
                        "Введите серийный номер без числа в скобках",
                        "Внимание",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
            }
            else    MessageBox.Show(
                    "Невозможно выполнить поиск по указанному пути\n" +
                    "Проверьте указанный путь в настройках",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
        }

        private DateTime    selectedMonth = DateTime.Now;
        private string      selectedType;
        private string      selectedModel;

        // Получаем список серийников если все поля заполнены
        private void MonthPickerCalendar_DisplayModeChanged(object sender, CalendarModeChangedEventArgs e)
        {
            if (MonthPickerCalendar.DisplayMode == CalendarMode.Month)
            {
                selectedMonth = MonthPickerCalendar.DisplayDate;
                Mouse.Capture(null);
                MonthPickerCalendar.DisplayMode = CalendarMode.Year;
                ShowSerialsIfPossible();
            }
        }

        private void ScannerTypeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (ScannerTypeCombobox.SelectedIndex)
            {
                case 0:
                    ScannerModelCombobox.ItemsSource = sensorModels.Models_NoneSelected;
                    selectedType = "-";
                    break;
                case 1:
                    ScannerModelCombobox.ItemsSource = sensorModels.Models_EnI100; 
                    break;
                case 2:
                    ScannerModelCombobox.ItemsSource = sensorModels.Models_EnI12;
                    break;
                default:
                    ScannerModelCombobox.SelectedIndex = -1;
                    break;
            }
            if (ScannerTypeCombobox.SelectedIndex != -1 && ScannerTypeCombobox.SelectedIndex != 0)
            {
                ComboBoxItem selectedItem = (ComboBoxItem)ScannerTypeCombobox.SelectedItem;
                selectedType = selectedItem.Content.ToString();
            }
            ShowSerialsIfPossible();
        }

        private void ScannerModelCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScannerModelCombobox.SelectedIndex != -1)
                if (ScannerModelCombobox.SelectedItem.ToString() == "Не указано") selectedModel = "-";
                else selectedModel = ScannerModelCombobox.SelectedItem.ToString();
            ShowSerialsIfPossible();
        }

        private void ShowSerialsIfPossible()
        {
            if (
                ScannerModelCombobox    != null &&
                ScannerTypeCombobox     != null)
            {
                if (
                    ScannerModelCombobox.SelectedIndex  != -1 &&
                    ScannerTypeCombobox.SelectedIndex   != -1 &&
                    Convert.ToString(selectedMonth)     != "01.01.0001 0:00:00")
                {
                    if (File.Exists(GetDbPath()))
                    {
                        SortedByDateComboBox.ItemsSource = databaseHelper.SelectSerials(
                        selectedMonth.ToString(),
                        selectedType,
                        selectedModel);
                        if (SortedByDateComboBox.Items.Count != 0)
                            SortedByDateComboBox.IsEnabled = true;
                        else
                        {
                            SortedByDateComboBox.IsEnabled = false;
                            MessageBox.Show(
                                    "Не было найдено датчиков по заданным параметрам",
                                    "Внимание",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                        }
                    }
                    else
                        MessageBox.Show(
                            "Возникла проблема при получении списка датчиков. " +
                            "Проверьте правильность указанного пути до базы данных в настройках",
                            "Внимание",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                }
            }
            else if (SortedByDateComboBox != null)
                SortedByDateComboBox.IsEnabled = false;
        }

        private void GoToCreditsWindow(object sender, RoutedEventArgs e)
        {
            credits = new CreditsWindow { Owner = this };
            credits.ShowDialog();
        }
    }
}
