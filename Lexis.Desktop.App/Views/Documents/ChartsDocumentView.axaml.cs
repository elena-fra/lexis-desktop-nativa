using Avalonia.Controls;
using Lexis.Desktop.App.ViewModels.Documents;
using LexisDesktop.Charts;
using ScottPlot;

namespace Lexis.Desktop.App.Views.Documents;

public partial class ChartsDocumentView : UserControl
{
    private CandlestickChart? _chart;
    private ChartsDocumentViewModel? _vm;

    public ChartsDocumentView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => EnsureChart();
        DataContextChanged += (_, _) => WireVm();
    }

    private void WireVm()
    {
        if (_vm is not null)
            _vm.ChartDataRequested -= OnChartDataRequested;

        _vm = DataContext as ChartsDocumentViewModel;
        if (_vm is null) return;

        _vm.ChartDataRequested += OnChartDataRequested;
        EnsureChart();
        _vm.NotifyAttached();
    }

    private void EnsureChart()
    {
        if (_chart is not null) return;
        if (ChartHost is null) return;

        _chart = new CandlestickChart();
        ChartHost.Children.Clear();
        ChartHost.Children.Add(_chart.PlotControl);
    }

    private void OnChartDataRequested(IReadOnlyList<OHLC> data, string longHex, string shortHex)
    {
        EnsureChart();
        if (_chart is null) return;
        _chart.ApplyCandleColors(longHex, shortHex);
        _chart.SetData(data);
    }
}
