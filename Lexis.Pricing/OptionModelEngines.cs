namespace Lexis.Pricing;

/// <summary>
/// Motori di pricing per GEX multi-metodo (parity LexisTrading.Api).
/// <list type="bullet">
/// <item><b>Black-Scholes</b> — greche analitiche BSM (r, q)</item>
/// <item><b>CRR</b> — albero binomiale American + bump centrali Δ/Γ/ν/Θ</item>
/// <item><b>Monte Carlo</b> — GBM europeo (seed deterministico)</item>
/// <item><b>Heston</b> — vol stocastica Euler + GBM</item>
/// <item><b>FDM</b> — PDE BS fully-implicit su griglia S</item>
/// </list>
/// Quando la chain porta OI/IV live, GexProfileEngine li usa; altrimenti OI sintetico.
/// </summary>
public static class OptionModelEngines
{
    public const double DefaultR = 0.042;
    public const double DefaultQ = 0.013;
    public const int DefaultMult = 100;

    public enum Method
    {
        BlackScholes = 0,
        CoxRossRubinstein = 1,
        MonteCarlo = 2,
        Boyle = 3,
        Heston = 4,
        Merton = 5,
        Fdm = 6,
    }

    public static Method Parse(string? method) => method switch
    {
        "0" or "black-scholes" or "bs" or "blackscholes" => Method.BlackScholes,
        "1" or "cox-ross-rubinstein" or "crr" => Method.CoxRossRubinstein,
        "2" or "monte-carlo" or "mc" => Method.MonteCarlo,
        "3" or "boyle" => Method.Boyle,
        "4" or "heston" => Method.Heston,
        "5" or "diffusion" or "merton" => Method.Merton,
        "6" or "fdm" or "finite-difference" => Method.Fdm,
        _ => Method.BlackScholes,
    };

    public static string MethodKey(Method m) => m switch
    {
        Method.CoxRossRubinstein => "cox-ross-rubinstein",
        Method.MonteCarlo => "monte-carlo",
        Method.Boyle => "boyle",
        Method.Heston => "heston",
        Method.Merton => "merton",
        Method.Fdm => "fdm",
        _ => "black-scholes",
    };

    public static string MethodLabel(Method m) => m switch
    {
        Method.CoxRossRubinstein => "Cox-Ross-Rubinstein",
        Method.MonteCarlo => "Monte Carlo",
        Method.Boyle => "Boyle",
        Method.Heston => "Heston",
        Method.Merton => "Merton",
        Method.Fdm => "FDM",
        _ => "Black-Scholes",
    };

    public readonly record struct ModelGreeks(double Price, double Delta, double Gamma, double Vega, double Theta);

    public static ModelGreeks Greeks(
        Method method, double S, double K, double T, double sigma, string type,
        double r = DefaultR, double q = DefaultQ)
    {
        S = Math.Max(1e-6, S);
        K = Math.Max(1e-6, K);
        T = Math.Max(1e-6, T);
        sigma = Math.Max(1e-4, sigma);
        type = type == "put" ? "put" : "call";

        if (method == Method.BlackScholes)
        {
            var g = BlackScholes.Calculate(S, K, T, sigma, type, r, q);
            return Sanitize(new ModelGreeks(g.Price, g.Delta, g.Gamma, g.Vega, g.Theta));
        }

        // Stochastic engines: Common Random Numbers — same seed for all bumps.
        // Seeding by S/σ independently made MC/Heston gamma explode (noise ≠ signal).
        var stochastic = method is Method.MonteCarlo or Method.Heston or Method.Merton;
        int? crnSeed = stochastic ? Hash(K, T, type) : null;

        var bumpS = Math.Max(0.05, S * 0.004);
        var bumpVol = Math.Max(0.0025, sigma * 0.02);
        var mid = Price(method, S, K, T, sigma, type, r, q, crnSeed);
        var up = Price(method, S + bumpS, K, T, sigma, type, r, q, crnSeed);
        var dn = Price(method, Math.Max(1e-6, S - bumpS), K, T, sigma, type, r, q, crnSeed);
        var volUp = Price(method, S, K, T, sigma + bumpVol, type, r, q, crnSeed);
        var tDown = Price(method, S, K, Math.Max(1e-6, T - (1.0 / 365.0)), sigma, type, r, q, crnSeed);

        var delta = (up - dn) / (2 * bumpS);
        var gamma = (up - 2 * mid + dn) / (bumpS * bumpS);
        var vega = (volUp - mid) / bumpVol / 100.0; // per 1 vol point
        var theta = tDown - mid; // 1 day
        return Sanitize(new ModelGreeks(mid, delta, gamma, vega, theta));
    }

