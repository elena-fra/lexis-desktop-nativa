namespace Lexis.Contracts.OrderFlow;

/// <summary>
/// Aggregated footprint cell per (bar, price level) — frozen contract (Mappa dipendenze §1).
/// Produced by aggregation engine; consumed by footprint, volume profile, studies.
/// </summary>
public sealed record FootprintCell(
    double Price,
    double BidVol,
    double AskVol,
    double Delta,
    double Total);

public sealed record FootprintBar(
    string Symbol,
    DateTimeOffset BarStart,
    DateTimeOffset BarEnd,
    double Open,
    double High,
    double Low,
    double Close,
    double BarDelta,
    double CumulativeDelta,
    IReadOnlyList<FootprintCell> Cells);
