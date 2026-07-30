using Lexis.Contracts.Market;
using Lexis.Pricing;

namespace Lexis.Desktop.App.Services;

/// <summary>
/// Mock option chain — TOS-style underlyings / expiries, priced with Lexis.Pricing.
/// </summary>
public sealed class MockChainFeed
{
    private readonly OptionPricingEngine _engine;
    private readonly Random _rng = new(42);

    public static IReadOnlyList<UnderlyingInfo> Underlyings { get; } =
    [
        new("SPY", "SPDR S&P 500", 520.0, 5.0),
        new("QQQ", "Invesco QQQ", 445.0, 5.0),
        new("AAPL", "Apple", 198.0, 2.5),
        new("TSLA", "Tesla", 248.0, 2.5),
        new("SPX", "S&P 500 Index", 5280.0, 25.0),
    ];

    public MockChainFeed(OptionPricingEngine? engine = null) =>
        _engine = engine ?? new OptionPricingEngine();

    public IReadOnlyList<ExpiryDto> BuildExpiries(string symbol)
    {
        var asOf = DateOnly.FromDateTime(DateTime.Today);
        int[] dtes = [7, 14, 21, 35, 63];
        return dtes.Select(dte =>
        {
            var expiry = AddTradingDays(asOf, dte);
            return new ExpiryDto(
                L: expiry.ToString("dd MMM yyyy"),
                Dte: dte,
                T: dte <= 14 ? "weekly" : "monthly",
                D: expiry.ToString("yyyy-MM-dd"),
                Iv: 0.16 + dte * 0.0008);
        }).ToList();
    }

    public ChainDto Create(
        string symbol = "SPY",
        double? spotOverride = null,
        int strikeCount = 21,
        int? dte = null,
        string? expiryDate = null)
    {
        var u = Underlyings.FirstOrDefault(x => x.Symbol == symbol) ?? Underlyings[0];
        var spot = spotOverride ?? u.DefaultSpot;
        var expiries = BuildExpiries(u.Symbol);
        var exp = expiries.FirstOrDefault(e => expiryDate is not null && e.D == expiryDate)
                  ?? expiries.FirstOrDefault(e => dte is not null && e.Dte == dte)
                  ?? expiries[2];

        var asOf = DateOnly.FromDateTime(DateTime.Today);
        var expiry = DateOnly.Parse(exp.D);
        var step = u.StrikeStep;
        var atm = Math.Round(spot / step) * step;
        var half = strikeCount / 2;
        var rows = new List<ChainRowDto>(strikeCount);

        for (var i = -half; i <= half; i++)
        {
            var strike = atm + i * step;
            var moneyness = Math.Abs(strike - spot) / spot;
            var iv = 0.14 + moneyness * 0.55 + (_rng.NextDouble() - 0.5) * 0.02;
            rows.Add(new ChainRowDto(
                strike,
                BuildLeg(spot, strike, asOf, expiry, iv, "C"),
                BuildLeg(spot, strike, asOf, expiry, iv + 0.005, "P")));
        }

        return new ChainDto(u.Symbol, Math.Round(spot, 2), exp, expiries, rows);
    }

    public ChainDto Tick(ChainDto chain)
    {
        var spot = chain.Spot * (1.0 + (_rng.NextDouble() - 0.5) * 0.0008);
        var asOf = DateOnly.FromDateTime(DateTime.Today);
        var expiry = DateOnly.Parse(chain.Expiry.D);
        var rows = new List<ChainRowDto>(chain.Rows.Count);

        foreach (var row in chain.Rows)
        {
            var callIv = Math.Max(0.05, row.Call.Iv / 100.0 + (_rng.NextDouble() - 0.5) * 0.002);
            var putIv = Math.Max(0.05, row.Put.Iv / 100.0 + (_rng.NextDouble() - 0.5) * 0.002);
            rows.Add(new ChainRowDto(
                row.Strike,
                BuildLeg(spot, row.Strike, asOf, expiry, callIv, "C", row.Call.Vol, row.Call.Oi),
                BuildLeg(spot, row.Strike, asOf, expiry, putIv, "P", row.Put.Vol, row.Put.Oi)));
        }

        return chain with { Spot = Math.Round(spot, 2), Rows = rows };
    }

    private OptionLegQuoteDto BuildLeg(
        double spot, double strike, DateOnly asOf, DateOnly expiry, double iv,
        string right, int? vol = null, int? oi = null)
    {
        var g = _engine.Price(new PricingInputs(
            Spot: spot,
            Strike: strike,
            AsOf: asOf,
            Expiry: expiry,
            ImpliedVol: iv,
            Right: right,
            Style: OptionExerciseStyle.European));

        var mid = g.OptPrice ?? 0;
        var spread = Math.Max(0.01, mid * 0.02);
        return new OptionLegQuoteDto(
            Mid: Math.Round(mid, 2),
            Bid: Math.Round(Math.Max(0.01, mid - spread / 2), 2),
            Ask: Math.Round(mid + spread / 2, 2),
            Last: Math.Round(mid, 2),
            Vol: vol ?? _rng.Next(10, 2500),
            Oi: oi ?? _rng.Next(100, 8000),
            Iv: g.ImpliedVol ?? iv * 100,
            Delta: Math.Round(g.Delta ?? 0, 4),
            Gamma: Math.Round(g.Gamma ?? 0, 6),
            Theta: Math.Round(g.Theta ?? 0, 4),
            Vega: Math.Round(g.Vega ?? 0, 4));
    }

    private static DateOnly AddTradingDays(DateOnly start, int days)
    {
        var d = start;
        var left = days;
        while (left > 0)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                left--;
        }
        return d;
    }
}

public sealed record UnderlyingInfo(string Symbol, string Name, double DefaultSpot, double StrikeStep);
