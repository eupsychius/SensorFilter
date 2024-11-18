using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
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
        private string DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sensor_data.db");

        bool adminRights;

        public FilteredTable(bool admin)
        {
            InitializeComponent();

            adminRights = admin;

            AdminTools.IsEnabled = adminRights;
            ExportCoefficientsButton.Visibility = Visibility.Hidden;
        }

        // Заносим сведения о датчике по серийнику и модели
        public void FilterBySerialNumber(string serialNumber, string model)
        {
            try
            {
                // Читаем БД через Хелпер
                var allSensorData       = databaseHelper.GetCharacterisationDataBySerialNumber  (serialNumber, model);
                var allVerificationData = databaseHelper.GetVerificationDataBySerialNumber      (serialNumber, model);
                var allCoefficientsData = databaseHelper.GetCoefficientsDataBySerialNumber      (serialNumber, model);

                // Пишем в таблицу
                FilteredDataGrid.ItemsSource            = allSensorData;
                VerifiedDataGrid.ItemsSource            = allVerificationData;
                SensorCoefficientsDataGrid.ItemsSource  = allCoefficientsData;

                // Заполняем лейблы над таблицей
                var sensor = databaseHelper.GetSensorTypeBySerialNumber(serialNumber, model);

                SensorID.Text = serialNumber;

                if (sensor != null)
                    SensorType.Text     = sensor.Type;

                SensorModel.Text = model;
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

        List<int> selectedCharasteristics   = new List<int>();
        List<int> selectedVerifications     = new List<int>();
        List<int> selectedCoefficients      = new List<int>();

        // Экспорт коэффициентов в тхт файл
        private async void ExportCoefficientsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем список коэффициентов, сгруппированных по датам
                var coefficientsByDate = await databaseHelper.GetCoefficientsBySerialNumber(SensorID.Text, SensorModel.Text);

                // Проверяем количество дат
                if (coefficientsByDate.Count == 1)
                {
                    // Если только одна дата, выполняем экспорт сразу
                    var coefficients = coefficientsByDate.First().Value;
                    string filePath = GetSaveFilePath();
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
                        string filePath = GetSaveFilePath();
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

        // Получаем путь для сохранения файла коэффициентов
        private string GetSaveFilePath()
        {
            // Создаём SaveFileDialog
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();

            // Настраиваем диалоговое окно
            saveFileDialog.Filter = "Text file (*.txt)|*.txt";    // Фильтр файлов
            saveFileDialog.FileName = $"SN{SensorID.Text}_C";       // Начальное имя файла
            saveFileDialog.DefaultExt = ".txt";                       // Расширение по умолчанию
            saveFileDialog.Title = "Экспорт файла";              // Заголовок окна

            // Показываем диалоговое окно пользователю
            bool? result = saveFileDialog.ShowDialog();
            if (result == true)
            {
                return saveFileDialog.FileName;
            }

            return null;
        }

        // Вывод окна выбора списка коэффициентов, если для одного датчика есть несколько характеризаций
        private DateTime? ShowDateSelectionDialog(List<DateTime> availableDates)
        {
            // Создаём окно выбора даты
            var dateSelectionWindow = new DateSelectionWindow(availableDates);
            if (dateSelectionWindow.ShowDialog() == true)
            {
                return dateSelectionWindow.SelectedDate;
            }
            return null;
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

        // Обработка селекта строк
        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (adminRights && (
                FilteredDataGrid.           SelectedItems.Count > 0 || 
                VerifiedDataGrid.           SelectedItems.Count > 0 || 
                SensorCoefficientsDataGrid. SelectedItems.Count > 0 ))
                DeleteRowsButton.IsEnabled = true;
            else
                DeleteRowsButton.IsEnabled = false;
        }

        // Выборка строк на удаление
        private void DeleteRowsButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedCount = 
                FilteredDataGrid.           SelectedItems.Count + 
                VerifiedDataGrid.           SelectedItems.Count + 
                SensorCoefficientsDataGrid. SelectedItems.Count;

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
            var sensorDataIds       = FilteredDataGrid.SelectedItems.           Cast<SensorData>().         Select(s => s.DataId).  ToList();
            var verificationDataIds = VerifiedDataGrid.SelectedItems.           Cast<SensorVerification>(). Select(v => v.Id).      ToList();
            var coefficientDataIds  = SensorCoefficientsDataGrid.SelectedItems. Cast<SensorCoefficients>(). Select(c => c.Id).      ToList();

            if (sensorDataIds.      Any())
                databaseHelper.DeleteSensorData         (sensorDataIds);
            if (verificationDataIds.Any())
                databaseHelper.DeleteVerificationData   (verificationDataIds);
            if (coefficientDataIds. Any())
                databaseHelper.DeleteCoefficientData    (coefficientDataIds);

            // Проверка наличия данных по серийному номеру и модели
            bool hasData = databaseHelper.HasSensorRelatedData(SensorID.Text, SensorModel.Text);

            // Если данных нет, удаляем сам датчик
            if (!hasData)
            {
                databaseHelper.DeleteSensor(SensorID.Text, SensorModel.Text);
                MessageBox.Show(
                    "Датчик был удалён, так как записи о нём отсутствуют в связанных таблицах.",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                this.Close();
            }
            else
                CheckCoefficientsGridLength();

            // Обновляем таблицы
            FilterBySerialNumber(SensorID.Text, SensorModel.Text);
        }

        // Отмена выделения строк в датагриде
        //private void DeselectButton_Click(object sender, RoutedEventArgs e)
        //{
        //    FilteredDataGrid.           UnselectAll();
        //    VerifiedDataGrid.           UnselectAll();
        //    SensorCoefficientsDataGrid. UnselectAll();
        //}

        // Аккуратное удаление строк при переключении вкладки
        private void TablesTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TablesTabControl.SelectedIndex != 0)
                FilteredDataGrid.           UnselectAll();
            if (TablesTabControl.SelectedIndex != 1)
                VerifiedDataGrid.           UnselectAll();
            if (TablesTabControl.SelectedIndex != 2)
            {
                SensorCoefficientsDataGrid.UnselectAll();

                CheckCoefficientsGridLength();
            }
                
        }

        // Проверяем датагрид на наличие строк
        private void CheckCoefficientsGridLength()
        {
            if (SensorCoefficientsDataGrid.Items.Count == 0)
                ExportCoefficientsButton.Visibility = Visibility.Hidden;

            else
                ExportCoefficientsButton.Visibility = Visibility.Visible;
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
                if (DeleteSensorAndAllRelatedData(SensorID.Text, SensorModel.Text))
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
        private bool DeleteSensorAndAllRelatedData(string serialNumber, string model)
        {
            try
            {
                // Удаление всех связанных данных
                databaseHelper.DeleteSensorDataBySerialNumber       (serialNumber, model);
                databaseHelper.DeleteVerificationDataBySerialNumber (serialNumber, model);
                databaseHelper.DeleteCoefficientDataBySerialNumber  (serialNumber, model);

                // Удаление самого датчика
                databaseHelper.DeleteSensor(serialNumber, model);

                // Обновление таблиц после удаления
                FilterBySerialNumber(serialNumber, model);

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