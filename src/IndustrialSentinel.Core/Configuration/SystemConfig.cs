namespace IndustrialSentinel.Core.Configuration;

public sealed class SystemConfig
{
    public int BufferCapacity { get; init; } = 1024;
    public int IngestRateHz { get; init; } = 250;
    public int RawQueueCapacity { get; init; } = 2048;
    public int PersistQueueCapacity { get; init; } = 2048;
    public double SmoothingAlpha { get; init; } = 0.15;

    public double RpmMin { get; init; } = 900;
    public double RpmMax { get; init; } = 3200;
    public double TemperatureMin { get; init; } = 30;
    public double TemperatureMax { get; init; } = 95;
    public double VibrationMin { get; init; } = 0;
    public double VibrationMax { get; init; } = 6.0;

    public static SystemConfig Default() => new();
}
