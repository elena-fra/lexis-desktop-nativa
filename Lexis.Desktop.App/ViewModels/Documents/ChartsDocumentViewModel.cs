using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Lexis.Desktop.App.Services;
using LexisDesktop.Charts;

namespace Lexis.Desktop.App.ViewModels.Documents;

/// <summary>
/// Grafici desk — parity with web Lexis Grafici core (TF, type, volume, indicators).
/// </summary>
public partial class ChartsDocumentViewModel : Document
{
    public ObservableCollection<string> SymbolOptions { get; } = new(MockChainFeed.Underlyings.Select(u => u.Symbol));

    public ObservableCollection<string> TimeframeOptions { get; } = new(
    [
        "1m", "2m", "5m", "15m", "30m", "1H", "2H", "4H", "1D", "1W", "1Mo"
    ]);

    public ObservableCollection<string> ChartTypeOptions { get; } = new(
    [
        "Candele", "Heikin Ashi", "Linea"
    ]);

    [ObservableProperty] private string _selectedSymbol = "SPY";
    [ObservableProperty] private string _selectedTimeframe = "5m";
    [ObservableProperty] private string _selectedChartType = "Candele";
    [ObservableProperty] private string _statusText = "Grafici · LexisDesktop.Charts";
    [ObservableProperty] private int _candleCount = 180;
    [ObservableProperty] private string _longColorHex = "#00FF7A";
    [ObservableProperty] private string _shortColorHex = "#FF3B5C";
    [ObservableProperty] private string _ohlcReadout = "—";

    // Indicators (web defaults: EMA+Volume on)
    [ObservableProperty] private bool _showVolume = true;
    [ObservableProperty] private bool _showEma = true;
    [ObservableProperty] private bool _showSma;
    [ObservableProperty] private bool _showBollinger;
    [ObservableProperty] private bool _showVwap;
    [ObservableProperty] private bool _showRsi;
    [ObservableProperty] private bool _showMacd;
    [ObservableProperty] private bool _showAtr;
    [ObservableProperty] private bool _indicatorsOpen;

    public event Action<IReadOnlyList<OhlcvBar>, LexisChartOptions>? ChartRenderRequested;

    public ChartsDocumentViewModel()
    {
        Id = "graf";
        Title = "Grafici";
        CanClose = true;
    }

    public void NotifyAttached() => Reload();

    [RelayCommand]
    private void ToggleIndicators() => IndicatorsOpen = !IndicatorsOpen;

    [RelayCommand]
    private void Reload()
    {
        var under = MockChainFeed.Underlyings.FirstOrDefault(u => u.Symbol == SelectedSymbol);
        var spot = under?.DefaultSpot ?? 500;
        var bars = DemoBarGenerator.Generate(
            SelectedSymbol,
            SelectedTimeframe,
            Math.Clamp(CandleCount, 20, 800),
            spot);

        var opt = BuildOptions();
        ChartRenderRequested?.Invoke(bars, opt);

        if (bars.Count > 0)
        {
            var last = bars[^1];
            OhlcReadout = $"O {last.Open:F2}  H {last.High:F2}  L {last.Low:F2}  C {last.Close:F2}  V {last.Volume:N0}";
        }

        var inds = new List<string>();
        if (ShowVolume) inds.Add("Vol");
        if (ShowEma) inds.Add("EMA");
        if (ShowSma) inds.Add("SMA");
        if (ShowBollinger) inds.Add("BB");
        if (ShowVwap) inds.Add("VWAP");
        if (ShowRsi) inds.Add("RSI");
        if (ShowMacd) inds.Add("MACD");
        if (ShowAtr) inds.Add("ATR");

        StatusText = $"{SelectedSymbol} · {SelectedTimeframe} · {SelectedChartType} · {bars.Count} barre"
                     + (inds.Count > 0 ? " · " + string.Join("+", inds) : "")
                     + " · demo";
    }

    private LexisChartOptions BuildOptions() => new()
    {
        ChartType = SelectedChartType switch
        {
            "Heikin Ashi" => LexisChartType.HeikinAshi,
            "Linea" => LexisChartType.Line,
            _ => LexisChartType.Candle,
        },
        LongColor = LongColorHex,
        ShortColor = ShortColorHex,
        ShowVolume = ShowVolume,
        ShowEma = ShowEma,
        ShowSma = ShowSma,
        ShowBollinger = ShowBollinger,
        ShowVwap = ShowVwap,
        ShowRsi = ShowRsi,
        ShowMacd = ShowMacd,
        ShowAtr = ShowAtr,
    };

    partial void OnSelectedSymbolChanged(string value) => Reload();
    partial void OnSelectedTimeframeChanged(string value) => Reload();
    partial void OnSelectedChartTypeChanged(string value) => Reload();
    partial void OnCandleCountChanged(int value) => Reload();
    partial void OnShowVolumeChanged(bool value) => Reload();
    partial void OnShowEmaChanged(bool value) => Reload();
    partial void OnShowSmaChanged(bool value) => Reload();
    partial void OnShowBollingerChanged(bool value) => Reload();
    partial void OnShowVwapChanged(bool value) => Reload();
    partial void OnShowRsiChanged(bool value) => Reload();
    partial void OnShowMacdChanged(bool value) => Reload();
    partial void OnShowAtrChanged(bool value) => Reload();
}
