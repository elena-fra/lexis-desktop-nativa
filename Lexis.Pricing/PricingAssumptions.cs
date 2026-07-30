namespace Lexis.Pricing;

/// <summary>
/// Pricing assumptions — copied from Lexis.Mexem.Gateway.Pricing.PricingAssumptions (original untouched).
/// </summary>
public sealed class PricingAssumptions
{
    /// <summary>Continuous annualized risk-free rate (e.g. 0.045 = 4.5%).</summary>
    public double RiskFreeRate { get; set; } = 0.045;

    /// <summary>Continuous annualized dividend yield (q).</summary>
    public double DividendYield { get; set; } = 0.013;

    /// <summary>
    /// If &gt; 0, convert pvDividend to equivalent yield q ≈ -ln(1 - pv/S)/T;
    /// otherwise use <see cref="DividendYield"/>.
    /// </summary>
    public double? PvDividendOverride { get; set; }

    /// <summary>Trading days/year for DTE → years (default 252).</summary>
    public int TradingDaysPerYear { get; set; } = 252;

    /// <summary>Count only weekdays when converting DTE.</summary>
    public bool UseTradingCalendar { get; set; } = true;

    /// <summary>CRR tree steps for Americans.</summary>
    public int BinomialSteps { get; set; } = 100;

    public double DeltaWarnThreshold { get; set; } = 0.05;
    public double VegaThetaWarnThreshold { get; set; } = 0.10;
}

public enum OptionExerciseStyle
{
    European,
    American,
}

public sealed record PricingInputs(
    double Spot,
    double Strike,
    DateOnly AsOf,
    DateOnly Expiry,
    double ImpliedVol,
    string Right,
    OptionExerciseStyle Style = OptionExerciseStyle.European,
    double? RiskFreeRate = null,
    double? DividendYield = null,
    double? PvDividend = null);

public static class OptionRight
{
    /// <summary>Normalize to "C" or "P".</summary>
    public static string Normalize(string? right)
    {
        var r = (right ?? "").Trim().ToUpperInvariant();
        if (r is "C" or "CALL" or "CALLS") return "C";
        if (r is "P" or "PUT" or "PUTS") return "P";
        return r.Length > 0 && r[0] == 'C' ? "C" : r.Length > 0 && r[0] == 'P' ? "P" : r;
    }
}
