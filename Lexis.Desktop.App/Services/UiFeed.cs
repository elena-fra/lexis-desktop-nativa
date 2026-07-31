using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia.Threading;

namespace Lexis.Desktop.App.Services;

/// <summary>
/// UI update discipline (scheda §2.1 / §3.4): produce off the UI thread,
/// coalesce with Sample/Buffer, post to Avalonia at Background priority.
/// </summary>
public static class UiFeed
{
    /// <summary>~20–30 fps ceiling for desk panels.</summary>
    public static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(48);

    /// <summary>Slightly slower for heavy list rebuilds (flow tape).</summary>
    public static readonly TimeSpan Heavy = TimeSpan.FromMilliseconds(120);

    public static void Post(Action action) =>
        Dispatcher.UIThread.Post(action, DispatcherPriority.Background);

    public static IDisposable SampleToUi<T>(
        IObservable<T> source,
        Action<T> onUi,
        TimeSpan? sample = null) =>
        source
            .Sample(sample ?? Frame, Scheduler.Default)
            .Subscribe(
                item => Post(() => onUi(item)),
                ex => Post(() => { /* swallow to keep desk alive */ System.Diagnostics.Trace.WriteLine(ex); }));
}
