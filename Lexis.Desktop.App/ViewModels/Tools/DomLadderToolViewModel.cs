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

/// <summary>
/// Depth of Market ladder — bid | vol | price | ask, mock L2 + prints + click-to-trade stubs.
/// </summary>
public partial class DomLadderToolViewModel : Tool, IDisposable
{
    private readonly MockDomFeed _feed;
    private IDisposable? _live;
    private readonly Dictionary<double, DomWorkingOrder> _working = new();
    private int _ltqTicksLeft;

    public ObservableCollection<DomLevelRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> Symbols { get; } = new() { "SPY", "QQQ", "AAPL", "TSLA", "ES", "SPX" };

    [ObservableProperty] private string _symbol = "SPY";
    [ObservableProperty] private string _statusText = "DOM mock · L2 + tape";
    [ObservableProperty] private bool _followLast = true;
    [ObservableProperty] private bool _paused;
    [ObservableProperty] private string _liveBadge = "BOOK LIVE";
    [ObservableProperty] private int _orderQty = 1;
    [ObservableProperty] private string _lastLabel = "—";
    [ObservableProperty] private string _spreadLabel = "—";
    [ObservableProperty] private string _ltqLabel = "";
    [ObservableProperty] private string _imbalanceLabel = "—";
    [ObservableProperty] private IBrush _ltqBrush = SolidColorBrush.Parse("#D4A8B0");

    public DomLadderToolViewModel() : this(new MockDomFeed()) { }

    public DomLadderToolViewModel(MockDomFeed feed)
    {
        _feed = feed;
        Id = "dom";
        Title = "DOM";
        CanClose = true;
        _feed.Reset(Symbol);
        RebuildLadder(center: true);
        StartLive();
    }

    public static DomLadderToolViewModel CreatePinned()
    {
        var vm = new DomLadderToolViewModel { CanClose = false };
        return vm;
    }

    partial void OnSymbolChanged(string value)
    {
        _working.Clear();
        _feed.Reset(value);
        RebuildLadder(center: true);
        StatusText = $"DOM mock · {_feed.Symbol} tick {_feed.TickSize}";
    }

    partial void OnOrderQtyChanged(int value)
    {
        if (value < 1) OrderQty = 1;
        if (value > 500) OrderQty = 500;
    }

    [RelayCommand]
    private void SetSymbol(string? sym)
    {
        if (!string.IsNullOrWhiteSpace(sym))
            Symbol = sym.ToUpperInvariant();
    }

    [RelayCommand]
    private void SetQty(string? q)
    {
        if (int.TryParse(q, out var n))
            OrderQty = n;
    }

    [RelayCommand]
    private void ToggleFollow() => FollowLast = !FollowLast;

    [RelayCommand]
    private void Center() => RebuildLadder(center: true);

    [RelayCommand]
    private void TogglePause()
    {
        Paused = !Paused;
        LiveBadge = Paused ? "BOOK PAUSA" : "BOOK LIVE";
        StatusText = Paused ? "DOM in pausa" : "DOM mock · L2 + tape";
    }

    [RelayCommand]
    private void ClickBid(DomLevelRowViewModel? row)
    {
        if (row is null) return;
        // Bid column click → buy limit (or stop if above market)
        PlaceWorking(row.Price, DomSide.Buy);
    }

    [RelayCommand]
    private void ClickAsk(DomLevelRowViewModel? row)
    {
        if (row is null) return;
        PlaceWorking(row.Price, DomSide.Sell);
    }

    [RelayCommand]
    private void CancelAtPrice(DomLevelRowViewModel? row)
    {
        if (row is null) return;
        _working.Remove(Round(row.Price));
        RefreshWorkingMarkers();
        StatusText = $"Annullato @ {row.PriceLabel}";
    }

    private void PlaceWorking(double price, DomSide side)
    {
        price = Round(price);
        _working[price] = new DomWorkingOrder(price, side, OrderQty);
        RefreshWorkingMarkers();
        var kind = side == DomSide.Buy
            ? (price >= _feed.BestAsk ? "BUY STOP" : "BUY LMT")
            : (price <= _feed.BestBid ? "SELL STOP" : "SELL LMT");
        StatusText = $"{kind} {OrderQty} @ {FormatPx(price)} (mock)";
    }

