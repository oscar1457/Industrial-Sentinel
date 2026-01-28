using System.Globalization;
using IndustrialSentinel.Core.Telemetry;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace IndustrialSentinel.Infrastructure.OpcUa;

public sealed class OpcUaTelemetrySource : ITelemetrySource, ISourceStatus
{
    private readonly OpcUaSettings _settings;
    private readonly object _sync = new();
    private Session? _session;
    private double _lastRpm;
    private double _lastTemp;
    private double _lastVibration;
    private DateTime _nextReconnectUtc;
    private int _reconnectAttempt;
    private string _status = "OPC-UA: Connecting";

    public OpcUaTelemetrySource(OpcUaSettings settings)
    {
        _settings = settings;
        TryReconnect();
    }

    public string Status => _status;

    public event Action<string>? StatusChanged;

    public TelemetrySample ReadSample()
    {
        try
        {
            var session = EnsureSession();
            var rpm = ReadDouble(session, _settings.RpmNodeId, _lastRpm);
            var temp = ReadDouble(session, _settings.TemperatureNodeId, _lastTemp);
            var vibration = ReadDouble(session, _settings.VibrationNodeId, _lastVibration);

            _lastRpm = rpm;
            _lastTemp = temp;
            _lastVibration = vibration;

            return new TelemetrySample(DateTime.UtcNow, rpm, temp, vibration);
        }
        catch (Exception ex)
        {
            ScheduleReconnect($"read error: {ex.GetType().Name}");
            return new TelemetrySample(DateTime.UtcNow, _lastRpm, _lastTemp, _lastVibration);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_session is not null)
            {
                _session.Close();
                _session.Dispose();
                _session = null;
            }
        }
    }

    private Session EnsureSession()
    {
        lock (_sync)
        {
            if (_session is not null && _session.Connected)
            {
                return _session;
            }

            if (DateTime.UtcNow < _nextReconnectUtc)
            {
                throw new InvalidOperationException("OPC-UA reconnect pending");
            }

            return TryReconnect() ?? throw new InvalidOperationException("OPC-UA session unavailable");
        }
    }

    private Session? TryReconnect()
    {
        try
        {
            _session?.Close();
            _session?.Dispose();
            _session = CreateSessionAsync(_settings).GetAwaiter().GetResult();
            _session.KeepAlive += OnKeepAlive;
            _session.KeepAliveInterval = _settings.KeepAliveIntervalMs;
            _reconnectAttempt = 0;
            _nextReconnectUtc = DateTime.MinValue;
            SetStatus("OPC-UA: Connected");
            return _session;
        }
        catch (Exception ex)
        {
            ScheduleReconnect($"connect error: {ex.GetType().Name}");
            return null;
        }
    }

    private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (ServiceResult.IsBad(e.Status))
        {
            ScheduleReconnect($"keepalive: {e.Status}");
        }
    }

    private void ScheduleReconnect(string reason)
    {
        lock (_sync)
        {
            _reconnectAttempt++;
            var delay = (int)Math.Min(_settings.ReconnectMaxDelayMs, _settings.ReconnectBaseDelayMs * Math.Pow(2, _reconnectAttempt - 1));
            var jitter = _settings.ReconnectJitterMs > 0 ? Random.Shared.Next(0, _settings.ReconnectJitterMs) : 0;
            _nextReconnectUtc = DateTime.UtcNow.AddMilliseconds(delay + jitter);
            SetStatus($"OPC-UA: Reconnect in {delay / 1000:0}s ({reason})");
        }
    }

    private void SetStatus(string status)
    {
        _status = status;
        StatusChanged?.Invoke(status);
    }

    private static double ReadDouble(Session session, string nodeId, double fallback)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return fallback;
        }

        var value = session.ReadValue(nodeId).Value;
        if (value is null)
        {
            return fallback;
        }

        if (value is double direct)
        {
            return direct;
        }

        if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static async Task<Session> CreateSessionAsync(OpcUaSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.EndpointUrl))
        {
            throw new InvalidOperationException("OPC UA endpoint URL is required.");
        }

        var config = new ApplicationConfiguration
        {
            ApplicationName = "IndustrialSentinel",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                AutoAcceptUntrustedCertificates = true,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = settings.OperationTimeoutMs
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = settings.SessionTimeoutMs
            }
        };

        await config.Validate(ApplicationType.Client).ConfigureAwait(false);
        config.CertificateValidator.CertificateValidation += (_, e) => e.Accept = true;

        var selectedEndpoint = CoreClientUtils.SelectEndpoint(settings.EndpointUrl, settings.UseSecurity, settings.OperationTimeoutMs);
        var endpointConfiguration = EndpointConfiguration.Create(config);
        var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

        return await Session.Create(config, endpoint, false, "IndustrialSentinel", (uint)settings.SessionTimeoutMs, null, null).ConfigureAwait(false);
    }
}
