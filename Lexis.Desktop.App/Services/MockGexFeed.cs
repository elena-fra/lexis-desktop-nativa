namespace Lexis.Desktop.App.Services;

/// <summary>Synthetic gamma / greek exposure profile by strike (desk mock · web-parity metrics).</summary>
public sealed class MockGexFeed
{
    private readonly Random _rng = new(17);

    public static IReadOnlyList<(string Key, string Label, string Name)> MetricCatalog { get; } =
    [
        ("gex", "GEX", "Gamma Exposure"),
        ("dex", "DEX", "Delta Exposure"),
        ("charma", "Charma", "Charm (decadimento Δ)"),
        ("vanna", "Vanna", "Vanna (Δ per vol)"),
        ("speed", "Speed", "Speed (dΓ/dS)"),
        ("vomma", "Vomma", "Vomma (dVega/dσ)"),
        ("net", "Netta", "Esposizione Netta Aggregata"),
    ];

    public static IReadOnlyList<(string Key, string Label)> Methods { get; } =
    [
        ("black-scholes", "Black-Scholes"),
        ("1", "Cox-Ross-Rubinstein"),
        ("2", "Monte Carlo"),
        ("3", "Boyle"),
        ("4", "Heston"),
        ("5", "Merton"),
        ("6", "FDM"),
    ];

    public static IReadOnlyList<(string Key, string Label)> Tabs { get; } =
    [
        ("macro", "Macro"),
        ("struct", "Strutturale"),
        ("levels", "Livelli"),
        ("term", "Scadenze"),
        ("deriv", "Derivate"),
        ("flow", "Flussi"),
    ];

    public string MetricLabel(string key) =>
        MetricCatalog.FirstOrDefault(m => m.Key == key).Label ?? "GEX";

    public string MetricName(string key) =>
        MetricCatalog.FirstOrDefault(m => m.Key == key).Name ?? "Gamma Exposure";

    public GexProfileSnapshot Build(string symbol, string metric)
    {
        var u = MockChainFeed.Underlyings.FirstOrDefault(x => x.Symbol == symbol)
                ?? MockChainFeed.Underlyings[0];
        var spot = u.DefaultSpot;
        var step = u.StrikeStep;
        var atm = Math.Round(spot / step) * step;
        var key = NormalizeMetric(metric);

        var levels = new List<GexLevel>(21);
        for (var i = -10; i <= 10; i++)
        {
            var strike = atm + i * step;
            var x = i / 10.0;
            double raw = key switch
            {
                "dex" => -180 * Math.Exp(-Math.Pow(x + 0.15, 2) * 2.2) + 420 * Math.Exp(-Math.Pow(x - 0.35, 2) * 2.5),
                "vanna" => -220 * Math.Exp(-Math.Pow(x + 0.4, 2) * 2) + 280 * Math.Exp(-Math.Pow(x - 0.45, 2) * 2),
                "charma" => 160 * Math.Exp(-Math.Pow(x + 0.5, 2) * 2.4) - 190 * Math.Exp(-Math.Pow(x - 0.4, 2) * 2.2),
                "speed" => -40 * Math.Exp(-Math.Pow(x + 0.3, 2) * 3) + 90 * Math.Exp(-Math.Pow(x, 2) * 4) - 70 * Math.Exp(-Math.Pow(x - 0.45, 2) * 3),
                "vomma" => 80 + 140 * Math.Pow(Math.Abs(x), 1.4),
                "net" => -320 * Math.Exp(-Math.Pow(x + 0.25, 2) * 1.8) + 520 * Math.Exp(-Math.Pow(x - 0.3, 2) * 1.9),
                _ => -480 * Math.Exp(-Math.Pow(x + 0.2, 2) * 2.1) + 390 * Math.Exp(-Math.Pow(x - 0.35, 2) * 2.3),
            };

            if (i == -4) raw -= key is "charma" or "speed" ? 40 : 160;
            if (i == 5) raw += key is "charma" or "speed" ? 30 : 140;

            var jitter = 1 + (_rng.NextDouble() - 0.5) * 0.08;
            levels.Add(new GexLevel(strike, Math.Round(raw * jitter, 1)));
        }

        var flip = atm - step * (key is "dex" ? 0.6 : 0.4);
        flip = Math.Round(flip / step) * step;

        var callWall = levels.OrderByDescending(l => l.Value).First().Strike;
        var putWall = levels.OrderBy(l => l.Value).First().Strike;
        var absMaxStrike = levels.OrderByDescending(l => Math.Abs(l.Value)).First().Strike;
        var net = levels.Sum(l => l.Value);
        var absMax = levels.Max(l => Math.Abs(l.Value));
        var pos = levels.Where(l => l.Value > 0).Sum(l => l.Value);
        var neg = levels.Where(l => l.Value < 0).Sum(l => Math.Abs(l.Value));
        var callPutRatio = (pos + neg) > 0 ? Math.Round(100.0 * pos / (pos + neg)) : 50;
        var delta24 = Math.Round(net * 0.08 + (_rng.NextDouble() - 0.5) * 40, 1);

        return new GexProfileSnapshot(
            Symbol: u.Symbol,
            Spot: spot,
            Step: step,
            Metric: key,
            MetricLabel: MetricLabel(key),
            MetricName: MetricName(key),
            Flip: flip,
            CallWall: callWall,
            PutWall: putWall,
            AbsGammaMax: absMaxStrike,
            VolTrigger: putWall - step,
            Net: Math.Round(net, 1),
            AbsMax: Math.Max(1, absMax),
            CallPutRatio: callPutRatio,
            Delta24h: delta24,
            Levels: levels);
    }

    public static string NormalizeMetric(string? key)
    {
        var k = (key ?? "gex").Trim().ToLowerInvariant();
        return k switch
        {
            "charm" => "charma",
            _ when MetricCatalog.Any(m => m.Key == k) => k,
            _ => "gex",
        };
    }
}

public sealed record GexLevel(double Strike, double Value);

public sealed record GexProfileSnapshot(
    string Symbol,
    double Spot,
    double Step,
    string Metric,
    string MetricLabel,
    string MetricName,
    double Flip,
    double CallWall,
    double PutWall,
    double AbsGammaMax,
    double VolTrigger,
    double Net,
    double AbsMax,
    double CallPutRatio,
    double Delta24h,
    IReadOnlyList<GexLevel> Levels,
    string MethodLabel = "Black-Scholes",
    string DataSource = "mock");
