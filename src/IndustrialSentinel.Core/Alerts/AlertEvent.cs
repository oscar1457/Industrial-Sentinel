namespace IndustrialSentinel.Core.Alerts;

public sealed record AlertEvent(
    DateTime TimestampUtc,
    AlertLevel Level,
    string Metric,
    double Value,
    double Threshold,
    string Message);
