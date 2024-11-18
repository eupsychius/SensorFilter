using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace SensorFilter
{
    /// <summary>
    /// Логика взаимодействия для PasswordWindow.xaml
    /// </summary>
    public partial class PasswordWindow : Window
    {
        public PasswordWindow()
        {
            InitializeComponent();
            PwordBox.Focus();
        }

        public bool CheckPassword(string enteredPassword)
        {
            string CorrectPassword = "123456";
            return enteredPassword == CorrectPassword;
        }

        private void PwordBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryPassword();
            }
        }

        private void TryPassword()
        {
            string enteredPassword = PwordBox.Password; // Получаем текст из TextBox
            if (CheckPassword(enteredPassword))
            {
                this.DialogResult = true; // Если пароль верный
                this.Close();
            }
            else
            {
                MessageBox.Show(
                    "Неверный пароль. Попробуйте снова",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
