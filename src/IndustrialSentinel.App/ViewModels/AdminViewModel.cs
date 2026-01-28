using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using IndustrialSentinel.Core.Security;
using IndustrialSentinel.Infrastructure.Security;

namespace IndustrialSentinel.App.ViewModels;

public sealed class AdminViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private string _newUsername = string.Empty;
    private UserRole _newRole = UserRole.Operator;
    private UserAccount? _selectedUser;
    private string _status = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedRoleFilter = "All";
    private readonly ICollectionView _usersView;

    public AdminViewModel(AuthService authService)
    {
        _authService = authService;
        Users = new ObservableCollection<UserAccount>();
        _usersView = CollectionViewSource.GetDefaultView(Users);
        _usersView.Filter = FilterUser;
        SafeRefresh();
    }

    public ObservableCollection<UserAccount> Users { get; }

    public ICollectionView UsersView => _usersView;

    public UserAccount? SelectedUser
    {
        get => _selectedUser;
        set => SetProperty(ref _selectedUser, value);
    }

    public string NewUsername
    {
        get => _newUsername;
        set => SetProperty(ref _newUsername, value);
    }

    public UserRole NewRole
    {
        get => _newRole;
        set => SetProperty(ref _newRole, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _usersView.Refresh();
            }
        }
    }

    public IEnumerable<string> RoleFilters => new[] { "All", "Admin", "Operator", "Viewer" };

    public string SelectedRoleFilter
    {
        get => _selectedRoleFilter;
        set
        {
            if (SetProperty(ref _selectedRoleFilter, value))
            {
                _usersView.Refresh();
            }
        }
    }

    public IEnumerable<UserRole> Roles => Enum.GetValues<UserRole>();

    public void Refresh()
    {
        SafeRefresh();
    }

    private void SafeRefresh()
    {
        Users.Clear();
        try
        {
            foreach (var user in _authService.GetUsers())
            {
                Users.Add(user);
            }
        }
        catch (Exception ex)
        {
            Status = $"Error DB: {ex.Message}";
        }
    }

    private bool FilterUser(object? item)
    {
        if (item is not UserAccount user)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var needle = _searchText.Trim().ToLowerInvariant();
            if (!user.Username.ToLowerInvariant().Contains(needle))
            {
                return false;
            }
        }

        if (!string.Equals(_selectedRoleFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(user.Role.ToString(), _selectedRoleFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public void CreateUser(string password, string confirm)
    {
        if (password != confirm)
        {
            Status = "Las contraseñas no coinciden.";
            return;
        }

        try
        {
            if (_authService.TryCreateUser(NewUsername, password, NewRole, out var message))
            {
                Status = message;
                Refresh();
                return;
            }

            Status = message;
        }
        catch (Exception ex)
        {
            Status = $"Error DB: {ex.Message}";
        }
    }

    public void ResetPassword(string password, string confirm)
    {
        if (SelectedUser is null)
        {
            Status = "Selecciona un usuario.";
            return;
        }

        if (password != confirm)
        {
            Status = "Las contraseñas no coinciden.";
            return;
        }

        try
        {
            if (_authService.TryChangePassword(SelectedUser.Username, password, out var message))
            {
                Status = message;
                Refresh();
                return;
            }

            Status = message;
        }
        catch (Exception ex)
        {
            Status = $"Error DB: {ex.Message}";
        }
    }

    public void UnlockSelected()
    {
        if (SelectedUser is null)
        {
            Status = "Selecciona un usuario.";
            return;
        }

        try
        {
            _authService.UnlockUser(SelectedUser.Username);
            Status = "Usuario desbloqueado.";
            Refresh();
        }
        catch (Exception ex)
        {
            Status = $"Error DB: {ex.Message}";
        }
    }

    public void ExportAudit(string path)
    {
        try
        {
            _authService.ExportAuditLog(path);
            Status = "Auditoria exportada.";
        }
        catch (Exception ex)
        {
            Status = $"Error DB: {ex.Message}";
        }
    }
}