    private void StartLive()
    {
        _live?.Dispose();
        _live = Observable
            .Interval(TimeSpan.FromMilliseconds(180))
            .Subscribe(_ => PostUi(() =>
            {
                if (Paused) return;
                try
                {
                    var trade = _feed.Tick();
                    if (trade is not null)
                    {
                        _ltqTicksLeft = 6;
                        LtqLabel = $"{trade.Size:0}";
                        LtqBrush = trade.Aggressor == AggressorSide.Buy
                            ? SolidColorBrush.Parse("#86EFAC")
                            : SolidColorBrush.Parse("#FCA5A5");
                        LastLabel = $"{FormatPx(trade.Price)} × {trade.Size:0} {(trade.Aggressor == AggressorSide.Buy ? "▲" : "▼")}";
                        TryFillWorking(trade);
                    }
                    else if (_ltqTicksLeft > 0)
                    {
                        _ltqTicksLeft--;
                        if (_ltqTicksLeft == 0) LtqLabel = "";
                    }

                    RebuildLadder(center: FollowLast);
                }
                catch (Exception ex)
                {
                    // Never let a mock tick kill the whole desktop process
                    StatusText = $"DOM mock hiccup · {ex.GetType().Name}";
                }
            }));
    }

    private void TryFillWorking(TradeEvent trade)
    {
        var px = Round(trade.Price);
        if (!_working.TryGetValue(px, out var wo)) return;
        // Naive: buy fills on sell aggressor (hit bid) at our price; sell fills on buy aggressor
        var fills = (wo.Side == DomSide.Buy && trade.Aggressor == AggressorSide.Sell)
                    || (wo.Side == DomSide.Sell && trade.Aggressor == AggressorSide.Buy);
        if (!fills) return;
        _working.Remove(px);
        StatusText = $"FILL {wo.Side} {wo.Qty} @ {FormatPx(px)} (mock)";
    }

    private void RebuildLadder(bool center)
    {
        var snap = _feed.Snapshot();
        var tick = _feed.TickSize;
        var last = Round(_feed.Last);
        var bestBid = Round(_feed.BestBid);
        var bestAsk = Round(_feed.BestAsk);
        var mid = Round((bestBid + bestAsk) / 2.0);

        SpreadLabel = $"{FormatPx(bestAsk - bestBid)}   {FormatPx(bestBid)}/{FormatPx(bestAsk)}";

        var bidNear = snap.Bids.Take(5).Sum(b => b.Size);
        var askNear = snap.Asks.Take(5).Sum(a => a.Size);
        var tot = bidNear + askNear;
        if (tot > 0)
        {
            var pct = bidNear / tot;
            ImbalanceLabel = pct > 0.58 ? $"BID {pct:P0}" : pct < 0.42 ? $"ASK {1 - pct:P0}" : "BALANCED";
        }

        const int half = 14;
        var top = Round(mid + half * tick);
        if (!center && Rows.Count > 0)
            top = Rows[0].Price;

        // Keep last near vertical center when following
        if (center)
            top = Round(last + half * tick);

        var bidMap = snap.Bids.ToDictionary(b => Round(b.Price), b => b.Size);
        var askMap = snap.Asks.ToDictionary(a => Round(a.Price), a => a.Size);
        var maxBook = Math.Max(
            1,
            Math.Max(
                bidMap.Values.DefaultIfEmpty(1).Max(),
                askMap.Values.DefaultIfEmpty(1).Max()));
        var maxVol = Math.Max(1, _feed.VolAtPrice.Values.DefaultIfEmpty(1).Max());

        var levels = half * 2 + 1;
        if (Rows.Count != levels)
        {
            Rows.Clear();
            for (var i = 0; i < levels; i++)
                Rows.Add(new DomLevelRowViewModel());
        }

        for (var i = 0; i < levels; i++)
        {
            var px = Round(top - i * tick);
            var bid = bidMap.GetValueOrDefault(px);
            var ask = askMap.GetValueOrDefault(px);
            // Classic ladder: bids only at/below best bid, asks only at/above best ask
            if (px > bestBid) bid = 0;
            if (px < bestAsk) ask = 0;

            var vol = _feed.VolAtPrice.GetValueOrDefault(px);
            var row = Rows[i];
            row.Update(
                price: px,
                tickSize: tick,
                bid: bid,
                ask: ask,
                vol: vol,
                maxBook: maxBook,
                maxVol: maxVol,
                isBestBid: px == bestBid,
                isBestAsk: px == bestAsk,
                isLast: px == last,
                ltq: px == last && _ltqTicksLeft > 0 ? _feed.LastSize : 0,
                aggressor: px == last ? _feed.LastAggressor : AggressorSide.Unknown,
                working: _working.GetValueOrDefault(px));
        }
    }

