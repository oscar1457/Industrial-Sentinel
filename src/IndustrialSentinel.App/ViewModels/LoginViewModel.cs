using IndustrialSentinel.Core.Configuration;
using IndustrialSentinel.Core.Security;
using IndustrialSentinel.Infrastructure.Security;

namespace IndustrialSentinel.App.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly SecuritySettings _settings;
    private string _username = string.Empty;
    private string _status = "";
    private bool _isBootstrap;
    private bool _allowSelfRegistration;

    public LoginViewModel(AuthService authService, SecuritySettings settings)
    {
        _authService = authService;
        _settings = settings;
        IsBootstrap = !_authService.HasAnyUsers();
        AllowSelfRegistration = _settings.AllowSelfRegistration;
    }

    public event Action<UserAccount>? Authenticated;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsBootstrap
    {
        get => _isBootstrap;
        private set => SetProperty(ref _isBootstrap, value);
    }

    public bool AllowSelfRegistration
    {
        get => _allowSelfRegistration;
        private set => SetProperty(ref _allowSelfRegistration, value);
    }

    public bool CanRegister => IsBootstrap || AllowSelfRegistration;

    public void Login(string password)
    {
        if (_authService.TryAuthenticate(Username, password, out var account, out var message))
        {
            Status = string.Empty;
            Authenticated?.Invoke(account!);
            return;
        }

        Status = message;
    }

    public void CreateAccount(string password, string confirm)
    {
        if (password != confirm)
        {
            Status = "Las contraseñas no coinciden.";
            return;
        }

        if (!CanRegister)
        {
            Status = "Registro deshabilitado.";
            return;
        }

        if (IsBootstrap)
        {
            if (_authService.TryCreateAdmin(Username, password, out var message))
            {
                Status = "Admin creado. Inicia sesion.";
                IsBootstrap = false;
                return;
            }

            Status = message;
            return;
        }

        if (_authService.TryCreateUser(Username, password, UserRole.Operator, out var createMessage))
        {
            Status = "Cuenta creada. Inicia sesion.";
            return;
        }

        Status = createMessage;
    }
}
