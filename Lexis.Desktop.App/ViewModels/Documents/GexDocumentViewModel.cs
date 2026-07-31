using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Lexis.Desktop.App.Services;
using Lexis.Pricing;

namespace Lexis.Desktop.App.ViewModels.Documents;

/// <summary>GEX desk — chain engine profiles (GEX/DEX/Charma/Vanna/Speed/Vomma/Netta).</summary>
public partial class GexDocumentViewModel : Document
{
    private readonly IGexFeed _feed;
    private bool _syncingMetric;

    public ObservableCollection<string> SymbolOptions { get; } = new(MockChainFeed.Underlyings.Select(u => u.Symbol));
    public ObservableCollection<GexMetricOption> MetricOptions { get; } = new(
        GexProfileEngine.Metrics.Select(m => new GexMetricOption(m.Key, m.Label, m.Name)));
    public ObservableCollection<GexRowViewModel> Rows { get; } = new();
    public ObservableCollection<GexFlowRowViewModel> FlowRows { get; } = new();
    public ObservableCollection<GexAlertViewModel> Alerts { get; } = new();
    public ObservableCollection<GexCardViewModel> TabCards { get; } = new();

    [ObservableProperty] private string _selectedSymbol = "SPX";
    [ObservableProperty] private GexMetricOption? _selectedMetricOption;
    [ObservableProperty] private string _metricKey = "gex";
    [ObservableProperty] private string _methodKey = "black-scholes";
    [ObservableProperty] private string _tabKey = "macro";
    [ObservableProperty] private string _statusText = "GEX";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _spotLabel = "—";
    [ObservableProperty] private string _flipLabel = "—";
    [ObservableProperty] private string _callWallLabel = "—";
    [ObservableProperty] private string _putWallLabel = "—";
    [ObservableProperty] private string _netLabel = "—";
    [ObservableProperty] private string _callPutLabel = "—";
    [ObservableProperty] private string _delta24Label = "—";
    [ObservableProperty] private string _netHint = "";
    [ObservableProperty] private string _metricTitle = "GEX · Gamma Exposure";
    [ObservableProperty] private string _methodLabel = "Black-Scholes";
    [ObservableProperty] private string _structureMetricLabel = "GEX";
    [ObservableProperty] private string _panelTitle = "GEX Profile";
    [ObservableProperty] private string _tabNote = "";
    [ObservableProperty] private bool _methodBs = true;
    [ObservableProperty] private bool _methodCrr;
    [ObservableProperty] private bool _methodMc;
    [ObservableProperty] private bool _methodHeston;
    [ObservableProperty] private bool _methodFdm;
    [ObservableProperty] private IBrush _netBrush = SolidColorBrush.Parse("#D4A8B0");
    [ObservableProperty] private IBrush _deltaBrush = SolidColorBrush.Parse("#00FF7A");

    public bool TabMacro => TabKey == "macro";
    public bool TabStruct => TabKey == "struct";
    public bool TabLevels => TabKey == "levels";
    public bool TabTerm => TabKey == "term";
    public bool TabDeriv => TabKey == "deriv";
    public bool TabFlow => TabKey == "flow";
    public bool ShowProfileRows => TabMacro || TabLevels;

    public bool MetricGex => MetricKey == "gex";
    public bool MetricDex => MetricKey == "dex";
    public bool MetricCharma => MetricKey == "charma";
    public bool MetricVanna => MetricKey == "vanna";
    public bool MetricSpeed => MetricKey == "speed";
    public bool MetricVomma => MetricKey == "vomma";
    public bool MetricNet => MetricKey == "net";

    public bool TabMacroOn => TabKey == "macro";
    public bool TabStructOn => TabKey == "struct";
    public bool TabLevelsOn => TabKey == "levels";
    public bool TabTermOn => TabKey == "term";
    public bool TabDerivOn => TabKey == "deriv";
    public bool TabFlowOn => TabKey == "flow";

