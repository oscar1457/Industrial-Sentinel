using System.Windows;
using IndustrialSentinel.Infrastructure.Security;

namespace IndustrialSentinel.App.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly AuthService _authService;
    private readonly string _username;

    public ChangePasswordWindow(AuthService authService, string username)
    {
        _authService = authService;
        _username = username;
        InitializeComponent();
    }

    private void OnChange(object sender, RoutedEventArgs e)
    {
        if (_authService.TryChangePassword(_username, NewPassword.Password, out var message))
        {
            StatusText.Text = message;
            DialogResult = true;
            Close();
            return;
        }

        StatusText.Text = message;
    }
}
