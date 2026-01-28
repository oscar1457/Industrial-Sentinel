using IndustrialSentinel.Core.Configuration;
using IndustrialSentinel.Infrastructure.OpcUa;

namespace IndustrialSentinel.App.Configuration;

public sealed class AppSettings
{
    public string Profile { get; init; } = "simulation";
    public DatabaseSettings Database { get; init; } = new();
    public SystemConfig System { get; init; } = SystemConfig.Default();
    public SecuritySettings Security { get; init; } = SecuritySettings.Default();
    public OpcUaSettings OpcUa { get; init; } = new();

    public static AppSettings Default() => new();
}

public sealed class DatabaseSettings
{
    public string Path { get; init; } = "%LocalAppData%\\IndustrialSentinel\\industrial_sentinel.db";
    public int BusyTimeoutMs { get; init; } = 5000;
    public bool UseWal { get; init; } = true;
    public bool TelemetryEnabled { get; init; } = true;
}
