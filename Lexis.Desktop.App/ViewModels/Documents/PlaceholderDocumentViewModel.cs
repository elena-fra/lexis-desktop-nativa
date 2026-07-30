using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace Lexis.Desktop.App.ViewModels.Documents;

/// <summary>Generic Option Desk panel shell — content filled later (no API yet).</summary>
public partial class PlaceholderDocumentViewModel : Document
{
    [ObservableProperty] private string _headline = "";
    [ObservableProperty] private string _blurb = "";
    [ObservableProperty] private string _badge = "MOCK · no API";

    public PlaceholderDocumentViewModel()
    {
        CanClose = true;
    }

    public static PlaceholderDocumentViewModel Create(string id, string title, string blurb)
    {
        return new PlaceholderDocumentViewModel
        {
            Id = id,
            Title = title,
            Headline = title,
            Blurb = blurb,
        };
    }
}
