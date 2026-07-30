using Lexis.Pricing;

namespace Lexis.Desktop.App.Services;

public interface IPortfolioFeed
{
    string SourceLabel { get; }
    PortfolioSnapshot Build(PortfolioQuery query);
}

public sealed record PortfolioQuery(string AccountType, string Range);

public sealed record PortfolioPositionDto(
    string GroupId,
    string Instrument,
    string Symbol,
    string Right,
    double Strike,
    string Side,
    int Qty,
    int Dte,
    string ExpiryLabel,
    double Entry,
    double Mark,
    double Spot,
    double Beta,
    double DeltaDollar,
    double Gamma,
    double ThetaDay,
    double Vega,
    double BetaWeightedDelta,
    double Pl);

public sealed record PortfolioUnderlyingExpoDto(
    string Symbol,
    double Spot,
    double Beta,
    int Legs,
    double DeltaDollar,
    double BetaWeightedDelta,
    double RiskPct);

public sealed record PortfolioSnapshot(
    bool IsDemo,
    string AccountNumber,
    string Range,
    double Equity,
    double Cash,
    double OpenPl,
    double OptionsValue,
    double MarginUsed,
    double BuyingPower,
    double MarginUtilPct,
    string RiskLabel,
    string RiskTone,
    double NetBetaWeightedDelta,
    double NetDeltaDollar,
    double NetGamma,
    double NetThetaDay,
    double NetVega,
    double GrossNotional,
    double Spx1Pt,
    double Spx1Pct,
    double EquityChange,
    double EquityChangePct,
    IReadOnlyList<double> EquitySeries,
    IReadOnlyList<PortfolioUnderlyingExpoDto> ByUnderlying,
    IReadOnlyList<PortfolioPositionDto> Positions,
    string SourceLabel);

/// <summary>Mock portfolio ledger — web Portafoglio / Greche-di-posizione parity until live OMS.</summary>
public sealed class MockPortfolioFeed : IPortfolioFeed
{
    private static readonly (string Sym, double Spot, double Beta)[] Unders =
    [
        ("SPX", 5280, 1.00),
        ("SPY", 520, 1.00),
        ("QQQ", 445, 1.15),
        ("AAPL", 198, 1.20),
        ("TSLA", 248, 1.80),
    ];

    public string SourceLabel => "portafoglio · mock";

    public PortfolioSnapshot Build(PortfolioQuery query)
    {
        var demo = !string.Equals(query.AccountType, "real", StringComparison.OrdinalIgnoreCase);
        var spySpot = Unders.First(u => u.Sym == "SPY").Spot;
        var positions = BuildPositions(demo, spySpot);

        var cash = demo ? 51_800.0 : 38_200.0;
        var optsLiq = positions.Sum(p =>
        {
            var dir = p.Side == "LONG" ? 1 : -1;
            return dir * p.Mark * p.Qty * 100;
        });
        var openPl = positions.Sum(p => p.Pl);
        var equity = cash + Math.Abs(optsLiq);
        // Prefer mark-to-market style: cash + options liquidation value
        equity = cash + optsLiq;
        var marginUsed = demo ? 12_400.0 : 41_200.0;
        if (positions.Count == 0) marginUsed = 0;
        var bp = equity - marginUsed;
        var util = equity > 0 ? marginUsed / equity * 100.0 : 0;
        var (riskLabel, riskTone) = util >= 70
            ? ("ELEVATO — vicino a margin call", "bad")
            : util >= 45
                ? ("MODERATO — monitorare", "warn")
                : ("BASSO — ampio margine", "ok");

        var netBwd = positions.Sum(p => p.BetaWeightedDelta);
        var dDollar = positions.Sum(p => p.DeltaDollar * p.Spot);
        var gamma = positions.Sum(p => p.Gamma);
        var theta = positions.Sum(p => p.ThetaDay);
        var vega = positions.Sum(p => p.Vega);
        var gross = positions.Sum(p => Math.Abs(p.DeltaDollar * p.Spot));

        var byU = positions
            .GroupBy(p => p.Symbol)
            .Select(g =>
            {
                var u = Unders.FirstOrDefault(x => x.Sym == g.Key);
                var spot = u.Spot > 0 ? u.Spot : g.First().Spot;
                var beta = u.Beta > 0 ? u.Beta : 1;
                var dd = g.Sum(x => x.DeltaDollar * x.Spot);
                var bwd = g.Sum(x => x.BetaWeightedDelta);
                return new { Sym = g.Key, Spot = spot, Beta = beta, Legs = g.Count(), Dd = dd, Bwd = bwd };
            })
            .OrderByDescending(x => Math.Abs(x.Bwd))
            .ToList();
        var totAbs = Math.Max(1, byU.Sum(x => Math.Abs(x.Bwd)));
        var expos = byU.Select(x => new PortfolioUnderlyingExpoDto(
            x.Sym, x.Spot, x.Beta, x.Legs, Math.Round(x.Dd, 0), Math.Round(x.Bwd, 0),
            Math.Round(Math.Abs(x.Bwd) / totAbs * 100, 0))).ToList();

        var series = BuildSeries(query.Range, equity, demo);
        var start = series[0];
        var chg = equity - start;
        var chgPct = start != 0 ? chg / start * 100.0 : 0;
        var spxSpot = Unders.First(u => u.Sym == "SPX").Spot;

        return new PortfolioSnapshot(
            IsDemo: demo,
            AccountNumber: demo ? "LX-48817-DEMO" : "LX-90233-LIVE",
            Range: query.Range,
            Equity: Math.Round(equity, 0),
            Cash: Math.Round(cash, 0),
            OpenPl: Math.Round(openPl, 0),
            OptionsValue: Math.Round(optsLiq, 0),
            MarginUsed: Math.Round(marginUsed, 0),
            BuyingPower: Math.Round(bp, 0),
            MarginUtilPct: Math.Round(util, 1),
            RiskLabel: riskLabel,
            RiskTone: riskTone,
            NetBetaWeightedDelta: Math.Round(netBwd, 0),
            NetDeltaDollar: Math.Round(dDollar, 0),
            NetGamma: Math.Round(gamma, 2),
            NetThetaDay: Math.Round(theta, 0),
            NetVega: Math.Round(vega, 0),
            GrossNotional: Math.Round(gross, 0),
            Spx1Pt: Math.Round(netBwd, 0),
            Spx1Pct: Math.Round(netBwd * spxSpot * 0.01, 0),
            EquityChange: Math.Round(chg, 0),
            EquityChangePct: Math.Round(chgPct, 2),
            EquitySeries: series,
            ByUnderlying: expos,
            Positions: positions,
            SourceLabel: SourceLabel);
    }

