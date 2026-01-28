using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using IndustrialSentinel.App.Configuration;
using IndustrialSentinel.App.ViewModels;
using IndustrialSentinel.App.Views;
using IndustrialSentinel.Core.Alerts;
using IndustrialSentinel.Core.Configuration;
using IndustrialSentinel.Core.Pipeline;
using IndustrialSentinel.Core.Security;
using IndustrialSentinel.Core.Processing;
using IndustrialSentinel.Core.Telemetry;
using IndustrialSentinel.Infrastructure.OpcUa;
using IndustrialSentinel.Infrastructure.Persistence;
using IndustrialSentinel.Infrastructure.Security;
using Microsoft.Data.Sqlite;

namespace IndustrialSentinel.App;

public partial class App : Application
{
    private AuthService? _authService;
    private SqliteConnectionFactory? _dbFactory;
    private SystemConfig? _config;
    private SecuritySettings? _securitySettings;
    private MainWindow? _mainWindow;
    private string? _dbPath;
    private DatabaseSettings? _dbSettings;
    private OpcUaSettings? _opcUaSettings;
    private string? _profile;
    private bool _isLoggingOut;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            var appSettings = TryLoadAppSettings();
            if (appSettings is null)
            {
                _config = SystemConfig.Default();
                _securitySettings = TryLoadSecuritySettings() ?? SecuritySettings.Default();
                _opcUaSettings = TryLoadOpcUaSettings();
                _dbSettings = new DatabaseSettings();
                _profile = "simulation";
            }
            else
            {
                _config = appSettings.System ?? SystemConfig.Default();
                _securitySettings = appSettings.Security ?? SecuritySettings.Default();
                _opcUaSettings = appSettings.OpcUa;
                _dbSettings = appSettings.Database ?? new DatabaseSettings();
                _profile = appSettings.Profile;
            }

            _dbPath = ResolveDatabasePath(_dbSettings?.Path);

            // SQLite sin cifrado con pragmas y verificacion basica.
            _dbFactory = new SqliteConnectionFactory(
                _dbPath,
                busyTimeoutMs: _dbSettings?.BusyTimeoutMs ?? 5000,
                useWal: _dbSettings?.UseWal ?? true);

            EnsureDatabase();

            var userRepository = new UserRepository(_dbFactory);
            var auditRepository = new AuditLogRepository(_dbFactory);
            _authService = new AuthService(userRepository, auditRepository, _securitySettings);

