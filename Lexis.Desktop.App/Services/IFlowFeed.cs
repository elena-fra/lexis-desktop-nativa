using Lexis.Contracts.Market;

namespace Lexis.Desktop.App.Services;

public interface IFlowFeed
{
    bool IsApiLive { get; }
    string SourceLabel { get; }
    IReadOnlyList<FlowRowDto> Seed(int count = 48);
    /// <summary>Start live stream/polling. Return dispose to stop.</summary>
    IDisposable StartLive(Action<FlowRowDto> onRow, Func<bool> isPaused);
}

/// <summary>Mock adapter for IFlowFeed.</summary>
public sealed class MockFlowFeedAdapter : IFlowFeed
{
    private readonly MockFlowFeed _inner;
    public MockFlowFeedAdapter(MockFlowFeed? inner = null) => _inner = inner ?? new MockFlowFeed();
    public bool IsApiLive => false;
    public string SourceLabel => "Feed simulato";
    public IReadOnlyList<FlowRowDto> Seed(int count = 48) => _inner.Seed(count);

    public IDisposable StartLive(Action<FlowRowDto> onRow, Func<bool> isPaused)
    {
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(650, cts.Token);
                    if (isPaused()) continue;
                    onRow(_inner.Next());
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, cts.Token);
        return cts;
    }
}

/// <summary>API snapshot + SSE flow.row (poll fallback).</summary>
public sealed class ApiFlowFeed : IFlowFeed, IDisposable
{
    private readonly LexisApiClient _api;
    private readonly MockFlowFeed _fallback = new();

    public ApiFlowFeed(LexisApiClient api) => _api = api;

    public bool IsApiLive => _api.IsAuthenticated;
    public string SourceLabel => _api.IsAuthenticated ? "API live · SSE" : "Feed simulato";

    public IReadOnlyList<FlowRowDto> Seed(int count = 48)
    {
        if (!_api.IsAuthenticated) return _fallback.Seed(count);
        try
        {
            return _api.GetFlowAsync(limit: Math.Max(count, 40), minPrem: 10000)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch
        {
            return _fallback.Seed(count);
        }
    }

    public IDisposable StartLive(Action<FlowRowDto> onRow, Func<bool> isPaused)
    {
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            if (_api.IsAuthenticated)
            {
                try
                {
                    await foreach (var row in _api.StreamFlowRowsAsync(cts.Token))
                    {
                        if (isPaused()) continue;
                        onRow(row);
                    }
                }
                catch (OperationCanceledException) { return; }
                catch { /* fall through to poll */ }

                // Poll fallback if SSE ends
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(3000, cts.Token);
                        if (isPaused()) continue;
                        var rows = await _api.GetFlowAsync(limit: 20, minPrem: 10000, cts.Token);
                        foreach (var r in rows.Take(5).Reverse())
                            onRow(r);
                    }
                    catch (OperationCanceledException) { break; }
                    catch { }
                }
                return;
            }

            // mock fallback live
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(650, cts.Token);
                    if (isPaused()) continue;
                    onRow(_fallback.Next());
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, cts.Token);
        return cts;
    }

    public void Dispose() { }
}