    private static List<PortfolioPositionDto> BuildPositions(bool demo, double spySpot)
    {
        // Seeded open book — same language as web mock positions / dashboard recent trades.
        var specs = demo
            ? new[]
            {
                ("g1", "SPX 5280 C", "SPX", "call", 5280.0, "LONG", 2, 21, 18.40, 21.60),
                ("g1", "SPX 5325 C", "SPX", "call", 5325.0, "SHORT", 2, 21, 9.20, 8.10),
                ("g2", "SPY 520 P", "SPY", "put", 520.0, "SHORT", 5, 14, 3.10, 2.55),
                ("g3", "QQQ 445 C", "QQQ", "call", 445.0, "LONG", 3, 35, 6.80, 7.40),
                ("g4", "AAPL 200 C", "AAPL", "call", 200.0, "LONG", 4, 28, 4.20, 5.05),
                ("g5", "TSLA 250 P", "TSLA", "put", 250.0, "SHORT", 2, 10, 7.50, 6.90),
            }
            : new[]
            {
                ("g1", "SPX 5300 C", "SPX", "call", 5300.0, "LONG", 4, 14, 22.10, 19.80),
                ("g2", "SPY 515 P", "SPY", "put", 515.0, "LONG", 8, 21, 4.40, 5.20),
                ("g3", "QQQ 450 C", "QQQ", "call", 450.0, "SHORT", 6, 7, 5.10, 6.30),
                ("g4", "AAPL 195 C", "AAPL", "call", 195.0, "LONG", 10, 45, 8.20, 9.10),
            };

        var list = new List<PortfolioPositionDto>();
        foreach (var (gid, ins, sym, right, k, side, qty, dte, entry, mark) in specs)
        {
            var u = Unders.FirstOrDefault(x => x.Sym == sym);
            var spot = u.Spot > 0 ? u.Spot : 100;
            var beta = u.Beta > 0 ? u.Beta : 1;
            var T = Math.Max(0.5, dte) / 365.0;
            var iv = Math.Max(0.12, 0.18 + Math.Abs(k - spot) / Math.Max(spot, 1) * 0.4);
            var g = BlackScholes.Calculate(spot, k, T, iv, right, OptionModelEngines.DefaultR, OptionModelEngines.DefaultQ);
            var dir = side == "LONG" ? 1 : -1;
            var mult = dir * qty * 100.0;
            var deltaDollar = g.Delta * mult;
            // Web Greche rail: Δ$ = sgn*q*delta*100 (share-eq), then β-w = Δ$ * beta * (spot/spy)
            // Portfolio page uses same Δ$ then * spot for dollar delta — we store share-eq as DeltaDollar
            // and dollar exposure separately in NetDeltaDollar via * spot when aggregating for portfolio page.
            var deltaShares = g.Delta * mult;
            var bwd = deltaShares * beta * (spot / spySpot);
            var pl = (mark - entry) * mult;
            list.Add(new PortfolioPositionDto(
                GroupId: gid,
                Instrument: ins,
                Symbol: sym,
                Right: right,
                Strike: k,
                Side: side,
                Qty: qty,
                Dte: dte,
                ExpiryLabel: $"{dte} DTE",
                Entry: entry,
                Mark: mark,
                Spot: spot,
                Beta: beta,
                DeltaDollar: Math.Round(deltaShares, 0),
                Gamma: Math.Round(g.Gamma * mult, 3),
                ThetaDay: Math.Round(g.Theta * mult, 0),
                Vega: Math.Round(g.Vega * mult, 1),
                BetaWeightedDelta: Math.Round(bwd, 0),
                Pl: Math.Round(pl, 0)));
        }

        return list;
    }

    private static IReadOnlyList<double> BuildSeries(string range, double equity, bool demo)
    {
        var n = range.ToUpperInvariant() switch
        {
            "1G" => 24,
            "1S" => 40,
            "YTD" => 80,
            _ => 48,
        };
        var seed = demo ? 19 : 53;
        var rng = new Random(seed + range.GetHashCode());
        var pts = new double[n];
        var v = equity * (demo ? 0.92 : 0.88);
        for (var i = 0; i < n - 1; i++)
        {
            v *= 1.0 + (rng.NextDouble() - 0.45) * (demo ? 0.012 : 0.018);
            pts[i] = Math.Round(v, 0);
        }
        pts[^1] = equity;
        return pts;
    }
}