    public static double Price(
        Method method, double S, double K, double T, double sigma, string type,
        double r = DefaultR, double q = DefaultQ, int? seedOverride = null)
    {
        var seed = seedOverride ?? Hash(S, K, T, sigma, type);
        var px = method switch
        {
            Method.CoxRossRubinstein => BlackScholes.CrrPrice(S, K, T, sigma, r, q, type, steps: 64),
            Method.Boyle => BoylePrice(S, K, T, sigma, r, type, steps: 36),
            Method.MonteCarlo => MonteCarloPrice(S, K, T, sigma, r, q, type, paths: 240, seed: seed),
            Method.Heston => HestonPrice(S, K, T, sigma, r, q, type, paths: 160, seed: seed),
            Method.Merton => MertonPrice(S, K, T, sigma, r, q, type, paths: 160, seed: seed),
            Method.Fdm => FdmPrice(S, K, T, sigma, r, q, type),
            _ => BlackScholes.Calculate(S, K, T, sigma, type, r, q).Price,
        };
        return double.IsFinite(px) ? Math.Max(0, px) : 0;
    }

    // --- Boyle trinomial (American) ---
    public static double BoylePrice(double S, double K, double T, double sigma, double r, string type, int steps = 60)
    {
        steps = Math.Clamp(steps, 8, 120);
        var dt = T / steps;
        var u = Math.Exp(sigma * Math.Sqrt(2 * dt));
        var d = 1.0 / u;
        var v = r - 0.5 * sigma * sigma;
        var erdt = Math.Exp(r * dt);
        var evdt = Math.Exp(v * dt);
        var den = u - d;
        var pu = den == 0 ? 1.0 / 3 : Math.Clamp((erdt - evdt * d) / den, 0, 1);
        var pd = den == 0 ? 1.0 / 3 : Math.Clamp((evdt * u - erdt) / den, 0, 1);
        var pm = Math.Clamp(1 - pu - pd, 0, 1);
        var disc = Math.Exp(-r * dt);
        var n = 2 * steps + 1;
        var values = new double[n];
        for (var j = 0; j < n; j++)
        {
            var st = S * Math.Pow(u, j - steps);
            values[j] = type == "call" ? Math.Max(0, st - K) : Math.Max(0, K - st);
        }
        for (var i = steps - 1; i >= 0; i--)
        {
            var next = new double[n];
            for (var j = steps - i; j <= steps + i; j++)
            {
                var cont = disc * (pu * values[j + 2] + pm * values[j + 1] + pd * values[j]);
                var st = S * Math.Pow(u, j - steps);
                var intrinsic = type == "call" ? Math.Max(0, st - K) : Math.Max(0, K - st);
                next[j + 1] = Math.Max(intrinsic, cont);
            }
            values = next;
        }
        return values[steps];
    }

    // --- Monte Carlo GBM European ---
    public static double MonteCarloPrice(
        double S, double K, double T, double sigma, double r, double q, string type, int paths, int seed)
    {
        paths = Math.Clamp(paths, 40, 800);
        var drift = (r - q - 0.5 * sigma * sigma) * T;
        var vol = sigma * Math.Sqrt(T);
        var disc = Math.Exp(-r * T);
        var rng = new Random(seed);
        double sum = 0;
        for (var i = 0; i < paths; i++)
        {
            var z = NextGaussian(rng);
            var st = S * Math.Exp(drift + vol * z);
            sum += type == "call" ? Math.Max(0, st - K) : Math.Max(0, K - st);
        }
        return disc * sum / paths;
    }

