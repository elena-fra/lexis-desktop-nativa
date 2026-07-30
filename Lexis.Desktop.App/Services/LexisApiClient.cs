using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lexis.Contracts.Market;

namespace Lexis.Desktop.App.Services;

public sealed record ApiEnvelope<T>(bool Ok, T? Data, string? Error);

public sealed class LexisApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly DeskSettings _settings;
    private readonly JsonSerializerOptions _json;

    public LexisApiClient(DeskSettings settings)
    {
        _settings = settings;
        _json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };
        _http = new HttpClient
        {
            BaseAddress = new Uri(settings.ApiBaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(20),
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string? Token { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public string BaseUrl => _settings.ApiBaseUrl.TrimEnd('/');
    public string ModeLabel { get; private set; } = "mock";

    public async Task<bool> TryConnectAsync(CancellationToken ct = default)
    {
        if (!_settings.PreferApi) { ModeLabel = "mock (PreferApi=false)"; return false; }

        try
        {
            using var health = await _http.GetAsync("api/v1/health", ct);
            if (!health.IsSuccessStatusCode)
            {
                ModeLabel = "mock (API health fail)";
                return false;
            }
        }
        catch (Exception ex)
        {
            ModeLabel = $"mock (API offline: {ex.GetType().Name})";
            return false;
        }

        if (await TryLoginAsync(_settings.Username, _settings.Password, ct))
        {
            ModeLabel = "API live";
            return true;
        }

        if (_settings.AutoRegister)
        {
            if (await TryRegisterAsync(_settings.Username, _settings.Password, ct)
                && await TryLoginAsync(_settings.Username, _settings.Password, ct))
            {
                ModeLabel = "API live (registered)";
                try { _settings.Save(); } catch { }
                return true;
            }
        }

        ModeLabel = "mock (auth failed)";
        return false;
    }

    public async Task<bool> TryLoginAsync(string username, string password, CancellationToken ct = default)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/v1/auth/login", new { username, password }, _json, ct);
            var body = await res.Content.ReadFromJsonAsync<ApiEnvelope<AuthPayload>>(ct);
            if (res.IsSuccessStatusCode && body?.Ok == true && !string.IsNullOrEmpty(body.Data?.Token))
            {
                SetToken(body.Data.Token);
                return true;
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> TryRegisterAsync(string username, string password, CancellationToken ct = default)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/v1/auth/register", new
            {
                username,
                password,
                privacyAccepted = true,
            }, _json, ct);
            var body = await res.Content.ReadFromJsonAsync<ApiEnvelope<AuthPayload>>(ct);
            if (res.IsSuccessStatusCode && body?.Ok == true && !string.IsNullOrEmpty(body.Data?.Token))
            {
                SetToken(body.Data.Token);
                return true;
            }
        }
        catch { }
        return false;
    }

    private void SetToken(string token)
    {
        Token = token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<IReadOnlyList<FlowRowDto>> GetFlowAsync(
        int limit = 80,
        long minPrem = 10000,
        CancellationToken ct = default)
    {
        EnsureAuth();
        var url = $"api/v1/flow?limit={limit}&minPrem={minPrem}";
        var body = await _http.GetFromJsonAsync<ApiEnvelope<List<FlowRowDto>>>(url, _json, ct);
        if (body?.Ok != true || body.Data is null)
            throw new InvalidOperationException(body?.Error ?? "flow empty");
        return body.Data;
    }

    public async Task<ChainDto> GetChainAsync(
        string symbol,
        string? expiry = null,
        string? strikes = null,
        CancellationToken ct = default)
    {
        EnsureAuth();
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(expiry)) qs.Add("expiry=" + Uri.EscapeDataString(expiry));
        if (!string.IsNullOrWhiteSpace(strikes)) qs.Add("strikes=" + Uri.EscapeDataString(strikes));
        var url = $"api/v1/market/chain/{Uri.EscapeDataString(symbol.ToUpperInvariant())}";
        if (qs.Count > 0) url += "?" + string.Join("&", qs);
        var body = await _http.GetFromJsonAsync<ApiEnvelope<ChainDto>>(url, _json, ct);
        if (body?.Ok != true || body.Data is null)
            throw new InvalidOperationException(body?.Error ?? "chain empty");
        return body.Data;
    }

    public async IAsyncEnumerable<FlowRowDto> StreamFlowRowsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        EnsureAuth();
        var url = $"api/v1/stream?token={Uri.EscapeDataString(Token!)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd("text/event-stream");
        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? eventName = null;
        var data = new StringBuilder();

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                eventName = line["event:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (data.Length > 0) data.Append('\n');
                data.Append(line["data:".Length..].TrimStart());
                continue;
            }

            if (line.Length == 0)
            {
                var payload = data.ToString();
                var ev = eventName;
                eventName = null;
                data.Clear();
                if (string.IsNullOrWhiteSpace(payload)) continue;
                if (!string.Equals(ev, "flow.row", StringComparison.OrdinalIgnoreCase)) continue;

                FlowRowDto? row = null;
                try
                {
                    using var doc = JsonDocument.Parse(payload);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("payload", out var p))
                        row = JsonSerializer.Deserialize<FlowRowDto>(p.GetRawText(), _json);
                    else
                        row = JsonSerializer.Deserialize<FlowRowDto>(payload, _json);
                }
                catch { }

                if (row is not null) yield return row;
            }
        }
    }

    private void EnsureAuth()
    {
        if (!IsAuthenticated)
            throw new InvalidOperationException("Not authenticated to LEXIS API.");
    }

    public void Dispose() => _http.Dispose();

    private sealed record AuthPayload(string Token, object? User);
}
