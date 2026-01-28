namespace IndustrialSentinel.Core.Telemetry;

public sealed record TelemetryFrame(
    DateTime TimestampUtc,
    double Rpm,
    double TemperatureC,
    double VibrationMmS,
    double RpmSmoothed,
    double TemperatureSmoothed,
    double VibrationSmoothed);
