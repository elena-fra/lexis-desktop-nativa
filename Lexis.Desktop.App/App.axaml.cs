using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Lexis.Desktop.App.ViewModels;
using Lexis.Desktop.App.ViewModels.Documents;
using Lexis.Desktop.App.ViewModels.Tools;
using Lexis.Desktop.App.Views;
using Lexis.Desktop.App.Views.Documents;
using Lexis.Desktop.App.Views.Tools;

namespace Lexis.Desktop.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Explicit tool→view maps first so Dock never paints the wrong pane
        // (string DataType templates alone were showing DOM inside Time & Sales).
        DataTemplates.Insert(0, new FuncDataTemplate<TimeSalesToolViewModel>(
            (_, _) => new TimeSalesToolView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<DomLadderToolViewModel>(
            (_, _) => new DomLadderToolView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<StatusToolViewModel>(
            (_, _) => new StatusToolView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<ChartsDocumentViewModel>(
            (_, _) => new ChartsDocumentView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<GexDocumentViewModel>(
            (_, _) => new GexDocumentView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<GrecheDocumentViewModel>(
            (_, _) => new GrecheDocumentView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<DashboardDocumentViewModel>(
            (_, _) => new DashboardDocumentView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<PortfolioDocumentViewModel>(
            (_, _) => new PortfolioDocumentView(), supportsRecycling: false));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };
            desktop.ShutdownRequested += async (_, _) =>
            {
                await vm.DisposeAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
