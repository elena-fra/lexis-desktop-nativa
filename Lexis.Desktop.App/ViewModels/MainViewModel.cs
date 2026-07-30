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

    public IFactory Factory => _factory;

    public string AppTitle { get; private set; } = "LEXIS · Options Desk";

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
        var layout = _factory.CreateLayout();
        _factory.InitLayout(layout);
        Layout = layout;
    }

    [RelayCommand]
    private void OpenPanel(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        _factory.OpenPanel(id);
    }

    public async ValueTask DisposeAsync()
    {
        _hub.Dispose();
        await _ipc.DisposeAsync();
    }
}
