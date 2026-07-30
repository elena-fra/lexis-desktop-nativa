namespace Lexis.Pricing;

/// <summary>
/// Black–Scholes–Merton Europeans + CRR American binomial.
/// Used by GEX multi-method engines (BS analytic greeks; CRR/FDM bump greeks).
/// </summary>
public static class BlackScholes
{
    public sealed record Greeks(double Price, double Delta, double Gamma, double Theta, double Vega);

    /// <summary>European BSM with continuous dividend yield q.</summary>
    public static Greeks Calculate(
        double s, double k, double t, double sigma, string type,
        double r = 0.042, double q = 0.0)
    {
        t = Math.Max(t, 1.0 / 365.0);
        sigma = Math.Max(sigma, 1e-4);
        s = Math.Max(s, 1e-6);
        k = Math.Max(k, 1e-6);

        var sqrtT = Math.Sqrt(t);
        var d1 = (Math.Log(s / k) + (r - q + 0.5 * sigma * sigma) * t) / (sigma * sqrtT);
        var d2 = d1 - sigma * sqrtT;
        var nd1 = NormPdf(d1);
        var disc = Math.Exp(-r * t);
        var dfq = Math.Exp(-q * t);
        var isPut = IsPut(type);

        double price, delta, theta;
        if (isPut)
        {
            price = k * disc * NormCdf(-d2) - s * dfq * NormCdf(-d1);
            delta = dfq * (NormCdf(d1) - 1);
            theta = (-s * dfq * nd1 * sigma / (2 * sqrtT)
                     + q * s * dfq * NormCdf(-d1)
                     + r * k * disc * NormCdf(-d2)) / 365.0;
        }
        else
        {
            price = s * dfq * NormCdf(d1) - k * disc * NormCdf(d2);
            delta = dfq * NormCdf(d1);
            theta = (-s * dfq * nd1 * sigma / (2 * sqrtT)
                     - q * s * dfq * NormCdf(d1)
                     - r * k * disc * NormCdf(d2)) / 365.0;
        }

        var gamma = dfq * nd1 / (s * sigma * sqrtT);
        var vega = s * dfq * nd1 * sqrtT / 100.0;
        return new Greeks(Math.Max(0, price), delta, gamma, theta, vega);
    }

    /// <summary>CRR binomial with early exercise (American). Default 64 steps.</summary>
    public static double CrrPrice(
        double S, double K, double T, double sig, double r, double q, string type, int steps = 64)
    {
        steps = Math.Clamp(steps, 8, 200);
        T = Math.Max(T, 1e-6);
        sig = Math.Max(sig, 1e-4);
        S = Math.Max(S, 1e-6);
        K = Math.Max(K, 1e-6);

        var dt = T / steps;
        var u = Math.Exp(sig * Math.Sqrt(dt));
        var d = 1.0 / u;
        var a = Math.Exp((r - q) * dt);
        var den = u - d;
        var p = den == 0 ? 0.5 : Math.Clamp((a - d) / den, 0, 1);
        var disc = Math.Exp(-r * dt);
        var isPut = IsPut(type);
        var v = new double[steps + 1];

        for (var i = 0; i <= steps; i++)
        {
            var st = S * Math.Pow(u, steps - i) * Math.Pow(d, i);
            v[i] = Math.Max(0, isPut ? K - st : st - K);
        }

        for (var step = steps - 1; step >= 0; step--)
        {
            for (var i = 0; i <= step; i++)
            {
                var cont = disc * (p * v[i] + (1 - p) * v[i + 1]);
                var st = S * Math.Pow(u, step - i) * Math.Pow(d, i);
                var ex = Math.Max(0, isPut ? K - st : st - K);
                v[i] = Math.Max(cont, ex);
            }
        }

        return v[0];
    }

    private static bool IsPut(string type) =>
        type.Equals("put", StringComparison.OrdinalIgnoreCase)
        || type.Equals("P", StringComparison.OrdinalIgnoreCase);

    internal static double NormPdf(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);

    /// <summary>Abramowitz &amp; Stegun 26.2.17 — standard normal CDF.</summary>
    internal static double NormCdf(double x)
    {
        const double a1 = 0.319381530, a2 = -0.356563782, a3 = 1.781477937, a4 = -1.821255978, a5 = 1.330274429;
        const double p = 0.2316419;
        var abs = Math.Abs(x);
        var t = 1.0 / (1.0 + p * abs);
        var y = 1.0 - NormPdf(abs) * ((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t;
        return x < 0 ? 1.0 - y : y;
    }
}
