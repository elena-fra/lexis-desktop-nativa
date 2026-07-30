using System.Collections.Concurrent;
using Lexis.Contracts.Market;

namespace Lexis.Pricing;

/// <summary>
/// GEX multi-metric profiles from option chain — port of LexisTrading.Api GexProfileService.
/// Dealer convention: GEX = Γ_call·OI_call·mult − Γ_put·OI_put·mult; Net = GEX + 0.35·DEX.
/// Uses live OI/IV when present; deterministic synthetic OI otherwise — ready for live feeds.
/// </summary>
public static class GexProfileEngine
{
    public static readonly (string Key, string Label, string Name)[] Metrics =
    [
        ("gex", "GEX", "Gamma Exposure"),
        ("dex", "DEX", "Delta Exposure"),
        ("charma", "Charma", "Charm (decadimento Δ)"),
        ("vanna", "Vanna", "Vanna (Δ per vol)"),
        ("speed", "Speed", "Speed (dΓ/dS)"),
        ("vomma", "Vomma", "Vomma (dVega/dσ)"),
        ("net", "Netta", "Esposizione Netta Aggregata"),
    ];

    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new();

    private sealed record CacheEntry(DateTime ExpiresUtc, ComputedBundle Bundle);

    public sealed record MetricProfile(
        string Key,
        string Label,
        string Name,
        IReadOnlyDictionary<double, double> Data,
        double Flip);

    public sealed record ComputedBundle(
        double Spot,
        string MethodKey,
        string MethodLabel,
        bool UsedLiveOi,
        int LiveOiContracts,
        int SyntheticOiContracts,
        IReadOnlyDictionary<string, MetricProfile> Profiles);

    public static ComputedBundle ComputeFromChain(ChainDto chain, string? methodKey = null)
    {
        var method = OptionModelEngines.Parse(methodKey);
        var cacheKey = Fingerprint(chain, method);
        if (Cache.TryGetValue(cacheKey, out var hit) && hit.ExpiresUtc > DateTime.UtcNow)
            return hit.Bundle;

        var bundle = ComputeCore(chain, method);
        var ttl = method is OptionModelEngines.Method.MonteCarlo
            or OptionModelEngines.Method.Heston
            or OptionModelEngines.Method.Merton
            ? TimeSpan.FromSeconds(45)
            : TimeSpan.FromSeconds(12);
        Cache[cacheKey] = new CacheEntry(DateTime.UtcNow.Add(ttl), bundle);
        return bundle;
    }

    public static void ClearCache() => Cache.Clear();

