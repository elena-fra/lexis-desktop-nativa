namespace Lexis.Contracts.OrderFlow;

/// <summary>
/// Normalized trade event — frozen contract (Mappa dipendenze §1).
/// Produced by ingestion; consumed by aggregation, footprint, profile, time &amp; sales.
/// </summary>
public enum AggressorSide : byte
{
    Unknown = 0,
    Buy = 1,  // lift the ask
    Sell = 2, // hit the bid
}

public sealed record TradeEvent(
    string Symbol,
    double Price,
    double Size,
    DateTimeOffset Timestamp,
    AggressorSide Aggressor,
    long? Sequence = null);
