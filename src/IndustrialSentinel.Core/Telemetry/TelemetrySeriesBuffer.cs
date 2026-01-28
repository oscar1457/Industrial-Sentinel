using IndustrialSentinel.Core.Utilities;

namespace IndustrialSentinel.Core.Telemetry;

public sealed class TelemetrySeriesBuffer
{
    private readonly RingBuffer<double> _rpm;
    private readonly RingBuffer<double> _temperature;
    private readonly RingBuffer<double> _vibration;
    private readonly RingBuffer<long> _ticks;
    private readonly object _sync = new();

    public TelemetrySeriesBuffer(int capacity)
    {
        _rpm = new RingBuffer<double>(capacity);
        _temperature = new RingBuffer<double>(capacity);
        _vibration = new RingBuffer<double>(capacity);
        _ticks = new RingBuffer<long>(capacity);
    }

    public void Add(TelemetryFrame frame)
    {
        lock (_sync)
        {
            _rpm.WriteUnsafe(frame.RpmSmoothed);
            _temperature.WriteUnsafe(frame.TemperatureSmoothed);
            _vibration.WriteUnsafe(frame.VibrationSmoothed);
            _ticks.WriteUnsafe(frame.TimestampUtc.Ticks);
        }
    }

    public TelemetrySeriesSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new TelemetrySeriesSnapshot(
                _rpm.SnapshotUnsafe(),
                _temperature.SnapshotUnsafe(),
                _vibration.SnapshotUnsafe(),
                _ticks.SnapshotUnsafe());
        }
    }
}
