using System.Threading;
using IndustrialSentinel.Core.Alerts;
using IndustrialSentinel.Core.Configuration;
using IndustrialSentinel.Core.Pipeline;
using IndustrialSentinel.Core.Processing;
using IndustrialSentinel.Core.Telemetry;
using IndustrialSentinel.Infrastructure.Persistence;
using Xunit;

namespace IndustrialSentinel.Tests;

public class PipelineTests
{
    [Fact]
    public void Pipeline_StartsAndProcessesSamples()
    {
        var config = new SystemConfig
        {
            IngestRateHz = 50,
            BufferCapacity = 256,
            RawQueueCapacity = 256,
            PersistQueueCapacity = 256
        };

        using var source = new TestSource();
        using var persistence = new NullTelemetryPersistence();
        var pipeline = new TelemetryPipeline(
            config,
            source,
            new TelemetryProcessor(config.SmoothingAlpha),
            new AlertService(config),
            persistence);

        var processed = 0;
        pipeline.TelemetryProcessed += _ => Interlocked.Increment(ref processed);

        pipeline.Start();
        Thread.Sleep(250);
        pipeline.Stop();

        Assert.True(processed > 0);
    }

    private sealed class TestSource : ITelemetrySource
    {
        public TelemetrySample ReadSample()
        {
            return new TelemetrySample(DateTime.UtcNow, 1800, 70, 3);
        }

        public void Dispose()
        {
        }
    }
}
