namespace IndustrialSentinel.Core.Telemetry;

public interface ITelemetrySource : IDisposable
{
    TelemetrySample ReadSample();
}
