namespace IndustrialSentinel.Core.Telemetry;

public interface ISourceStatus
{
    string Status { get; }
    event Action<string>? StatusChanged;
}
