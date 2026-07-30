using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Lexis.Contracts.Market;
using Lexis.Desktop.App.Services;

namespace Lexis.Desktop.App.ViewModels.Documents;

/// <summary>Greche desk — matrix + heatmap / intensity map + greche di posizione.</summary>
public partial class GrecheDocumentViewModel : Document
{
    private readonly IGreeksFeed _feed;
    private readonly IPortfolioFeed _portfolio;

    public ObservableCollection<string> SymbolOptions { get; } = new();
    public ObservableCollection<ExpiryDto> ExpiryOptions { get; } = new();
    public ObservableCollection<GrecheRowViewModel> Rows { get; } = new();
    public ObservableCollection<GrecheIntensityCellViewModel> IntensityCells { get; } = new();
    public ObservableCollection<GrechePosRowViewModel> PositionRows { get; } = new();

    [ObservableProperty] private string _selectedSymbol = "SPX";
    [ObservableProperty] private ExpiryDto? _selectedExpiry;
    [ObservableProperty] private string _side = "call";
    [ObservableProperty] private int _strikeCount = 12;
    [ObservableProperty] private string _displayMode = "contract";
    [ObservableProperty] private string _heatMode = "gamma";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _spotLabel = "—";
    [ObservableProperty] private bool _colDelta = true;
    [ObservableProperty] private bool _colGamma = true;
    [ObservableProperty] private bool _colTheta = true;
    [ObservableProperty] private bool _colVega = true;
    [ObservableProperty] private bool _colRho;
    [ObservableProperty] private bool _colVanna;
    [ObservableProperty] private bool _colCharm;
    [ObservableProperty] private bool _colVomma;
    [ObservableProperty] private bool _columnsOpen;
    [ObservableProperty] private bool _showPositionPanel = true;
    [ObservableProperty] private bool _heatLegendVisible;
    [ObservableProperty] private string _heatLegendTitle = "";
    [ObservableProperty] private string _heatLegendLo = "";
    [ObservableProperty] private string _heatLegendHi = "";
    [ObservableProperty] private string _heatLegendNote = "";
    [ObservableProperty] private IBrush _heatGradStart = Brushes.Transparent;
    [ObservableProperty] private IBrush _heatGradMid = Brushes.Transparent;
    [ObservableProperty] private IBrush _heatGradEnd = Brushes.Transparent;
    [ObservableProperty] private bool _hasPositions;
    [ObservableProperty] private string _posTitle = "Greche di posizione";
    [ObservableProperty] private string _posDeltaLabel = "—";
    [ObservableProperty] private string _posGammaLabel = "—";
    [ObservableProperty] private string _posThetaLabel = "—";
    [ObservableProperty] private string _posVegaLabel = "—";
    [ObservableProperty] private string _posBwdLabel = "—";
    [ObservableProperty] private string _posBwdHint = "";
    [ObservableProperty] private IBrush _posDeltaBrush = SolidColorBrush.Parse("#86EFAC");
    [ObservableProperty] private IBrush _posGammaBrush = SolidColorBrush.Parse("#86EFAC");
    [ObservableProperty] private IBrush _posThetaBrush = SolidColorBrush.Parse("#FCA5A5");
    [ObservableProperty] private IBrush _posVegaBrush = SolidColorBrush.Parse("#86EFAC");
    [ObservableProperty] private IBrush _posBwdBrush = SolidColorBrush.Parse("#86EFAC");

    public bool SideCall => Side == "call";
    public bool SidePut => Side == "put";
    public bool Strike6 => StrikeCount == 6;
    public bool Strike12 => StrikeCount == 12;
    public bool Strike20 => StrikeCount == 20;
    public bool StrikeAll => StrikeCount >= 40;
    public bool DisplayContract => DisplayMode == "contract";
    public bool DisplayMoney => DisplayMode == "money";
    public bool HeatOff => HeatMode == "none";
    public bool HeatDelta => HeatMode == "delta";
    public bool HeatGamma => HeatMode == "gamma";
    public bool HeatTheta => HeatMode == "theta";
    public bool HeatVega => HeatMode == "vega";
    public bool HeatRho => HeatMode == "rho";
    public bool HeatVanna => HeatMode == "vanna";
    public bool HeatCharm => HeatMode == "charm";
    public bool HeatVomma => HeatMode == "vomma";

