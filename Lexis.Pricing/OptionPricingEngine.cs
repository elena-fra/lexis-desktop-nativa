using Lexis.Contracts.Pricing;

namespace Lexis.Pricing;

/// <summary>
/// Broker-independent pricing engine.
/// Europeans: Black–Scholes–Merton (explicit r, q).
/// Americans: CRR binomial with early exercise.
/// Copied from Lexis.Mexem.Gateway.Pricing.OptionPricingEngine — original untouched.
/// </summary>
public sealed class OptionPricingEngine
{
    private readonly PricingAssumptions _a;

    public OptionPricingEngine(PricingAssumptions assumptions) =>
        _a = assumptions ?? new PricingAssumptions();

    public OptionPricingEngine() : this(new PricingAssumptions()) { }

    public PricingAssumptions Assumptions => _a;

    /// <summary>Local greeks with Source = "local".</summary>
    public OptionGreeks Price(PricingInputs inputs)
    {
        var r = inputs.RiskFreeRate ?? _a.RiskFreeRate;
        var t = YearsToExpiry(inputs.AsOf, inputs.Expiry);
        var q = ResolveDividendYield(inputs.Spot, t, inputs.DividendYield, inputs.PvDividend);
        var sigma = Math.Max(inputs.ImpliedVol, 1e-4);
        if (sigma > 5) sigma /= 100.0; // accept IV in %
        var isPut = OptionRight.Normalize(inputs.Right) == "P";

        return inputs.Style == OptionExerciseStyle.American
            ? PriceAmericanCrr(inputs.Spot, inputs.Strike, t, r, q, sigma, isPut)
            : PriceEuropeanBs(inputs.Spot, inputs.Strike, t, r, q, sigma, isPut);
    }

    public double YearsToExpiry(DateOnly asOf, DateOnly expiry)
    {
        if (expiry <= asOf) return 1.0 / _a.TradingDaysPerYear;
        int days;
        if (_a.UseTradingCalendar)
        {
            days = 0;
            for (var d = asOf.AddDays(1); d <= expiry; d = d.AddDays(1))
            {
                if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                    days++;
            }
        }
        else
        {
            days = expiry.DayNumber - asOf.DayNumber;
        }
        return Math.Max(days, 1) / (double)_a.TradingDaysPerYear;
    }

    public int TradingDaysBetween(DateOnly asOf, DateOnly expiry)
    {
        if (expiry <= asOf) return 0;
        if (!_a.UseTradingCalendar) return expiry.DayNumber - asOf.DayNumber;
        var n = 0;
        for (var d = asOf.AddDays(1); d <= expiry; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) n++;
        }
        return n;
    }

    private double ResolveDividendYield(double spot, double t, double? qOverride, double? pvDiv)
    {
        if (qOverride is not null) return qOverride.Value;
        var pv = pvDiv ?? _a.PvDividendOverride;
        if (pv is > 0 && spot > 0 && t > 0)
        {
            var ratio = Math.Clamp(pv.Value / spot, 0, 0.99);
            return -Math.Log(1.0 - ratio) / t;
        }
        return _a.DividendYield;
    }

    private static OptionGreeks PriceEuropeanBs(
        double s, double k, double t, double r, double q, double sigma, bool isPut)
    {
        t = Math.Max(t, 1e-6);
        var sqrtT = Math.Sqrt(t);
        var d1 = (Math.Log(s / k) + (r - q + 0.5 * sigma * sigma) * t) / (sigma * sqrtT);
        var d2 = d1 - sigma * sqrtT;
        var discQ = Math.Exp(-q * t);
        var discR = Math.Exp(-r * t);
        var nd1 = BlackScholes.NormPdf(d1);
        var price = isPut
            ? k * discR * BlackScholes.NormCdf(-d2) - s * discQ * BlackScholes.NormCdf(-d1)
            : s * discQ * BlackScholes.NormCdf(d1) - k * discR * BlackScholes.NormCdf(d2);
        var delta = isPut ? discQ * (BlackScholes.NormCdf(d1) - 1) : discQ * BlackScholes.NormCdf(d1);
        var gamma = discQ * nd1 / (s * sigma * sqrtT);
        var thetaAnnual = isPut
            ? -s * discQ * nd1 * sigma / (2 * sqrtT) + q * s * discQ * BlackScholes.NormCdf(-d1) - r * k * discR * BlackScholes.NormCdf(-d2)
            : -s * discQ * nd1 * sigma / (2 * sqrtT) - q * s * discQ * BlackScholes.NormCdf(d1) + r * k * discR * BlackScholes.NormCdf(d2);
        var theta = thetaAnnual / 365.0;
        var vega = s * discQ * nd1 * sqrtT / 100.0;
        return new OptionGreeks(
            ImpliedVol: Math.Round(sigma * 100, 4),
            Delta: delta,
            Gamma: gamma,
            Vega: vega,
            Theta: theta,
            OptPrice: Math.Max(0, price),
            UndPrice: s,
            PvDividend: null,
            Source: GreeksSource.Local);
    }

    private OptionGreeks PriceAmericanCrr(
        double s, double k, double t, double r, double q, double sigma, bool isPut)
    {
        var n = Math.Clamp(_a.BinomialSteps, 20, 500);
        var price = AmericanCrrPrice(s, k, t, r, q, sigma, isPut, n);
        var bumpS = s * 0.001;
        var bumpVol = 0.01;
        var up = AmericanCrrPrice(s + bumpS, k, t, r, q, sigma, isPut, n);
        var dn = AmericanCrrPrice(s - bumpS, k, t, r, q, sigma, isPut, n);
        var delta = (up - dn) / (2 * bumpS);
        var gamma = (up - 2 * price + dn) / (bumpS * bumpS);
        var vUp = AmericanCrrPrice(s, k, t, r, q, sigma + bumpVol, isPut, n);
        var vega = (vUp - price) / 100.0;
        var dt = Math.Max(t / 365.0, 1e-6);
        var tUp = AmericanCrrPrice(s, k, Math.Max(t - dt, 1e-6), r, q, sigma, isPut, n);
        var theta = tUp - price;
        return new OptionGreeks(
            ImpliedVol: Math.Round(sigma * 100, 4),
            Delta: delta,
            Gamma: gamma,
            Vega: vega,
            Theta: theta,
            OptPrice: Math.Max(0, price),
            UndPrice: s,
            Source: GreeksSource.Local);
    }

    private static double AmericanCrrPrice(
        double s, double k, double t, double r, double q, double sigma, bool isPut, int n)
    {
        t = Math.Max(t, 1e-6);
        var dt = t / n;
        var u = Math.Exp(sigma * Math.Sqrt(dt));
        var d = 1.0 / u;
        var a = Math.Exp((r - q) * dt);
        var p = (a - d) / (u - d);
        p = Math.Clamp(p, 1e-8, 1 - 1e-8);
        var disc = Math.Exp(-r * dt);

        var values = new double[n + 1];
        for (var i = 0; i <= n; i++)
        {
            var st = s * Math.Pow(u, n - i) * Math.Pow(d, i);
            values[i] = isPut ? Math.Max(k - st, 0) : Math.Max(st - k, 0);
        }
        for (var step = n - 1; step >= 0; step--)
        {
            for (var i = 0; i <= step; i++)
            {
                var cont = disc * (p * values[i] + (1 - p) * values[i + 1]);
                var st = s * Math.Pow(u, step - i) * Math.Pow(d, i);
                var exercise = isPut ? Math.Max(k - st, 0) : Math.Max(st - k, 0);
                values[i] = Math.Max(cont, exercise);
            }
        }
        return values[0];
    }
}
