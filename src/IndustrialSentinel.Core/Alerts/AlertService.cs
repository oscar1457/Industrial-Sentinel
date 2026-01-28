using IndustrialSentinel.Core.Configuration;
using IndustrialSentinel.Core.Telemetry;

namespace IndustrialSentinel.Core.Alerts;

public sealed class AlertService
{
    private readonly IReadOnlyList<AlertRule> _rules;

    public AlertService(SystemConfig config)
    {
        var rpmWarn = config.RpmMax * 0.9;
        var tempWarn = config.TemperatureMax * 0.9;
        var vibWarn = config.VibrationMax * 0.85;

        _rules = new List<AlertRule>
        {
            AlertRule.High("RPM", AlertLevel.Critical, config.RpmMax, f => f.RpmSmoothed, "RPM sobre limite critico"),
            AlertRule.High("RPM", AlertLevel.Warning, rpmWarn, f => f.RpmSmoothed, "RPM alto"),
            AlertRule.Low("RPM", AlertLevel.Warning, config.RpmMin, f => f.RpmSmoothed, "RPM bajo"),
            AlertRule.High("Temp", AlertLevel.Critical, config.TemperatureMax, f => f.TemperatureSmoothed, "Temperatura critica"),
            AlertRule.High("Temp", AlertLevel.Warning, tempWarn, f => f.TemperatureSmoothed, "Temperatura alta"),
            AlertRule.High("Vibration", AlertLevel.Critical, config.VibrationMax, f => f.VibrationSmoothed, "Vibracion critica"),
            AlertRule.High("Vibration", AlertLevel.Warning, vibWarn, f => f.VibrationSmoothed, "Vibracion alta")
        };
    }

    public IReadOnlyList<AlertEvent> Evaluate(TelemetryFrame frame)
    {
        var strongest = new Dictionary<string, AlertEvent>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in _rules)
        {
            var alert = rule.Evaluate(frame);
            if (alert is null)
            {
                continue;
            }

            if (strongest.TryGetValue(alert.Metric, out var existing))
            {
                if (alert.Level > existing.Level)
                {
                    strongest[alert.Metric] = alert;
                }
            }
            else
            {
                strongest[alert.Metric] = alert;
            }
        }

        return strongest.Values.ToList();
    }
}