    public GrecheDocumentViewModel(IGreeksFeed feed, IPortfolioFeed? portfolio = null)
    {
        _feed = feed;
        _portfolio = portfolio ?? new MockPortfolioFeed();
        Id = "greche";
        Title = "Greche";
        CanClose = true;

        foreach (var u in feed.Underlyings)
            SymbolOptions.Add(u.Symbol);

        if (!SymbolOptions.Contains(SelectedSymbol) && SymbolOptions.Count > 0)
            SelectedSymbol = SymbolOptions.Contains("SPX") ? "SPX" : SymbolOptions[0];

        ReloadExpiries();
        Reload();
    }

    partial void OnSelectedSymbolChanged(string value)
    {
        ReloadExpiries();
        Reload();
    }

    partial void OnSelectedExpiryChanged(ExpiryDto? value) => Reload();
    partial void OnSideChanged(string value)
    {
        OnPropertyChanged(nameof(SideCall));
        OnPropertyChanged(nameof(SidePut));
        Reload();
    }

    partial void OnStrikeCountChanged(int value)
    {
        OnPropertyChanged(nameof(Strike6));
        OnPropertyChanged(nameof(Strike12));
        OnPropertyChanged(nameof(Strike20));
        OnPropertyChanged(nameof(StrikeAll));
        Reload();
    }

