using Avalonia.Controls;
using Lexis.Desktop.App.ViewModels.Documents;
using LexisDesktop.Charts;

namespace Lexis.Desktop.App.Views.Documents;

public partial class ChartsDocumentView : UserControl
{
    private LexisTradingChart? _chart;
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
            _vm.ChartRenderRequested -= OnChartRenderRequested;

        _vm = DataContext as ChartsDocumentViewModel;
        if (_vm is null) return;

        _vm.ChartRenderRequested += OnChartRenderRequested;
        EnsureChart();
        _vm.NotifyAttached();
    }

    private void EnsureChart()
    {
        if (_chart is not null) return;
        if (ChartHost is null) return;

        _chart = new LexisTradingChart();
        ChartHost.Children.Clear();
        ChartHost.Children.Add(_chart.Root);
    }

    private void OnChartRenderRequested(IReadOnlyList<OhlcvBar> bars, LexisChartOptions options)
    {
        EnsureChart();
        _chart?.Render(bars, options);
    }
}
