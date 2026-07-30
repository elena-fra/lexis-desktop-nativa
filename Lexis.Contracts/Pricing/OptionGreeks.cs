namespace Lexis.Contracts.Pricing;

public static class GreeksSource
{
    public const string Broker = "broker";
    public const string Local = "local";
}

/// <summary>
/// Greeks/IV from broker tick or local engine. Copied from Lexis.Mexem.Gateway.Models.OptionGreeks.
/// </summary>
public sealed record OptionGreeks(
    double? ImpliedVol = null,
    double? Delta = null,
    double? Gamma = null,
    double? Vega = null,
    double? Theta = null,
    double? OptPrice = null,
    double? UndPrice = null,
    double? PvDividend = null,
    string Source = GreeksSource.Broker)
{
    public bool HasCoreFive =>
        ImpliedVol is > 0
        && Delta is not null
        && Gamma is not null
        && Vega is not null
        && Theta is not null;

    public bool HasAny =>
        ImpliedVol is not null || Delta is not null || Gamma is not null
        || Vega is not null || Theta is not null || OptPrice is not null
        || UndPrice is not null || PvDividend is not null;
}
