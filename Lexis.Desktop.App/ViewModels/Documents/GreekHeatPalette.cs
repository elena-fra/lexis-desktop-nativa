using Avalonia.Media;
using Lexis.Desktop.App.Services;

namespace Lexis.Desktop.App.ViewModels.Documents;

/// <summary>Greche heatmap / intensity-map palette for all desk greeks.</summary>
public static class GreekHeatPalette
{
    public static readonly string[] Modes =
        ["delta", "gamma", "theta", "vega", "rho", "vanna", "charm", "vomma"];

    public sealed record LegendInfo(
        string Title,
        string Lo,
        string Hi,
        string Note,
        IBrush GradStart,
        IBrush GradMid,
        IBrush GradEnd);

    public static bool IsMode(string? mode) =>
        !string.IsNullOrWhiteSpace(mode)
        && mode != "none"
        && Modes.Contains(mode, StringComparer.OrdinalIgnoreCase);

    public static IBrush Color(string mode, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var stops = mode switch
        {
            "delta" => new[] { (28, 48, 40), (72, 168, 120), (180, 220, 140) },
            "theta" => new[] { (58, 42, 38), (210, 148, 72), (214, 84, 74) },
            "vega" => new[] { (42, 38, 78), (118, 108, 188), (212, 168, 176) },
            "rho" => new[] { (36, 44, 58), (88, 130, 176), (160, 190, 220) },
            "vanna" => new[] { (48, 36, 58), (148, 96, 168), (220, 170, 210) },
            "charm" => new[] { (48, 40, 32), (168, 128, 72), (220, 180, 100) },
            "vomma" => new[] { (40, 36, 52), (120, 100, 168), (190, 150, 210) },
            _ => new[] { (34, 58, 72), (62, 168, 158), (232, 196, 96) }, // gamma
        };
        var seg = t * (stops.Length - 1);
        var i = Math.Min(stops.Length - 2, (int)Math.Floor(seg));
        var f = seg - i;
        var (r0, g0, b0) = stops[i];
        var (r1, g1, b1) = stops[i + 1];
        var r = (byte)Math.Round(r0 + (r1 - r0) * f);
        var g = (byte)Math.Round(g0 + (g1 - g0) * f);
        var b = (byte)Math.Round(b0 + (b1 - b0) * f);
        var a = (byte)Math.Round((0.42 + t * 0.5) * 255);
        return new SolidColorBrush(Avalonia.Media.Color.FromArgb(a, r, g, b));
    }

    public static LegendInfo? Meta(string mode)
    {
        if (!IsMode(mode)) return null;
        var (sym, title, note) = mode switch
        {
            "delta" => ("Δ", "Intensity map · Δ", "Colore proporzionale a |Δ| sul set di strike (esposizione direzionale)."),
            "gamma" => ("Γ", "Intensity map · Γ", "Colore proporzionale a |Γ| sul set di strike (concentrazione / pinning)."),
            "theta" => ("Θ", "Intensity map · Θ", "Colore proporzionale a |Θ| sul set di strike (decadimento temporale)."),
            "vega" => ("ν", "Intensity map · ν", "Colore proporzionale a |ν| sul set di strike (sensibilità all’IV)."),
            "rho" => ("ρ", "Intensity map · ρ", "Colore proporzionale a |ρ| sul set di strike (sensibilità ai tassi)."),
            "vanna" => ("Vanna", "Intensity map · Vanna", "Colore proporzionale a |Vanna| sul set di strike (Δ vs vol)."),
            "charm" => ("Charm", "Intensity map · Charm", "Colore proporzionale a |Charm| sul set di strike (Δ vs tempo)."),
            "vomma" => ("Vomma", "Intensity map · Vomma", "Colore proporzionale a |Vomma| sul set di strike (ν vs vol)."),
            _ => ("?", "Intensity map", ""),
        };
        return new(
            title,
            $"min |{sym}|",
            $"max |{sym}|",
            note,
            Color(mode, 0), Color(mode, 0.5), Color(mode, 1));
    }

    public static double Pick(GreeksStrikeRow r, string mode) => mode switch
    {
        "delta" => r.Delta,
        "gamma" => r.Gamma,
        "theta" => r.Theta,
        "vega" => r.Vega,
        "rho" => r.Rho,
        "vanna" => r.Vanna,
        "charm" => r.Charm,
        "vomma" => r.Vomma,
        _ => 0,
    };

    public static string Tip(GreeksStrikeRow r, string mode) => mode switch
    {
        "delta" => $"Δ {r.Delta:0.000}",
        "gamma" => $"Γ {r.Gamma:0.0000}",
        "theta" => $"Θ {r.Theta:0.000}",
        "vega" => $"ν {r.Vega:0.000}",
        "rho" => $"ρ {r.Rho:0.000}",
        "vanna" => $"Vanna {r.Vanna:0.0000}",
        "charm" => $"Charm {r.Charm:0.0000}",
        "vomma" => $"Vomma {r.Vomma:0.0000}",
        _ => r.Strike.ToString("0.##"),
    };
}
