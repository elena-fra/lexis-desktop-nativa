using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Lexis.Desktop.App.Services;
using LexisDesktop.Charts;
using ScottPlot;

namespace Lexis.Desktop.App.ViewModels.Documents;

/// <summary>
/// Grafici desk — hosts CandlestickChart from modulo-visivo (LexisDesktop.Charts).
/// </summary>
public partial class ChartsDocumentViewModel : Document
{
    public ObservableCollection<string> SymbolOptions { get; } = new(MockChainFeed.Underlyings.Select(u => u.Symbol));

    [ObservableProperty] private string _selectedSymbol = "SPY";
    [ObservableProperty] private string _statusText = "ScottPlot · LexisDesktop.Charts · dati demo";
    [ObservableProperty] private int _candleCount = 120;
    [ObservableProperty] private string _longColorHex = "#4ADE80";
    [ObservableProperty] private string _shortColorHex = "#FB7185";

    /// <summary>Raised when the view should reload OHLC into the plot.</summary>
    public event Action<IReadOnlyList<OHLC>, string, string>? ChartDataRequested;

    public ChartsDocumentViewModel()
    {
        Id = "graf";
        Title = "Grafici";
        CanClose = true;
    }

    public void NotifyAttached() => Reload();

    [RelayCommand]
    private void Reload()
    {
        var data = CandlestickChart.GeneraDatiFinti(Math.Clamp(CandleCount, 20, 500));
        // Bias demo price around selected underlier so symbol change feels intentional
        var under = MockChainFeed.Underlyings.FirstOrDefault(u => u.Symbol == SelectedSymbol);
        if (under is not null && data.Count > 0)
        {
            var scale = under.DefaultSpot / Math.Max(1, data[^1].Close);
            data = data.Select(c => new OHLC(
                c.Open * scale,
                c.High * scale,
                c.Low * scale,
                c.Close * scale,
                c.DateTime,
                c.TimeSpan)).ToList();
        }

        ChartDataRequested?.Invoke(data, LongColorHex, ShortColorHex);
        StatusText = $"{SelectedSymbol} · {data.Count} candele 1m · LexisDesktop.Charts (demo)";
    }

    partial void OnSelectedSymbolChanged(string value) => Reload();
    partial void OnCandleCountChanged(int value) => Reload();
}
