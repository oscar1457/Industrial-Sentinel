using IndustrialSentinel.Core.Telemetry;

namespace IndustrialSentinel.Core.Processing;

public sealed class TelemetryProcessor
{
    private readonly double _alpha;
    private bool _initialized;
    private double _rpmSmoothed;
    private double _tempSmoothed;
    private double _vibrationSmoothed;

    public TelemetryProcessor(double alpha)
    {
        _alpha = Math.Clamp(alpha, 0.01, 1.0);
    }

    public TelemetryFrame Process(TelemetrySample sample)
    {
        if (!_initialized)
        {
            _rpmSmoothed = sample.Rpm;
            _tempSmoothed = sample.TemperatureC;
            _vibrationSmoothed = sample.VibrationMmS;
            _initialized = true;
        }
        else
        {
            _rpmSmoothed = _alpha * sample.Rpm + (1 - _alpha) * _rpmSmoothed;
            _tempSmoothed = _alpha * sample.TemperatureC + (1 - _alpha) * _tempSmoothed;
            _vibrationSmoothed = _alpha * sample.VibrationMmS + (1 - _alpha) * _vibrationSmoothed;
        }

        return new TelemetryFrame(
            sample.TimestampUtc,
            sample.Rpm,
            sample.TemperatureC,
            sample.VibrationMmS,
            _rpmSmoothed,
            _tempSmoothed,
            _vibrationSmoothed);
    }
}