    public GexDocumentViewModel(IGexFeed? feed = null)
    {
        _feed = feed ?? new MockGexFeedAdapter();
        Id = "gex";
        Title = "GEX";
        CanClose = true;
        SelectedMetricOption = MetricOptions.FirstOrDefault(m => m.Key == MetricKey) ?? MetricOptions[0];
        SyncMethodFlags();
        Reload();
    }

    partial void OnSelectedSymbolChanged(string value) => Reload();

    partial void OnSelectedMetricOptionChanged(GexMetricOption? value)
    {
        if (value is null || _syncingMetric) return;
        StructureMetricLabel = value.Label;
        if (!string.Equals(MetricKey, value.Key, StringComparison.OrdinalIgnoreCase))
            MetricKey = value.Key;
    }

    partial void OnMetricKeyChanged(string value)
    {
        NotifyMetricFlags();
        _syncingMetric = true;
        try
        {
            var opt = MetricOptions.FirstOrDefault(m => m.Key == value);
            if (opt is not null && !ReferenceEquals(SelectedMetricOption, opt))
                SelectedMetricOption = opt;
            StructureMetricLabel = opt?.Label ?? value.ToUpperInvariant();
        }
        finally { _syncingMetric = false; }
        Reload();
    }

    partial void OnMethodKeyChanged(string value)
    {
        MethodLabel = MockGexFeed.Methods.FirstOrDefault(m => m.Key == value).Label
                      ?? OptionModelEngines.MethodLabel(OptionModelEngines.Parse(value));
        SyncMethodFlags();
        Reload();
    }

    partial void OnTabKeyChanged(string value)
    {
        OnPropertyChanged(nameof(TabMacro));
        OnPropertyChanged(nameof(TabStruct));
        OnPropertyChanged(nameof(TabLevels));
        OnPropertyChanged(nameof(TabTerm));
        OnPropertyChanged(nameof(TabDeriv));
        OnPropertyChanged(nameof(TabFlow));
        OnPropertyChanged(nameof(TabMacroOn));
        OnPropertyChanged(nameof(TabStructOn));
        OnPropertyChanged(nameof(TabLevelsOn));
        OnPropertyChanged(nameof(TabTermOn));
        OnPropertyChanged(nameof(TabDerivOn));
        OnPropertyChanged(nameof(TabFlowOn));
        OnPropertyChanged(nameof(ShowProfileRows));
        Reload();
    }

    private void NotifyMetricFlags()
    {
        OnPropertyChanged(nameof(MetricGex));
        OnPropertyChanged(nameof(MetricDex));
        OnPropertyChanged(nameof(MetricCharma));
        OnPropertyChanged(nameof(MetricVanna));
        OnPropertyChanged(nameof(MetricSpeed));
        OnPropertyChanged(nameof(MetricVomma));
        OnPropertyChanged(nameof(MetricNet));
    }

    private void SyncMethodFlags()
    {
        MethodBs = MethodKey is "black-scholes" or "0";
        MethodCrr = MethodKey is "1" or "cox-ross-rubinstein" or "crr";
        MethodMc = MethodKey is "2" or "monte-carlo" or "mc";
        MethodHeston = MethodKey is "4" or "heston";
        MethodFdm = MethodKey is "6" or "fdm";
    }

    [RelayCommand]
    private void SetSymbol(string? sym)
    {
        if (!string.IsNullOrWhiteSpace(sym))
            SelectedSymbol = sym.ToUpperInvariant();
    }

    [RelayCommand]
    private void SetMetric(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            MetricKey = MockGexFeed.NormalizeMetric(key);
    }

