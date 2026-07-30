using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Lexis.Contracts.OrderFlow;
using Lexis.Desktop.App.Services;

namespace Lexis.Desktop.App.ViewModels.Tools;

/// <summary>Time &amp; Sales tape — executed prints with aggressor coloring (mock).</summary>
public partial class TimeSalesToolViewModel : Tool, IDisposable
{
    private readonly MockTimeSalesFeed _feed;
    private IDisposable? _live;
    private const int MaxRows = 220;

    public ObservableCollection<TapeRowViewModel> Rows { get; } = new();

    [ObservableProperty] private string _symbol = "SPY";
    [ObservableProperty] private string _statusText = "Time & Sales mock · executed tape";
    [ObservableProperty] private bool _paused;
    [ObservableProperty] private string _liveBadge = "TAPE LIVE";
    [ObservableProperty] private string _minSizeKey = "0";
    [ObservableProperty] private bool _blocksOnly;
    [ObservableProperty] private string _bidQuote = "—";
    [ObservableProperty] private string _askQuote = "—";
    [ObservableProperty] private string _lastQuote = "—";
    [ObservableProperty] private string _lastTimeLabel = "—:—:—";
    [ObservableProperty] private string _rangeLabel = "—";
    [ObservableProperty] private string _deltaLabel = "Δ 0";
    [ObservableProperty] private string _buyVolLabel = "0";
    [ObservableProperty] private string _sellVolLabel = "0";
    [ObservableProperty] private string _printsLabel = "0";
    [ObservableProperty] private string _blockThreshLabel = "BLK ≥200";
    [ObservableProperty] private IBrush _deltaBrush = SolidColorBrush.Parse("#D4A8B0");

    public bool Min0 => MinSizeKey == "0";
    public bool Min10 => MinSizeKey == "10";
    public bool Min50 => MinSizeKey == "50";
    public bool Min100 => MinSizeKey == "100";

    public TimeSalesToolViewModel() : this(new MockTimeSalesFeed()) { }

    public TimeSalesToolViewModel(MockTimeSalesFeed feed)
    {
        _feed = feed;
        Id = "timesales";
        Title = "Time & Sales";
        CanClose = true;
        _feed.Reset(Symbol);
        Seed();
        StartLive();
    }

    partial void OnSymbolChanged(string value)
    {
        _feed.Reset(value);
        Seed();
        StatusText = $"Time & Sales mock · {_feed.Symbol}";
        BlockThreshLabel = $"BLK ≥{_feed.BlockThreshold():0}";
    }

    partial void OnMinSizeKeyChanged(string value)
    {
        OnPropertyChanged(nameof(Min0));
        OnPropertyChanged(nameof(Min10));
        OnPropertyChanged(nameof(Min50));
        OnPropertyChanged(nameof(Min100));
    }

    [RelayCommand]
    private void SetSymbol(string? sym)
    {
        if (!string.IsNullOrWhiteSpace(sym))
            Symbol = sym.ToUpperInvariant();
    }

    [RelayCommand]
    private void SetMinSize(string? key)
    {
        if (key is not null) MinSizeKey = key;
    }

    [RelayCommand]
    private void ToggleBlocksOnly() => BlocksOnly = !BlocksOnly;

    [RelayCommand]
    private void TogglePause()
    {
        Paused = !Paused;
        LiveBadge = Paused ? "TAPE PAUSA" : "TAPE LIVE";
        StatusText = Paused ? "Tape in pausa (freeze)" : "Time & Sales mock · executed tape";
    }

    [RelayCommand]
    private void Clear()
    {
        Rows.Clear();
        PrintsLabel = "0";
        StatusText = "Tape cleared";
    }

    private void Seed()
    {
        Rows.Clear();
        foreach (var p in _feed.Seed(48))
            Rows.Insert(0, TapeRowViewModel.From(p, _feed.TickSize));
        RefreshQuotes();
        RefreshKpis();
        BlockThreshLabel = $"BLK ≥{_feed.BlockThreshold():0}";
    }

    private void StartLive()
    {
        _live?.Dispose();
        _live = Observable
            .Interval(TimeSpan.FromMilliseconds(140))
            .Subscribe(_ => PostUi(() =>
            {
                if (Paused) return;
                try
                {
                    // burst 1–3 prints
                    var n = Random.Shared.Next(1, 4);
                    for (var i = 0; i < n; i++)
                        TryAdd(_feed.Next(), filter: true);
                    while (Rows.Count > MaxRows)
                        Rows.RemoveAt(Rows.Count - 1);
                    RefreshQuotes();
                    RefreshKpis();
                }
                catch (Exception ex)
                {
                    StatusText = $"T&S hiccup · {ex.GetType().Name}";
                }
            }));
    }

