using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Settings;
using Lexis.Contracts.Market;
using Lexis.Desktop.App.Services;
using Lexis.Desktop.App.ViewModels.Documents;
using Lexis.Desktop.App.ViewModels.Tools;
using Lexis.Desktop.App.Workspace;

namespace Lexis.Desktop.App.ViewModels;

/// <summary>
/// Dock workspace factory: Option Desk documents + tools, openable from menu.
/// </summary>
public sealed class LexisDockFactory : Factory
{
    private readonly MarketDataHub _hub;
    private readonly IpcTradeFeed? _ipc;

    private IRootDock? _rootDock;
    private DocumentDock? _documentDock;
    private ToolDock? _toolDock;
    private OptionChainDocumentViewModel? _chain;
    private OptionFlowDocumentViewModel? _flow;
    private ChartsDocumentViewModel? _charts;
    private GexDocumentViewModel? _gex;
    private GrecheDocumentViewModel? _greche;
    private DashboardDocumentViewModel? _dashboard;
    private PortfolioDocumentViewModel? _portfolio;
    private StatusToolViewModel? _status;
    private DomLadderToolViewModel? _dom;
    private TimeSalesToolViewModel? _timeSales;

    public LexisDockFactory(MarketDataHub hub, IpcTradeFeed? ipc = null)
    {
        _hub = hub;
        _ipc = ipc;
    }

    public IRootDock? Root => _rootDock;

