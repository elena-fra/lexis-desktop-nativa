namespace Lexis.Contracts.Oms;

/// <summary>
/// Order / fill / position contracts — frozen OMS surface (Mappa dipendenze §1).
/// Skeleton for FIX later; interim adapters (Mexem/paper) can map into these shapes.
/// </summary>
public enum OrderSide : byte { Buy = 1, Sell = 2 }

public enum OrderType : byte { Market = 1, Limit = 2, Stop = 3, StopLimit = 4 }

public enum OrderStatus : byte
{
    New = 0,
    Submitted = 1,
    PartiallyFilled = 2,
    Filled = 3,
    Cancelled = 4,
    Rejected = 5,
}

public sealed record OrderRequest(
    string Symbol,
    OrderSide Side,
    OrderType Type,
    double Quantity,
    double? LimitPrice = null,
    double? StopPrice = null,
    string? ClientOrderId = null);

public sealed record OrderState(
    string OrderId,
    string? ClientOrderId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    double Quantity,
    double FilledQuantity,
    double? LimitPrice,
    OrderStatus Status,
    DateTimeOffset UpdatedAt);

public sealed record FillEvent(
    string OrderId,
    string Symbol,
    OrderSide Side,
    double Price,
    double Quantity,
    DateTimeOffset Timestamp,
    string? ExecId = null);

public sealed record PositionDto(
    string Symbol,
    double Quantity,
    double AvgPrice,
    double? UnrealizedPnl = null);
