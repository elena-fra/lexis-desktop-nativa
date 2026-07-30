using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Lexis.Desktop.App.ViewModels.Documents;

namespace Lexis.Desktop.App.Views.Documents;

public partial class OptionChainDocumentView : UserControl
{
    public OptionChainDocumentView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookVm();
        AttachedToVisualTree += (_, _) =>
        {
            HookVm();
            ApplyColumnVisibility();
        };
    }

    private OptionChainDocumentViewModel? _vm;

    private void HookVm()
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as OptionChainDocumentViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        ApplyColumnVisibility();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OptionChainDocumentViewModel.ShowIv)
            or nameof(OptionChainDocumentViewModel.ShowDelta)
            or nameof(OptionChainDocumentViewModel.ShowGamma)
            or nameof(OptionChainDocumentViewModel.ShowTheta)
            or nameof(OptionChainDocumentViewModel.ShowVega)
            or nameof(OptionChainDocumentViewModel.ShowVol)
            or nameof(OptionChainDocumentViewModel.ShowOi)
            or null)
        {
            Dispatcher.UIThread.Post(ApplyColumnVisibility);
        }
    }

    private void ApplyColumnVisibility()
    {
        if (_vm is null || ChainGrid?.Columns is null) return;

        // Column order: call OI,Vol,Vega,Θ,Γ,Δ,IV, Bid,Ask, Strike, Bid,Ask, put IV,Δ,Γ,Θ,Vega,Vol,OI
        void Set(int index, bool visible)
        {
            if (index >= 0 && index < ChainGrid.Columns.Count)
                ChainGrid.Columns[index].IsVisible = visible;
        }

        Set(0, _vm.ShowOi);      // call OI
        Set(1, _vm.ShowVol);
        Set(2, _vm.ShowVega);
        Set(3, _vm.ShowTheta);
        Set(4, _vm.ShowGamma);
        Set(5, _vm.ShowDelta);
        Set(6, _vm.ShowIv);
        // 7 Call Bid, 8 Call Ask, 9 Strike, 10 Put Bid, 11 Put Ask — always on
        Set(12, _vm.ShowIv);
        Set(13, _vm.ShowDelta);
        Set(14, _vm.ShowGamma);
        Set(15, _vm.ShowTheta);
        Set(16, _vm.ShowVega);
        Set(17, _vm.ShowVol);
        Set(18, _vm.ShowOi);
    }
}
