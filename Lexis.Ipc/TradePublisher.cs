using Lexis.Contracts.OrderFlow;
using NetMQ;
using NetMQ.Sockets;

namespace Lexis.Ipc;

/// <summary>PUB socket — binds endpoint and publishes encoded trades (producer side).</summary>
public sealed class TradePublisher : IDisposable
{
    private readonly PublisherSocket _pub;
    private long _sequence;

    public TradePublisher(string? endpoint = null)
    {
        _pub = new PublisherSocket();
        _pub.Bind(endpoint ?? IpcDefaults.Endpoint);
        // Give slow joiners a moment (ZMQ PUB/SUB slow joiner).
        Thread.Sleep(100);
    }

    public long Publish(TradeEvent trade)
    {
        var seq = Interlocked.Increment(ref _sequence);
        var payload = IpcCodec.EncodeTrade(trade with { Sequence = seq }, seq);
        _pub.SendMoreFrame(IpcDefaults.TopicTrades).SendFrame(payload);
        return seq;
    }

    public void Dispose() => _pub.Dispose();
}