    public override IRootDock CreateLayout()
    {
        _chain = new OptionChainDocumentViewModel(_hub.Chain, _ipc)
        {
            Id = "chain",
            Title = "Option Chain",
        };
        _status = new StatusToolViewModel(_ipc);
        _dom = new DomLadderToolViewModel
        {
            Id = "dom",
            Title = "DOM",
            CanClose = true,
        };

        var flow = EnsureFlow();
        var gex = EnsureGex();
        var greche = EnsureGreche();
        var dash = EnsureDashboard();
        _documentDock = new DocumentDock
        {
            Id = "Documents",
            IsCollapsable = false,
            ActiveDockable = dash,
            VisibleDockables = CreateList<IDockable>(dash, _chain, flow, gex, greche),
            CanCreateDocument = false,
        };

        _toolDock = new ToolDock
        {
            Id = "RightTools",
            Proportion = 0.32,
            Alignment = Alignment.Right,
            ActiveDockable = EnsureTimeSales(),
            VisibleDockables = CreateList<IDockable>(_status, _dom, EnsureTimeSales()),
        };

        var mainLayout = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                _documentDock,
                new ProportionalDockSplitter(),
                _toolDock),
        };

        _rootDock = CreateRootDock();
        _rootDock.Id = "Root";
        _rootDock.IsCollapsable = false;
        _rootDock.ActiveDockable = mainLayout;
        _rootDock.DefaultDockable = mainLayout;
        _rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        return _rootDock;
    }

    public void OpenPanel(string id)
    {
        var panel = DeskCatalog.Find(id);
        if (panel is null) return;

        if (panel.Kind == DeskCatalog.PanelKind.Tool)
            OpenTool(panel);
        else
            OpenDocument(panel);
    }

    private void OpenDocument(DeskCatalog.Panel panel)
    {
        if (_documentDock is null) return;

        if (panel.Id == "chain")
        {
            EnsureDockable(_documentDock, EnsureChain());
            return;
        }

        if (panel.Id == "order")
        {
            EnsureDockable(_documentDock, EnsureFlow());
            return;
        }

        if (panel.Id == "graf")
        {
            EnsureDockable(_documentDock, EnsureCharts());
            return;
        }

        if (panel.Id == "gex")
        {
            EnsureDockable(_documentDock, EnsureGex());
            return;
        }

        if (panel.Id == "greche")
        {
            EnsureDockable(_documentDock, EnsureGreche());
            return;
        }

        if (panel.Id == "dashboard")
        {
            EnsureDockable(_documentDock, EnsureDashboard());
            return;
        }

        if (panel.Id == "portfolio")
        {
            EnsureDockable(_documentDock, EnsurePortfolio());
            return;
        }

        if (FindById(_documentDock, panel.Id) is { } existing)
        {
            Focus(_documentDock, existing);
            return;
        }

        var doc = PlaceholderDocumentViewModel.Create(panel.Id, panel.Title, panel.Blurb);
        AddDockable(_documentDock, doc);
        Focus(_documentDock, doc);
    }

    private OptionChainDocumentViewModel EnsureChain() =>
        _chain ??= new OptionChainDocumentViewModel(_hub.Chain, _ipc)
        {
            Id = "chain",
            Title = "Option Chain",
        };

    private OptionFlowDocumentViewModel EnsureFlow()
    {
        if (_flow is not null) return _flow;
        _flow = new OptionFlowDocumentViewModel(_hub.Flow)
        {
            Id = "order",
            Title = "Option Flow",
        };
        _flow.OpenInChain = OnFlowOpenInChain;
        return _flow;
    }

    private ChartsDocumentViewModel EnsureCharts() =>
        _charts ??= new ChartsDocumentViewModel
        {
            Id = "graf",
            Title = "Grafici",
        };

    private GexDocumentViewModel EnsureGex()
    {
        if (_gex is not null && (_documentDock is null || FindById(_documentDock, "gex") is not null))
            return _gex;

        _gex = new GexDocumentViewModel(_hub.Gex)
        {
            Id = "gex",
            Title = "GEX",
            CanClose = true,
        };
        return _gex;
    }

    private GrecheDocumentViewModel EnsureGreche()
    {
        if (_greche is not null && (_documentDock is null || FindById(_documentDock, "greche") is not null))
            return _greche;

        _greche = new GrecheDocumentViewModel(_hub.Greeks, _hub.Portfolio)
        {
            Id = "greche",
            Title = "Greche",
            CanClose = true,
        };
        return _greche;
    }

    private DashboardDocumentViewModel EnsureDashboard()
    {
        if (_dashboard is not null && (_documentDock is null || FindById(_documentDock, "dashboard") is not null))
            return _dashboard;

        _dashboard = new DashboardDocumentViewModel(_hub.Dashboard, OpenPanel)
        {
            Id = "dashboard",
            Title = "Riepilogo",
            CanClose = true,
        };
        return _dashboard;
    }

    private PortfolioDocumentViewModel EnsurePortfolio()
    {
        if (_portfolio is not null && (_documentDock is null || FindById(_documentDock, "portfolio") is not null))
            return _portfolio;

        _portfolio = new PortfolioDocumentViewModel(_hub.Portfolio, OpenPanel)
        {
            Id = "portfolio",
            Title = "Portafoglio",
            CanClose = true,
        };
        return _portfolio;
    }

    private void OnFlowOpenInChain(FlowRowDto row)
    {
        var chain = EnsureChain();
        EnsureDockable(_documentDock!, chain);
        chain.FocusFromFlow(row.Ticker, row.Strike, row.Type, row.Dte);
    }

    private void OpenTool(DeskCatalog.Panel panel)
    {
        if (_toolDock is null) return;

        if (panel.Id == "status")
        {
            EnsureDockable(_toolDock, _status ??= new StatusToolViewModel(_ipc));
            return;
        }

        if (panel.Id == "dom")
        {
            EnsureDockable(_toolDock, EnsureDom());
            return;
        }

        if (panel.Id == "timesales")
        {
            EnsureDockable(_toolDock, EnsureTimeSales());
            return;
        }

        if (FindById(_toolDock, panel.Id) is { } existing)
        {
            Focus(_toolDock, existing);
            return;
        }

        var tool = PlaceholderToolViewModel.Create(panel.Id, panel.Title, panel.Blurb);
        AddDockable(_toolDock, tool);
        Focus(_toolDock, tool);
    }

    private DomLadderToolViewModel EnsureDom()
    {
        if (_dom is not null && _toolDock is not null && FindById(_toolDock, "dom") is not null)
            return _dom;

        _dom?.Dispose();
        _dom = new DomLadderToolViewModel
        {
            Id = "dom",
            Title = "DOM",
            CanClose = true,
        };
        return _dom;
    }

    private TimeSalesToolViewModel EnsureTimeSales()
    {
        if (_timeSales is not null && _toolDock is not null && FindById(_toolDock, "timesales") is not null)
            return _timeSales;

        // First layout call: _toolDock may still be null — create once
        if (_timeSales is not null && _toolDock is null)
            return _timeSales;

        _timeSales?.Dispose();
        _timeSales = new TimeSalesToolViewModel
        {
            Id = "timesales",
            Title = "Time & Sales",
            CanClose = true,
        };
        return _timeSales;
    }

    private void EnsureDockable(IDock dock, IDockable dockable)
    {
        if (FindById(dock, dockable.Id ?? "") is null)
            AddDockable(dock, dockable);
        Focus(dock, dockable);
    }

    private void Focus(IDock dock, IDockable dockable)
    {
        SetActiveDockable(dockable);
        SetFocusedDockable(dock, dockable);
        dock.ActiveDockable = dockable;
    }

    private static IDockable? FindById(IDock dock, string id) =>
        dock.VisibleDockables?.FirstOrDefault(d => d.Id == id);

    public override void InitLayout(IDockable layout)
    {
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () =>
                DockSettings.UseManagedWindows ? new ManagedHostWindow() : new HostWindow(),
        };
        base.InitLayout(layout);
    }

    public override IDockWindow? CreateWindowFrom(IDockable dockable)
    {
        var window = base.CreateWindowFrom(dockable);
        if (window is not null)
            window.Title = "LEXIS";
        return window;
    }
}