    private static ComputedBundle ComputeCore(ChainDto chain, OptionModelEngines.Method method)
    {
        var spot = chain.Spot > 0 ? chain.Spot : 100;
        var T = Math.Max(0.5, chain.Expiry.Dte) / 365.0;
        var r = OptionModelEngines.DefaultR;
        var q = OptionModelEngines.DefaultQ;
        var mult = OptionModelEngines.DefaultMult;
        var baseIv = chain.Expiry.Iv > 1 ? chain.Expiry.Iv / 100.0
            : chain.Expiry.Iv > 0 ? chain.Expiry.Iv
            : 0.18;

        var gex = new Dictionary<double, double>();
        var dex = new Dictionary<double, double>();
        var charma = new Dictionary<double, double>();
        var vanna = new Dictionary<double, double>();
        var speed = new Dictionary<double, double>();
        var vomma = new Dictionary<double, double>();
        var net = new Dictionary<double, double>();

        var liveOi = 0;
        var synthOi = 0;
        var anyLive = false;
        var idx = 0;

        var rows = chain.Rows
            .OrderBy(x => Math.Abs(x.Strike - spot))
            .Take(28)
            .OrderBy(x => x.Strike)
            .ToList();

        // Higher-order greeks: BS on heavy Monte Carlo methods to keep desk responsive.
        var heavy = method is OptionModelEngines.Method.MonteCarlo
            or OptionModelEngines.Method.Heston
            or OptionModelEngines.Method.Merton;

        foreach (var row in rows)
        {
            var K = row.Strike;
            var call = row.Call;
            var put = row.Put;

            var callIv = ResolveIv(call, baseIv, spot, K);
            var putIv = ResolveIv(put, baseIv, spot, K);

            var (callOi, callLive) = ResolveOi(call.Oi, chain.Symbol, K, "C", idx);
            var (putOi, putLive) = ResolveOi(put.Oi, chain.Symbol, K, "P", idx);
            if (callLive) { anyLive = true; liveOi++; } else synthOi++;
            if (putLive) { anyLive = true; liveOi++; } else synthOi++;

            var cg = OptionModelEngines.Greeks(method, spot, K, T, callIv, "call", r, q);
            var pg = OptionModelEngines.Greeks(method, spot, K, T, putIv, "put", r, q);

            // Prefer broker greeks when method is BS and chain already has them.
            if (method == OptionModelEngines.Method.BlackScholes)
            {
                if (Math.Abs(call.Gamma) > 0)
                    cg = cg with { Gamma = call.Gamma, Delta = call.Delta, Vega = call.Vega, Theta = call.Theta };
                if (Math.Abs(put.Gamma) > 0)
                    pg = pg with { Gamma = put.Gamma, Delta = put.Delta, Vega = put.Vega, Theta = put.Theta };
            }

            var gexK = (cg.Gamma * callOi - pg.Gamma * putOi) * mult;
            var dexK = (cg.Delta * callOi + pg.Delta * putOi) * mult;
            var charmK = cg.Theta * callOi + pg.Theta * putOi;
            var greekMethod = heavy ? OptionModelEngines.Method.BlackScholes : method;
            var vannaK = EstimateVanna(greekMethod, spot, K, T, callIv, putIv, callOi, putOi, mult, r, q);
            var speedK = EstimateSpeed(greekMethod, spot, K, T, callIv, putIv, callOi, putOi, mult, r, q);
            var vommaK = EstimateVomma(greekMethod, spot, K, T, callIv, putIv, callOi, putOi, mult, r, q);

            gex[K] = RoundM(gexK);
            dex[K] = RoundM(dexK);
            charma[K] = Math.Round(charmK / 1000.0, 2);
            vanna[K] = Math.Round(vannaK / 1000.0, 2);
            speed[K] = Math.Round(speedK / 1000.0, 2);
            vomma[K] = Math.Round(vommaK / 1000.0, 2);
            net[K] = Math.Round(gex[K] + 0.35 * dex[K], 2);
            idx++;
        }

        if (gex.Count == 0)
        {
            var step = GuessStep(spot);
            var atm = Math.Round(spot / step) * step;
            for (var i = -4; i <= 4; i++)
            {
                var K = Math.Round(atm + i * step, 2);
                gex[K] = 0; dex[K] = 0; charma[K] = 0; vanna[K] = 0; speed[K] = 0; vomma[K] = 0; net[K] = 0;
            }
        }

        var profiles = new Dictionary<string, MetricProfile>();
        void Add(string key, string label, string name, Dictionary<double, double> data)
            => profiles[key] = new MetricProfile(
                key, label, $"{name} ({OptionModelEngines.MethodLabel(method)})", data, FindFlip(data, spot));

        Add("gex", "GEX", "Gamma Exposure", gex);
        Add("dex", "DEX", "Delta Exposure", dex);
        Add("charma", "Charma", "Charm (decadimento Δ)", charma);
        Add("vanna", "Vanna", "Vanna (Δ per vol)", vanna);
        Add("speed", "Speed", "Speed (dΓ/dS)", speed);
        Add("vomma", "Vomma", "Vomma (dVega/dσ)", vomma);
        Add("net", "Netta", "Esposizione Netta Aggregata", net);

        return new ComputedBundle(
            spot,
            OptionModelEngines.MethodKey(method),
            OptionModelEngines.MethodLabel(method),
            anyLive,
            liveOi,
            synthOi,
            profiles);
    }

    /// <summary>Invalidates when symbol/method/spot/expiry/OI/IV change — live-data ready.</summary>
    private static string Fingerprint(ChainDto chain, OptionModelEngines.Method method)
    {
        unchecked
        {
            var h = OptionModelEngines.StableHash(OptionModelEngines.MethodKey(method));
            h ^= OptionModelEngines.StableHash(chain.Symbol ?? "");
            h ^= chain.Spot.GetHashCode();
            h ^= chain.Expiry.Dte.GetHashCode();
            h ^= chain.Expiry.Iv.GetHashCode();
            foreach (var row in chain.Rows)
            {
                h = h * 31 + row.Strike.GetHashCode();
                h = h * 31 + row.Call.Oi;
                h = h * 31 + row.Put.Oi;
                h = h * 31 + row.Call.Iv.GetHashCode();
                h = h * 31 + row.Put.Iv.GetHashCode();
                h = h * 31 + row.Call.Gamma.GetHashCode();
                h = h * 31 + row.Put.Gamma.GetHashCode();
            }
            return h.ToString("X8");
        }
    }

