using System.Diagnostics;

namespace IndustrialSentinel.Core.Pipeline;

public sealed class RateCounter
{
    private long _count;
    private long _lastTick;
    private double _rate;

    public RateCounter()
    {
        _lastTick = Stopwatch.GetTimestamp();
    }

    public void Mark()
    {
        Interlocked.Increment(ref _count);
        var now = Stopwatch.GetTimestamp();
        var elapsed = (now - _lastTick) / (double)Stopwatch.Frequency;
        if (elapsed >= 1.0)
        {
            var count = Interlocked.Exchange(ref _count, 0);
            _rate = count / elapsed;
            _lastTick = now;
        }
    }

    public double RateHz => _rate;
}
