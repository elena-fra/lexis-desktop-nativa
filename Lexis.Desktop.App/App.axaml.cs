using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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

        // Explicit VM→view maps first so Dock never paints the wrong pane
        // (XAML DataType templates alone recycled one control across tabs —
        // DOM inside Time & Sales, and Option Chain ↔ Option Flow swapped).
        DataTemplates.Insert(0, new FuncDataTemplate<TimeSalesToolViewModel>(
            (_, _) => new TimeSalesToolView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<DomLadderToolViewModel>(
            (_, _) => new DomLadderToolView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<StatusToolViewModel>(
            (_, _) => new StatusToolView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<OptionChainDocumentViewModel>(
            (_, _) => new OptionChainDocumentView(), supportsRecycling: false));
        DataTemplates.Insert(0, new FuncDataTemplate<OptionFlowDocumentViewModel>(
            (_, _) => new OptionFlowDocumentView(), supportsRecycling: false));
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
        DataTemplates.Insert(0, new FuncDataTemplate<PlaceholderDocumentViewModel>(
            (_, _) => new PlaceholderDocumentView(), supportsRecycling: false));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Paint a shell window first, then build the desk after Opened so a
            // slow MainViewModel ctor cannot leave an invisible/hung process.
            var window = new MainWindow();
            desktop.MainWindow = window;
            window.Title = "LEXIS · loading…";

            window.Opened += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        var vm = new MainViewModel();
                        window.DataContext = vm;
                        window.Title = vm.AppTitle;
                        desktop.ShutdownRequested += async (_, _) =>
                        {
                            await vm.DisposeAsync();
                        };
                    }
                    catch (Exception ex)
                    {
                        window.Title = "LEXIS · startup error";
                        System.Diagnostics.Trace.WriteLine(ex);
                    }
                }, DispatcherPriority.Background);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
