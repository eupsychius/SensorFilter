using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace SensorFilter
{
    /// <summary>
    /// Логика взаимодействия для DateSelectionWindow.xaml
    /// </summary>
    public partial class DateSelectionWindow : Window
    {
        public DateTime? SelectedDate { get; private set; }

        List<DateTime> dates = new();

        public DateSelectionWindow(List<DateTime> availableDates)
        {
            InitializeComponent();

            dates = availableDates;

            // Форматируем даты перед добавлением в ComboBox
            var formattedDates = availableDates.Select(d => d.ToString("MMM yyyy", CultureInfo.CreateSpecificCulture("ru-RU"))).ToList();

            DateComboBox.ItemsSource = formattedDates;
            DateComboBox.SelectedIndex = 0;  // Выбираем первую дату по умолчанию
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем индекс выбранного элемента и используем его для выбора даты из исходного списка availableDates
            if (DateComboBox.SelectedIndex >= 0)
            {
                SelectedDate = dates[DateComboBox.SelectedIndex];
            }

            DialogResult = true;
            Close();
        }

    }
}
