using IndustrialSentinel.Core.Telemetry;
using Xunit;

namespace IndustrialSentinel.Tests;

public class TelemetrySimulatorTests
{
    [Fact]
    public void NextSample_ReturnsReasonableRanges()
    {
        var simulator = new TelemetrySimulator(seed: 42);
        var sample = simulator.NextSample();

        Assert.InRange(sample.Rpm, 500, 5000);
        Assert.InRange(sample.TemperatureC, 30, 130);
        Assert.InRange(sample.VibrationMmS, 0, 15);
    }

    [Fact]
    public void NextSample_TimestampsIncrease()
    {
        var simulator = new TelemetrySimulator(seed: 7);
        var first = simulator.NextSample();
        Thread.Sleep(5);
        var second = simulator.NextSample();

        Assert.True(second.TimestampUtc >= first.TimestampUtc);
    }
}