    partial void OnDisplayModeChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayContract));
        OnPropertyChanged(nameof(DisplayMoney));
        Reload();
    }

    partial void OnHeatModeChanged(string value)
    {
        OnPropertyChanged(nameof(HeatOff));
        OnPropertyChanged(nameof(HeatDelta));
        OnPropertyChanged(nameof(HeatGamma));
        OnPropertyChanged(nameof(HeatTheta));
        OnPropertyChanged(nameof(HeatVega));
        OnPropertyChanged(nameof(HeatRho));
        OnPropertyChanged(nameof(HeatVanna));
        OnPropertyChanged(nameof(HeatCharm));
        OnPropertyChanged(nameof(HeatVomma));
        EnsureHeatColumn(value);
        Reload();
    }

    partial void OnColDeltaChanged(bool value) => Reload();
    partial void OnColGammaChanged(bool value) => Reload();
    partial void OnColThetaChanged(bool value) => Reload();
    partial void OnColVegaChanged(bool value) => Reload();
    partial void OnColRhoChanged(bool value) => Reload();
    partial void OnColVannaChanged(bool value) => Reload();
    partial void OnColCharmChanged(bool value) => Reload();
    partial void OnColVommaChanged(bool value) => Reload();

    private void EnsureHeatColumn(string heat)
    {
        switch (heat)
        {
            case "delta": ColDelta = true; break;
            case "gamma": ColGamma = true; break;
            case "theta": ColTheta = true; break;
            case "vega": ColVega = true; break;
            case "rho": ColRho = true; break;
            case "vanna": ColVanna = true; break;
            case "charm": ColCharm = true; break;
            case "vomma": ColVomma = true; break;
        }
    }

    private void ReloadExpiries()
    {
        ExpiryOptions.Clear();
        foreach (var e in _feed.BuildExpiries(SelectedSymbol))
            ExpiryOptions.Add(e);
        SelectedExpiry = ExpiryOptions.FirstOrDefault(e => e.Dte >= 14) ?? ExpiryOptions.FirstOrDefault();
    }

    [RelayCommand] private void SetSymbol(string? s) { if (!string.IsNullOrWhiteSpace(s)) SelectedSymbol = s.ToUpperInvariant(); }
    [RelayCommand] private void SetSide(string? s) { if (!string.IsNullOrWhiteSpace(s)) Side = s.ToLowerInvariant(); }
    [RelayCommand] private void SetStrikeCount(string? s)
    {
        if (string.Equals(s, "all", StringComparison.OrdinalIgnoreCase)) StrikeCount = 41;
        else if (int.TryParse(s, out var n)) StrikeCount = n;
    }
    [RelayCommand] private void SetDisplay(string? s) { if (!string.IsNullOrWhiteSpace(s)) DisplayMode = s.ToLowerInvariant(); }
    [RelayCommand] private void SetHeat(string? s) { if (!string.IsNullOrWhiteSpace(s)) HeatMode = s.ToLowerInvariant(); }
    [RelayCommand] private void ToggleColumns() => ColumnsOpen = !ColumnsOpen;
    [RelayCommand] private void ClosePositionPanel() => ShowPositionPanel = false;
    [RelayCommand] private void OpenPositionPanel() => ShowPositionPanel = true;
    [RelayCommand] private void TogglePositionPanel() => ShowPositionPanel = !ShowPositionPanel;
    [RelayCommand] private void Reload() => Rebuild();

    private void Rebuild()
    {
        var snap = _feed.Build(
            SelectedSymbol,
            SelectedExpiry?.L,
            Side,
            StrikeCount);

        SpotLabel = snap.Spot.ToString("0.00");
        Subtitle =
            $"{snap.Expiry.L} · {snap.Expiry.Dte} DTE · lato {(snap.Side == "call" ? "CALL" : "PUT")} · " +
            $"{(DisplayMoney ? "valore monetario (×100)" : "valore contrattuale")} · {snap.SourceLabel}";
        StatusText = $"spot {snap.Spot:0.00}";

        var money = DisplayMoney;
        var heat = HeatMode;
        var maxBy = GreekHeatPalette.Modes.ToDictionary(
            m => m,
            m => Math.Max(1e-9, snap.Rows.Max(r => Math.Abs(GreekHeatPalette.Pick(r, m)))));

        ApplyHeatLegend(heat);

        Rows.Clear();
        IntensityCells.Clear();
        foreach (var r in snap.Rows.OrderByDescending(x => x.Strike))
        {
            Rows.Add(GrecheRowViewModel.From(
                snap.Symbol, r, money, heat, maxBy,
                ColDelta, ColGamma, ColTheta, ColVega, ColRho, ColVanna, ColCharm, ColVomma));

            if (!GreekHeatPalette.IsMode(heat))
                continue;

            var abs = Math.Abs(GreekHeatPalette.Pick(r, heat));
            var t = Math.Clamp(abs / maxBy[heat], 0, 1);
            IntensityCells.Add(new GrecheIntensityCellViewModel(
                StrikeLabel: r.Strike.ToString("0.##"),
                Intensity: t,
                Fill: GreekHeatPalette.Color(heat, t),
                IsAtm: r.IsAtm,
                Tip: GreekHeatPalette.Tip(r, heat)));
        }

        RebuildPositionRail();
    }

    private void RebuildPositionRail()
    {
        var snap = _portfolio.Build(new PortfolioQuery("demo", "1M"));
        HasPositions = snap.Positions.Count > 0;
        PosTitle = HasPositions ? "Greche di posizione · portafoglio aperto" : "Greche di posizione";

        PositionRows.Clear();
        if (!HasPositions) return;

        var pd = snap.Positions.Sum(p => p.DeltaDollar);
        var pg = snap.Positions.Sum(p => p.Gamma);
        var pt = snap.Positions.Sum(p => p.ThetaDay);
        var pv = snap.Positions.Sum(p => p.Vega);
        var bwd = snap.NetBetaWeightedDelta;
        var spy = 520.0;
        var spyMove = bwd * spy * 0.01;

        PosDeltaLabel = (pd >= 0 ? "+" : "") + "€" + Math.Abs(pd).ToString("N0");
        PosGammaLabel = (pg >= 0 ? "+" : "") + pg.ToString("0.0");
        PosThetaLabel = (pt >= 0 ? "+" : "") + "€" + Math.Abs(pt).ToString("N0");
        PosVegaLabel = (pv >= 0 ? "+" : "") + "€" + Math.Abs(pv).ToString("N0");
        PosBwdLabel = (bwd >= 0 ? "+" : "") + Math.Abs(bwd).ToString("0") + " SPY-eq";
        PosBwdHint =
            $"Se l'S&P 500 si muove dell'1% (~€{spy * 0.01:0} di SPY), il portafoglio " +
            $"{(spyMove >= 0 ? "guadagna" : "perde")} ≈ €{Math.Abs(spyMove):N0}";
        PosDeltaBrush = pd >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5");
        PosGammaBrush = pg >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5");
        PosThetaBrush = pt >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5");
        PosVegaBrush = pv >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5");
        PosBwdBrush = bwd >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5");

        foreach (var p in snap.Positions)
            PositionRows.Add(GrechePosRowViewModel.From(p));
    }

    private void ApplyHeatLegend(string heat)
    {
        var meta = GreekHeatPalette.Meta(heat);
        HeatLegendVisible = meta is not null;
        if (meta is null)
        {
            HeatLegendTitle = "";
            HeatLegendNote = "";
            return;
        }
        HeatLegendTitle = meta.Title;
        HeatLegendLo = meta.Lo;
        HeatLegendHi = meta.Hi;
        HeatLegendNote = meta.Note;
        HeatGradStart = meta.GradStart;
        HeatGradMid = meta.GradMid;
        HeatGradEnd = meta.GradEnd;
    }
}

