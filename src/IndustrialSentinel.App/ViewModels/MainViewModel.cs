using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using IndustrialSentinel.Core.Alerts;
using IndustrialSentinel.Core.Pipeline;
using IndustrialSentinel.Core.Security;
using IndustrialSentinel.Core.Telemetry;
using IndustrialSentinel.Infrastructure.Security;

namespace IndustrialSentinel.App.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly TimeSpan _sessionTimeout;

    private readonly TelemetryPipeline _pipeline;
    private readonly TelemetrySeriesBuffer _seriesBuffer;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _sessionTimer;
    private readonly RelayCommand _startCommand;
    private readonly RelayCommand _stopCommand;
    private readonly RelayCommand _logoutCommand;
    private readonly RelayCommand _adminCommand;
    private readonly RelayCommand _changePasswordCommand;
    private readonly RelayCommand _clearLogCommand;
    private readonly RelayCommand _exportHealthCommand;
    private readonly RelayCommand _toggleCompactCommand;
    private readonly AuthService _authService;
    private readonly UserAccount _user;
    private readonly ISourceStatus? _sourceStatusProvider;

    private double _rpm;
    private double _temperatureC;
    private double _vibrationMmS;
    private string _pipelineState = "Stopped";
    private string _queueDepthText = "Queue: -";
    private string _rateText = "Rate: -";
    private string _dropText = "Dropped: -";
    private string _healthText = "Health: -";
    private string _lastSampleTime = "--:--:--";
    private string _userLabel;
    private string _notification = string.Empty;
    private string _sourceStatus = "Source: -";
    private DateTime _lastActivityUtc;
    private DateTime _lastLogUtc;
    private double _fps;
    private bool _isCompact;

    public MainViewModel(TelemetryPipeline pipeline, TelemetrySeriesBuffer seriesBuffer, AuthService authService, UserAccount user, TimeSpan sessionTimeout, ISourceStatus? sourceStatus = null)
    {
        _pipeline = pipeline;
        _seriesBuffer = seriesBuffer;
        _authService = authService;
        _user = user;
        _sessionTimeout = sessionTimeout;
        _userLabel = $"{user.Username} ({user.Role})";
        _sourceStatusProvider = sourceStatus;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _lastActivityUtc = DateTime.UtcNow;

        _startCommand = new RelayCommand(_ => Start(), _ => !_pipeline.IsRunning && _user.Role != UserRole.Viewer);
        _stopCommand = new RelayCommand(_ => Stop(), _ => _pipeline.IsRunning && _user.Role != UserRole.Viewer);
        _logoutCommand = new RelayCommand(_ => RequestLogout());
        _adminCommand = new RelayCommand(_ => RequestAdmin(), _ => IsAdmin);
        _changePasswordCommand = new RelayCommand(_ => RequestChangePassword());
        _clearLogCommand = new RelayCommand(_ => ClearLog());
        _exportHealthCommand = new RelayCommand(_ => ExportHealthReport());
        _toggleCompactCommand = new RelayCommand(_ => ToggleCompact());

        Alerts = new ObservableCollection<AlertViewModel>();
        TelemetryLog = new ObservableCollection<string>();

        _pipeline.TelemetryProcessed += OnTelemetryProcessed;
        _pipeline.AlertRaised += OnAlertRaised;
        _pipeline.PipelineFaulted += OnPipelineFaulted;

        if (_sourceStatusProvider is not null)
        {
            SourceStatus = _sourceStatusProvider.Status;
            _sourceStatusProvider.StatusChanged += OnSourceStatusChanged;
        }

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _statusTimer.Tick += (_, _) => UpdateStatus();

        _sessionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _sessionTimer.Tick += (_, _) => CheckSessionTimeout();
        _sessionTimer.Start();
    }

    public event Action? LogoutRequested;
    public event Action? AdminRequested;
    public event Action? ChangePasswordRequested;

    public TelemetrySeriesBuffer SeriesBuffer => _seriesBuffer;

    public ObservableCollection<AlertViewModel> Alerts { get; }

    public ObservableCollection<string> TelemetryLog { get; }

    public RelayCommand StartCommand => _startCommand;

    public RelayCommand StopCommand => _stopCommand;

    public RelayCommand LogoutCommand => _logoutCommand;

    public RelayCommand AdminCommand => _adminCommand;

    public RelayCommand ChangePasswordCommand => _changePasswordCommand;

    public RelayCommand ClearLogCommand => _clearLogCommand;

    public RelayCommand ExportHealthCommand => _exportHealthCommand;

    public RelayCommand ToggleCompactCommand => _toggleCompactCommand;

    public string UserLabel
    {
        get => _userLabel;
        private set => SetProperty(ref _userLabel, value);
    }

    public bool IsAdmin => _user.Role == UserRole.Admin;

    public bool IsCompact
    {
        get => _isCompact;
        private set => SetProperty(ref _isCompact, value);
    }

    public double Rpm
    {
        get => _rpm;
        private set => SetProperty(ref _rpm, value);
    }

    public double TemperatureC
    {
        get => _temperatureC;
        private set => SetProperty(ref _temperatureC, value);
    }

    public double VibrationMmS
    {
        get => _vibrationMmS;
        private set => SetProperty(ref _vibrationMmS, value);
    }

    public string PipelineState
    {
        get => _pipelineState;
        private set => SetProperty(ref _pipelineState, value);
    }

    public string QueueDepthText
    {
        get => _queueDepthText;
        private set => SetProperty(ref _queueDepthText, value);
    }

    public string RateText
    {
        get => _rateText;
        private set => SetProperty(ref _rateText, value);
    }

    public string DropText
    {
        get => _dropText;
        private set => SetProperty(ref _dropText, value);
    }

    public string HealthText
    {
        get => _healthText;
        private set => SetProperty(ref _healthText, value);
    }

    public string LastSampleTime
    {
        get => _lastSampleTime;
        private set => SetProperty(ref _lastSampleTime, value);
    }

    public string Notification
    {
        get => _notification;
        set => SetProperty(ref _notification, value);
    }

    public string SourceStatus
    {
        get => _sourceStatus;
        private set => SetProperty(ref _sourceStatus, value);
    }

    public void RegisterActivity()
    {
        _lastActivityUtc = DateTime.UtcNow;
    }

    public void UpdateFps(double fps)
    {
        _fps = fps;
    }

    public void Start()
    {
        if (_pipeline.IsRunning)
        {
            return;
        }

        _pipeline.Start();
        PipelineState = "Running";
        _statusTimer.Start();
        UpdateStatus();
        _startCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
        _authService.LogAction(_user.Username, "pipeline.start", "user start");
    }

    public void Stop()
    {
        if (!_pipeline.IsRunning)
        {
            return;
        }

        _pipeline.Stop();
        PipelineState = "Stopped";
        _statusTimer.Stop();
        UpdateStatus();
        _startCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
        _authService.LogAction(_user.Username, "pipeline.stop", "user stop");
    }

    private void UpdateStatus()
    {
        var status = _pipeline.SnapshotStatus();
        QueueDepthText = $"Queue: raw {status.RawQueueDepth} | persist {status.PersistQueueDepth}";
        RateText = $"Rate: ingest {status.IngestRateHz:0} Hz | proc {status.ProcessRateHz:0} Hz | persist {status.PersistRateHz:0} Hz";
        DropText = $"Dropped: {status.DroppedSamples} | Persist drop: {status.PersistDrops}";

        var workingSetMb = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
        var persistState = status.PersistenceHealthy ? "OK" : "OFF";
        HealthText = $"Latency: {status.EndToEndLatencyMs:0} ms | FPS: {_fps:0} | RAM: {workingSetMb:0} MB | Persist: {persistState}";
    }

    private void OnTelemetryProcessed(TelemetryFrame frame)
    {
        _seriesBuffer.Add(frame);
        _dispatcher.BeginInvoke(() =>
        {
            Rpm = frame.RpmSmoothed;
            TemperatureC = frame.TemperatureSmoothed;
            VibrationMmS = frame.VibrationSmoothed;
            LastSampleTime = frame.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        });

        if (DateTime.UtcNow - _lastLogUtc > TimeSpan.FromMilliseconds(250))
        {
            _lastLogUtc = DateTime.UtcNow;
            var line = $"{frame.TimestampUtc:HH:mm:ss.fff} | RPM {frame.RpmSmoothed,6:0} | T {frame.TemperatureSmoothed,5:0.0}C | V {frame.VibrationSmoothed,4:0.00}";
            _dispatcher.BeginInvoke(() =>
            {
                TelemetryLog.Insert(0, line);
                var limit = IsCompact ? 200 : 120;
                while (TelemetryLog.Count > limit)
                {
                    TelemetryLog.RemoveAt(TelemetryLog.Count - 1);
                }
            });
        }
    }

    private void OnAlertRaised(AlertEvent alert)
    {
        _dispatcher.BeginInvoke(() =>
        {
            Alerts.Insert(0, new AlertViewModel(alert));
            var limit = IsCompact ? 240 : 200;
            while (Alerts.Count > limit)
            {
                Alerts.RemoveAt(Alerts.Count - 1);
            }
        });
    }

    private void OnPipelineFaulted(Exception ex)
    {
        _dispatcher.BeginInvoke(() =>
        {
            Notification = $"Error pipeline: {ex.Message}";
        });
    }

    private void OnSourceStatusChanged(string status)
    {
        _dispatcher.BeginInvoke(() =>
        {
            SourceStatus = status;
        });
    }

    private void CheckSessionTimeout()
    {
        if (DateTime.UtcNow - _lastActivityUtc > _sessionTimeout)
        {
            _authService.LogAction(_user.Username, "session.timeout", "auto logout");
            RequestLogout();
        }
    }

    private void RequestLogout()
    {
        LogoutRequested?.Invoke();
    }

    private void RequestAdmin()
    {
        AdminRequested?.Invoke();
    }

    private void RequestChangePassword()
    {
        ChangePasswordRequested?.Invoke();
    }

    private void ClearLog()
    {
        TelemetryLog.Clear();
        Alerts.Clear();
        Notification = "Logs limpiados.";
    }

    private void ToggleCompact()
    {
        IsCompact = !IsCompact;
        Notification = IsCompact ? "Modo compacto activo." : "Modo compacto desactivado.";
    }

    private void ExportHealthReport()
    {
        try
        {
            var status = _pipeline.SnapshotStatus();
            var report = new
            {
                GeneratedUtc = DateTime.UtcNow,
                User = _user.Username,
                Pipeline = status,
                SessionTimeoutMinutes = _sessionTimeout.TotalMinutes,
                WorkingSetMb = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0),
                Gc = new
                {
                    Gen0 = GC.CollectionCount(0),
                    Gen1 = GC.CollectionCount(1),
                    Gen2 = GC.CollectionCount(2),
                    TotalMemoryMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0)
                },
                Environment = new
                {
                    Machine = Environment.MachineName,
                    OS = Environment.OSVersion.ToString(),
                    Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                }
            };

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IndustrialSentinel", "reports");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"health_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(path, json);
            Notification = $"Health report guardado: {path}";
            _authService.LogAction(_user.Username, "health.export", path);
        }
        catch (Exception ex)
        {
            Notification = $"Error reporte: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _sessionTimer.Stop();
        _pipeline.TelemetryProcessed -= OnTelemetryProcessed;
        _pipeline.AlertRaised -= OnAlertRaised;
        _pipeline.PipelineFaulted -= OnPipelineFaulted;
        if (_sourceStatusProvider is not null)
        {
            _sourceStatusProvider.StatusChanged -= OnSourceStatusChanged;
        }
        _pipeline.Dispose();
    }
}
