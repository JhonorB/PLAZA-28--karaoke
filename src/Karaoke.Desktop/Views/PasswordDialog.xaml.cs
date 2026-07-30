using System.Windows;

namespace Karaoke.Desktop.Views;

public partial class PasswordDialog : Window
{
    public string EnteredPassword => TxtPassword.Password;

    public PasswordDialog()
    {
        InitializeComponent();
        TxtPassword.Focus();
    }

    private void OnAcceptClicked(object sender, RoutedEventArgs e)
    {
        if (EnteredPassword == "admin 123" || EnteredPassword == "admin123" || EnteredPassword == "admin")
        {
            DialogResult = true;
            Close();
        }
        else
        {
            LblError.Text = "❌ Contraseña incorrecta. (Clave: admin 123)";
            LblError.Visibility = Visibility.Visible;
            TxtPassword.Clear();
            TxtPassword.Focus();
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
