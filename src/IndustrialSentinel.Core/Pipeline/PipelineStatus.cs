namespace IndustrialSentinel.Core.Pipeline;

public sealed record PipelineStatus(
    DateTime TimestampUtc,
    int RawQueueDepth,
    int PersistQueueDepth,
    long DroppedSamples,
    long PersistDrops,
    double IngestRateHz,
    double ProcessRateHz,
    double PersistRateHz,
    double EndToEndLatencyMs,
    bool PersistenceHealthy);
