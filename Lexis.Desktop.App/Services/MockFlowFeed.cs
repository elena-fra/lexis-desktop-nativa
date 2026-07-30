using Lexis.Contracts.Market;

namespace Lexis.Desktop.App.Services;

/// <summary>
/// Synthetic option-flow tape for Avalonia Track D (mirrors web genFlowRow; no API).
/// </summary>
public sealed class MockFlowFeed
{
    private readonly Random _rng = new(7);
    private long _seq = 1;

    private static readonly (string Symbol, string Sector, double Spot)[] Tickers =
    [
        ("SPY", "ETF", 520),
        ("QQQ", "ETF", 445),
        ("AAPL", "Tech", 198),
        ("TSLA", "Auto", 248),
        ("SPX", "Index", 5280),
    ];

    public IReadOnlyList<FlowRowDto> Seed(int count = 48)
    {
        var rows = new List<FlowRowDto>(count);
        var sec = DateTime.Now.TimeOfDay.TotalSeconds;
        for (var i = 0; i < count; i++)
        {
            rows.Add(Next(sec));
            sec -= 1 + _rng.Next(0, 5);
        }
        return rows;
    }

    public FlowRowDto Next(double? timeSec = null)
    {
        var tk = Tickers[_rng.Next(Tickers.Length)];
        var spot = Math.Round(tk.Spot * (0.985 + _rng.NextDouble() * 0.03), 2);
        var type = _rng.NextDouble() < 0.5 ? "call" : "put";
        var r = _rng.NextDouble();
        var side = r < 0.46 ? "ASK" : r < 0.9 ? "BID" : "MID";
        var er = _rng.NextDouble();
        var exec = er < 0.45 ? "SWEEP" : er < 0.7 ? "BLOCK" : "SPLIT";
        var dtes = new[] { 0, 0, 1, 2, 3, 7, 7, 14, 21, 30, 60, 120 };
        var dte = dtes[_rng.Next(dtes.Length)];
        var exp = ExpLabel(dte);
        var step = spot > 2000 ? 25 : spot > 500 ? 10 : spot > 100 ? 5 : spot > 40 ? 2.5 : 1;
        var atm = Math.Round(spot / step) * step;
        var strike = Math.Round(atm + (_rng.Next(0, 9) - 4) * step, 2);
        var size = (int)(Math.Pow(_rng.NextDouble(), 2.3) * 4000) + 5;
        var oi = (int)(Math.Pow(_rng.NextDouble(), 1.4) * 30000) + 40;
        var dist = Math.Abs(strike - spot) / Math.Max(1, spot);
        var price = Math.Max(0.05,
            (0.4 + _rng.NextDouble() * 3) * (spot / 100.0) * (0.4 + dte / 120.0)
            * (type == "call" ? (strike < spot ? 1.4 : 0.7) : (strike > spot ? 1.4 : 0.7))
            * (1 - dist * 0.8));
        price = Math.Round(price, 2);
        var prem = (long)Math.Round(price * size * 100);
        var anomaly = size > oi;
        var sentiment = side == "MID"
            ? "neutral"
            : side == "ASK"
                ? (type == "call" ? "bullish" : "bearish")
                : (type == "call" ? "bearish" : "bullish");
        var golden = prem >= 1_000_000 && exec == "SWEEP" && dte <= 7;
        var conf = Math.Clamp(40 + (golden ? 35 : 0) + (exec == "SWEEP" ? 10 : 0) + (anomaly ? 8 : 0) - (int)(dist * 40), 15, 98);

        var ts = timeSec is { } s
            ? TimeSpan.FromSeconds(Math.Max(0, s))
            : DateTime.Now.TimeOfDay;

        return new FlowRowDto(
            Id: _seq++,
            Timestamp: $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}",
            Ticker: tk.Symbol,
            Sector: tk.Sector,
            Spot: spot,
            Type: type,
            Side: side,
            Exec: exec,
            Dte: dte,
            Exp: exp,
            Strike: strike,
            Size: size,
            Oi: oi,
            Price: price,
            Prem: prem,
            Anomaly: anomaly,
            Sentiment: sentiment,
            Golden: golden,
            Confidence: conf);
    }

    public static FlowKpisDto ComputeKpis(IEnumerable<FlowRowDto> rows)
    {
        var list = rows as IList<FlowRowDto> ?? rows.ToList();
        var callSize = list.Where(r => r.Type == "call").Sum(r => r.Size);
        var putSize = list.Where(r => r.Type == "put").Sum(r => r.Size);
        var ratio = putSize > 0 ? (double)callSize / putSize : callSize > 0 ? 99 : 0;
        return new FlowKpisDto(
            FilteredPremium: list.Sum(r => r.Prem),
            CallPutRatio: Math.Round(ratio, 2),
            Bullish: list.Count(r => r.Sentiment == "bullish"),
            Bearish: list.Count(r => r.Sentiment == "bearish"),
            Sweeps: list.Count(r => r.Exec == "SWEEP"),
            Blocks: list.Count(r => r.Exec == "BLOCK"),
            Golden: list.Count(r => r.Golden),
            Rows: list.Count);
    }

    private static string ExpLabel(int dte)
    {
        var d = DateOnly.FromDateTime(DateTime.Today).AddDays(dte);
        return d.ToString("dd MMM");
    }
}
