using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Lexis.Desktop.App.Services;
using Lexis.Ipc;

namespace Lexis.Desktop.App.ViewModels.Tools;

public partial class StatusToolViewModel : Tool, IDisposable
{
    private readonly IDisposable? _statsSub;

    [ObservableProperty] private string _summary =
        "IPC consumer starting…\nWaiting for C++ Lexis.Ipc.Producer.Native on " + IpcDefaults.Endpoint;

    public StatusToolViewModel() : this(null) { }

    public StatusToolViewModel(IpcTradeFeed? feed)
    {
        Id = "status";
        Title = "Status / IPC";
        CanClose = true;

        if (feed is null) return;

        _statsSub = feed.Stats.Subscribe(s =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                Summary =
                    $"Fase 0 IPC (NetMQ PUB/SUB)\n" +
                    $"Endpoint: {IpcDefaults.Endpoint}\n" +
                    $"Receiving: {(s.ConnectedHint ? "YES" : "no — start producer")}\n" +
                    $"Received: {s.Received:N0}\n" +
                    $"Avg rate: {s.TicksPerSecond:F0} tick/s\n" +
                    $"Last seq: {s.LastSequence}\n" +
                    $"Last: {s.LastSymbol} {s.LastPrice:F2} x {s.LastSize} {s.LastAggressor}";
            });
        });
    }

    public void Dispose()
    {
        _statsSub?.Dispose();
        GC.SuppressFinalize(this);
    }
}
