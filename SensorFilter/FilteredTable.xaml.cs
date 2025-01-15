using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace SensorFilter
{
    /// <summary>
    /// Логика взаимодействия для FilteredTable.xaml
    /// </summary>
    public partial class FilteredTable : Window
    {
        // Инстансы классов
        private DatabaseHelper databaseHelper = new DatabaseHelper();

        bool adminRights;
        int sensorId;

        public FilteredTable(bool admin, int id)
        {
            InitializeComponent();

            adminRights = admin;
            sensorId = id;

            if (!adminRights) AdminTools.Visibility = Visibility.Hidden;
        }

        // Заносим сведения о датчике по серийнику и модели
        public void FilterBySerialNumber(int sensorId)
        {
            try
            {
                // Читаем БД через Хелпер
                var allCharacterisationData = databaseHelper.GetCharacterisationData(sensorId);
                var allVerificationData     = databaseHelper.GetVerificationData    (sensorId);
                var allCoefficientsData     = databaseHelper.GetCoefficientsData    (sensorId);

                // Пишем в таблицу
                ChDataGrid.   ItemsSource = allCharacterisationData;
                VrDataGrid.       ItemsSource = allVerificationData;
                CfDataGrid.       ItemsSource = allCoefficientsData;

                // Заполняем лейблы над таблицей
                var sensor = databaseHelper.GetSensorInfo(sensorId);

                SensorID.   Text = sensor.Value.SerialNumber;
                SensorType. Text = sensor.Value.Type;
                SensorModel.Text = sensor.Value.Model;
            }
            catch
            {
                MessageBox.Show(
                    "Возникла ошибка при обмене данных." +
                    "Возможно, вы подключены к устаревшей версии БД",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Получаем путь для сохранения файла коэффициентов
        private string GetSaveFilePath(string fileName,string filter, string extension)
        {
            // Создаём SaveFileDialog
            SaveFileDialog saveFileDialog = new()
            {
                // Настраиваем диалоговое окно
                Filter      = filter,           // Фильтр файлов
                FileName    = fileName,         // Начальное имя файла
                DefaultExt  = extension,        // Расширение по умолчанию
                Title       = "Экспорт файла"   // Заголовок окна
            };

            // Показываем диалоговое окно пользователю
            bool? result = saveFileDialog.ShowDialog();
            if (result == true) return saveFileDialog.FileName;

            return null;
        }

        // Вывод окна выбора списка коэффициентов, если для одного датчика есть несколько характеризаций
        private DateTime? ShowDateSelectionDialog(List<DateTime> availableDates)
        {
            // Создаём окно выбора даты
            var dateSelectionWindow = new DateSelectionWindow(availableDates);
            if (dateSelectionWindow.ShowDialog() == true) return dateSelectionWindow.SelectedDate;

            return null;
        }

        // Обработка селекта строк
        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (adminRights && (
                ChDataGrid.SelectedItems.Count > 0 || 
                VrDataGrid.SelectedItems.Count > 0 || 
                CfDataGrid.SelectedItems.Count > 0 ))
                DeleteRowsButton.IsEnabled = true;
            else
                DeleteRowsButton.IsEnabled = false;
        }

        // Выборка строк на удаление
        private void DeleteRowsButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedCount = 
                ChDataGrid.SelectedItems.Count + 
                VrDataGrid.SelectedItems.Count + 
                CfDataGrid.SelectedItems.Count;

            MessageBoxResult result = MessageBox.Show(
                $"Вы действительно хотите удалить {selectedCount} выбранных строк?", 
                "Подтверждение удаления", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DeleteSelectedRows();
            }
        }

        // Формирование запроса на удаление выбранных строк
        private void DeleteSelectedRows()
        {
            var characterisationDataIds = ChDataGrid.SelectedItems.Cast<SensorCharacterisation>(). Select(s => s.CharacterisationId).  ToList();
            var verificationDataIds     = VrDataGrid.SelectedItems.Cast<SensorVerification>().     Select(v => v.VerificationId).      ToList();
            var coefficientDataIds      = CfDataGrid.SelectedItems.Cast<SensorCoefficients>().     Select(c => c.CoefficientId).       ToList();

            if (characterisationDataIds.Any()) databaseHelper.DeleteSensorCharacterisationData  (characterisationDataIds);
            if (verificationDataIds.    Any()) databaseHelper.DeleteVerificationData            (verificationDataIds);
            if (coefficientDataIds.     Any()) databaseHelper.DeleteCoefficientData             (coefficientDataIds);

            // Проверка наличия данных по серийному номеру и модели
            bool hasData = databaseHelper.HasSensorRelatedData(sensorId);

            // Если данных нет, удаляем сам датчик
            if (!hasData)
            {
                databaseHelper.DeleteSensor(sensorId);
                MessageBox.Show(
                    "Датчик был удалён, так как записи о нём отсутствуют в связанных таблицах.",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Close();
            }
            else
                CheckDataGridLength();

            // Обновляем таблицы
            FilterBySerialNumber(sensorId);
        }

        // Аккуратное удаление строк при переключении вкладки
        private void TablesTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TablesTabControl.SelectedIndex != 0) ChDataGrid.UnselectAll();
            if (TablesTabControl.SelectedIndex != 1) VrDataGrid.UnselectAll();
            if (TablesTabControl.SelectedIndex != 2) CfDataGrid.UnselectAll();

            CheckDataGridLength();
        }

        // Проверяем датагрид на наличие строк
        private void CheckDataGridLength()
        {
            Menu_ExportCh.IsEnabled = ChDataGrid.Items.Count != 0;
            Menu_ExportVr.IsEnabled = VrDataGrid.Items.Count != 0;
            Menu_ExportCf.IsEnabled = CfDataGrid.Items.Count != 0;
        }

        // Кнопка удаления датчика
        private void DeleteSensorButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                $"Вы действительно хотите удалить датчик {SensorID.Text}?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (DeleteSensorAndAllRelatedData(SensorID.Text, SensorType.Text, SensorModel.Text))
                {
                    MessageBox.Show(
                        "Датчик и все связанные данные успешно удалены.",
                        "Информация",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Close();
                } 
            }
        }

        // Формирование запросов на удаление данных о датчике
        private bool DeleteSensorAndAllRelatedData(string serialNumber, string type, string model)
        {
            try
            {
                // Удаление всех связанных данных
                databaseHelper.DeleteCharacterisationData   (sensorId);
                databaseHelper.DeleteVerificationData       (sensorId);
                databaseHelper.DeleteCoefficientData        (sensorId);
                // Удаление самого датчика
                databaseHelper.DeleteSensor                 (sensorId);
                // Обновление таблиц после удаления
                FilterBySerialNumber                        (sensorId);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Произошла ошибка при удалении датчика: {ex.Message}", 
                    "Ошибка", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);

                return false;
            }
        }

        private async void Menu_ExportCh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем путь для сохранения
                string saveType = "-";
                if (SensorType.Text.Contains("12"))     saveType = "eni12";
                if (SensorType.Text.Contains("100"))    saveType = "eni100";
                
                string filePath = GetSaveFilePath($"ch_{SensorID.Text}_{saveType}_{SensorModel.Text}", "CSV files (*.csv)|*.csv", ".csv");

                if (filePath != null)
                {
                    // Получаем данные характеризации
                    var characterisationData = databaseHelper.GetCharacterisationData(sensorId);

                    // Формируем CSV
                    StringBuilder csvContent = new StringBuilder();
                    csvContent.AppendLine("Дата,Температура (ºC),Диапазон,Давление (кПа),Напряжение (мВ),Сопротивнение (Ом),Отклонение");

                    foreach (var data in characterisationData)
                    {
                        csvContent.AppendLine(string.Join(",",
                            data.DateTime.      ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                            data.Temperature.   ToString(                       CultureInfo.InvariantCulture),
                            data.Range.         ToString(                       CultureInfo.InvariantCulture),
                            data.Pressure.      ToString(                       CultureInfo.InvariantCulture),
                            data.Voltage.       ToString("F4",                  CultureInfo.InvariantCulture),
                            data.Resistance.    ToString("F4",                  CultureInfo.InvariantCulture),
                            data.Deviation.     ToString(                       CultureInfo.InvariantCulture)));
                    }

                    // Сохраняем файл
                    await File.WriteAllTextAsync(filePath, csvContent.ToString());
                    MessageBox.Show(
                        "Данные характеризации успешно экспортированы", 
                        "Информация", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
            }
            catch
            {
                MessageBox.Show(
                    "Произошла ошибка получения строк характеризации",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void Menu_ExportVr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем путь для сохранения

                string saveType = "-";
                if (SensorType.Text.Contains("12"))     saveType = "eni12";
                if (SensorType.Text.Contains("100"))    saveType = "eni100";

                string filePath = GetSaveFilePath($"vr_{SensorID.Text}_{saveType}_{SensorModel.Text}", "CSV files (*.csv)|*.csv", ".csv");

                if (filePath != null)
                {
                    // Получаем данные верификации
                    var verificationData = databaseHelper.GetVerificationData(sensorId);

                    // Формируем CSV
                    StringBuilder csvContent = new StringBuilder();
                    csvContent.AppendLine("Дата,Температура (°C),НПИ (кПа),ВПИ (кПа),Давление рассчитанное (кПа),Давление фактическое (кПа),Ток рассчитанный (мА),Ток фактический (мА),Напряжение (мВ),Сопротивление (Ом)");

                    foreach (var data in verificationData)
                    {
                        csvContent.AppendLine(string.Join(",",
                            data.DateTime.      ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                            data.Temperature.   ToString(                       CultureInfo.InvariantCulture),
                            data.NPI.           ToString(                       CultureInfo.InvariantCulture),
                            data.VPI.           ToString(                       CultureInfo.InvariantCulture),
                            data.PressureGiven. ToString("F2",                  CultureInfo.InvariantCulture),
                            data.PressureReal.  ToString("F2",                  CultureInfo.InvariantCulture),
                            data.CurrentGiven.  ToString("F4",                  CultureInfo.InvariantCulture),
                            data.CurrentReal.   ToString("F4",                  CultureInfo.InvariantCulture),
                            data.Voltage.       ToString("F4",                  CultureInfo.InvariantCulture),
                            data.Resistance.    ToString("F4",                  CultureInfo.InvariantCulture)));
                    }

                    // Сохраняем файл
                    await File.WriteAllTextAsync(filePath, csvContent.ToString());
                    MessageBox.Show(
                        "Данные верификации успешно экспортированы", 
                        "Информация", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
            }
            catch
            {
                MessageBox.Show(
                    "Произошла ошибка получения строк верификации",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void Menu_ExportCf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем список коэффициентов, сгруппированных по датам
                var coefficientsByDate = await databaseHelper.GetCoefficients(sensorId);

                // Проверяем количество дат
                if (coefficientsByDate.Count == 1)
                {
                    // Если только одна дата, выполняем экспорт сразу
                    var coefficients = coefficientsByDate.First().Value;
                    string filePath = GetSaveFilePath($"SN{SensorID.Text}_C", "Text file (*.txt)|*.txt", ".txt");
                    if (filePath != null)
                        SaveCoefficientsToFile(SensorID.Text, coefficients, filePath);
                }
                else if (coefficientsByDate.Count > 1)
                {
                    // Если несколько дат, открываем окно выбора
                    var selectedDate = ShowDateSelectionDialog(coefficientsByDate.Keys.ToList());
                    if (selectedDate != null)
                    {
                        var coefficients = coefficientsByDate[selectedDate.Value];
                        string filePath = GetSaveFilePath($"SN{SensorID.Text}_C", "Text file (*.txt)|*.txt", ".txt");
                        if (filePath != null)
                            SaveCoefficientsToFile(SensorID.Text, coefficients, filePath);
                    }
                }
            }
            catch
            {
                MessageBox.Show(
                    "Произошла ошибка получения коэффициентов",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Запись коэффициентов в тхт файл
        public void SaveCoefficientsToFile(string serialNumber, List<SensorCoefficients> coefficients, string filePath)
        {
            // Создание строки по образцу
            StringBuilder fileContent = new StringBuilder();
            fileContent.AppendLine("[coefs]");

            foreach (var coef in coefficients)
            {
                // Форматирование строки, например: ca0 = 175.10406494140625
                fileContent.AppendLine($"ca{coef.CoefficientIndex} = {coef.CoefficientValue.ToString(CultureInfo.InvariantCulture)}");
            }

            // Название файла в формате "SN{SerialNumber}_C"
            string fileName = $"SN{serialNumber}_C.txt";

            // Сохранение файла
            File.WriteAllText(filePath, fileContent.ToString());
        }
    }
}

//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⠥⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⡻⠝⢧⠈⠄⠀⠀⠀⠀⠀⠀⠀⢀⠘⠨⢱⡉⢟⡽⣻⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⠑⢾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⢁⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡳⢧⡉⠄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⡤⠘⡼⡼⣟⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⠷⣬⣿⢯⣿⡷⣿⢯⣿⣯⣿⡾⣟⣯⡷⣟⣿⣯⣿⣽⡿⣿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣟⣿⣿⠟⢠⣾⣿⣯⣿⣿⣿⣿⣿⣿⣿⢯⢿⢿⣿⣿⣽⣭⣍⡓⠚⠠⠤⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠘⠔⢱⣯⡿⣽⣿⣿⣿⣿⣿⣿⣟⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⢿⣿⣿⣟⣯⣿⢯⣿⣿⣻⣷⣧⣿⣻⣽⢯⡿⣽⢞⣷⢯⡿⣽⣟⣿⣻⣾⣿⣽⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣻⣰⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣯⠋⣨⣿⣯⣅⠂⠀⠀⠉⠑⠢⡀⠀⠁⠀⠄⠀⠀⠀⠀⠀⠀⠀⣈⠔⠚⠉⣀⠶⠳⣿⣻⣿⣾⣿⣿⢯⣿⣿⣿⣿⣿⣯⣿⠿⢿⣿⣷⣏⠝⢻⡿⣾⣿⣻⣏⢿⣷⡿⣽⢯⣟⣯⢿⡽⣻⣞⣯⠿⣽⡞⣯⢟⡷⣯⢿⡾⣟⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣽⣿⣿⣿⣟⣾⠡⣿⣿⣿⣿⣿⣿⣿⣿⣿⠇⢇⣾⣿⣿⣿⣿⣷⣤⣄⠀⠀⠀⠘⠴⠄⠀⠀⠀⠀⠀⠀⡶⠃⠀⠀⢀⡴⠊⠠⣈⣷⣿⣿⣿⣿⣿⣿⣿⡿⣿⣿⣿⣿⣿⣿⣶⢯⣭⣿⣿⣧⣜⣿⡽⣷⣟⣻⣾⡿⣽⢯⣟⡾⣽⢾⡽⣳⣝⢮⡻⢵⠺⡥⢯⡹⣭⣏⡟⣿⡽⣞⣿⢿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣻⣿⣿⢃⡜⢢⣿⣿⣿⣿⣿⣿⣿⡟⢬⣿⡿⠟⠻⡟⠛⠿⣿⣿⣧⡀⠀⠀⠀⠑⢤⡀⠘⢧⠀⢲⡇⠀⣠⠖⠁⠀⠀⣰⣿⣿⣿⠟⡯⣝⣿⣿⣿⣿⡼⣿⣿⣿⣿⣿⣾⣿⣿⣿⣻⣿⣧⢻⣽⣿⣾⣟⡾⣽⢯⡟⣾⡽⣯⣿⣽⢳⡞⣣⡝⡎⢧⡙⢦⡙⢦⡝⣞⣳⢿⣻⣟⣿⢿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣽⣿⣿⣿⠖⣻⣼⣷⣿⣿⣿⣿⣿⡇⣾⢫⣁⠲⣔⣦⣙⣶⣬⣿⢿⣿⣦⡀⣀⢀⣤⣽⡆⢠⠆⠠⣧⣜⠁⣠⢄⣤⣾⣿⣿⣿⣷⣮⣵⣟⣿⣿⣿⣿⣷⢻⣿⣿⣿⣿⣿⣿⣿⣶⣿⡟⢿⣹⢾⢿⠻⣼⣻⣭⢯⣭⡿⣽⢯⡟⡼⢧⣛⠵⣎⡝⣦⡙⢦⡙⠶⣙⢮⡳⢯⡟⣾⡽⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⣽⣻⣿⣟⡿⣷⣾⠉⣾⣿⣿⣿⣿⣿⣿⡇⣿⢾⣿⣯⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣭⣷⣤⣸⣷⡾⡣⠘⣷⣞⣻⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⢘⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⣘⣿⣏⢭⣛⠶⣳⢎⡿⣜⠿⣭⢷⣫⡝⡷⣭⢻⡼⡝⡶⣙⠦⢡⡙⡜⣢⠟⣽⣻⢷⣻⣽
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⣾⣏⣿⡿⠇⣾⣿⣿⣿⣿⣿⣿⣿⡇⡸⣿⣿⣿⣿⣿⡿⢹⡿⣿⢿⣿⣿⣿⣿⣿⣿⣿⣿⡾⠷⣷⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⠈⢿⣿⣿⣿⣿⣿⢾⣿⣿⣿⣿⣿⣷⣿⣿⣿⡇⢹⠸⣏⢇⣎⡹⣶⠏⣶⢏⡿⡸⢷⣇⢿⣱⡎⡇⢷⢹⡰⢏⡸⢇⡶⢱⣈⢹⣾⣹⣏⣿⣹
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣻⡾⣏⢳⣶⡟⣼⣿⣿⣿⣿⣿⣿⠧⠿⣿⢿⣿⣿⣿⡷⣼⣆⣿⣫⣿⣼⣿⣿⣿⣿⣯⣟⣷⠲⣶⣿⢿⡿⣿⣿⣿⣿⣿⣧⣽⣯⣼⣥⣾⣿⣿⣿⣿⣛⡃⣿⣿⣿⣿⣿⣿⣿⡽⢩⠿⣝⣦⣸⡏⢮⣱⢋⡟⣜⣫⠼⣁⡳⣜⢣⡖⣹⠜⣎⠳⡜⣣⠝⣎⢲⢃⠸⣎⢾⣱⣟⣾⣻
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣽⣺⡽⣿⣴⠿⡻⢿⣿⣿⣿⣿⠂⡴⢚⠿⠿⢛⣻⠿⣷⣶⣶⣿⣿⣶⣾⣿⣿⣿⣿⡿⠋⠀⠀⢸⢯⣶⣿⣿⣿⣿⣿⣿⣿⡿⠿⣿⠿⡟⢻⠣⢄⣮⠃⣻⣿⣿⣿⣿⣿⣛⢯⠓⢯⡉⢭⢹⣯⣷⣮⣭⡿⢉⣁⣋⡟⠶⣮⣭⣝⣧⢻⣭⢳⡹⣜⡻⣼⢳⢮⣗⢮⣳⢯⣞⣿⣽
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣟⣧⣷⢻⣿⣷⣷⣿⣿⣿⣿⣿⣶⡏⢻⢛⡉⠛⠿⠿⠿⠿⢿⠿⡭⠿⠿⠛⣿⣯⡛⡥⠀⠀⢀⠸⡼⣣⣏⣼⡙⠛⠛⠿⠿⠽⠿⠟⠋⢃⢂⠳⣮⢿⣿⣼⣿⣿⣿⣿⡟⡌⠒⠘⠠⣬⡚⠉⡝⢃⢦⣉⠛⠢⡉⣙⠛⠻⣜⡻⣟⣧⣏⣼⣳⢿⣽⣛⡷⣯⣟⣞⣻⣼⣻⣞⣯⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⢿⣿⣟⣿⣻⣰⣯⣿⣿⢿⣿⣿⣿⣿⣿⣟⣶⣿⠧⠤⠀⠀⠀⠀⠀⠀⠐⣊⣻⣽⣯⢷⡙⠀⠀⠈⢳⣾⣟⣽⣿⣇⡀⠀⠀⠀⠀⠀⠀⠀⠘⢻⣤⣿⣾⣟⢯⣿⣿⣿⣿⡦⢁⡄⠘⡂⠃⡁⢰⣤⠬⠶⠭⣔⣊⢑⡚⠾⢍⣿⣷⢬⡿⣽⢲⢯⡟⣾⣭⢿⡵⣾⣽⣳⢯⡷⢯⣻⣽
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⢿⣟⣯⢛⣿⣯⣽⣿⣿⣿⣿⣿⢿⣿⣿⣿⣯⣟⣿⣷⡶⠀⠀⠀⠀⠀⣠⣶⣿⣿⡟⠉⢻⣾⠃⠁⠀⢀⡩⣬⢻⠛⠹⣿⣻⣷⣄⡀⡀⠀⠀⠀⠀⠈⣯⢷⣿⣿⣧⣿⣿⣧⡝⠀⠀⠀⢸⣿⣶⣢⣶⣟⢳⣘⡄⡶⢨⣟⠲⠿⣿⣷⡿⣯⣧⢻⣽⢶⡻⢾⣭⡿⣽⣳⣯⣟⣯⣟⣯⢷⣻
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⣿⣿⣾⠮⡝⣡⡟⣏⣹⡻⣿⣿⣿⠻⣿⣿⣿⣿⣯⢻⣿⡐⠀⢀⣲⣾⣿⠟⠛⣯⣿⣀⣸⡿⠀⠀⠀⠀⠈⢿⣃⣤⣾⣿⡿⠈⠻⣿⣾⣤⡀⠀⠀⠀⢾⣾⣿⣿⣿⣿⣟⣾⣣⣙⣶⠂⠘⣿⣿⣿⣯⡟⣩⠟⠠⠶⠩⠌⠑⠦⠨⠁⠛⣿⣿⣿⣯⣟⣷⢯⡷⣟⣿⣻⣽⡿⣾⡽⣞⡭⣷
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⣿⣧⣶⣟⣋⣙⢫⣝⣻⣿⡭⣟⣿⣿⣆⢹⣿⣿⣿⣿⣿⣾⣷⣶⣿⡿⠟⠁⠀⣀⢻⣿⣿⣿⣷⠂⠀⠀⠀⢀⣴⣿⣿⡿⠟⠑⠲⠦⣌⢻⣿⣿⣷⣦⣤⣚⣿⣷⣾⣟⣿⡎⡿⠍⡛⣧⣔⠚⠘⢛⣿⣷⣅⣀⠀⠀⠀⠀⡠⠀⠀⠀⢠⣯⣽⣿⣿⣿⣿⣿⣿⡿⣿⣿⣿⣿⡿⣯⡽⡞⡵⢣
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣯⣽⣿⣿⣿⣿⣿⣿⣯⢷⣯⣿⣻⢿⣿⢻⡙⣿⣼⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⡈⠑⠃⠀⠀⠀⠉⠻⣿⣷⣤⣠⣤⣾⡿⢿⠛⠀⠀⠀⠀⠀⠸⣿⣿⣿⣿⣿⣿⣿⣿⣿⠟⣿⣿⢡⣏⣦⣵⠊⠸⣷⣶⣿⣿⣾⣿⣭⣿⣿⣍⠓⢚⠝⢹⣺⣿⣿⣿⣿⣿⣿⣿⣿⣻⣽⣳⣯⢯⡟⣽⣣⠟⡼⣱⢫
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⣿⣾⣟⣷⢿⡽⣷⣿⣿⣟⢾⣻⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣮⣄⣀⡀⠈⠄⠀⠈⠉⠙⢿⣛⣩⣀⣀⣁⣀⣀⣀⣠⣤⣤⣾⣿⣿⣿⣿⣿⢿⣛⡏⡰⢾⢿⡿⣿⣟⠶⣷⣾⣿⣿⣿⣟⠛⣿⣿⣿⣷⣶⣄⣎⡉⠈⠙⠿⣿⣿⣿⣿⣿⢯⡷⣟⣾⣿⣽⡿⣽⣧⢯⣞⡵⣧⣻
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡷⣟⡷⣯⣿⣟⣯⣿⣟⣿⣟⣯⣽⣯⣿⣿⡟⣿⡟⣿⣿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣯⣿⣶⣾⣛⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⣽⡟⣾⣸⣣⠇⣿⣮⣿⡿⣭⢿⣾⣿⣿⣿⣽⣯⡄⠀⣻⠿⣿⣿⣟⣏⠡⣀⣘⣶⣿⣿⣿⣿⣯⣿⣿⣿⣷⣿⣿⣿⣿⣯⣿⢾⣽⣳⢯
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣽⠋⣠⣿⢿⣿⢻⣿⣽⢺⣧⣿⣿⣽⢯⢻⢿⣾⠜⡞⣆⣿⣿⣿⣿⣿⣿⣻⡟⢿⢿⣿⠿⢿⣿⡿⠿⣿⠿⣿⢿⣿⣿⣿⣿⣿⣿⠏⢸⣱⡿⢨⢵⣏⣿⢻⣿⣿⣿⣿⡟⠾⣿⣻⣾⡏⢹⣿⠀⢰⡏⡹⣟⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⢿⣾⣟⣾⣽⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣵⣾⡿⣞⡿⣞⣧⣿⠉⣿⠿⣧⣿⡸⣿⣿⡜⢧⢳⣾⣿⣿⣿⣿⣿⣿⣧⣣⠈⡇⠀⠀⣿⠀⠀⢸⠀⡾⣼⣿⣿⣿⣿⣿⠃⠠⢸⣿⡗⢃⣾⣼⣇⣾⠂⣜⣻⠤⢣⣾⣿⣿⡿⠖⡻⠏⠀⣩⣿⣕⣾⣿⣾⣿⣿⣿⣿⣿⣿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣾
//⣿⣿⣿⣿⣽⣿⣿⣿⣿⣿⣿⣽⣯⣟⣿⢯⡿⣽⣿⣿⣿⣿⣾⣿⣿⣿⣽⣿⣿⣿⣭⣿⣦⣈⣿⣿⣷⣿⣽⣷⠘⣺⣿⣿⣿⣿⣿⣿⣿⡋⣿⠙⡿⢶⠚⢻⢲⡖⢿⠚⣿⢻⣿⣿⣿⣿⠏⠀⣸⣿⣿⢁⣾⢯⣿⣼⣟⢤⣙⣶⠅⠟⣿⡿⢟⣡⡤⠀⠀⠀⣈⣋⢯⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣻⣿⣯⣿⣽⣳⢻⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣯⣿⣽⣾⣿⣾⣿⣯⣞⣿⣿⣆⢌⠻⣿⣿⣿⣿⣿⣿⣿⣿⣤⣧⣼⣤⣾⣤⣿⣾⣷⣿⣿⣿⣿⣿⡯⣤⢴⣿⣿⣹⣿⣿⣿⣯⣷⣿⣾⡟⢃⣀⣹⣄⡾⠣⠗⢀⣬⡁⣉⡼⠃⣮⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣾⣷⣯⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣧⡽⣿⣿⣿⡿⢃⣾⣿⣧⡘⢿⣆⠻⡼⣿⣜⢿⡻⣿⣿⣿⣛⠛⠙⠛⣛⠛⠛⢋⣯⣿⣿⣿⡿⢋⠞⢡⣿⡟⢣⣽⣿⣿⣿⣿⣿⣿⣿⣕⠺⢛⠁⣠⣴⠄⠒⠎⢩⡟⢋⣳⣿⠽⢿⣿⣿⣿⣿⢿⣿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡷⣯⣿⣿⣿⣿⣿⣿⣿⣿⣿⣻⢿⣿⣿⣽⣿⣿⣿⣿⣿⣎⢿⣯⣹⣿⣿⣮⢷⡈⣛⢿⣿⣿⣷⣾⣷⣾⠿⠿⠿⠟⠋⠁⢠⠋⣠⣿⡿⣭⣳⣿⣿⣿⣛⣿⣿⣿⣿⣽⠿⣟⠀⠛⠀⢺⠶⠀⠈⠐⠈⠁⢀⡒⣿⣿⡿⣟⣬⣟⣾⣻⢯⣿⡿⣿⣽⡷⣿⣽⣾⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⡿⢿⣿⣿⣿⣿⣿⡿⣿⣿⣶⣿⣿⣏⣿⣿⣿⣿⣿⣿⣆⣿⣿⣿⣿⣿⠀⢿⣀⡀⣀⠸⠷⣀⠀⠀⠀⠀⠀⠀⢀⡶⣱⣏⣿⡿⢷⣹⣿⣿⣿⣿⢇⣿⣿⣿⣿⣿⣿⣇⣇⠀⠀⠀⠀⣀⣶⠆⠀⢀⣈⡹⢿⣿⣱⣾⣾⣷⢿⡿⣿⢷⣿⣿⡿⣿⡿⣷⣿⢿⣏⣿⡿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣯⣿⣽⣿⣿⣽⣿⣿⣽⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣽⣷⣿⣿⣿⡆⠈⢯⠛⣿⣻⣶⣾⣶⣶⣶⣶⣖⣛⠩⠼⢋⣼⡟⠹⣽⣿⣿⣿⣿⣿⣯⣿⣿⣿⣿⣿⣿⣿⣿⣧⢫⣝⣂⢘⡋⠁⠟⡊⢉⣀⠁⢆⡍⢻⠛⣭⢛⡽⣭⡟⣾⡽⣟⣿⣻⡽⣯⣟⣯⢿⡽⣿⢾⣟⣿⡿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⢿⢧⣾⢿⡿⣾⣿⣶⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣾⣿⣿⣿⣷⡒⢎⠳⠤⣤⣜⣋⣉⣭⣭⣅⡬⠄⡖⠀⡾⢓⠈⣷⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣎⡃⠀⠀⠀⠀⠀⢀⣛⡉⢂⢦⢣⡛⢦⢫⡜⣣⡝⣶⢫⡛⣼⢳⢿⣱⡞⣧⣛⡾⣹⣻⣞⣯⢿⣻
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⣯⣿⣾⣿⢵⡿⢳⣿⣶⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡻⡜⠦⠀⠀⠀⣉⠛⠁⠀⠀⠀⠰⡐⢨⡐⢩⣞⣵⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣟⢓⣒⠦⢤⣀⡀⠤⢀⡠⠅⡎⢱⡸⣝⢦⡝⣦⡝⣮⣟⡽⣾⡽⣞⣷⣛⣷⣹⢞⣵⣳⣻⣞⣿⣻
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⣻⣭⣖⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⣟⣧⠇⠀⠀⠀⡀⠰⣬⠀⠀⠀⠀⠈⡡⢇⡎⣽⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⣌⠻⣖⢮⣝⡦⢧⣘⡡⢚⡥⣳⡹⣎⡿⣶⣻⢷⣯⣟⣷⣻⣽⣞⣧⢟⣺⣟⣮⢷⣯⣿⣻⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡝⣶⡱⡄⠀⠀⠄⠁⣿⠂⠀⠀⠈⠒⢁⡿⠸⣣⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣼⣿⣳⣏⣞⠶⣽⡒⠮⢭⣟⡲⢥⣿⡽⣽⣳⢿⣟⣿⣾⣯⣷⣻⣾⣭⣟⣷⣻⣾⢿⣞⣿⣿⣻
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣖⢯⣵⡎⣅⡤⣀⢼⣄⠦⣠⣤⡴⣾⣏⣼⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣯⣿⣷⣻⣾⣿⣯⣷⣻⢦⣻⣿⣐⢮⣽⣓⠿⣿⣿⣿⣽⣯⣷⣿⣳⢿⣞⣳⣿⣽⣿⣿⣿⣾⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣽⣵⣿⣵⣾⣿⣿⣷⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣳⣿⣿⣿⣟⣯⣷⣿⣾⣿⣿⣼⣟⣿⣶⣖⣮⢥⣉⡙⣷⣿⣻⣿⣟⣿⣾⣿⣞⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣾⣿⣿⣯⣿⣿⣿⣿⣿⣷⣿⣿⣿⣿⣾⣽⡾⣵⡺⢯⣷⡹⢧⡻⢿⣽⡿⣯⣷⣿⣿⣿⣿⣿