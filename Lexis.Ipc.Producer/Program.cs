using Lexis.Contracts.OrderFlow;
using Lexis.Ipc;

// C# reference / fallback producer. Preferred path: Lexis.Ipc.Producer.Native (C++ / libzmq).
// Same LX01 wire format + endpoint as the native PUB.

var endpoint = args.Length > 0 ? args[0] : IpcDefaults.Endpoint;
var symbol = args.Length > 1 ? args[1] : "SPY";
var ticksPerSec = args.Length > 2 && int.TryParse(args[2], out var tps) ? tps : 2000;

Console.WriteLine($"LEXIS IPC Producer (Fase 0)");
Console.WriteLine($"  endpoint = {endpoint}");
Console.WriteLine($"  symbol   = {symbol}");
Console.WriteLine($"  rate     = {ticksPerSec} tick/s");
Console.WriteLine("Ctrl+C to stop.");

using var publisher = new TradePublisher(endpoint);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var rng = new Random(7);
var price = 520.0;
var delayMs = Math.Max(1, 1000 / Math.Max(1, ticksPerSec));
var published = 0L;
var sw = System.Diagnostics.Stopwatch.StartNew();
var lastReport = sw.Elapsed;

try
{
    while (!cts.IsCancellationRequested)
    {
        price = Math.Max(1, price * (1.0 + (rng.NextDouble() - 0.5) * 0.0004));
        var size = rng.Next(1, 40);
        var side = rng.NextDouble() < 0.5 ? AggressorSide.Buy : AggressorSide.Sell;
        publisher.Publish(new TradeEvent(symbol, Math.Round(price, 2), size, DateTimeOffset.UtcNow, side));
        published++;

        if (sw.Elapsed - lastReport > TimeSpan.FromSeconds(1))
        {
            var sec = Math.Max(0.001, sw.Elapsed.TotalSeconds);
            Console.WriteLine($"seq≈{published}  last={price:F2}  ~{published / sec:F0} tick/s avg");
            lastReport = sw.Elapsed;
        }

        // Burst-friendly: sleep in chunks when rate is high
        if (ticksPerSec <= 1000)
            Thread.Sleep(delayMs);
        else if (published % (ticksPerSec / 200) == 0)
            Thread.Sleep(5);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

Console.WriteLine($"Stopped. Published {published} trades.");
return 0;
