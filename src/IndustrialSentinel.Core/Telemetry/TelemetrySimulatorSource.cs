using IndustrialSentinel.Core.Telemetry;

namespace IndustrialSentinel.Core.Telemetry;

public sealed class TelemetrySimulatorSource : ITelemetrySource, ISourceStatus
{
    private readonly TelemetrySimulator _simulator;

    public TelemetrySimulatorSource(TelemetrySimulator simulator)
    {
        _simulator = simulator;
    }

    public string Status => "SIM";

    public event Action<string>? StatusChanged;

    public TelemetrySample ReadSample()
    {
        return _simulator.NextSample();
    }

    public void Dispose()
    {
    }
}