            ShowLogin();
        }
        catch (Exception ex)
        {
            ShowFatalError(ex);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.Close();
        base.OnExit(e);
    }

    private void EnsureDatabase()
    {
        if (_dbFactory is null)
        {
            throw new InvalidOperationException("DB factory not initialized.");
        }

        try
        {
            using var connection = _dbFactory.OpenConnection();
            SqliteSchema.EnsureCreated(connection);
        }
        catch (Exception)
        {
            if (!string.IsNullOrWhiteSpace(_dbPath))
            {
                TryDelete(_dbPath + "-wal");
                TryDelete(_dbPath + "-shm");
                TryDelete(_dbPath);
            }

            using var connection = _dbFactory.OpenConnection();
            SqliteSchema.EnsureCreated(connection);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private void ShowLogin()
    {
        if (_authService is null)
        {
            Shutdown();
            return;
        }

        var loginVm = new LoginViewModel(_authService, _securitySettings ?? SecuritySettings.Default());
        var loginWindow = new LoginWindow
        {
            DataContext = loginVm
        };

        loginVm.Authenticated += user =>
        {
            LaunchMain(user);
            loginWindow.DialogResult = true;
            loginWindow.Close();
        };

        var result = loginWindow.ShowDialog();
        if (result != true)
        {
            Shutdown();
        }
    }

    private void LaunchMain(UserAccount user)
    {
            if (_authService is null || _dbFactory is null || _config is null || _securitySettings is null)
            {
                Shutdown();
                return;
            }

            var seriesBuffer = new TelemetrySeriesBuffer(_config.BufferCapacity);
        var telemetryPersistenceEnabled = _dbSettings?.TelemetryEnabled ?? true;
        ITelemetryPersistence persistence = telemetryPersistenceEnabled
            ? new SqliteTelemetryPersistence(_dbFactory)
            : new NullTelemetryPersistence();
        var telemetrySource = CreateTelemetrySource();
        var pipeline = new TelemetryPipeline(
            _config,
            telemetrySource,
            new TelemetryProcessor(_config.SmoothingAlpha),
            new AlertService(_config),
            persistence,
            persistenceEnabled: telemetryPersistenceEnabled);

        var viewModel = new MainViewModel(pipeline, seriesBuffer, _authService, user, TimeSpan.FromMinutes(_securitySettings.SessionTimeoutMinutes), telemetrySource as ISourceStatus);
        if (!telemetryPersistenceEnabled)
        {
            viewModel.Notification = "Persistencia desactivada por configuracion.";
        }
        else if (_authService.IsPasswordExpired(user))
        {
            viewModel.Notification = "Contraseña expirada. Cambiala en Password.";
        }
        viewModel.LogoutRequested += () => Logout(viewModel, user);
        viewModel.AdminRequested += () => ShowAdmin();
        viewModel.ChangePasswordRequested += () => ShowChangePassword(user.Username);

        _mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        MainWindow = _mainWindow;
        _mainWindow.Closed += (_, _) =>
        {
            if (!_isLoggingOut)
            {
                Shutdown();
            }
        };

        _mainWindow.Show();
        viewModel.Start();
    }

    private void ShowAdmin()
    {
        if (_authService is null || _mainWindow is null)
        {
            return;
        }

        try
        {
            var adminVm = new AdminViewModel(_authService);
            var adminWindow = new AdminWindow
            {
                Owner = _mainWindow,
                DataContext = adminVm
            };
            adminWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowFatalError(ex);
        }
    }

    private void ShowChangePassword(string username)
    {
        if (_authService is null || _mainWindow is null)
        {
            return;
        }

        try
        {
            var window = new ChangePasswordWindow(_authService, username)
            {
                Owner = _mainWindow
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowFatalError(ex);
        }
    }

    private void Logout(MainViewModel viewModel, UserAccount user)
    {
        _authService?.LogAction(user.Username, "auth.logout", "user logout");
        viewModel.Stop();
        viewModel.Dispose();

        if (_mainWindow is not null)
        {
            _isLoggingOut = true;
            _mainWindow.RequestClose();
            _mainWindow = null;
            _isLoggingOut = false;
        }

        ShowLogin();
    }

    private ITelemetrySource CreateTelemetrySource()
    {
        var opcUaSettings = _opcUaSettings ?? TryLoadOpcUaSettings();
        var profile = _profile?.Trim().ToLowerInvariant() ?? "simulation";

        if (profile == "opcua")
        {
            if (opcUaSettings?.Enabled == true)
            {
                return new OpcUaTelemetrySource(opcUaSettings);
            }
        }

        if (profile != "simulation" && opcUaSettings?.Enabled == true)
        {
            return new OpcUaTelemetrySource(opcUaSettings);
        }

        return new TelemetrySimulatorSource(new TelemetrySimulator());
    }

    private static OpcUaSettings? TryLoadOpcUaSettings()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "opcua.json");
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<OpcUaSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static SecuritySettings? TryLoadSecuritySettings()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "security.json");
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<SecuritySettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static void ShowFatalError(Exception ex)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var logPath = Path.Combine(baseDir, "data", "startup_error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? baseDir);
            File.WriteAllText(logPath, ex.ToString());
        }
        catch
        {
        }

        MessageBox.Show(ex.Message, "Industrial Sentinel - Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFatalError(e.Exception);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowFatalError(e.Exception);
        e.SetObserved();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowFatalError(ex);
        }
        else
        {
            ShowFatalError(new Exception("Unhandled non-exception error."));
        }
    }

    private static AppSettings? TryLoadAppSettings()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveDatabasePath(string? rawPath)
    {
        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IndustrialSentinel",
            "industrial_sentinel.db");

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return fallback;
        }

        var expanded = Environment.ExpandEnvironmentVariables(rawPath);
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, expanded);
    }
}