    [RelayCommand]
    private void SetMethod(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key) && MockGexFeed.Methods.Any(m => m.Key == key))
            MethodKey = key;
    }

    [RelayCommand]
    private void SetTab(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key) && MockGexFeed.Tabs.Any(t => t.Key == key))
            TabKey = key;
    }

    [RelayCommand]
    private void Reload()
    {
        var snap = _feed.Build(SelectedSymbol, MetricKey, MethodKey);
        MetricTitle = $"{snap.MetricLabel} · {snap.MetricName}";
        MethodLabel = string.IsNullOrWhiteSpace(snap.MethodLabel)
            ? (MockGexFeed.Methods.FirstOrDefault(m => m.Key == MethodKey).Label ?? MethodKey)
            : snap.MethodLabel;
        StructureMetricLabel = snap.MetricLabel;
        SpotLabel = snap.Spot.ToString("0.00");
        FlipLabel = snap.Flip.ToString("0.##");
        CallWallLabel = snap.CallWall.ToString("0.##");
        PutWallLabel = snap.PutWall.ToString("0.##");
        NetLabel = FormatSignedEuroM(snap.Net);
        CallPutLabel = $"{snap.CallPutRatio:0} / {100 - snap.CallPutRatio:0}";
        Delta24Label = FormatSignedEuroM(snap.Delta24h);
        NetBrush = snap.Net >= 0 ? SolidColorBrush.Parse("#00FF7A") : SolidColorBrush.Parse("#FF3B5C");
        DeltaBrush = snap.Delta24h >= 0 ? SolidColorBrush.Parse("#00FF7A") : SolidColorBrush.Parse("#FF3B5C");
        NetHint = snap.Net >= 0
            ? "long gamma · mercato pinnato"
            : "short gamma · mosse amplificate";
        var src = string.IsNullOrWhiteSpace(snap.DataSource) ? _feed.SourceLabel : snap.DataSource;
        Subtitle =
            $"{MethodLabel} · {src} · Net {FormatSignedEuroM(snap.Net)} · Flip {snap.Flip:0.##} · {(snap.Net >= 0 ? "long gamma" : "short gamma")}";
        StatusText = $"{snap.Symbol} · {snap.MetricLabel} · {src}";

        Rows.Clear();
        foreach (var lvl in snap.Levels.OrderByDescending(l => l.Strike))
        {
            Rows.Add(GexRowViewModel.From(
                lvl,
                snap.AbsMax,
                isFlip: Nearly(lvl.Strike, snap.Flip, snap.Step),
                isCallWall: Nearly(lvl.Strike, snap.CallWall, snap.Step),
                isPutWall: Nearly(lvl.Strike, snap.PutWall, snap.Step),
                isAtm: Nearly(lvl.Strike, Math.Round(snap.Spot / snap.Step) * snap.Step, snap.Step)));
        }

        BuildTabExtras(snap);
    }

    private void BuildTabExtras(GexProfileSnapshot snap)
    {
        TabCards.Clear();
        FlowRows.Clear();
        Alerts.Clear();

        switch (TabKey)
        {
            case "macro":
                PanelTitle = $"GEX Profile · {snap.MetricLabel} per strike";
                TabNote = $"Verde = exposure positiva · Rosso = negativa · FLIP / CW / PW · {snap.DataSource}";
                break;

            case "struct":
                PanelTitle = $"Risk Profile · {snap.MetricLabel} ({snap.MethodLabel})";
                TabNote = "Concentrazione per strike · punto di azzeramento ≈ Flip";
                TabCards.Add(new($"{snap.MetricLabel} netto", FormatSignedEuroM(snap.Net), "aggregato su catena"));
                TabCards.Add(new("Flip", snap.Flip.ToString("0.##"), "da + a −"));
                TabCards.Add(new("Picco positivo", snap.CallWall.ToString("0.##"), "call wall"));
                TabCards.Add(new("Picco negativo", snap.PutWall.ToString("0.##"), "put wall"));
                break;

            case "levels":
                PanelTitle = "Mappa livelli operativi · Gamma per strike";
                TabNote = $"Major levels · ⟂ Flip {snap.Flip:0.##}";
                TabCards.Add(new("Absolute Gamma Max", snap.AbsGammaMax.ToString("0.##"), "massima concentrazione OI"));
                TabCards.Add(new("Call Wall", snap.CallWall.ToString("0.##"), "resistenza estrema"));
                TabCards.Add(new("Put Wall", snap.PutWall.ToString("0.##"), "pavimento monetario"));
                TabCards.Add(new("Major Vol Trigger", snap.VolTrigger.ToString("0.##"), "sotto: IV accelera"));
                break;

            case "term":
                PanelTitle = "GEX Forward Curve · peso gamma per scadenza";
                TabNote = "Il gamma si concentra sulle scadenze brevi (0DTE / weekly)";
                var total = Math.Abs(snap.Net);
                var zdte = Math.Round(total * 0.42, 1);
                TabCards.Add(new("0DTE GEX Monitor", FormatSignedEuroM(zdte), "gamma in scadenza oggi"));
                TabCards.Add(new("OpEx Imbalance", "42%", "del gamma totale scade oggi"));
                TabCards.Add(new("Gamma Decay Speed", "Alta", $"−{zdte * 0.6:0}M/h verso il close"));
                TabCards.Add(new("Net GEX residuo", FormatSignedEuroM(total - zdte), "scadenze successive"));
                break;

            case "deriv":
                PanelTitle = "Derivate di secondo ordine · Vanna / Charm / Speed / Vomma";
                TabNote = $"Calcolati con {snap.MethodLabel} · {snap.DataSource}";
                var speed = _feed.Build(SelectedSymbol, "speed", MethodKey).Net;
                var vanna = _feed.Build(SelectedSymbol, "vanna", MethodKey).Net;
                var charm = _feed.Build(SelectedSymbol, "charma", MethodKey).Net;
                var vomma = _feed.Build(SelectedSymbol, "vomma", MethodKey).Net;
                TabCards.Add(new("Σ Speed (dΓ/dS)", FormatNum(speed), "accelerazione gamma"));
                TabCards.Add(new("Charm netto", $"{FormatNum(charm)} Δ/g", "Δ per giorno"));
                TabCards.Add(new("Vanna netto", $"{FormatNum(vanna)} Δ/σ", "Δ per +1% IV"));
                TabCards.Add(new("Vomma netto", $"{FormatNum(vomma)}", "dVega/dσ"));
                break;

            case "flow":
                PanelTitle = "Large Block Trade Scanner · contratti > $500k";
                TabNote = "Aggressor / dealer hedging · mock tape";
                var buyPct = 67;
                var hedge = Math.Round(snap.Net * 1.4, 0);
                TabCards.Add(new("Aggressor Imbalance", $"{buyPct}% BUY", "Ask vs Bid"));
                TabCards.Add(new("Dealer Hedging Pressure", $"{(hedge >= 0 ? "+" : "")}{hedge:0}k", hedge >= 0 ? "azioni da comprare" : "azioni da vendere"));
                TabCards.Add(new("Block > $500k", "6", "nella sessione"));
                TabCards.Add(new("Squeeze Risk", snap.Net < 0 ? "Elevato" : "Contenuto", snap.Net < 0 ? "gamma negativo + velocità" : "gamma positivo"));

                FlowRows.Add(new($"{snap.Symbol} {snap.CallWall:0}C", "0DTE", "€2.4M", "ASK", "BUY", true));
                FlowRows.Add(new($"{snap.Symbol} {snap.PutWall:0}P", "0DTE", "€1.8M", "ASK", "BUY", true));
                FlowRows.Add(new($"{snap.Symbol} {snap.Flip:0}C", "Weekly", "€3.1M", "ASK", "BUY", true));
                FlowRows.Add(new($"{snap.Symbol} {snap.PutWall - snap.Step:0}P", "Weekly", "€1.2M", "BID", "SELL", false));
                FlowRows.Add(new($"{snap.Symbol} {snap.CallWall + snap.Step:0}C", "0DTE", "€0.9M", "ASK", "BUY", true));
                FlowRows.Add(new($"{snap.Symbol} {snap.PutWall:0}P", "Monthly", "€1.5M", "BID", "SELL", false));

                Alerts.Add(new("15:42:08", $"Prezzo sotto Gamma Flip {snap.Flip:0} — regime short gamma, alta velocità"));
                Alerts.Add(new("15:38:51", $"Squeeze setup: Put Wall {snap.PutWall:0} testato con dealer short"));
                Alerts.Add(new("15:31:20", $"Block BUY €3,1M su {snap.Flip:0}C weekly — dealer hedging long"));
                break;
        }
    }

    private static string FormatSignedEuroM(double v) =>
        v >= 0 ? $"+€{Math.Abs(v):0}M" : $"-€{Math.Abs(v):0}M";

    private static string FormatNum(double v) =>
        v >= 0 ? $"+{v:0}" : $"{v:0}";

    private static bool Nearly(double a, double b, double step) =>
        Math.Abs(a - b) < step * 0.51;
}

