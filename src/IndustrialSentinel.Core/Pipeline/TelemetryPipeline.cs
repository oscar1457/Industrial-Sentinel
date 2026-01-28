using System.Collections.Concurrent;
using System.Diagnostics;
using IndustrialSentinel.Core.Alerts;
using IndustrialSentinel.Core.Configuration;
using IndustrialSentinel.Core.Processing;
using IndustrialSentinel.Core.Telemetry;

namespace IndustrialSentinel.Core.Pipeline;

public sealed class TelemetryPipeline : IDisposable
{
    private readonly SystemConfig _config;
    private readonly ITelemetrySource _source;
    private readonly TelemetryProcessor _processor;
    private readonly TelemetrySanitizer _sanitizer;
    private readonly AlertService _alertService;
    private readonly ITelemetryPersistence _persistence;
    private readonly bool _persistenceEnabled;
    private readonly RateCounter _ingestRate = new();
    private readonly RateCounter _processRate = new();
    private readonly RateCounter _persistRate = new();
    private BlockingCollection<TelemetrySample>? _rawQueue;
    private BlockingCollection<PersistItem>? _persistQueue;
    private CancellationTokenSource? _cts;
    private Thread? _ingestThread;
    private Thread? _processThread;
    private Thread? _persistThread;
    private long _droppedSamples;
    private long _persistDrops;
    private double _latencyEmaMs;
    private volatile bool _running;
    private volatile bool _persistenceHealthy = true;

    public TelemetryPipeline(
        SystemConfig config,
        ITelemetrySource source,
        TelemetryProcessor processor,
        AlertService alertService,
        ITelemetryPersistence persistence,
        bool persistenceEnabled = true)
    {
        _config = config;
        _source = source;
        _processor = processor;
        _sanitizer = new TelemetrySanitizer(config);
        _alertService = alertService;
        _persistence = persistence;
        _persistenceEnabled = persistenceEnabled;
    }

    public bool IsRunning => _running;

    public event Action<TelemetryFrame>? TelemetryProcessed;

    public event Action<AlertEvent>? AlertRaised;

    public event Action<Exception>? PipelineFaulted;

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _rawQueue = new BlockingCollection<TelemetrySample>(_config.RawQueueCapacity);
        _persistQueue = _persistenceEnabled
            ? new BlockingCollection<PersistItem>(_config.PersistQueueCapacity)
            : null;
        _persistenceHealthy = _persistenceEnabled;

        _ingestThread = new Thread(() => IngestLoop(_cts.Token))
        {
            IsBackground = true,
            Name = "TelemetryIngest"
        };

        _processThread = new Thread(() => ProcessLoop(_cts.Token))
        {
            IsBackground = true,
            Name = "TelemetryProcess"
        };

        if (_persistenceEnabled)
        {
            _persistThread = new Thread(() => PersistLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "TelemetryPersist"
            };
        }

        _running = true;
        _ingestThread.Start();
        _processThread.Start();
        _persistThread?.Start();
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _cts?.Cancel();
        _rawQueue?.CompleteAdding();
        _persistQueue?.CompleteAdding();

        _ingestThread?.Join(TimeSpan.FromSeconds(2));
        _processThread?.Join(TimeSpan.FromSeconds(2));
        _persistThread?.Join(TimeSpan.FromSeconds(2));

        _running = false;
    }

    public PipelineStatus SnapshotStatus()
    {
        return new PipelineStatus(
            DateTime.UtcNow,
            _rawQueue?.Count ?? 0,
            _persistQueue?.Count ?? 0,
            Interlocked.Read(ref _droppedSamples),
            Interlocked.Read(ref _persistDrops),
            _ingestRate.RateHz,
            _processRate.RateHz,
            _persistRate.RateHz,
            _latencyEmaMs,
            _persistenceHealthy);
    }

    private void IngestLoop(CancellationToken token)
    {
        try
        {
            var intervalTicks = (long)(Stopwatch.Frequency / (double)_config.IngestRateHz);
            var nextTick = Stopwatch.GetTimestamp();

            while (!token.IsCancellationRequested)
            {
                var sample = _source.ReadSample();
                _ingestRate.Mark();

                try
                {
                    if (!(_rawQueue?.TryAdd(sample, 5, token) ?? false))
                    {
                        Interlocked.Increment(ref _droppedSamples);
                    }
                }
                catch (InvalidOperationException)
                {
                    // adding has been completed; exit loop
                    break;
                }

                nextTick += intervalTicks;
                var sleepTicks = nextTick - Stopwatch.GetTimestamp();
                if (sleepTicks > 0)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(sleepTicks / (double)Stopwatch.Frequency));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            PipelineFaulted?.Invoke(ex);
        }
    }

    private void ProcessLoop(CancellationToken token)
    {
        if (_rawQueue is null || _persistQueue is null)
        {
            return;
        }

        try
        {
            foreach (var sample in _rawQueue.GetConsumingEnumerable(token))
            {
                var sanitized = _sanitizer.Sanitize(sample);
                UpdateLatency(sample.TimestampUtc);
                var frame = _processor.Process(sanitized);
                _processRate.Mark();
                TelemetryProcessed?.Invoke(frame);

                var alerts = _alertService.Evaluate(frame);
                if (alerts.Count > 0)
                {
                    foreach (var alert in alerts)
                    {
                        AlertRaised?.Invoke(alert);
                        EnqueuePersist(new AlertPersistItem(alert));
                    }
                }

                EnqueuePersist(new TelemetryPersistItem(frame));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            PipelineFaulted?.Invoke(ex);
        }
    }

    private void PersistLoop(CancellationToken token)
    {
        if (_persistQueue is null)
        {
            return;
        }

        try
        {
            foreach (var item in _persistQueue.GetConsumingEnumerable(token))
            {
                if (!_persistenceHealthy)
                {
                    continue;
                }

                try
                {
                    switch (item)
                    {
                        case TelemetryPersistItem telemetry:
                            _persistence.SaveTelemetry(telemetry.Frame);
                            _persistRate.Mark();
                            break;
                        case AlertPersistItem alert:
                            _persistence.SaveAlert(alert.Alert);
                            _persistRate.Mark();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _persistenceHealthy = false;
                    PipelineFaulted?.Invoke(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            PipelineFaulted?.Invoke(ex);
        }
    }

    private void EnqueuePersist(PersistItem item)
    {
        if (_persistQueue is null)
        {
            return;
        }

        if (!_persistenceHealthy)
        {
            Interlocked.Increment(ref _persistDrops);
            return;
        }

        try
        {
            if (!_persistQueue.TryAdd(item, 5))
            {
                Interlocked.Increment(ref _persistDrops);
            }
        }
        catch (InvalidOperationException)
        {
            Interlocked.Increment(ref _persistDrops);
        }
    }

    private void UpdateLatency(DateTime sampleTimestampUtc)
    {
        var latency = (DateTime.UtcNow - sampleTimestampUtc).TotalMilliseconds;
        if (_latencyEmaMs <= 0)
        {
            _latencyEmaMs = latency;
            return;
        }

        const double alpha = 0.2;
        _latencyEmaMs = alpha * latency + (1 - alpha) * _latencyEmaMs;
    }

    public void Dispose()
    {
        Stop();
        _source.Dispose();
        _persistence.Dispose();
    }

    private abstract record PersistItem;

    private sealed record TelemetryPersistItem(TelemetryFrame Frame) : PersistItem;

    private sealed record AlertPersistItem(AlertEvent Alert) : PersistItem;
}
