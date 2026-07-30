using Lexis.Contracts.OrderFlow;

namespace Lexis.Desktop.App.Services;

/// <summary>Simulated Time &amp; Sales prints with L1 bid/ask context (no live API).</summary>
public sealed class MockTimeSalesFeed
{
    private readonly Random _rng = new(91);
    private long _seq;
    private double _last;

    public string Symbol { get; private set; } = "SPY";
    public double TickSize { get; private set; } = 0.01;
    public double BestBid { get; private set; }
    public double BestAsk { get; private set; }
    public double BidSize { get; private set; }
    public double AskSize { get; private set; }
    public double SessionHigh { get; private set; }
    public double SessionLow { get; private set; }
    public double CumBuy { get; private set; }
    public double CumSell { get; private set; }

    public MockTimeSalesFeed() => Reset("SPY");

    public void Reset(string symbol)
    {
        Symbol = symbol.ToUpperInvariant();
        (TickSize, _last) = Symbol switch
        {
            "QQQ" => (0.01, 478.40),
            "AAPL" => (0.01, 214.55),
            "TSLA" => (0.01, 248.20),
            "SPX" => (0.25, 5620.00),
            "ES" => (0.25, 5622.50),
            _ => (0.01, 528.35),
        };
        BestBid = Round(_last - TickSize);
        BestAsk = Round(_last + TickSize);
        BidSize = BaseSize();
        AskSize = BaseSize();
        SessionHigh = _last;
        SessionLow = _last;
        CumBuy = 0;
        CumSell = 0;
        _seq = 0;
    }

    public TapePrint Next()
    {
        // Mild L1 jitter
        if (_rng.NextDouble() < 0.25)
        {
            BidSize = Math.Max(1, BidSize + _rng.Next(-40, 55));
            AskSize = Math.Max(1, AskSize + _rng.Next(-40, 55));
        }

        var liftAsk = _rng.NextDouble() < 0.5;
        AggressorSide agg;
        double px;
        if (liftAsk)
        {
            px = BestAsk;
            agg = AggressorSide.Buy;
            // occasionally walk ask
            if (_rng.NextDouble() < 0.18)
            {
                BestAsk = Round(BestAsk + TickSize);
                BestBid = Round(BestAsk - TickSize);
                AskSize = BaseSize();
            }
            else
                AskSize = Math.Max(1, AskSize - _rng.Next(1, 30));
        }
        else
        {
            px = BestBid;
            agg = AggressorSide.Sell;
            if (_rng.NextDouble() < 0.18)
            {
                BestBid = Round(BestBid - TickSize);
                BestAsk = Round(BestBid + TickSize);
                BidSize = BaseSize();
            }
            else
                BidSize = Math.Max(1, BidSize - _rng.Next(1, 30));
        }

        // Rare mid print
        if (_rng.NextDouble() < 0.06)
        {
            px = Round((BestBid + BestAsk) / 2.0);
            agg = AggressorSide.Unknown;
        }

        var size = RollSize();
        var tickDir = px > _last ? 1 : px < _last ? -1 : 0;
        _last = px;
        if (px > SessionHigh) SessionHigh = px;
        if (px < SessionLow) SessionLow = px;
        if (agg == AggressorSide.Buy) CumBuy += size;
        else if (agg == AggressorSide.Sell) CumSell += size;

        var trade = new TradeEvent(Symbol, px, size, DateTimeOffset.Now, agg, ++_seq);
        return new TapePrint(
            Trade: trade,
            Bid: BestBid,
            Ask: BestAsk,
            BidSize: BidSize,
            AskSize: AskSize,
            TickDir: tickDir,
            IsBlock: size >= BlockThreshold());
    }

    public IEnumerable<TapePrint> Seed(int n)
    {
        var list = new List<TapePrint>(n);
        for (var i = 0; i < n; i++)
            list.Add(Next());
        list.Reverse(); // oldest first then we insert newest at top in VM
        return list;
    }

    private double RollSize()
    {
        var u = _rng.NextDouble();
        var scale = Symbol is "ES" or "SPX" ? 1.0 : 2.2;
        if (u < 0.62) return Math.Max(1, Math.Round((1 + _rng.NextDouble() * 12) * scale));
        if (u < 0.88) return Math.Round((15 + _rng.NextDouble() * 60) * scale);
        if (u < 0.97) return Math.Round((80 + _rng.NextDouble() * 140) * scale);
        return Math.Round((250 + _rng.NextDouble() * 500) * scale); // block
    }

    private double BaseSize() => Symbol is "ES" or "SPX"
        ? 20 + _rng.Next(0, 80)
        : 80 + _rng.Next(0, 220);

    public double BlockThreshold() => Symbol is "ES" or "SPX" ? 80 : 200;

    public double Round(double px) =>
        Math.Round(px / TickSize, MidpointRounding.AwayFromZero) * TickSize;
}

public sealed record TapePrint(
    TradeEvent Trade,
    double Bid,
    double Ask,
    double BidSize,
    double AskSize,
    int TickDir,
    bool IsBlock);
