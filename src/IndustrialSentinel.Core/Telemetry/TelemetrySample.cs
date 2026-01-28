namespace IndustrialSentinel.Core.Telemetry;

public sealed record TelemetrySample(
    DateTime TimestampUtc,
    double Rpm,
    double TemperatureC,
    double VibrationMmS);
