using System.Reactive.Linq;
using System.Reactive.Subjects;
using Lexis.Contracts.OrderFlow;
using Lexis.Ipc;

namespace Lexis.Desktop.App.Services;

/// <summary>
/// Fase 0 IPC consumer: NetMQ SUB → channel → Rx stream coalesced for UI (~60fps).
/// </summary>
public sealed class IpcTradeFeed : IAsyncDisposable
{
    private readonly TradeSubscriber _subscriber;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;
    private readonly Subject<TradeEvent> _raw = new();
    private long _received;
    private long _lastSeq;
    private DateTimeOffset _started = DateTimeOffset.UtcNow;
    private TradeEvent? _last;

    public IObservable<TradeEvent> Trades => _raw.AsObservable();

    public IObservable<IpcFeedStats> Stats { get; }

    public IpcTradeFeed(string? endpoint = null)
    {
        _subscriber = new TradeSubscriber(endpoint);
        _pump = Task.Run(PumpAsync);

        Stats = Observable
            .Interval(TimeSpan.FromMilliseconds(250))
            .Select(_ => Snapshot())
            .DistinctUntilChanged();
    }

    public IObservable<TradeEvent> CoalescedTrades(TimeSpan sample) =>
        _raw.Sample(sample);

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var trade in _subscriber.Trades.ReadAllAsync(_cts.Token))
            {
                Interlocked.Increment(ref _received);
                _lastSeq = trade.Sequence ?? _lastSeq;
                _last = trade;
                _raw.OnNext(trade);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _raw.OnError(ex);
        }
    }

    private IpcFeedStats Snapshot()
    {
        var elapsed = Math.Max(0.001, (DateTimeOffset.UtcNow - _started).TotalSeconds);
        var n = Interlocked.Read(ref _received);
        return new IpcFeedStats(
            ConnectedHint: n > 0,
            Received: n,
            LastSequence: _lastSeq,
            TicksPerSecond: n / elapsed,
            LastPrice: _last?.Price,
            LastSize: _last?.Size,
            LastSymbol: _last?.Symbol,
            LastAggressor: _last?.Aggressor.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _pump.ConfigureAwait(false); } catch { /* ignore */ }
        await _subscriber.DisposeAsync().ConfigureAwait(false);
        _raw.OnCompleted();
        _raw.Dispose();
        _cts.Dispose();
    }
}

public readonly record struct IpcFeedStats(
    bool ConnectedHint,
    long Received,
    long LastSequence,
    double TicksPerSecond,
    double? LastPrice,
    double? LastSize,
    string? LastSymbol,
    string? LastAggressor);
