using Lexis.Ipc;

// Quick verify: C++ PUB (libzmq) → .NET SUB (NetMQ) LX01 decode.
Console.WriteLine("IPC smoke: connecting SUB…");
await using var sub = new TradeSubscriber();
var count = 0;
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
try
{
    await foreach (var trade in sub.Trades.ReadAllAsync(cts.Token))
    {
        count++;
        if (count == 1)
            Console.WriteLine($"first: {trade.Symbol} {trade.Price} size={trade.Size} aggr={trade.Aggressor} seq={trade.Sequence}");
        if (count >= 50)
            break;
    }
}
catch (OperationCanceledException)
{
    // timeout
}

Console.WriteLine($"received={count}");
return count > 0 ? 0 : 2;