public sealed record GrecheIntensityCellViewModel(
    string StrikeLabel,
    double Intensity,
    IBrush Fill,
    bool IsAtm,
    string Tip);

public partial class GrechePosRowViewModel : ObservableObject
{
    public string Instrument { get; init; } = "";
    public string Side { get; init; } = "";
    public string QtyLabel { get; init; } = "";
    public string DeltaLabel { get; init; } = "";
    public string BwdLabel { get; init; } = "";
    public IBrush SideBrush { get; init; } = SolidColorBrush.Parse("#D4A8B0");
    public IBrush DeltaBrush { get; init; } = SolidColorBrush.Parse("#E8DFE4");
    public IBrush BwdBrush { get; init; } = SolidColorBrush.Parse("#E8DFE4");

    public static GrechePosRowViewModel From(PortfolioPositionDto p) => new()
    {
        Instrument = p.Instrument,
        Side = p.Side,
        QtyLabel = p.Qty.ToString(),
        DeltaLabel = p.DeltaDollar >= 0 ? $"+{p.DeltaDollar:0}" : $"{p.DeltaDollar:0}",
        BwdLabel = p.BetaWeightedDelta >= 0 ? $"+{p.BetaWeightedDelta:0}" : $"{p.BetaWeightedDelta:0}",
        SideBrush = p.Side == "LONG" ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5"),
        DeltaBrush = p.DeltaDollar >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5"),
        BwdBrush = p.BetaWeightedDelta >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5"),
    };
}