    private static double RoundM(double rawNotional) =>
        Math.Round(rawNotional / 1_000_000.0, 4);

    private static double ResolveIv(OptionLegQuoteDto leg, double baseIv, double spot, double K)
    {
        if (leg.Iv > 0)
            return leg.Iv > 1.5 ? leg.Iv / 100.0 : leg.Iv;
        var mny = Math.Abs((K - spot) / Math.Max(1, spot));
        return Math.Max(0.05, baseIv + mny * 0.55);
    }

    private static (int Oi, bool Live) ResolveOi(int oi, string sym, double K, string cp, int idx)
    {
        if (oi > 0) return (oi, true);
        var h = OptionModelEngines.StableHash($"{sym}:{K}:{cp}:{idx}");
        var synth = 700 + (h % 5200);
        return (synth, false);
    }

    private static double EstimateVanna(
        OptionModelEngines.Method method, double S, double K, double T,
        double callIv, double putIv, int callOi, int putOi, int mult, double r, double q)
    {
        const double bump = 0.01;
        var c0 = OptionModelEngines.Greeks(method, S, K, T, callIv, "call", r, q).Delta;
        var c1 = OptionModelEngines.Greeks(method, S, K, T, callIv + bump, "call", r, q).Delta;
        var p0 = OptionModelEngines.Greeks(method, S, K, T, putIv, "put", r, q).Delta;
        var p1 = OptionModelEngines.Greeks(method, S, K, T, putIv + bump, "put", r, q).Delta;
        return (((c1 - c0) / bump) * callOi + ((p1 - p0) / bump) * putOi) * mult;
    }

    private static double EstimateSpeed(
        OptionModelEngines.Method method, double S, double K, double T,
        double callIv, double putIv, int callOi, int putOi, int mult, double r, double q)
    {
        var bump = Math.Max(0.05, S * 0.004);
        var c0 = OptionModelEngines.Greeks(method, S, K, T, callIv, "call", r, q).Gamma;
        var c1 = OptionModelEngines.Greeks(method, S + bump, K, T, callIv, "call", r, q).Gamma;
        var p0 = OptionModelEngines.Greeks(method, S, K, T, putIv, "put", r, q).Gamma;
        var p1 = OptionModelEngines.Greeks(method, S + bump, K, T, putIv, "put", r, q).Gamma;
        return (((c1 - c0) / bump) * callOi - ((p1 - p0) / bump) * putOi) * mult;
    }

    private static double EstimateVomma(
        OptionModelEngines.Method method, double S, double K, double T,
        double callIv, double putIv, int callOi, int putOi, int mult, double r, double q)
    {
        const double bump = 0.01;
        var c0 = OptionModelEngines.Greeks(method, S, K, T, callIv, "call", r, q).Vega;
        var c1 = OptionModelEngines.Greeks(method, S, K, T, callIv + bump, "call", r, q).Vega;
        var p0 = OptionModelEngines.Greeks(method, S, K, T, putIv, "put", r, q).Vega;
        var p1 = OptionModelEngines.Greeks(method, S, K, T, putIv + bump, "put", r, q).Vega;
        return (((c1 - c0) / bump) * callOi + ((p1 - p0) / bump) * putOi) * mult;
    }

    /// <summary>Prefer zero-crossing between adjacent strikes; else nearest-to-zero.</summary>
    private static double FindFlip(Dictionary<double, double> data, double fallback)
    {
        if (data.Count == 0) return fallback;
        var ordered = data.OrderBy(kv => kv.Key).ToList();
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var (k0, v0) = ordered[i];
            var (k1, v1) = ordered[i + 1];
            if (v0 == 0) return k0;
            if (v0 * v1 < 0)
            {
                var t = Math.Abs(v0) / (Math.Abs(v0) + Math.Abs(v1));
                return Math.Round(k0 + (k1 - k0) * t, 2);
            }
        }

        var best = double.PositiveInfinity;
        var flip = fallback;
        foreach (var (k, v) in data)
        {
            var a = Math.Abs(v);
            if (a < best) { best = a; flip = k; }
        }
        return flip;
    }

    private static double GuessStep(double spot) =>
        spot >= 1000 ? 25 : spot >= 200 ? 5 : spot >= 50 ? 2.5 : 1;
}
