using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace Lexis.Desktop.App.ViewModels.Tools;

/// <summary>Generic tool pane placeholder (DOM / footprint / profile / tape).</summary>
public partial class PlaceholderToolViewModel : Tool
{
    [ObservableProperty] private string _headline = "";
    [ObservableProperty] private string _blurb = "";
    [ObservableProperty] private string _badge = "MOCK · no API";

    public PlaceholderToolViewModel()
    {
        CanClose = true;
    }

    public static PlaceholderToolViewModel Create(string id, string title, string blurb)
    {
        return new PlaceholderToolViewModel
        {
            Id = id,
            Title = title,
            Headline = title,
            Blurb = blurb,
        };
    }
}
