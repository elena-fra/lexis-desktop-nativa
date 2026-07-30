using Lexis.Contracts.OrderFlow;

namespace Lexis.Desktop.App.Services;

/// <summary>Simulated L2 book + prints for the DOM ladder (no live API yet).</summary>
public sealed class MockDomFeed
{
    private readonly Random _rng = new(42);
    private readonly Dictionary<double, double> _bids = new();
    private readonly Dictionary<double, double> _asks = new();
    private readonly Dictionary<double, double> _volAtPrice = new();
    private long _seq;
    private double _mid;

    public string Symbol { get; private set; } = "SPY";
    public double TickSize { get; private set; } = 0.01;
    public double Last { get; private set; }
    public double LastSize { get; private set; }
    public AggressorSide LastAggressor { get; private set; } = AggressorSide.Unknown;
    public double BestBid { get; private set; }
    public double BestAsk { get; private set; }

    public IReadOnlyDictionary<double, double> VolAtPrice => _volAtPrice;

    public void Reset(string symbol)
    {
        Symbol = symbol.ToUpperInvariant();
        (TickSize, _mid) = Symbol switch
        {
            "QQQ" => (0.01, 478.40),
            "AAPL" => (0.01, 214.55),
            "TSLA" => (0.01, 248.20),
            "SPX" => (0.25, 5620.00),
            "ES" => (0.25, 5622.50),
            _ => (0.01, 528.35), // SPY
        };
        _bids.Clear();
        _asks.Clear();
        _volAtPrice.Clear();
        Last = Round(_mid);
        LastSize = 0;
        LastAggressor = AggressorSide.Unknown;
        SeedBook();
    }

    public MockDomFeed() => Reset("SPY");

    public BookL2Snapshot Snapshot()
    {
        var bids = _bids
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new BookLevel(kv.Key, kv.Value))
            .ToList();
        var asks = _asks
            .Where(kv => kv.Value > 0)
            .OrderBy(kv => kv.Key)
            .Select(kv => new BookLevel(kv.Key, kv.Value))
            .ToList();
        BestBid = bids.Count > 0 ? bids[0].Price : Last - TickSize;
        BestAsk = asks.Count > 0 ? asks[0].Price : Last + TickSize;
        return new BookL2Snapshot(Symbol, ++_seq, DateTimeOffset.UtcNow, bids, asks);
    }

    /// <summary>One simulation step: jitter liquidity, maybe print a trade.</summary>
    public TradeEvent? Tick()
    {
        JitterBook();
        if (_rng.NextDouble() > 0.55)
            return null;
        return PrintTrade();
    }

    private void SeedBook()
    {
        var bid0 = Round(Last - TickSize);
        var ask0 = Round(Last + TickSize);
        for (var i = 0; i < 40; i++)
        {
            var bp = Round(bid0 - i * TickSize);
            var ap = Round(ask0 + i * TickSize);
            _bids[bp] = SizeForDepth(i);
            _asks[ap] = SizeForDepth(i);
        }
        BestBid = bid0;
        BestAsk = ask0;
    }

    private double SizeForDepth(int i)
    {
        var baseSz = Symbol is "ES" or "SPX" ? 40 : 120;
        var wall = i is 3 or 7 or 12 ? 2.8 : 1.0;
        return Math.Max(1, Math.Round((baseSz / (1 + i * 0.35) + _rng.Next(0, 40)) * wall));
    }

    private void JitterBook()
    {
        MutateSide(_bids, below: true);
        MutateSide(_asks, below: false);

        // Occasional pull/stack near touch
        if (_rng.NextDouble() < 0.2 && _bids.Count > 0)
        {
            var p = Round(BestBid);
            if (_bids.TryGetValue(p, out var s))
                _bids[p] = Math.Max(0, s + _rng.Next(-25, 45));
        }
        if (_rng.NextDouble() < 0.2 && _asks.Count > 0)
        {
            var p = Round(BestAsk);
            if (_asks.TryGetValue(p, out var s))
                _asks[p] = Math.Max(0, s + _rng.Next(-25, 45));
        }
    }

    private void MutateSide(Dictionary<double, double> side, bool below)
    {
        if (side.Count == 0) return;
        var keys = side.Keys.ToList();
        var n = Math.Min(6, keys.Count);
        for (var i = 0; i < n; i++)
        {
            var k = keys[_rng.Next(keys.Count)];
            if (!side.TryGetValue(k, out var cur)) continue; // may already have been removed
            var next = Math.Max(0, cur + _rng.Next(-18, 22));
            if (next <= 0) side.Remove(k);
            else side[k] = next;
        }

        // Refill a hole so depth does not evaporate
        var anchor = below ? BestBid : BestAsk;
        for (var i = 0; i < 18; i++)
        {
            var p = Round(anchor + (below ? -i : i) * TickSize);
            if (!side.TryGetValue(p, out var sz) || sz <= 0)
                side[p] = SizeForDepth(i);
        }
    }

    private TradeEvent PrintTrade()
    {
        var liftAsk = _rng.NextDouble() < 0.5;
        double px;
        double hit;
        AggressorSide agg;
        if (liftAsk)
        {
            px = Round(BestAsk);
            hit = Math.Min(_asks.GetValueOrDefault(px, 10), 5 + _rng.Next(1, 80));
            _asks[px] = Math.Max(0, _asks.GetValueOrDefault(px) - hit);
            if (_asks.GetValueOrDefault(px) <= 0)
            {
                _asks.Remove(px);
                // walk the book
                BestAsk = Round(BestAsk + TickSize);
                if (!_asks.ContainsKey(BestAsk))
                    _asks[BestAsk] = SizeForDepth(0);
            }
            agg = AggressorSide.Buy;
        }
        else
        {
            px = Round(BestBid);
            hit = Math.Min(_bids.GetValueOrDefault(px, 10), 5 + _rng.Next(1, 80));
            _bids[px] = Math.Max(0, _bids.GetValueOrDefault(px) - hit);
            if (_bids.GetValueOrDefault(px) <= 0)
            {
                _bids.Remove(px);
                BestBid = Round(BestBid - TickSize);
                if (!_bids.ContainsKey(BestBid))
                    _bids[BestBid] = SizeForDepth(0);
            }
            agg = AggressorSide.Sell;
        }

        Last = px;
        LastSize = hit;
        LastAggressor = agg;
        _volAtPrice[px] = _volAtPrice.GetValueOrDefault(px) + hit;
        _mid = px;

        // Keep a one-tick spread around last
        if (BestAsk - BestBid < TickSize * 0.5)
        {
            BestBid = Round(px - TickSize);
            BestAsk = Round(px + TickSize);
        }

        return new TradeEvent(Symbol, px, hit, DateTimeOffset.UtcNow, agg, ++_seq);
    }

    public double Round(double px) =>
        Math.Round(px / TickSize, MidpointRounding.AwayFromZero) * TickSize;
}
