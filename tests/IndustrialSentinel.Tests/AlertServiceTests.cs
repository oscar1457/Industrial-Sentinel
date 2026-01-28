using IndustrialSentinel.Core.Alerts;
using IndustrialSentinel.Core.Configuration;
using IndustrialSentinel.Core.Telemetry;
using Xunit;

namespace IndustrialSentinel.Tests;

public class AlertServiceTests
{
    [Fact]
    public void Evaluate_WhenCriticalRpm_ReturnsCriticalAlert()
    {
        var config = new SystemConfig { RpmMax = 3000 };
        var service = new AlertService(config);
        var frame = new TelemetryFrame(DateTime.UtcNow, 3200, 70, 3, 3200, 70, 3);

        var alerts = service.Evaluate(frame);

        Assert.Contains(alerts, alert => alert.Metric == "RPM" && alert.Level == AlertLevel.Critical);
    }

    [Fact]
    public void Evaluate_WhenValuesNormal_ReturnsEmpty()
    {
        var config = SystemConfig.Default();
        var service = new AlertService(config);
        var frame = new TelemetryFrame(DateTime.UtcNow, 1800, 70, 3, 1800, 70, 3);

        var alerts = service.Evaluate(frame);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_PrefersCriticalOverWarningForSameMetric()
    {
        var config = new SystemConfig { RpmMax = 3000 };
        var service = new AlertService(config);
        var frame = new TelemetryFrame(DateTime.UtcNow, 4000, 70, 3, 4000, 70, 3);

        var alerts = service.Evaluate(frame);

        var rpmAlert = Assert.Single(alerts, alert => alert.Metric == "RPM");
        Assert.Equal(AlertLevel.Critical, rpmAlert.Level);
    }
}
