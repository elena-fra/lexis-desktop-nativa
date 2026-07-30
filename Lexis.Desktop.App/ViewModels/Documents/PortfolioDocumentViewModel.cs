using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Lexis.Desktop.App.Services;

namespace Lexis.Desktop.App.ViewModels.Documents;

/// <summary>Portafoglio — web parity: Net Liq, BP, risk greeks, β-weighted expo, equity curve.</summary>
public partial class PortfolioDocumentViewModel : Document
{
    private readonly IPortfolioFeed _feed;
    private readonly Action<string>? _navigate;

    public ObservableCollection<PortfolioExpoRowViewModel> ExpoRows { get; } = new();
    public ObservableCollection<PortfolioPosRowViewModel> PositionRows { get; } = new();

    [ObservableProperty] private string _accountType = "demo";
    [ObservableProperty] private string _range = "1M";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _equityLabel = "—";
    [ObservableProperty] private string _bpLabel = "—";
    [ObservableProperty] private string _cashLabel = "—";
    [ObservableProperty] private string _marginLabel = "—";
    [ObservableProperty] private string _utilLabel = "—";
    [ObservableProperty] private string _openPlLabel = "—";
    [ObservableProperty] private string _riskLabel = "";
    [ObservableProperty] private string _bwdLabel = "—";
    [ObservableProperty] private string _deltaDollarLabel = "—";
    [ObservableProperty] private string _gammaLabel = "—";
    [ObservableProperty] private string _thetaLabel = "—";
    [ObservableProperty] private string _vegaLabel = "—";
    [ObservableProperty] private string _grossLabel = "—";
    [ObservableProperty] private string _scenarioLabel = "";
    [ObservableProperty] private string _curveSub = "";
    [ObservableProperty] private string _marginBarUsed = "40*";
    [ObservableProperty] private string _marginBarFree = "60*";
    [ObservableProperty] private double _marginUsedWidth = 120;
    [ObservableProperty] private double _marginFreeWidth = 180;
    [ObservableProperty] private string _equityPath = "M 0,70 L 640,70";
    [ObservableProperty] private IBrush _equityBrush = SolidColorBrush.Parse("#86EFAC");
    [ObservableProperty] private IBrush _openPlBrush = SolidColorBrush.Parse("#86EFAC");
    [ObservableProperty] private IBrush _bpBrush = SolidColorBrush.Parse("#86EFAC");
    [ObservableProperty] private IBrush _utilBrush = SolidColorBrush.Parse("#86EFAC");
    [ObservableProperty] private IBrush _riskBrush = SolidColorBrush.Parse("#86EFAC");

    public bool IsDemo => AccountType == "demo";
    public bool IsReal => AccountType == "real";
    public bool Range1G => Range == "1G";
    public bool Range1S => Range == "1S";
    public bool Range1M => Range == "1M";
    public bool RangeYtd => Range == "YTD";

    public PortfolioDocumentViewModel(IPortfolioFeed feed, Action<string>? navigate = null)
    {
        _feed = feed;
        _navigate = navigate;
        Id = "portfolio";
        Title = "Portafoglio";
        CanClose = true;
        Reload();
    }

