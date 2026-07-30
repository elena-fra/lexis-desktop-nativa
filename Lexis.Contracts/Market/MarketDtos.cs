namespace Lexis.Contracts.Market;

/// <summary>
/// Market / options desk DTOs — copied from Lexis.DataAdapter.Core.Models for the desktop track.
/// Originals under Lexis.DataAdapter.Core are unchanged.
/// </summary>
public sealed record QuoteDto(
    string Symbol,
    string Name,
    double Spot,
    double ChangePct,
    double Bid,
    double Ask,
    double Iv,
    int IvRank,
    int IvPercentile);

public sealed record OptionLegQuoteDto(
    double Mid,
    double Bid,
    double Ask,
    double Last,
    int Vol,
    int Oi,
    double Iv,
    double Delta,
    double Gamma,
    double Theta,
    double Vega);

public sealed record ChainRowDto(double Strike, OptionLegQuoteDto Call, OptionLegQuoteDto Put);

public sealed record ExpiryDto(string L, int Dte, string T, string D, double Iv);

public sealed record ChainDto(
    string Symbol,
    double Spot,
    ExpiryDto Expiry,
    IReadOnlyList<ExpiryDto> Expiries,
    IReadOnlyList<ChainRowDto> Rows);

public sealed record DarkPoolLevelDto(double P, double V, string S);

public sealed record GexDto(
    string Symbol,
    string Metric,
    string Label,
    string Name,
    double Spot,
    double Flip,
    Dictionary<double, double> Data,
    double Net,
    double CallPutRatio,
    IReadOnlyList<DarkPoolLevelDto> DarkPool);

public sealed record VolatilityDto(string Symbol, double IvAtm, double Hv30, int IvRank, int IvPercentile);

public sealed record VolSurfaceDto(
    string Symbol,
    double Spot,
    IReadOnlyList<double> Strikes,
    IReadOnlyList<int> Dtes,
    IReadOnlyList<string> Labels,
    IReadOnlyList<IReadOnlyList<double>> IvMatrix,
    object Units,
    object Range,
    object? DataLinks = null);

public sealed record FlowRowDto(
    long Id,
    string Timestamp,
    string Ticker,
    string Sector,
    double Spot,
    string Type,
    string Side,
    string Exec,
    int Dte,
    string Exp,
    double Strike,
    int Size,
    int Oi,
    double Price,
    long Prem,
    bool Anomaly,
    string Sentiment,
    bool Golden,
    object? Confidence);

public sealed record FlowKpisDto(
    long FilteredPremium,
    double CallPutRatio,
    int Bullish,
    int Bearish,
    int Sweeps,
    int Blocks,
    int Golden,
    int Rows);

public sealed record FlowHeatStrikeDto(double Strike, int Call, int Put);

public sealed record FlowHeatmapDto(string Ticker, IReadOnlyList<FlowHeatStrikeDto> Strikes);
