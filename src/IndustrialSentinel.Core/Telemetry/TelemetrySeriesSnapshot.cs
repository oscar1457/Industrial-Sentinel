namespace IndustrialSentinel.Core.Telemetry;

public sealed class TelemetrySeriesSnapshot
{
    public TelemetrySeriesSnapshot(double[] rpm, double[] temperature, double[] vibration, long[] ticks)
    {
        Rpm = rpm;
        Temperature = temperature;
        Vibration = vibration;
        Ticks = ticks;
    }

    public double[] Rpm { get; }
    public double[] Temperature { get; }
    public double[] Vibration { get; }
    public long[] Ticks { get; }
    public int Count => Rpm.Length;
}
