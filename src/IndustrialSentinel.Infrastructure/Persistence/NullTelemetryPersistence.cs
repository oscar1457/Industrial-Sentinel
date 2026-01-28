using IndustrialSentinel.Core.Alerts;
using IndustrialSentinel.Core.Pipeline;
using IndustrialSentinel.Core.Telemetry;

namespace IndustrialSentinel.Infrastructure.Persistence;

public sealed class NullTelemetryPersistence : ITelemetryPersistence
{
    public void SaveTelemetry(TelemetryFrame frame)
    {
        // no-op
    }

    public void SaveAlert(AlertEvent alert)
    {
        // no-op
    }

    public void Dispose()
    {
    }
}
