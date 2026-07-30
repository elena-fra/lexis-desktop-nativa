namespace Lexis.Contracts.Ui;

/// <summary>
/// Shared price axis &amp; viewport — frozen contract (Mappa dipendenze §1).
/// Used by price chart, footprint, DOM, and profile so pan/zoom stay synchronized.
/// </summary>
public sealed record PriceViewport(
    double MinPrice,
    double MaxPrice,
    double TickSize,
    double PixelHeight);

public static class PriceAxis
{
    /// <summary>Map price → Y (top = high price).</summary>
    public static double PriceToY(double price, in PriceViewport vp)
    {
        var range = vp.MaxPrice - vp.MinPrice;
        if (range <= 0 || vp.PixelHeight <= 0) return 0;
        return (vp.MaxPrice - price) / range * vp.PixelHeight;
    }

    /// <summary>Map Y → price (for hit-testing / click-to-trade).</summary>
    public static double YToPrice(double y, in PriceViewport vp)
    {
        var range = vp.MaxPrice - vp.MinPrice;
        if (vp.PixelHeight <= 0) return vp.MinPrice;
        var price = vp.MaxPrice - y / vp.PixelHeight * range;
        if (vp.TickSize <= 0) return price;
        return Math.Round(price / vp.TickSize) * vp.TickSize;
    }
}
