using Lexis.Contracts.Market;
using Lexis.Pricing;

namespace Lexis.Desktop.App.Services;

public interface IChainFeed
{
    bool IsApiLive { get; }
    string SourceLabel { get; }
    IReadOnlyList<UnderlyingInfo> Underlyings { get; }
    IReadOnlyList<ExpiryDto> BuildExpiries(string symbol);
    ChainDto Create(string symbol = "SPY", double? spotOverride = null, int strikeCount = 21, int? dte = null, string? expiryDate = null);
    ChainDto Tick(ChainDto chain);
    /// <summary>When API live, soft-poll refresh. Returns null if mock mode (use Tick).</summary>
    IDisposable? StartApiPoll(Func<ChainDto> current, Action<ChainDto> onUpdate, Func<bool> isActive);
}

public sealed class MockChainFeedAdapter : IChainFeed
{
    private readonly MockChainFeed _inner;
    public MockChainFeedAdapter(MockChainFeed? inner = null) => _inner = inner ?? new MockChainFeed();
    public bool IsApiLive => false;
    public string SourceLabel => "mock";
    public IReadOnlyList<UnderlyingInfo> Underlyings => MockChainFeed.Underlyings;
    public IReadOnlyList<ExpiryDto> BuildExpiries(string symbol) => _inner.BuildExpiries(symbol);
    public ChainDto Create(string symbol = "SPY", double? spotOverride = null, int strikeCount = 21, int? dte = null, string? expiryDate = null)
        => _inner.Create(symbol, spotOverride, strikeCount, dte, expiryDate);
    public ChainDto Tick(ChainDto chain) => _inner.Tick(chain);
    public IDisposable? StartApiPoll(Func<ChainDto> current, Action<ChainDto> onUpdate, Func<bool> isActive) => null;
}

/// <summary>Prefers LEXIS API chain; falls back to local mock + Tick.</summary>
public sealed class HybridChainFeed : IChainFeed
{
    private readonly LexisApiClient? _api;
    private readonly MockChainFeed _mock = new();
    private readonly bool _apiOk;

    public HybridChainFeed(LexisApiClient? api, bool apiOk)
    {
        _api = api;
        _apiOk = apiOk && api?.IsAuthenticated == true;
    }

    public bool IsApiLive => _apiOk;
    public string SourceLabel => _apiOk ? "API" : "mock";
    public IReadOnlyList<UnderlyingInfo> Underlyings => MockChainFeed.Underlyings;
    public IReadOnlyList<ExpiryDto> BuildExpiries(string symbol) => _mock.BuildExpiries(symbol);

    public ChainDto Create(string symbol = "SPY", double? spotOverride = null, int strikeCount = 21, int? dte = null, string? expiryDate = null)
    {
        if (_apiOk && _api is not null)
        {
            try
            {
                // Prefer expiry label from mock list if we only have date — API uses L like "0DTE"/"Weekly"
                var mock = _mock.Create(symbol, spotOverride, strikeCount, dte, expiryDate);
                var expiryLabel = mock.Expiry.L;
                var chain = _api.GetChainAsync(symbol, expiryLabel, strikeCount.ToString())
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                // Keep expiries list usable in UI if API returns empty
                if (chain.Expiries is null || chain.Expiries.Count == 0)
                    return chain with { Expiries = mock.Expiries };
                return chain;
            }
            catch
            {
                return _mock.Create(symbol, spotOverride, strikeCount, dte, expiryDate);
            }
        }

        return _mock.Create(symbol, spotOverride, strikeCount, dte, expiryDate);
    }

    public ChainDto Tick(ChainDto chain)
    {
        // When API live, Tick is no-opish — soft poll updates instead
        if (_apiOk) return chain;
        return _mock.Tick(chain);
    }

    public IDisposable? StartApiPoll(Func<ChainDto> current, Action<ChainDto> onUpdate, Func<bool> isActive)
    {
        if (!_apiOk || _api is null) return null;
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, cts.Token);
                    if (!isActive()) continue;
                    var cur = current();
                    var next = await _api.GetChainAsync(cur.Symbol, cur.Expiry.L, null, cts.Token);
                    if (next.Expiries is null || next.Expiries.Count == 0)
                        next = next with { Expiries = cur.Expiries };
                    onUpdate(next);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, cts.Token);
        return cts;
    }
}