    // --- Heston (Euler vol + GBM) ---
    public static double HestonPrice(
        double S, double K, double T, double sigma, double r, double q, string type, int paths, int seed,
        double kappa = 2.0, double theta = 0.04, double xi = 0.5, double rho = -0.6)
    {
        paths = Math.Clamp(paths, 40, 800);
        var steps = Math.Clamp((int)Math.Ceiling(T * 24), 4, 16);
        var dt = T / steps;
        var sqrtDt = Math.Sqrt(dt);
        var v0 = Math.Max(1e-6, sigma * sigma);
        theta = Math.Max(1e-6, theta);
        var disc = Math.Exp(-r * T);
        var rng = new Random(seed);
        double sum = 0;
        for (var p = 0; p < paths; p++)
        {
            var s = S;
            var v = v0;
            for (var i = 0; i < steps; i++)
            {
                var z1 = NextGaussian(rng);
                var z2 = rho * z1 + Math.Sqrt(Math.Max(0, 1 - rho * rho)) * NextGaussian(rng);
                v = Math.Max(1e-8, v + kappa * (theta - v) * dt + xi * Math.Sqrt(Math.Max(v, 0)) * sqrtDt * z2);
                s *= Math.Exp((r - q - 0.5 * v) * dt + Math.Sqrt(Math.Max(v, 0)) * sqrtDt * z1);
            }
            sum += type == "call" ? Math.Max(0, s - K) : Math.Max(0, K - s);
        }
        return disc * sum / paths;
    }

    // --- Merton jump-diffusion ---
    public static double MertonPrice(
        double S, double K, double T, double sigma, double r, double q, string type, int paths, int seed,
        double lambda = 0.35, double muJ = -0.08, double sigmaJ = 0.18)
    {
        paths = Math.Clamp(paths, 40, 800);
        var steps = Math.Clamp((int)Math.Ceiling(T * 18), 4, 12);
        var dt = T / steps;
        var sqrtDt = Math.Sqrt(dt);
        var kBar = Math.Exp(muJ + 0.5 * sigmaJ * sigmaJ) - 1;
        var disc = Math.Exp(-r * T);
        var rng = new Random(seed);
        double sum = 0;
        for (var p = 0; p < paths; p++)
        {
            var s = S;
            for (var i = 0; i < steps; i++)
            {
                var z = NextGaussian(rng);
                s *= Math.Exp((r - q - lambda * kBar - 0.5 * sigma * sigma) * dt + sigma * sqrtDt * z);
                if (rng.NextDouble() < lambda * dt)
                {
                    var j = Math.Exp(muJ + sigmaJ * NextGaussian(rng));
                    s *= j;
                }
            }
            sum += type == "call" ? Math.Max(0, s - K) : Math.Max(0, K - s);
        }
        return disc * sum / paths;
    }

