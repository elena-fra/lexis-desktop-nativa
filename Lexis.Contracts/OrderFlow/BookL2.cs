namespace Lexis.Contracts.OrderFlow;

/// <summary>
/// L2 book snapshot / delta — frozen contract (Mappa dipendenze §1).
/// Produced by ingestion; consumed by DOM, aggregation, heatmap.
/// </summary>
public sealed record BookLevel(double Price, double Size);

public sealed record BookL2Snapshot(
    string Symbol,
    long Sequence,
    DateTimeOffset Timestamp,
    IReadOnlyList<BookLevel> Bids,
    IReadOnlyList<BookLevel> Asks);

/// <summary>
/// Incremental book update. Apply in sequence order; gap → request resnapshot.
/// Size 0 means remove level.
/// </summary>
public sealed record BookL2Delta(
    string Symbol,
    long Sequence,
    DateTimeOffset Timestamp,
    IReadOnlyList<BookLevel> BidUpdates,
    IReadOnlyList<BookLevel> AskUpdates);
