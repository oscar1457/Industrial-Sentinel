using System.Windows;
using IndustrialSentinel.App.ViewModels;
using Microsoft.Win32;

namespace IndustrialSentinel.App.Views;

public partial class AdminWindow : Window
{
    public AdminWindow()
    {
        InitializeComponent();
    }

    private void OnCreateUser(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            vm.CreateUser(NewUserPassword.Password, NewUserConfirm.Password);
        }
    }

    private void OnResetPassword(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            vm.ResetPassword(ResetPassword.Password, ResetConfirm.Password);
        }
    }

    private void OnUnlock(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            vm.UnlockSelected();
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminViewModel vm)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Exportar auditoria",
            Filter = "CSV (*.csv)|*.csv",
            FileName = "audit_log.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            vm.ExportAudit(dialog.FileName);
        }
    }
}
