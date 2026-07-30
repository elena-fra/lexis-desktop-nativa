using Avalonia.Controls;
using Avalonia.Interactivity;
using Lexis.Desktop.App.ViewModels.Documents;

namespace Lexis.Desktop.App.Views.Documents;

public partial class OptionFlowDocumentView : UserControl
{
    public OptionFlowDocumentView() => InitializeComponent();

    private void OnFlowRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FlowRowItemViewModel row }) return;
        if (DataContext is not OptionFlowDocumentViewModel vm) return;
        vm.SelectRowCommand.Execute(row);
    }
}
