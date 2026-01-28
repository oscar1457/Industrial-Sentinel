using System.Windows;
using IndustrialSentinel.App.ViewModels;

namespace IndustrialSentinel.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    private void OnLogin(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Login(PasswordBox.Password);
        }
    }

    private void OnCreateAccount(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.CreateAccount(PasswordBox.Password, ConfirmBox.Password);
        }
    }
}
