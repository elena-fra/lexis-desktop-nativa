using Lexis.Contracts.Market;
using Lexis.Contracts.Pricing;
using Lexis.Pricing;

namespace Lexis.Desktop.App.Services;

public interface IGreeksFeed
{
    string SourceLabel { get; }
    IReadOnlyList<UnderlyingInfo> Underlyings { get; }
    IReadOnlyList<ExpiryDto> BuildExpiries(string symbol);
    GreeksMatrixSnapshot Build(
        string symbol,
        string? expiryLabel,
        string side,
        int strikeCount);
}

/// <summary>Greche matrix from chain spot/strikes + Lexis.Pricing (core + ρ/vanna/charm/vomma).</summary>
public sealed class GreeksFeed : IGreeksFeed
{
    private readonly IChainFeed _chain;
    private readonly OptionPricingEngine _engine = new();

    public GreeksFeed(IChainFeed chain) => _chain = chain;

    public string SourceLabel => $"greche · {_chain.SourceLabel}";
    public IReadOnlyList<UnderlyingInfo> Underlyings => _chain.Underlyings;
    public IReadOnlyList<ExpiryDto> BuildExpiries(string symbol) => _chain.BuildExpiries(symbol);

    public GreeksMatrixSnapshot Build(
        string symbol,
        string? expiryLabel,
        string side,
        int strikeCount)
    {
        var n = strikeCount <= 0 ? 41 : Math.Clamp(strikeCount, 5, 41);
        if (n % 2 == 0) n++; // keep ATM centered

        var expiries = _chain.BuildExpiries(symbol);
        var exp = expiries.FirstOrDefault(e => expiryLabel is not null &&
                                               (e.L == expiryLabel || e.D == expiryLabel))
                  ?? expiries.FirstOrDefault(e => e.Dte >= 14)
                  ?? expiries[0];

        var chain = _chain.Create(symbol, null, n, exp.Dte, exp.D);
        var isCall = !string.Equals(side, "put", StringComparison.OrdinalIgnoreCase);
        var asOf = DateOnly.FromDateTime(DateTime.Today);
        var expiry = DateOnly.Parse(chain.Expiry.D);
        var step = MockChainFeed.Underlyings.FirstOrDefault(u => u.Symbol == chain.Symbol)?.StrikeStep ?? 5.0;

        var rows = new List<GreeksStrikeRow>(chain.Rows.Count);
        foreach (var row in chain.Rows)
        {
            var leg = isCall ? row.Call : row.Put;
            var iv = leg.Iv > 5 ? leg.Iv / 100.0 : leg.Iv;
            var g = PriceExtended(chain.Spot, row.Strike, asOf, expiry, iv, isCall ? "C" : "P");
            var atm = Math.Abs(row.Strike - chain.Spot) < step * 0.51;
            var itm = isCall ? row.Strike < chain.Spot : row.Strike > chain.Spot;
            rows.Add(new GreeksStrikeRow(
                Strike: row.Strike,
                IsAtm: atm,
                IsItm: itm,
                IvPct: g.ImpliedVol ?? leg.Iv,
                Price: g.OptPrice ?? leg.Mid,
                Delta: g.Delta ?? 0,
                Gamma: g.Gamma ?? 0,
                Theta: g.Theta ?? 0,
                Vega: g.Vega ?? 0,
                Rho: g.Rho ?? 0,
                Vanna: g.Vanna ?? 0,
                Charm: g.Charm ?? 0,
                Vomma: g.Vomma ?? 0));
        }

        return new GreeksMatrixSnapshot(
            Symbol: chain.Symbol,
            Spot: chain.Spot,
            Expiry: chain.Expiry,
            Expiries: chain.Expiries,
            Side: isCall ? "call" : "put",
            SourceLabel: SourceLabel,
            Rows: rows);
    }

    private ExtendedGreeks PriceExtended(
        double spot, double strike, DateOnly asOf, DateOnly expiry, double iv, string right)
    {
        var inputs = new PricingInputs(
            Spot: spot,
            Strike: strike,
            AsOf: asOf,
            Expiry: expiry,
            ImpliedVol: iv,
            Right: right,
            Style: OptionExerciseStyle.European);

        var g = _engine.Price(inputs);
        var t = _engine.YearsToExpiry(asOf, expiry);
        var r = _engine.Assumptions.RiskFreeRate;

        // ρ ≈ dP / d(r) for +1% rate bump
        var gUpR = _engine.Price(inputs with { RiskFreeRate = r + 0.01 });
        var rho = (gUpR.OptPrice ?? 0) - (g.OptPrice ?? 0);

        // Vanna / Vomma: finite diff on IV (±1%)
        var gUpV = _engine.Price(inputs with { ImpliedVol = iv + 0.01 });
        var gDnV = _engine.Price(inputs with { ImpliedVol = Math.Max(0.01, iv - 0.01) });
        var vanna = ((gUpV.Delta ?? 0) - (gDnV.Delta ?? 0)) / 2.0;
        var vomma = ((gUpV.Vega ?? 0) - (gDnV.Vega ?? 0)) / 2.0;

        // Charm: Δ change over ~1 day
        var asOfM = asOf.AddDays(1);
        if (asOfM >= expiry) asOfM = asOf;
        var gTm = _engine.Price(inputs with { AsOf = asOfM });
        var charm = (gTm.Delta ?? 0) - (g.Delta ?? 0);

        return new ExtendedGreeks(
            g.ImpliedVol,
            g.OptPrice,
            g.Delta,
            g.Gamma,
            g.Theta,
            g.Vega,
            Rho: rho,
            Vanna: vanna,
            Charm: charm,
            Vomma: vomma);
    }

    private sealed record ExtendedGreeks(
        double? ImpliedVol,
        double? OptPrice,
        double? Delta,
        double? Gamma,
        double? Theta,
        double? Vega,
        double? Rho,
        double? Vanna,
        double? Charm,
        double? Vomma);
}

public sealed record GreeksStrikeRow(
    double Strike,
    bool IsAtm,
    bool IsItm,
    double IvPct,
    double Price,
    double Delta,
    double Gamma,
    double Theta,
    double Vega,
    double Rho,
    double Vanna,
    double Charm,
    double Vomma);

public sealed record GreeksMatrixSnapshot(
    string Symbol,
    double Spot,
    ExpiryDto Expiry,
    IReadOnlyList<ExpiryDto> Expiries,
    string Side,
    string SourceLabel,
    IReadOnlyList<GreeksStrikeRow> Rows);
