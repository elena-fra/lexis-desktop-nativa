using System.Threading.Channels;
using Lexis.Contracts.OrderFlow;
using NetMQ;
using NetMQ.Sockets;

namespace Lexis.Ipc;

/// <summary>SUB socket — connects to producer and pushes decoded trades into a channel.</summary>
public sealed class TradeSubscriber : IAsyncDisposable
{
    private readonly SubscriberSocket _sub;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly Channel<TradeEvent> _channel;

    public ChannelReader<TradeEvent> Trades => _channel.Reader;

    public TradeSubscriber(string? endpoint = null)
    {
        _channel = Channel.CreateBounded<TradeEvent>(new BoundedChannelOptions(8192)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = true,
        });

        _sub = new SubscriberSocket();
        _sub.Connect(endpoint ?? IpcDefaults.Endpoint);
        _sub.Subscribe(IpcDefaults.TopicTrades);
        _loop = Task.Factory.StartNew(ReceiveLoop, TaskCreationOptions.LongRunning);
    }

    private void ReceiveLoop()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                // topic + payload
                if (!_sub.TryReceiveFrameString(TimeSpan.FromMilliseconds(200), out _))
                    continue;
                if (!_sub.TryReceiveFrameBytes(TimeSpan.FromMilliseconds(50), out var payload) || payload is null)
                    continue;

                if (!IpcCodec.TryDecodeTrade(payload, out var trade, out _))
                    continue;

                _channel.Writer.TryWrite(trade);
            }
        }
        catch (ObjectDisposedException)
        {
            // shutting down
        }
        finally
        {
            _channel.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _loop.ConfigureAwait(false); } catch { /* ignore */ }
        _sub.Dispose();
        _cts.Dispose();
    }
}
