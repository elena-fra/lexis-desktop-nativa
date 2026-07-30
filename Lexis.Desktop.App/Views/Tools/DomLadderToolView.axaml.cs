using Avalonia.Controls;
using Avalonia.Interactivity;
using Lexis.Desktop.App.ViewModels.Tools;

namespace Lexis.Desktop.App.Views.Tools;

public partial class DomLadderToolView : UserControl
{
    public DomLadderToolView() => InitializeComponent();

    private DomLadderToolViewModel? Vm => DataContext as DomLadderToolViewModel;

    private void OnBidClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DomLevelRowViewModel row })
            Vm?.ClickBidCommand.Execute(row);
    }

    private void OnAskClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DomLevelRowViewModel row })
            Vm?.ClickAskCommand.Execute(row);
    }

    private void OnPriceClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DomLevelRowViewModel row })
            Vm?.CancelAtPriceCommand.Execute(row);
    }
}
