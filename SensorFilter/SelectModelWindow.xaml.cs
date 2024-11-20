using System.Collections.Generic;
using System.Windows;

namespace SensorFilter
{
    public partial class SelectModelWindow : Window
    {
        public string SelectedModel { get; private set; }

        public SelectModelWindow(List<string> types, List<string> models)
        {
            InitializeComponent();

            // Проверяем, что длина списков совпадает
            if (types.Count != models.Count)
            {
                MessageBox.Show("Количество типов и моделей не совпадает.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Объединяем списки типов и моделей в формат "Type (Model)"
            var combinedItems = new List<string>();
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == "-" && models[i] == "-")
                {
                    combinedItems.Add("Не указано"); // Отображаем как "Не указано"
                }
                else
                {
                    combinedItems.Add($"{types[i]} ({models[i]})"); // Формат "Type (Model)"
                }
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
                    SelectedModel = "-"; // Если выбран "Не указано", присваиваем "-"
                }
                else
                {
                    // Разбиваем строку "Type (Model)" и извлекаем модель (после скобок)
                    var startIndex = selectedItem.IndexOf('(');
                    var endIndex = selectedItem.IndexOf(')');

                    if (startIndex != -1 && endIndex != -1)
                    {
                        // Извлекаем строку между скобками (модель)
                        SelectedModel = selectedItem.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
                    }
                    else
                    {
                        // На случай, если формат отличается (хотя это не должно происходить)
                        SelectedModel = selectedItem;
                    }
                }

                DialogResult = true; // Закрываем окно и возвращаем результат
            }
        }
    }
}
