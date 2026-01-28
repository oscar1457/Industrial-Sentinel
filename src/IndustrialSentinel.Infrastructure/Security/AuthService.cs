using IndustrialSentinel.Core.Configuration;
using IndustrialSentinel.Core.Security;

namespace IndustrialSentinel.Infrastructure.Security;

public sealed class AuthService
{
    private readonly UserRepository _users;
    private readonly AuditLogRepository _audit;
    private readonly SecuritySettings _settings;

    public AuthService(UserRepository users, AuditLogRepository audit, SecuritySettings settings)
    {
        _users = users;
        _audit = audit;
        _settings = settings;
    }

    public bool HasAnyUsers() => _users.HasAnyUsers();

    public bool TryCreateAdmin(string username, string password, out string message)
    {
        if (!IsStrongPassword(password, _settings, out message))
        {
            return false;
        }

        var normalized = NormalizeUsername(username);
        if (normalized.Length < 3)
        {
            message = "Usuario invalido.";
            return false;
        }

        if (_users.GetUser(normalized) is not null)
        {
            message = "Usuario ya existe.";
            return false;
        }

        _users.CreateUser(normalized, password, UserRole.Admin);
        _audit.Add(normalized, "user.create", "admin bootstrap");
        message = "Admin creado.";
        return true;
    }

    public bool TryCreateUser(string username, string password, UserRole role, out string message)
    {
        if (!IsStrongPassword(password, _settings, out message))
        {
            return false;
        }

        var normalized = NormalizeUsername(username);
        if (normalized.Length < 3)
        {
            message = "Usuario invalido.";
            return false;
        }

        if (_users.GetUser(normalized) is not null)
        {
            message = "Usuario ya existe.";
            return false;
        }

        _users.CreateUser(normalized, password, role);
        _audit.Add(normalized, "user.create", $"role:{role}");
        message = "Usuario creado.";
        return true;
    }

    public bool TryAuthenticate(string username, string password, out UserAccount? account, out string message)
    {
        account = null;
        var normalized = NormalizeUsername(username);
        var creds = _users.GetCredentials(normalized);
        if (creds is null)
        {
            message = "Credenciales invalidas.";
            _audit.Add(normalized, "auth.fail", "unknown user");
            return false;
        }

        var lockout = creds.Value.LockoutUntilUtc;
        if (lockout.HasValue && lockout.Value > DateTime.UtcNow)
        {
            message = $"Cuenta bloqueada hasta {lockout.Value.ToLocalTime():HH:mm:ss}.";
            _audit.Add(normalized, "auth.locked", "locked account");
            return false;
        }

        if (!PasswordHasher.Verify(password, creds.Value.Salt, creds.Value.Hash))
        {
            _users.RegisterFailedAttempt(normalized, _settings.MaxFailedAttempts, TimeSpan.FromMinutes(_settings.LockoutMinutes));
            message = "Credenciales invalidas.";
            _audit.Add(normalized, "auth.fail", "bad password");
            return false;
        }

        _users.ResetFailures(normalized);
        account = _users.GetUser(normalized);
        message = "OK";
        _audit.Add(normalized, "auth.success", "login");
        return true;
    }

    public bool IsPasswordExpired(UserAccount account)
    {
        if (_settings.PasswordMaxAgeDays <= 0)
        {
            return false;
        }

        if (account.PasswordChangedUtc is null)
        {
            return false;
        }

        var maxAge = TimeSpan.FromDays(_settings.PasswordMaxAgeDays);
        return DateTime.UtcNow - account.PasswordChangedUtc.Value > maxAge;
    }

    public bool TryChangePassword(string username, string newPassword, out string message)
    {
        if (!IsStrongPassword(newPassword, _settings, out message))
        {
            return false;
        }

        var normalized = NormalizeUsername(username);
        _users.UpdatePassword(normalized, newPassword);
        _audit.Add(normalized, "user.password", "changed");
        message = "Contraseña actualizada.";
        return true;
    }

    public void UnlockUser(string username)
    {
        var normalized = NormalizeUsername(username);
        _users.ResetFailures(normalized);
        _audit.Add(normalized, "user.unlock", "manual unlock");
    }

    public IReadOnlyList<UserAccount> GetUsers() => _users.GetAll();

    public void LogAction(string username, string action, string details)
    {
        try
        {
            _audit.Add(NormalizeUsername(username), action, details);
        }
        catch
        {
        }
    }

    public void ExportAuditLog(string path)
    {
        _audit.ExportToCsv(path);
    }

    public static bool IsStrongPassword(string password, SecuritySettings settings, out string message)
    {
        message = string.Empty;
        if (password.Length < settings.PasswordMinLength)
        {
            message = $"Minimo {settings.PasswordMinLength} caracteres.";
            return false;
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));

        if (settings.RequireUpper && !hasUpper)
        {
            message = "Debe incluir mayusculas.";
            return false;
        }

        if (settings.RequireLower && !hasLower)
        {
            message = "Debe incluir minusculas.";
            return false;
        }

        if (settings.RequireDigit && !hasDigit)
        {
            message = "Debe incluir numeros.";
            return false;
        }

        if (settings.RequireSymbol && !hasSymbol)
        {
            message = "Debe incluir simbolos.";
            return false;
        }

        return true;
    }

    private static string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }
}
