using System.Collections.Generic;
using System.Windows;

namespace SensorFilter
{
    public partial class SelectModelWindow : Window
    {
        public string SelectedModel { get; private set; }

        public SelectModelWindow(List<string> models)
        {
            InitializeComponent();
            ModelComboBox.ItemsSource = models;
            ModelComboBox.SelectedIndex = -1;
        }

        private void ModelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ModelComboBox.SelectedItem != null)
            {
                SelectedModel = ModelComboBox.SelectedItem.ToString();
                DialogResult = true; // Закрываем окно и возвращаем результат
            }
        }
    }
}