public sealed record GexMetricOption(string Key, string Label, string Name)
{
    public string Display => $"{Label} — {Name}";
    public override string ToString() => Display;
}
public sealed record GexMethodOption(string Key, string Label);
public sealed record GexCardViewModel(string Title, string Value, string Hint);
public sealed record GexFlowRowViewModel(string Contract, string Expiry, string Prem, string Side, string Aggr, bool IsBuy);
public sealed record GexAlertViewModel(string Time, string Message);

public partial class GexRowViewModel : ObservableObject
{
    public string StrikeLabel { get; init; } = "";
    public string ValueLabel { get; init; } = "";
    public string TagLabel { get; init; } = "";
    public double PosBar { get; init; }
    public double NegBar { get; init; }
    public bool IsFlip { get; init; }
    public bool IsCallWall { get; init; }
    public bool IsPutWall { get; init; }
    public bool IsAtm { get; init; }
    public IBrush RowBg { get; init; } = SolidColorBrush.Parse("#100E14");
    public IBrush StrikeFg { get; init; } = SolidColorBrush.Parse("#C4B8BF");
    public IBrush ValueFg { get; init; } = SolidColorBrush.Parse("#D4A8B0");

    public static GexRowViewModel From(
        GexLevel lvl,
        double absMax,
        bool isFlip,
        bool isCallWall,
        bool isPutWall,
        bool isAtm)
    {
        var mag = Math.Abs(lvl.Value) / absMax;
        var bar = Math.Clamp(mag, 0.04, 1) * 140;
        var pos = lvl.Value > 0 ? bar : 0;
        var neg = lvl.Value < 0 ? bar : 0;

        var tags = new List<string>();
        if (isFlip) tags.Add("FLIP");
        if (isCallWall) tags.Add("CW");
        if (isPutWall) tags.Add("PW");
        if (isAtm) tags.Add("ATM");

        IBrush bg = SolidColorBrush.Parse("#100E14");
        IBrush strikeFg = SolidColorBrush.Parse("#C4B8BF");
        if (isFlip)
        {
            bg = SolidColorBrush.Parse("#2A1E24");
            strikeFg = SolidColorBrush.Parse("#D4A8B0");
        }
        else if (isCallWall)
        {
            bg = SolidColorBrush.Parse("#0F1A14");
            strikeFg = SolidColorBrush.Parse("#00FF7A");
        }
        else if (isPutWall)
        {
            bg = SolidColorBrush.Parse("#1A1212");
            strikeFg = SolidColorBrush.Parse("#FF3B5C");
        }
        else if (isAtm)
        {
            bg = SolidColorBrush.Parse("#18141C");
        }

        return new GexRowViewModel
        {
            StrikeLabel = lvl.Strike.ToString("0.##"),
            ValueLabel = lvl.Value >= 0 ? $"+{lvl.Value:0.0}" : $"{lvl.Value:0.0}",
            TagLabel = string.Join(" ", tags),
            PosBar = pos,
            NegBar = neg,
            IsFlip = isFlip,
            IsCallWall = isCallWall,
            IsPutWall = isPutWall,
            IsAtm = isAtm,
            RowBg = bg,
            StrikeFg = strikeFg,
            ValueFg = lvl.Value >= 0
                ? SolidColorBrush.Parse("#00FF7A")
                : SolidColorBrush.Parse("#FF3B5C"),
        };
    }
}
