using IndustrialSentinel.Core.Telemetry;

namespace IndustrialSentinel.Core.Alerts;

public enum ComparisonKind
{
    GreaterThan,
    LessThan
}

public sealed class AlertRule
{
    public AlertRule(
        string metric,
        AlertLevel level,
        double threshold,
        ComparisonKind comparison,
        Func<TelemetryFrame, double> valueSelector,
        string message)
    {
        Metric = metric;
        Level = level;
        Threshold = threshold;
        Comparison = comparison;
        ValueSelector = valueSelector;
        Message = message;
    }

    public string Metric { get; }
    public AlertLevel Level { get; }
    public double Threshold { get; }
    public ComparisonKind Comparison { get; }
    public Func<TelemetryFrame, double> ValueSelector { get; }
    public string Message { get; }

    public AlertEvent? Evaluate(TelemetryFrame frame)
    {
        var value = ValueSelector(frame);
        var triggered = Comparison switch
        {
            ComparisonKind.GreaterThan => value > Threshold,
            ComparisonKind.LessThan => value < Threshold,
            _ => false
        };

        if (!triggered)
        {
            return null;
        }

        return new AlertEvent(frame.TimestampUtc, Level, Metric, value, Threshold, Message);
    }

    public static AlertRule High(string metric, AlertLevel level, double threshold, Func<TelemetryFrame, double> selector, string message)
    {
        return new AlertRule(metric, level, threshold, ComparisonKind.GreaterThan, selector, message);
    }

    public static AlertRule Low(string metric, AlertLevel level, double threshold, Func<TelemetryFrame, double> selector, string message)
    {
        return new AlertRule(metric, level, threshold, ComparisonKind.LessThan, selector, message);
    }
}
