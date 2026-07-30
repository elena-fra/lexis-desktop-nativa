using Dock.Model.Mvvm.Controls;

namespace Lexis.Desktop.App.ViewModels.Tools;

/// <summary>Placeholder for DOM ladder (order-flow M3 later).</summary>
public class DomPlaceholderToolViewModel : Tool
{
    public string Message { get; private set; } =
        "DOM ladder — Track D placeholder.\nLive book + click-to-trade arrive after order-flow M1–M3.";

    public DomPlaceholderToolViewModel()
    {
        Id = "dom";
        Title = "DOM";
        CanClose = true;
    }

    public static DomPlaceholderToolViewModel CreatePinned()
    {
        return new DomPlaceholderToolViewModel { CanClose = false };
    }
}