    private void TryAdd(TapePrint print, bool filter)
    {
        if (filter && !PassesFilter(print)) return;
        Rows.Insert(0, TapeRowViewModel.From(print, _feed.TickSize));
    }

    private bool PassesFilter(TapePrint p)
    {
        var min = MinSizeKey switch
        {
            "10" => 10d,
            "50" => 50d,
            "100" => 100d,
            _ => 0d,
        };
        if (p.Trade.Size < min) return false;
        if (BlocksOnly && !p.IsBlock) return false;
        return true;
    }

    private void RefreshQuotes()
    {
        BidQuote = $"{FormatPx(_feed.BestBid)} × {_feed.BidSize:0}";
        AskQuote = $"{FormatPx(_feed.BestAsk)} × {_feed.AskSize:0}";
        if (Rows.Count > 0)
        {
            LastQuote = Rows[0].Price;
            LastTimeLabel = Rows[0].Time;
        }
        else
        {
            LastQuote = "—";
            LastTimeLabel = "—:—:—";
        }
        RangeLabel = $"{FormatPx(_feed.SessionLow)} – {FormatPx(_feed.SessionHigh)}";
    }

    private void RefreshKpis()
    {
        BuyVolLabel = FormatVol(_feed.CumBuy);
        SellVolLabel = FormatVol(_feed.CumSell);
        var delta = _feed.CumBuy - _feed.CumSell;
        DeltaLabel = delta >= 0 ? $"Δ +{FormatVol(delta)}" : $"Δ −{FormatVol(Math.Abs(delta))}";
        DeltaBrush = delta > 0
            ? SolidColorBrush.Parse("#86EFAC")
            : delta < 0
                ? SolidColorBrush.Parse("#FCA5A5")
                : SolidColorBrush.Parse("#D4A8B0");
        PrintsLabel = Rows.Count.ToString();
    }

    private string FormatPx(double px) => px.ToString("0.00");

    private static string FormatVol(double v) =>
        v >= 1000 ? $"{v / 1000.0:0.0}K" : v.ToString("0");

    private static void PostUi(Action a) =>
        Dispatcher.UIThread.Post(a, DispatcherPriority.Background);

    public void Dispose()
    {
        _live?.Dispose();
        _live = null;
        GC.SuppressFinalize(this);
    }
}

public partial class TapeRowViewModel : ObservableObject
{
    public string Time { get; init; } = "";
    public string Ticker { get; init; } = "";
    public string Price { get; init; } = "";
    public string Size { get; init; } = "";
    public string Side { get; init; } = "";
    public string Bid { get; init; } = "";
    public string Ask { get; init; } = "";
    public string Tick { get; init; } = "";
    public string Block { get; init; } = "";
    public bool IsBlock { get; init; }
    public IBrush RowBg { get; init; } = SolidColorBrush.Parse("#100E14");
    public IBrush Accent { get; init; } = SolidColorBrush.Parse("#D4A8B0");
    public IBrush SizeFg { get; init; } = SolidColorBrush.Parse("#F3ECEF");

    public static TapeRowViewModel From(TapePrint p, double tickSize)
    {
        var t = p.Trade;
        var buy = t.Aggressor == AggressorSide.Buy;
        var sell = t.Aggressor == AggressorSide.Sell;
        var accent = buy ? "#86EFAC" : sell ? "#FCA5A5" : "#D4A8B0";
        var bg = p.IsBlock
            ? "#1A1520"
            : buy ? "#0F1A14" : sell ? "#1A1212" : "#100E14";
        var tick = p.TickDir > 0 ? "▲" : p.TickDir < 0 ? "▼" : "·";
        var side = buy ? "ASK" : sell ? "BID" : "MID";
        var sizeHot = t.Size >= (tickSize >= 0.25 ? 80 : 200);
        var local = t.Timestamp.ToLocalTime();

        return new TapeRowViewModel
        {
            Time = local.ToString("HH:mm:ss.fff"),
            Ticker = t.Symbol,
            Price = t.Price.ToString("0.00"),
            Size = t.Size.ToString("0"),
            Side = side,
            Bid = p.Bid.ToString("0.00"),
            Ask = p.Ask.ToString("0.00"),
            Tick = tick,
            Block = p.IsBlock ? "BLK" : "",
            IsBlock = p.IsBlock,
            RowBg = SolidColorBrush.Parse(bg),
            Accent = SolidColorBrush.Parse(accent),
            SizeFg = SolidColorBrush.Parse(sizeHot ? accent : "#F3ECEF"),
        };
    }
}