    /// <summary>
    /// Fully-implicit finite-difference on the European Black–Scholes PDE
    /// ∂V/∂t + (r−q)S ∂V/∂S + ½σ²S² ∂V/∂S² − rV = 0.
    /// </summary>
    public static double FdmPrice(double S, double K, double T, double sigma, double r, double q, string type)
    {
        S = Math.Max(1e-6, S);
        K = Math.Max(1e-6, K);
        T = Math.Max(1e-6, T);
        sigma = Math.Max(1e-4, sigma);

        const int M = 100; // space nodes
        var N = Math.Clamp((int)Math.Ceiling(T * 160), 24, 100);
        var Smax = Math.Max(S, K) * 4.0;
        var dS = Smax / M;
        var dt = T / N;
        var isPut = type == "put";

        var V = new double[M + 1];
        for (var i = 0; i <= M; i++)
        {
            var s = i * dS;
            V[i] = isPut ? Math.Max(K - s, 0) : Math.Max(s - K, 0);
        }

        // Tridiagonal: a[i] V[i-1] + b[i] V[i] + c[i] V[i+1] = V_old[i]
        var a = new double[M + 1];
        var b = new double[M + 1];
        var c = new double[M + 1];
        var rhs = new double[M + 1];

        for (var n = 0; n < N; n++)
        {
            var tau = (n + 1) * dt; // time from expiry after this step

            for (var i = 1; i < M; i++)
            {
                var Si = i * dS;
                var sig2 = sigma * sigma;
                var alpha = 0.5 * dt * (sig2 * Si * Si / (dS * dS) - (r - q) * Si / dS);
                var beta = 1.0 + dt * (sig2 * Si * Si / (dS * dS) + r);
                var gamma = 0.5 * dt * (sig2 * Si * Si / (dS * dS) + (r - q) * Si / dS);
                a[i] = -alpha;
                b[i] = beta;
                c[i] = -gamma;
                rhs[i] = V[i];
            }

            // Boundary conditions at next time level
            if (isPut)
            {
                V[0] = K * Math.Exp(-r * tau);
                V[M] = 0;
            }
            else
            {
                V[0] = 0;
                V[M] = Smax * Math.Exp(-q * tau) - K * Math.Exp(-r * tau);
            }

            // Move known boundary contribution into RHS for i=1 and i=M-1
            rhs[1] -= a[1] * V[0];
            a[1] = 0;
            rhs[M - 1] -= c[M - 1] * V[M];
            c[M - 1] = 0;

            SolveTridiagonal(a, b, c, rhs, 1, M - 1);
            for (var i = 1; i < M; i++)
                V[i] = rhs[i];
        }

        // Linear interpolate at spot S
        var idx = Math.Clamp((int)(S / dS), 0, M - 1);
        var w = (S - idx * dS) / dS;
        return (1 - w) * V[idx] + w * V[idx + 1];
    }

    /// <summary>Thomas algorithm on interior nodes [lo..hi].</summary>
    private static void SolveTridiagonal(double[] a, double[] b, double[] c, double[] d, int lo, int hi)
    {
        var n = hi - lo + 1;
        var cp = new double[n];
        var dp = new double[n];

        cp[0] = c[lo] / b[lo];
        dp[0] = d[lo] / b[lo];
        for (var i = 1; i < n; i++)
        {
            var j = lo + i;
            var denom = b[j] - a[j] * cp[i - 1];
            if (Math.Abs(denom) < 1e-14) denom = 1e-14;
            cp[i] = i < n - 1 ? c[j] / denom : 0;
            dp[i] = (d[j] - a[j] * dp[i - 1]) / denom;
        }

        d[hi] = dp[n - 1];
        for (var i = n - 2; i >= 0; i--)
            d[lo + i] = dp[i] - cp[i] * d[lo + i + 1];
    }

    private static ModelGreeks Sanitize(ModelGreeks g) => new(
        double.IsFinite(g.Price) ? g.Price : 0,
        double.IsFinite(g.Delta) ? g.Delta : 0,
        double.IsFinite(g.Gamma) ? g.Gamma : 0,
        double.IsFinite(g.Vega) ? g.Vega : 0,
        double.IsFinite(g.Theta) ? g.Theta : 0);

    private static double NextGaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    private static int Hash(double S, double K, double T, double sigma, string type)
    {
        unchecked
        {
            var h = 17;
            h = h * 31 + S.GetHashCode();
            h = h * 31 + K.GetHashCode();
            h = h * 31 + T.GetHashCode();
            h = h * 31 + sigma.GetHashCode();
            h = h * 31 + (type == "put" ? 2 : 1);
            return h == int.MinValue ? 1 : Math.Abs(h);
        }
    }

    /// <summary>Seed for CRN bumps — independent of spot/vol so finite differences cancel path noise.</summary>
    private static int Hash(double K, double T, string type)
    {
        unchecked
        {
            var h = 41;
            h = h * 31 + K.GetHashCode();
            h = h * 31 + T.GetHashCode();
            h = h * 31 + (type == "put" ? 2 : 1);
            return h == int.MinValue ? 1 : Math.Abs(h);
        }
    }

    public static int StableHash(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (var c in s)
            {
                h ^= c;
                h *= 16777619;
            }
            return (int)(h & 0x7fffffff);
        }
    }
}
