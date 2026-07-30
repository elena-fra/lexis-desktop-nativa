namespace Lexis.Desktop.App.Services;

/// <summary>
/// D1 feed hub — single construction point for chain/flow/greeks/gex (+ API).
/// Mock-first; swap adapters when live data arrives without rewriting panels.
/// </summary>
public sealed class MarketDataHub : IDisposable
{
    public DeskSettings Settings { get; }
    public LexisApiClient? Api { get; }
    public bool ApiOk { get; }
    public IChainFeed Chain { get; }
    public IFlowFeed Flow { get; }
    public IGreeksFeed Greeks { get; }
    public IGexFeed Gex { get; }
    public IDashboardFeed Dashboard { get; }
    public IPortfolioFeed Portfolio { get; }
    public string StatusLabel { get; }
    public string ModeLabel { get; }

    private MarketDataHub(
        DeskSettings settings,
        LexisApiClient? api,
        bool apiOk,
        IChainFeed chain,
        IFlowFeed flow,
        IGreeksFeed greeks,
        IGexFeed gex,
        IDashboardFeed dashboard,
        IPortfolioFeed portfolio,
        string statusLabel,
        string modeLabel)
    {
        Settings = settings;
        Api = api;
        ApiOk = apiOk;
        Chain = chain;
        Flow = flow;
        Greeks = greeks;
        Gex = gex;
        Dashboard = dashboard;
        Portfolio = portfolio;
        StatusLabel = statusLabel;
        ModeLabel = modeLabel;
    }

    public static MarketDataHub Create(DeskSettings? settings = null)
    {
        settings ??= DeskSettings.Load();
        LexisApiClient? api = null;
        var apiOk = false;

        if (settings.PreferApi)
        {
            try
            {
                api = new LexisApiClient(settings);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                apiOk = api.TryConnectAsync(cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch
            {
                apiOk = false;
            }
        }

        IChainFeed chain;
        IFlowFeed flow;
        string status;
        string mode;

        if (apiOk && api is not null)
        {
            chain = new HybridChainFeed(api, true);
            flow = new ApiFlowFeed(api);
            status = $"API · {settings.ApiBaseUrl} · {api.ModeLabel}";
            mode = "API";
        }
        else
        {
            chain = new MockChainFeedAdapter();
            flow = new MockFlowFeedAdapter();
            status = api?.ModeLabel ?? "mock (API off)";
            mode = "mock";
        }

        var greeks = new GreeksFeed(chain);
        var gex = new ChainGexFeed(chain);
        var dashboard = new MockDashboardFeed();
        var portfolio = new MockPortfolioFeed();

        return new MarketDataHub(settings, api, apiOk, chain, flow, greeks, gex, dashboard, portfolio, status, mode);
    }

    public void Dispose() => Api?.Dispose();
}
