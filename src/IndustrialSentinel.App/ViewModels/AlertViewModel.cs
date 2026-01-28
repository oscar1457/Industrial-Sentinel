using IndustrialSentinel.Core.Alerts;

namespace IndustrialSentinel.App.ViewModels;

public sealed class AlertViewModel
{
    public AlertViewModel(AlertEvent alert)
    {
        Message = alert.Message;
        Metric = alert.Metric;
        Value = alert.Value;
        Level = alert.Level;
        Timestamp = alert.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
    }

    public string Message { get; }
    public string Metric { get; }
    public double Value { get; }
    public AlertLevel Level { get; }
    public string Timestamp { get; }
}