public partial class GrecheRowViewModel : ObservableObject
{
    public string StrikeLabel { get; init; } = "";
    public string MnyLabel { get; init; } = "";
    public string IvLabel { get; init; } = "";
    public string DeltaLabel { get; init; } = "";
    public string GammaLabel { get; init; } = "";
    public string ThetaLabel { get; init; } = "";
    public string VegaLabel { get; init; } = "";
    public string RhoLabel { get; init; } = "";
    public string VannaLabel { get; init; } = "";
    public string CharmLabel { get; init; } = "";
    public string VommaLabel { get; init; } = "";
    public bool ShowDelta { get; init; }
    public bool ShowGamma { get; init; }
    public bool ShowTheta { get; init; }
    public bool ShowVega { get; init; }
    public bool ShowRho { get; init; }
    public bool ShowVanna { get; init; }
    public bool ShowCharm { get; init; }
    public bool ShowVomma { get; init; }
    public bool IsAtm { get; init; }
    public IBrush RowBg { get; init; } = SolidColorBrush.Parse("#100E14");
    public IBrush DeltaFg { get; init; } = SolidColorBrush.Parse("#E8DFE4");
    public IBrush ThetaFg { get; init; } = SolidColorBrush.Parse("#FCA5A5");
    public IBrush DeltaHeatBg { get; init; } = Brushes.Transparent;
    public IBrush GammaHeatBg { get; init; } = Brushes.Transparent;
    public IBrush ThetaHeatBg { get; init; } = Brushes.Transparent;
    public IBrush VegaHeatBg { get; init; } = Brushes.Transparent;
    public IBrush RhoHeatBg { get; init; } = Brushes.Transparent;
    public IBrush VannaHeatBg { get; init; } = Brushes.Transparent;
    public IBrush CharmHeatBg { get; init; } = Brushes.Transparent;
    public IBrush VommaHeatBg { get; init; } = Brushes.Transparent;

    public static GrecheRowViewModel From(
        string symbol,
        GreeksStrikeRow r,
        bool money,
        string heat,
        IReadOnlyDictionary<string, double> maxBy,
        bool d, bool g, bool t, bool v, bool rho, bool va, bool ch, bool vo)
    {
        string Fmt(string key, double val)
        {
            if (money)
            {
                var m = val * 100;
                return $"€{(Math.Abs(m) >= 100 ? m.ToString("0") : m.ToString("0.00"))}";
            }
            return key is "gamma" or "vanna" or "charm" or "vomma"
                ? val.ToString("0.0000")
                : val.ToString("0.000");
        }

        IBrush HeatBg(string mode, double value)
        {
            if (heat != mode) return Brushes.Transparent;
            var max = maxBy.TryGetValue(mode, out var m) ? m : 1e-9;
            return GreekHeatPalette.Color(mode, Math.Clamp(Math.Abs(value) / max, 0, 1));
        }

        return new GrecheRowViewModel
        {
            StrikeLabel = $"{symbol} {r.Strike:0.##}" + (r.IsAtm ? "  ATM" : ""),
            MnyLabel = r.IsItm ? "ITM" : "OTM",
            IvLabel = $"{r.IvPct:0.0}%",
            DeltaLabel = Fmt("delta", r.Delta),
            GammaLabel = Fmt("gamma", r.Gamma),
            ThetaLabel = Fmt("theta", r.Theta),
            VegaLabel = Fmt("vega", r.Vega),
            RhoLabel = Fmt("rho", r.Rho),
            VannaLabel = Fmt("vanna", r.Vanna),
            CharmLabel = Fmt("charm", r.Charm),
            VommaLabel = Fmt("vomma", r.Vomma),
            ShowDelta = d,
            ShowGamma = g,
            ShowTheta = t,
            ShowVega = v,
            ShowRho = rho,
            ShowVanna = va,
            ShowCharm = ch,
            ShowVomma = vo,
            IsAtm = r.IsAtm,
            RowBg = r.IsAtm ? SolidColorBrush.Parse("#2A1E24") : SolidColorBrush.Parse("#100E14"),
            DeltaFg = r.Delta >= 0 ? SolidColorBrush.Parse("#86EFAC") : SolidColorBrush.Parse("#FCA5A5"),
            ThetaFg = SolidColorBrush.Parse("#FCA5A5"),
            DeltaHeatBg = HeatBg("delta", r.Delta),
            GammaHeatBg = HeatBg("gamma", r.Gamma),
            ThetaHeatBg = HeatBg("theta", r.Theta),
            VegaHeatBg = HeatBg("vega", r.Vega),
            RhoHeatBg = HeatBg("rho", r.Rho),
            VannaHeatBg = HeatBg("vanna", r.Vanna),
            CharmHeatBg = HeatBg("charm", r.Charm),
            VommaHeatBg = HeatBg("vomma", r.Vomma),
        };
    }
}