    private void RefreshWorkingMarkers()
    {
        foreach (var row in Rows)
            row.SetWorking(_working.GetValueOrDefault(Round(row.Price)));
    }

    private double Round(double px) => _feed.Round(px);

    private string FormatPx(double px) =>
        _feed.TickSize >= 0.25 ? px.ToString("0.00") : px.ToString("0.00");

    private static void PostUi(Action a) =>
        Dispatcher.UIThread.Post(a, DispatcherPriority.Background);

    public void Dispose()
    {
        _live?.Dispose();
        _live = null;
        GC.SuppressFinalize(this);
    }
}

public enum DomSide { Buy, Sell }

public sealed record DomWorkingOrder(double Price, DomSide Side, int Qty);

public partial class DomLevelRowViewModel : ObservableObject
{
    [ObservableProperty] private double _price;
    [ObservableProperty] private string _priceLabel = "";
    [ObservableProperty] private string _bidLabel = "";
    [ObservableProperty] private string _askLabel = "";
    [ObservableProperty] private string _volLabel = "";
    [ObservableProperty] private string _ltqFlash = "";
    [ObservableProperty] private string _workingLabel = "";
    [ObservableProperty] private double _bidBar;
    [ObservableProperty] private double _askBar;
    [ObservableProperty] private double _volBar;
    [ObservableProperty] private bool _isBestBid;
    [ObservableProperty] private bool _isBestAsk;
    [ObservableProperty] private bool _isLast;
    [ObservableProperty] private bool _isInside;
    [ObservableProperty] private IBrush _rowBg = SolidColorBrush.Parse("#100E14");
    [ObservableProperty] private IBrush _priceFg = SolidColorBrush.Parse("#F3ECEF");
    [ObservableProperty] private IBrush _bidFg = SolidColorBrush.Parse("#86EFAC");
    [ObservableProperty] private IBrush _askFg = SolidColorBrush.Parse("#FCA5A5");
    [ObservableProperty] private IBrush _ltqFg = SolidColorBrush.Parse("#D4A8B0");

    public void Update(
        double price,
        double tickSize,
        double bid,
        double ask,
        double vol,
        double maxBook,
        double maxVol,
        bool isBestBid,
        bool isBestAsk,
        bool isLast,
        double ltq,
        AggressorSide aggressor,
        DomWorkingOrder? working)
    {
        Price = price;
        PriceLabel = tickSize >= 0.25 ? price.ToString("0.00") : price.ToString("0.00");
        BidLabel = bid > 0 ? bid.ToString("0") : "";
        AskLabel = ask > 0 ? ask.ToString("0") : "";
        VolLabel = vol > 0 ? vol.ToString("0") : "";
        BidBar = bid > 0 ? Math.Clamp(bid / maxBook, 0.04, 1) * 72 : 0;
        AskBar = ask > 0 ? Math.Clamp(ask / maxBook, 0.04, 1) * 72 : 0;
        VolBar = vol > 0 ? Math.Clamp(vol / maxVol, 0.05, 1) * 40 : 0;
        IsBestBid = isBestBid;
        IsBestAsk = isBestAsk;
        IsLast = isLast;
        IsInside = bid <= 0 && ask <= 0 && !isBestBid && !isBestAsk;

        LtqFlash = ltq > 0 ? ltq.ToString("0") : "";
        LtqFg = aggressor == AggressorSide.Buy
            ? SolidColorBrush.Parse("#86EFAC")
            : aggressor == AggressorSide.Sell
                ? SolidColorBrush.Parse("#FCA5A5")
                : SolidColorBrush.Parse("#D4A8B0");

        if (isLast)
        {
            RowBg = SolidColorBrush.Parse("#2A1E24");
            PriceFg = SolidColorBrush.Parse("#F3ECEF");
        }
        else if (isBestBid || isBestAsk)
        {
            RowBg = SolidColorBrush.Parse("#18141C");
            PriceFg = SolidColorBrush.Parse("#D4A8B0");
        }
        else
        {
            RowBg = SolidColorBrush.Parse("#100E14");
            PriceFg = SolidColorBrush.Parse("#C4B8BF");
        }

        SetWorking(working);
    }

    public void SetWorking(DomWorkingOrder? working)
    {
        if (working is null)
        {
            WorkingLabel = "";
            return;
        }
        WorkingLabel = working.Side == DomSide.Buy
            ? $"B{working.Qty}"
            : $"S{working.Qty}";
    }
}
