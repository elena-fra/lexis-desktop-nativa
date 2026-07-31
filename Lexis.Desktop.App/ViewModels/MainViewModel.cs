using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using Lexis.Desktop.App.Services;
using Lexis.Desktop.App.Workspace;

namespace Lexis.Desktop.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly LexisDockFactory _factory;
    private readonly IpcTradeFeed _ipc;
    private readonly MarketDataHub _hub;

    [ObservableProperty] private IRootDock? _layout;
    [ObservableProperty] private string _connectionStatus = "Connecting…";
    [ObservableProperty] private bool _isStatusOpen = true;

    public IFactory Factory => _factory;

    public string AppTitle { get; private set; } = "LEXIS · Options Desk";

    /// <summary>Show header button when Status / IPC was closed.</summary>
    public bool ShowReopenStatus => !IsStatusOpen;

    public ObservableCollection<DeskCatalog.Panel> DeskPanels { get; } = new(DeskCatalog.Documents);

    public ObservableCollection<DeskCatalog.Panel> ToolPanels { get; } = new(DeskCatalog.Tools);

    public MainViewModel()
    {
        _ipc = new IpcTradeFeed();
        _hub = MarketDataHub.Create();
        ConnectionStatus = _hub.StatusLabel;
        AppTitle = _hub.ApiOk
            ? "LEXIS · Options Desk · API"
            : "LEXIS · Options Desk · mock";

        _factory = new LexisDockFactory(_hub, _ipc);
        _factory.StatusVisibilityChanged = open =>
        {
            IsStatusOpen = open;
            OnPropertyChanged(nameof(ShowReopenStatus));
        };
        var layout = _factory.CreateLayout();
        _factory.InitLayout(layout);
        Layout = layout;
        IsStatusOpen = _factory.IsStatusVisible();
    }

    partial void OnIsStatusOpenChanged(bool value) => OnPropertyChanged(nameof(ShowReopenStatus));

    [RelayCommand]
    private void OpenPanel(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        _factory.OpenPanel(id);
        if (id == "status")
        {
            IsStatusOpen = true;
            OnPropertyChanged(nameof(ShowReopenStatus));
        }
    }

    [RelayCommand]
    private void ReopenStatus() => OpenPanel("status");

    public async ValueTask DisposeAsync()
    {
        _hub.Dispose();
        await _ipc.DisposeAsync();
    }
}
