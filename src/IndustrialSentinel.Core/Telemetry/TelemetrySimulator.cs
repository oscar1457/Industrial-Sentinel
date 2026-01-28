using System.Diagnostics;

namespace IndustrialSentinel.Core.Telemetry;

public sealed class TelemetrySimulator
{
    private readonly Random _rng;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly double _rpmBase;
    private readonly double _tempBase;
    private readonly double _vibrationBase;

    public TelemetrySimulator(int? seed = null, double rpmBase = 1800, double tempBase = 70, double vibrationBase = 3.2)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        _rpmBase = rpmBase;
        _tempBase = tempBase;
        _vibrationBase = vibrationBase;
    }

    public TelemetrySample NextSample()
    {
        var t = _clock.Elapsed.TotalSeconds;

        var rpm = _rpmBase
            + 600 * Math.Sin(t * 0.65)
            + 250 * Math.Sin(t * 1.2)
            + Noise(35);

        var temp = _tempBase
            + 12 * Math.Sin(t * 0.2)
            + 4 * Math.Sin(t * 0.7)
            + Noise(0.6);

        var vibration = _vibrationBase
            + 1.2 * Math.Sin(t * 1.6)
            + 0.6 * Math.Sin(t * 2.1)
            + Noise(0.08);

        if (_rng.NextDouble() < 0.002)
        {
            rpm += 900;
            temp += 8;
            vibration += 2.5;
        }

        if (_rng.NextDouble() < 0.003)
        {
            vibration += 3.5;
        }

        return new TelemetrySample(DateTime.UtcNow, rpm, temp, Math.Max(0, vibration));
    }

    private double Noise(double amplitude)
    {
        return (_rng.NextDouble() * 2 - 1) * amplitude;
    }
}
