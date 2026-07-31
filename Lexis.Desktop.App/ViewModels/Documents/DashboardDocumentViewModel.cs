using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Lexis.Desktop.App.Services;

namespace Lexis.Desktop.App.ViewModels.Documents;

/// <summary>Riepilogo conto — web dashboard parity on mock ledger.</summary>
public partial class DashboardDocumentViewModel : Document
{
    private readonly IDashboardFeed _feed;
    private readonly Action<string>? _navigate;

    public ObservableCollection<string> Providers { get; }
    public ObservableCollection<string> Brokers { get; }
    public ObservableCollection<DashboardTradeRowViewModel> RecentTrades { get; } = new();

    [ObservableProperty] private string _accountType = "demo";
    [ObservableProperty] private string _selectedProvider = "LEXIS Data Cloud";
    [ObservableProperty] private string _selectedBroker = "LEXIS Paper · marks CBOE";
    [ObservableProperty] private string _range = "1M";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _equityLabel = "—";
    [ObservableProperty] private string _deltaLabel = "—";
    [ObservableProperty] private string _accountNumber = "—";
    [ObservableProperty] private string _badgeLabel = "Demo paper";
    [ObservableProperty] private string _providerShort = "";
    [ObservableProperty] private string _openPlLabel = "—";
    [ObservableProperty] private string _openPlSub = "";
    [ObservableProperty] private string _totalPlLabel = "—";
    [ObservableProperty] private string _totalPlSub = "";
    [ObservableProperty] private string _winRateLabel = "—";
    [ObservableProperty] private string _winRateSub = "";
    [ObservableProperty] private string _cashLabel = "—";
    [ObservableProperty] private string _freeMarginLabel = "—";
    [ObservableProperty] private string _marginSub = "";
    [ObservableProperty] private string _chartSub = "";
    [ObservableProperty] private string _callLabel = "";
    [ObservableProperty] private string _putLabel = "";
    [ObservableProperty] private double _callBarWidth = 160;
    [ObservableProperty] private double _putBarWidth = 160;
    [ObservableProperty] private string _openCountLabel = "0";
    [ObservableProperty] private string _optionsValueLabel = "—";
    [ObservableProperty] private string _exposurePlLabel = "—";
    [ObservableProperty] private string _equityPath = "M 0,70 L 640,70";
    [ObservableProperty] private IBrush _deltaBrush = SolidColorBrush.Parse("#00FF7A");
    [ObservableProperty] private IBrush _openPlBrush = SolidColorBrush.Parse("#00FF7A");
    [ObservableProperty] private IBrush _totalPlBrush = SolidColorBrush.Parse("#00FF7A");
    [ObservableProperty] private IBrush _winBrush = SolidColorBrush.Parse("#F3ECEF");
    [ObservableProperty] private IBrush _chartBrush = SolidColorBrush.Parse("#00FF7A");
    [ObservableProperty] private IBrush _exposurePlBrush = SolidColorBrush.Parse("#00FF7A");

    public bool IsDemo => AccountType == "demo";
    public bool IsReal => AccountType == "real";
    public bool Range1G => Range == "1G";
    public bool Range1S => Range == "1S";
    public bool Range1M => Range == "1M";
    public bool RangeYtd => Range == "YTD";

    public DashboardDocumentViewModel(IDashboardFeed feed, Action<string>? navigate = null)
    {
        _feed = feed;
        _navigate = navigate;
        Id = "dashboard";
        Title = "Riepilogo";
        CanClose = true;

        Providers = new ObservableCollection<string>(feed.Providers);
        Brokers = new ObservableCollection<string>(feed.Brokers);
        if (Providers.Count > 0) SelectedProvider = Providers[0];
        if (Brokers.Count > 0) SelectedBroker = Brokers[0];
        Reload();
    }

