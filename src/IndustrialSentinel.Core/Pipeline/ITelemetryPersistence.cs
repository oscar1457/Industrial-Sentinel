using IndustrialSentinel.Core.Alerts;
using IndustrialSentinel.Core.Telemetry;

namespace IndustrialSentinel.Core.Pipeline;

public interface ITelemetryPersistence : IDisposable
{
    void SaveTelemetry(TelemetryFrame frame);
    void SaveAlert(AlertEvent alert);
}
