using Lexis.Contracts.Market;
using Lexis.Pricing;

namespace Lexis.Desktop.App.Services;

public interface IGexFeed
{
    string SourceLabel { get; }
    IReadOnlyList<(string Key, string Label, string Name)> MetricCatalog { get; }
    GexProfileSnapshot Build(string symbol, string metric, string? method = null);
}

/// <summary>Chain-based GEX engine (API algorithms) with mock chain fallback.</summary>
public sealed class ChainGexFeed : IGexFeed
{
    private readonly IChainFeed _chain;
    private readonly object _gate = new();
    private string? _lastKey;
    private GexProfileEngine.ComputedBundle? _lastBundle;

    public ChainGexFeed(IChainFeed chain) => _chain = chain;

    public string SourceLabel => $"gex · {_chain.SourceLabel} · engine";
    public IReadOnlyList<(string Key, string Label, string Name)> MetricCatalog => GexProfileEngine.Metrics;

    public GexProfileSnapshot Build(string symbol, string metric, string? method = null)
    {
        var key = MockGexFeed.NormalizeMetric(metric);
        var methodKey = method ?? "black-scholes";
        var chain = _chain.Create(symbol, null, 21, null, null);
        var bundle = GetBundle(chain, methodKey);
        if (!bundle.Profiles.TryGetValue(key, out var profile))
            profile = bundle.Profiles["gex"];

        var levels = profile.Data
            .OrderBy(kv => kv.Key)
            .Select(kv => new GexLevel(kv.Key, kv.Value))
            .ToList();

        if (levels.Count == 0)
            return new MockGexFeed().Build(symbol, key);

        var step = levels.Count > 1
            ? levels.Zip(levels.Skip(1), (a, b) => b.Strike - a.Strike).Where(x => x > 0).DefaultIfEmpty(5).Min()
            : 5;
        var callWall = levels.OrderByDescending(l => l.Value).First().Strike;
        var putWall = levels.OrderBy(l => l.Value).First().Strike;
        var absMaxStrike = levels.OrderByDescending(l => Math.Abs(l.Value)).First().Strike;
        var net = levels.Sum(l => l.Value);
        var absMax = Math.Max(1, levels.Max(l => Math.Abs(l.Value)));
        var pos = levels.Where(l => l.Value > 0).Sum(l => l.Value);
        var neg = levels.Where(l => l.Value < 0).Sum(l => Math.Abs(l.Value));
        var callPutRatio = (pos + neg) > 0 ? Math.Round(100.0 * pos / (pos + neg)) : 50;
        var delta24 = Math.Round(net * 0.08, 1);
        var src = bundle.UsedLiveOi
            ? $"chain-oi · {bundle.LiveOiContracts} live"
            : $"synthetic-oi · {bundle.SyntheticOiContracts} synth";

        return new GexProfileSnapshot(
            Symbol: chain.Symbol,
            Spot: bundle.Spot,
            Step: step,
            Metric: key,
            MetricLabel: profile.Label,
            MetricName: profile.Name,
            Flip: profile.Flip,
            CallWall: callWall,
            PutWall: putWall,
            AbsGammaMax: absMaxStrike,
            VolTrigger: putWall - step,
            Net: Math.Round(net, 1),
            AbsMax: absMax,
            CallPutRatio: callPutRatio,
            Delta24h: delta24,
            Levels: levels,
            MethodLabel: bundle.MethodLabel,
            DataSource: src);
    }

    private GexProfileEngine.ComputedBundle GetBundle(ChainDto chain, string methodKey)
    {
        // Reuse last bundle when switching metric on same symbol/method/chain snapshot.
        var cacheKey = $"{chain.Symbol}|{methodKey}|{chain.Spot:F2}|{chain.Expiry.Dte}|{chain.Rows.Count}";
        lock (_gate)
        {
            if (_lastKey == cacheKey && _lastBundle is not null)
                return _lastBundle;
            _lastBundle = GexProfileEngine.ComputeFromChain(chain, methodKey);
            _lastKey = cacheKey;
            return _lastBundle;
        }
    }
}

/// <summary>Legacy synthetic shapes — kept as emergency fallback.</summary>
public sealed class MockGexFeedAdapter : IGexFeed
{
    private readonly MockGexFeed _inner = new();
    public string SourceLabel => "gex · mock-shape";
    public IReadOnlyList<(string Key, string Label, string Name)> MetricCatalog => MockGexFeed.MetricCatalog;
    public GexProfileSnapshot Build(string symbol, string metric, string? method = null)
    {
        var snap = _inner.Build(symbol, metric);
        var methodLabel = MockGexFeed.Methods.FirstOrDefault(m => m.Key == (method ?? "black-scholes")).Label
                          ?? "Black-Scholes";
        return snap with { MethodLabel = methodLabel, DataSource = "mock-shape" };
    }
}
