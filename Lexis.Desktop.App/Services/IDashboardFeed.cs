namespace Lexis.Desktop.App.Services;

public interface IDashboardFeed
{
    string SourceLabel { get; }
    IReadOnlyList<string> Providers { get; }
    IReadOnlyList<string> Brokers { get; }
    DashboardSnapshot Build(DashboardQuery query);
}

public sealed record DashboardQuery(
    string AccountType,
    string Provider,
    string Broker,
    string Range);

public sealed record DashboardTradeRow(
    string Instrument,
    string Side,
    int Qty,
    double Pl,
    string Time);

public sealed record DashboardSnapshot(
    string AccountNumber,
    bool IsDemo,
    string Provider,
    string Broker,
    string Range,
    double Equity,
    double EquityChange,
    double EquityChangePct,
    IReadOnlyList<double> EquitySeries,
    double OpenPl,
    double OpenPlPct,
    double TotalPl,
    double Realized,
    double WinRate,
    int Wins,
    int TradeCount,
    double Cash,
    double FreeMargin,
    double MarginUsed,
    double CallExposure,
    double PutExposure,
    double OptionsValue,
    int OpenPositions,
    IReadOnlyList<DashboardTradeRow> RecentTrades,
    string SourceLabel);

/// <summary>Mock account summary — web Riepilogo parity until live ledger/API.</summary>
public sealed class MockDashboardFeed : IDashboardFeed
{
    public string SourceLabel => "riepilogo · mock";

    public IReadOnlyList<string> Providers { get; } =
    [
        "LEXIS Data Cloud",
        "OPRA Direct (CTA/UTP)",
        "Cboe DataShop",
        "Polygon.io",
        "Databento",
    ];

    public IReadOnlyList<string> Brokers { get; } =
    [
        "LEXIS Paper · marks CBOE",
        "Interactive Brokers",
        "Tastytrade",
        "Charles Schwab",
        "AvaTrade",
        "Mexem",
    ];

    public DashboardSnapshot Build(DashboardQuery query)
    {
        var demo = !string.Equals(query.AccountType, "real", StringComparison.OrdinalIgnoreCase);
        var seed = demo ? 17 : 41;
        var rng = new Random(seed + Hash(query.Range) + Hash(query.Broker));

        var optionsValue = demo ? 48_200.0 : 126_400.0;
        var cash = demo ? 51_800.0 : 38_200.0;
        var openPl = demo ? 1_840.0 : -2_260.0;
        var realized = demo ? 6_420.0 : 14_880.0;
        var calls = optionsValue * (demo ? 0.58 : 0.46);
        var puts = optionsValue - calls;
        var cost = optionsValue - openPl;
        var equity = optionsValue + cash;
        var totalPl = openPl + realized;
        var marginUsed = demo ? 12_400.0 : 41_200.0;
        var freeMargin = Math.Max(0, equity - marginUsed);
        var openPlPct = cost > 0 ? openPl / cost * 100.0 : 0;
        var wins = demo ? 9 : 14;
        var trades = demo ? 14 : 22;
        var winRate = trades > 0 ? wins * 100.0 / trades : 0;

        var series = BuildSeries(query.Range, equity, demo, rng);
        var start = series[0];
        var chg = equity - start;
        var chgPct = start != 0 ? chg / start * 100.0 : 0;

        var recent = new List<DashboardTradeRow>
        {
            new("SPX 5280 C", "LONG", 2, demo ? 640 : -320, "15:42"),
            new("SPY 520 P", "SHORT", 5, demo ? 280 : 410, "15:18"),
            new("QQQ 445 C", "LONG", 3, demo ? -120 : 190, "14:55"),
            new("AAPL 200 C", "LONG", 4, demo ? 310 : -85, "14:12"),
            new("TSLA 250 P", "SHORT", 2, demo ? 95 : 220, "13:40"),
            new("SPX 5250 P", "LONG", 1, demo ? -180 : 540, "12:08"),
        };

        return new DashboardSnapshot(
            AccountNumber: demo ? "LX-48817-DEMO" : "LX-90233-LIVE",
            IsDemo: demo,
            Provider: query.Provider,
            Broker: query.Broker,
            Range: query.Range,
            Equity: Math.Round(equity, 0),
            EquityChange: Math.Round(chg, 0),
            EquityChangePct: Math.Round(chgPct, 2),
            EquitySeries: series,
            OpenPl: Math.Round(openPl, 0),
            OpenPlPct: Math.Round(openPlPct, 2),
            TotalPl: Math.Round(totalPl, 0),
            Realized: Math.Round(realized, 0),
            WinRate: Math.Round(winRate, 0),
            Wins: wins,
            TradeCount: trades,
            Cash: Math.Round(cash, 0),
            FreeMargin: Math.Round(freeMargin, 0),
            MarginUsed: Math.Round(marginUsed, 0),
            CallExposure: Math.Round(calls, 0),
            PutExposure: Math.Round(puts, 0),
            OptionsValue: Math.Round(optionsValue, 0),
            OpenPositions: demo ? 4 : 7,
            RecentTrades: recent,
            SourceLabel: SourceLabel);
    }

    private static IReadOnlyList<double> BuildSeries(string range, double end, bool demo, Random rng)
    {
        var (n, vol) = range switch
        {
            "1G" => (48, 0.0009),
            "1S" => (60, 0.0017),
            "YTD" => (130, 0.0052),
            _ => (90, 0.0026), // 1M
        };

        var pts = new double[n];
        pts[n - 1] = end;
        var drift = demo ? 0.00015 : 0.00005;
        for (var i = n - 2; i >= 0; i--)
        {
            var shock = 1.0 + (rng.NextDouble() - 0.48) * vol * 2 - drift;
            pts[i] = Math.Max(end * 0.7, pts[i + 1] * shock);
        }

        // Rescale so last point is exactly end
        var scale = end / pts[n - 1];
        for (var i = 0; i < n; i++)
            pts[i] = Math.Round(pts[i] * scale, 0);
        pts[n - 1] = end;
        return pts;
    }

    private static int Hash(string s)
    {
        unchecked
        {
            var h = 19;
            foreach (var c in s)
                h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }
}
