using IndustrialSentinel.Core.Configuration;
using IndustrialSentinel.Core.Telemetry;

namespace IndustrialSentinel.Core.Processing;

public sealed class TelemetrySanitizer
{
    private readonly SystemConfig _config;

    public TelemetrySanitizer(SystemConfig config)
    {
        _config = config;
    }

    public TelemetrySample Sanitize(TelemetrySample sample)
    {
        var rpm = Clamp(sample.Rpm, _config.RpmMin, _config.RpmMax);
        var temp = Clamp(sample.TemperatureC, _config.TemperatureMin, _config.TemperatureMax);
        var vib = Clamp(sample.VibrationMmS, _config.VibrationMin, _config.VibrationMax);
        return new TelemetrySample(sample.TimestampUtc, rpm, temp, vib);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return min;
        }

        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