    partial void OnAccountTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDemo));
        OnPropertyChanged(nameof(IsReal));
        Reload();
    }

    partial void OnSelectedProviderChanged(string value) => Reload();
    partial void OnSelectedBrokerChanged(string value) => Reload();
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
    [RelayCommand] private void OpenPortfolio() => _navigate?.Invoke("portfolio");
    [RelayCommand] private void OpenMentor() => _navigate?.Invoke("journal");

    private void Rebuild()
    {
        var snap = _feed.Build(new DashboardQuery(AccountType, SelectedProvider, SelectedBroker, Range));
        EquityLabel = $"{snap.Equity:N0}";
        var flat = Math.Abs(snap.Equity) < 0.01 && Math.Abs(snap.EquityChange) < 0.01;
        DeltaLabel = flat
            ? $"● €0  0,00% · {snap.Range}"
            : $"{(snap.EquityChange >= 0 ? "▲ +" : "▼ −")}€{Math.Abs(snap.EquityChange):0}  {(snap.EquityChangePct >= 0 ? "+" : "")}{snap.EquityChangePct:0.00}% · {snap.Range}";
        DeltaBrush = snap.EquityChange >= 0
            ? SolidColorBrush.Parse("#00FF7A")
            : SolidColorBrush.Parse("#FF3B5C");
        ChartBrush = DeltaBrush;

        AccountNumber = snap.AccountNumber;
        BadgeLabel = snap.IsDemo ? "Demo paper" : "Reale live";
        ProviderShort = snap.Provider.Split('(')[0].Trim();
        Subtitle = snap.IsDemo
            ? $"conto demo paper · fill in-process · marks CBOE · dati {ProviderShort}"
            : $"ambiente reale · ordini live · dati {ProviderShort}";

        OpenPlLabel = FormatSignedEuro(snap.OpenPl);
        OpenPlSub = $"{(snap.OpenPlPct >= 0 ? "+" : "")}{snap.OpenPlPct:0.00}%";
        OpenPlBrush = Brush(snap.OpenPl);
        TotalPlLabel = FormatSignedEuro(snap.TotalPl);
        TotalPlSub = $"real. {FormatSignedEuro(snap.Realized)}";
        TotalPlBrush = Brush(snap.TotalPl);
        WinRateLabel = $"{snap.WinRate:0}%";
        WinRateSub = $"{snap.Wins}/{snap.TradeCount} trade";
        WinBrush = snap.WinRate >= 50 ? SolidColorBrush.Parse("#00FF7A") : SolidColorBrush.Parse("#F3ECEF");
        CashLabel = FormatEuro(snap.Cash);
        FreeMarginLabel = FormatEuro(snap.FreeMargin);
        MarginSub = $"usato {FormatEuro(snap.MarginUsed)}";

        ChartSub = $"{(snap.EquityChange >= 0 ? "▲ +" : "▼ −")}€{Math.Abs(snap.EquityChange):0} · {(snap.EquityChangePct >= 0 ? "+" : "")}{snap.EquityChangePct:0.00}% nel periodo";

        var tot = Math.Max(1, snap.CallExposure + snap.PutExposure);
        var callPct = snap.CallExposure / tot;
        var putPct = 1.0 - callPct;
        const double barMax = 320;
        CallBarWidth = Math.Max(4, callPct * barMax);
        PutBarWidth = Math.Max(4, putPct * barMax);
        CallLabel = $"Call {FormatEuro(snap.CallExposure)} · {callPct * 100:0}%";
        PutLabel = $"Put {FormatEuro(snap.PutExposure)} · {putPct * 100:0}%";
        OpenCountLabel = snap.OpenPositions.ToString();
        OptionsValueLabel = FormatEuro(snap.OptionsValue);
        ExposurePlLabel = FormatSignedEuro(snap.OpenPl);
        ExposurePlBrush = Brush(snap.OpenPl);

        RecentTrades.Clear();
        foreach (var t in snap.RecentTrades)
            RecentTrades.Add(DashboardTradeRowViewModel.From(t));

        RebuildChart(snap.EquitySeries);
    }

    private void RebuildChart(IReadOnlyList<double> series)
    {
        if (series.Count < 2)
        {
            EquityPath = "M 0,70 L 640,70";
            return;
        }

        const double w = 640, h = 140, pad = 8;
        var min = series.Min();
        var max = series.Max();
        var span = Math.Max(1, max - min);
        var sb = new System.Text.StringBuilder(series.Count * 16);
        for (var i = 0; i < series.Count; i++)
        {
            var x = pad + (w - pad * 2) * i / (series.Count - 1.0);
            var y = pad + (h - pad * 2) * (1.0 - (series[i] - min) / span);
            sb.Append(i == 0 ? 'M' : 'L');
            sb.Append(' ');
            sb.Append(x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(' ');
        }
        EquityPath = sb.ToString();
    }

    private static IBrush Brush(double v) =>
        v >= 0 ? SolidColorBrush.Parse("#00FF7A") : SolidColorBrush.Parse("#FF3B5C");

    private static string FormatEuro(double v) => $"€{v:N0}";
    private static string FormatSignedEuro(double v) =>
        v >= 0 ? $"+€{v:N0}" : $"-€{Math.Abs(v):N0}";
}

public partial class DashboardTradeRowViewModel : ObservableObject
{
    public string Instrument { get; init; } = "";
    public string Side { get; init; } = "";
    public string QtyLabel { get; init; } = "";
    public string PlLabel { get; init; } = "";
    public string Time { get; init; } = "";
    public IBrush SideBrush { get; init; } = SolidColorBrush.Parse("#D4A8B0");
    public IBrush PlBrush { get; init; } = SolidColorBrush.Parse("#00FF7A");

    public static DashboardTradeRowViewModel From(DashboardTradeRow t) => new()
    {
        Instrument = t.Instrument,
        Side = t.Side,
        QtyLabel = t.Qty.ToString(),
        PlLabel = t.Pl >= 0 ? $"+€{t.Pl:0}" : $"-€{Math.Abs(t.Pl):0}",
        Time = t.Time,
        SideBrush = t.Side == "LONG"
            ? SolidColorBrush.Parse("#00FF7A")
            : SolidColorBrush.Parse("#FF3B5C"),
        PlBrush = t.Pl >= 0
            ? SolidColorBrush.Parse("#00FF7A")
            : SolidColorBrush.Parse("#FF3B5C"),
    };
}
