using System;
using System.Collections.Generic;
using System.Windows;

namespace SensorFilter
{
    public partial class SelectModelWindow : Window
    {
        public string SelectedType { get; private set; }
        public string SelectedModel { get; private set; }

        public SelectModelWindow(List<Sensor> sensors) //List<string> types, List<string> models, int sensorCount
        {
            InitializeComponent();

            // Объединяем списки типов и моделей в формат "Type (Model)"
            var combinedItems = new List<string>();
            foreach (var sensor in sensors)
            {
                if (sensor.Type == "-" && sensor.Model == "-")
                    combinedItems.Add("Не указано"); // Отображаем как "Не указано"
                else
                    combinedItems.Add($"{sensor.Type} ({sensor.Model})"); // Формат "Type (Model)"
            }

            // Устанавливаем объединённый список как источник данных для ComboBox
            ModelComboBox.ItemsSource = combinedItems;
            ModelComboBox.SelectedIndex = -1;
        }

        private void ModelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ModelComboBox.SelectedItem != null)
            {
                // Извлекаем выбранный элемент
                var selectedItem = ModelComboBox.SelectedItem.ToString();

                if (selectedItem == "Не указано")
                {
                    SelectedType    = "-";
                    SelectedModel   = "-"; // Если выбран "Не указано", присваиваем "-"
                }
                else
                {
                    // Разбиваем строку "Type (Model)" и извлекаем модель (после скобок)
                    string[] parts  = selectedItem.Split(new[] { " (" }, StringSplitOptions.None);

                    // Убираем закрывающую скобку из второй части
                    SelectedType    = parts[0];
                    SelectedModel   = parts[1].TrimEnd(')');
                }

                DialogResult = true; // Закрываем окно и возвращаем результат
            }
        }
    }
}