    partial void OnAccountTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDemo));
        OnPropertyChanged(nameof(IsReal));
        Reload();
    }

    partial void OnRangeChanged(string value)
    {
        OnPropertyChanged(nameof(Range1G));
        OnPropertyChanged(nameof(Range1S));
        OnPropertyChanged(nameof(Range1M));
        OnPropertyChanged(nameof(RangeYtd));
        Reload();
    }

    [RelayCommand] private void SetAccount(string? t) { if (!string.IsNullOrWhiteSpace(t)) AccountType = t.ToLowerInvariant(); }
    [RelayCommand] private void SetRange(string? r) { if (!string.IsNullOrWhiteSpace(r)) Range = r.ToUpperInvariant(); }
    [RelayCommand] private void Reload() => Rebuild();
    [RelayCommand] private void OpenPositions() => _navigate?.Invoke("positions");
    [RelayCommand] private void OpenGreche() => _navigate?.Invoke("greche");

    private void Rebuild()
    {
        var snap = _feed.Build(new PortfolioQuery(AccountType, Range));
        EquityLabel = FormatEuro(snap.Equity);
        BpLabel = FormatEuro(snap.BuyingPower);
        CashLabel = FormatEuro(snap.Cash);
        MarginLabel = FormatEuro(snap.MarginUsed);
        UtilLabel = $"{snap.MarginUtilPct:0.0}%";
        OpenPlLabel = FormatSignedEuro(snap.OpenPl);
        RiskLabel = snap.RiskLabel;
        BwdLabel = FormatSigned(snap.NetBetaWeightedDelta);
        DeltaDollarLabel = FormatSignedEuro(snap.NetDeltaDollar);
        GammaLabel = snap.NetGamma.ToString("0.00");
        ThetaLabel = FormatSignedEuro(snap.NetThetaDay);
        VegaLabel = FormatSignedEuro(snap.NetVega);
        GrossLabel = $"€{snap.GrossNotional / 1000:0}k";
        ScenarioLabel =
            $"Se SPX si muove di 1 punto → {FormatSignedEuro(snap.Spx1Pt)} · se SPX si muove di 1% → {FormatSignedEuro(snap.Spx1Pct)}";
        CurveSub =
            $"{(snap.EquityChange >= 0 ? "▲ +" : "▼ −")}€{Math.Abs(snap.EquityChange):0} " +
            $"({(snap.EquityChangePct >= 0 ? "+" : "")}{snap.EquityChangePct:0.00}%) · Net Liq €{snap.Equity:N0}";
        Subtitle =
            $"conto {(snap.IsDemo ? "demo paper" : "reale")} · Net Liq €{snap.Equity:N0} · BP €{snap.BuyingPower:N0} · " +
            $"β-Δ {FormatSigned(snap.NetBetaWeightedDelta)} SPX-eq · {snap.SourceLabel}";

        OpenPlBrush = Brush(snap.OpenPl);
        BpBrush = Brush(snap.BuyingPower);
        EquityBrush = Brush(snap.EquityChange);
        UtilBrush = snap.RiskTone switch
        {
            "bad" => SolidColorBrush.Parse("#FCA5A5"),
            "warn" => SolidColorBrush.Parse("#D4A8B0"),
            _ => SolidColorBrush.Parse("#86EFAC"),
        };
        RiskBrush = UtilBrush;

        var util = Math.Clamp(snap.MarginUtilPct, 0, 100);
        MarginUsedWidth = Math.Max(4, 300 * util / 100.0);
        MarginFreeWidth = Math.Max(4, 300 - MarginUsedWidth);

        EquityPath = BuildPath(snap.EquitySeries);

        ExpoRows.Clear();
        foreach (var u in snap.ByUnderlying)
            ExpoRows.Add(PortfolioExpoRowViewModel.From(u));

        PositionRows.Clear();
        foreach (var p in snap.Positions)
            PositionRows.Add(PortfolioPosRowViewModel.From(p));
    }

    private static string BuildPath(IReadOnlyList<double> series)
    {
        if (series.Count < 2) return "M 0,70 L 640,70";
        const double w = 640, h = 140, pad = 8;
        var min = series.Min();
        var max = series.Max();
        var span = Math.Max(1, max - min);
        var sb = new StringBuilder(series.Count * 16);
        for (var i = 0; i < series.Count; i++)
        {
            var x = pad + (w - pad * 2) * i / (series.Count - 1.0);
            var y = pad + (h - pad * 2) * (1.0 - (series[i] - min) / span);
            sb.Append(i == 0 ? 'M' : 'L');
            sb.Append(' ');
            sb.Append(x.ToString("0.##", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(y.ToString("0.##", CultureInfo.InvariantCulture));
            sb.Append(' ');
        }
        return sb.ToString();
    }

    private static IBrush Brush(double v) =>
        v >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5");

    private static string FormatEuro(double v) => $"€{v:N0}";
    private static string FormatSignedEuro(double v) =>
        v >= 0 ? $"+€{v:N0}" : $"-€{Math.Abs(v):N0}";
    private static string FormatSigned(double v) =>
        v >= 0 ? $"+{v:0}" : $"{v:0}";
}

public partial class PortfolioExpoRowViewModel : ObservableObject
{
    public string Symbol { get; init; } = "";
    public string SpotLabel { get; init; } = "";
    public string BetaLabel { get; init; } = "";
    public string LegsLabel { get; init; } = "";
    public string DeltaDollarLabel { get; init; } = "";
    public string BwdLabel { get; init; } = "";
    public string RiskPctLabel { get; init; } = "";
    public IBrush DeltaBrush { get; init; } = SolidColorBrush.Parse("#E8DFE4");
    public IBrush BwdBrush { get; init; } = SolidColorBrush.Parse("#E8DFE4");

    public static PortfolioExpoRowViewModel From(PortfolioUnderlyingExpoDto u) => new()
    {
        Symbol = u.Symbol,
        SpotLabel = u.Spot >= 1000 ? $"€{u.Spot:0}" : $"€{u.Spot:0.00}",
        BetaLabel = u.Beta.ToString("0.00"),
        LegsLabel = u.Legs.ToString(),
        DeltaDollarLabel = u.DeltaDollar >= 0 ? $"+€{u.DeltaDollar:N0}" : $"-€{Math.Abs(u.DeltaDollar):N0}",
        BwdLabel = u.BetaWeightedDelta >= 0 ? $"+{u.BetaWeightedDelta:0}" : $"{u.BetaWeightedDelta:0}",
        RiskPctLabel = $"{u.RiskPct:0}%",
        DeltaBrush = u.DeltaDollar >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5"),
        BwdBrush = u.BetaWeightedDelta >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5"),
    };
}

public partial class PortfolioPosRowViewModel : ObservableObject
{
    public string Instrument { get; init; } = "";
    public string Side { get; init; } = "";
    public string QtyLabel { get; init; } = "";
    public string PlLabel { get; init; } = "";
    public string DeltaLabel { get; init; } = "";
    public string BwdLabel { get; init; } = "";
    public IBrush SideBrush { get; init; } = SolidColorBrush.Parse("#D4A8B0");
    public IBrush PlBrush { get; init; } = SolidColorBrush.Parse("#86EFAC");

    public static PortfolioPosRowViewModel From(PortfolioPositionDto p) => new()
    {
        Instrument = p.Instrument,
        Side = p.Side,
        QtyLabel = p.Qty.ToString(),
        PlLabel = p.Pl >= 0 ? $"+€{p.Pl:N0}" : $"-€{Math.Abs(p.Pl):N0}",
        DeltaLabel = p.DeltaDollar >= 0 ? $"+{p.DeltaDollar:0}" : $"{p.DeltaDollar:0}",
        BwdLabel = p.BetaWeightedDelta >= 0 ? $"+{p.BetaWeightedDelta:0}" : $"{p.BetaWeightedDelta:0}",
        SideBrush = p.Side == "LONG" ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5"),
        PlBrush = p.Pl >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5"),
    };
}
