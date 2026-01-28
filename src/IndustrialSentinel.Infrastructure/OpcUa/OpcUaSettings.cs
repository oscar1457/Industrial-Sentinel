namespace IndustrialSentinel.Infrastructure.OpcUa;

public sealed class OpcUaSettings
{
    public bool Enabled { get; init; }
    public string EndpointUrl { get; init; } = string.Empty;
    public string RpmNodeId { get; init; } = string.Empty;
    public string TemperatureNodeId { get; init; } = string.Empty;
    public string VibrationNodeId { get; init; } = string.Empty;
    public int OperationTimeoutMs { get; init; } = 2000;
    public int SessionTimeoutMs { get; init; } = 60000;
    public int KeepAliveIntervalMs { get; init; } = 10000;
    public int ReconnectBaseDelayMs { get; init; } = 1000;
    public int ReconnectMaxDelayMs { get; init; } = 30000;
    public int ReconnectJitterMs { get; init; } = 500;
    public bool UseSecurity { get; init; }
}
