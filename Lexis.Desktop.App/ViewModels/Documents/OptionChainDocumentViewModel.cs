using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Lexis.Contracts.Market;
using Lexis.Desktop.App.Services;
using Lexis.Desktop.App.ViewModels;

namespace Lexis.Desktop.App.ViewModels.Documents;

public partial class OptionChainDocumentViewModel : Document, IDisposable
{
    private readonly IChainFeed _feed;
    private readonly IpcTradeFeed? _ipc;
    private readonly object _chainGate = new();
    private IDisposable? _subscription;
    private IDisposable? _ipcSub;
    private ChainDto _chain = null!;
    private double _strikeStep = 5;
    private int _applyEpoch;

    public ObservableCollection<ChainStrikeRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> SymbolOptions { get; }
    public ObservableCollection<string> ExpiryOptions { get; } = new();
    public ObservableCollection<MockOrderLegViewModel> Legs { get; } = new();
    public ObservableCollection<string> StrikeCountOptions { get; } = new(["8", "12", "21", "41"]);

    private UnderlyingInfo SelectedUnderlying =>
        _feed.Underlyings.FirstOrDefault(u => u.Symbol == SelectedSymbol)
        ?? _feed.Underlyings[0];

    private ExpiryDto? SelectedExpiry =>
        _expiries.FirstOrDefault(e => FormatExpiry(e) == SelectedExpiryOption)
        ?? _expiries.FirstOrDefault();

    private List<ExpiryDto> _expiries = new();

    [ObservableProperty] private string _selectedSymbol = "SPY";
    [ObservableProperty] private string _selectedExpiryOption = "";
    [ObservableProperty] private string _selectedStrikeCountOption = "21";
    [ObservableProperty] private string _symbol = "SPY";
    [ObservableProperty] private double _spot;
    [ObservableProperty] private string _expiryLabel = "";
    [ObservableProperty] private int _dte;
    [ObservableProperty] private bool _isLive;
    [ObservableProperty] private bool _followIpc;
    [ObservableProperty] private string _statusText = "Option Chain · click Ask=BUY Bid=SELL";
    [ObservableProperty] private bool _columnsOpen;
    [ObservableProperty] private string _liveHud = "";

    public string SpotHud => $"{Symbol}  {Spot:F2}";
    public string ExpiryHud => string.IsNullOrEmpty(ExpiryLabel) ? "" : $"{ExpiryLabel} · DTE {Dte}";
    public string TicketFooter =>
        $"Total notional ${TicketNotional:N0}  ·  {TicketDebitCredit}  ·  multiplier ×100";

    public int SelectedStrikeCount =>
        int.TryParse(SelectedStrikeCountOption, out var n) ? n : 21;

    // Default: quotes + strike + IV/Δ only — extra greeks overlap/crush the strike column.
    [ObservableProperty] private bool _showIv = true;
    [ObservableProperty] private bool _showDelta = true;
    [ObservableProperty] private bool _showGamma = false;
    [ObservableProperty] private bool _showTheta = false;
    [ObservableProperty] private bool _showVega = false;
    [ObservableProperty] private bool _showVol = false;
    [ObservableProperty] private bool _showOi = false;

    [ObservableProperty] private bool _ticketOpen;
    [ObservableProperty] private string _ticketAccent = "#00FF7A";
    [ObservableProperty] private string _ticketHeadline = "";
    [ObservableProperty] private string _ticketDetails = "";
    [ObservableProperty] private double _ticketQty = 1;
    [ObservableProperty] private double _ticketNotional;
    [ObservableProperty] private string _ticketDebitCredit = "";

    public OptionChainDocumentViewModel() : this(new MockChainFeedAdapter(), null) { }

    public OptionChainDocumentViewModel(IChainFeed feed, IpcTradeFeed? ipc = null)
    {
        _feed = feed;
        _ipc = ipc;
        SymbolOptions = new ObservableCollection<string>(_feed.Underlyings.Select(u => u.Symbol));
        Id = "chain";
        Title = "Option Chain";
        CanClose = false;
        SelectedSymbol = SymbolOptions[0];
        ReloadChain();
    }

    partial void OnTicketQtyChanged(double value) => RecalcTicketMetrics();

    [RelayCommand]
    private void ApplyUnderlyingExpiry()
    {
        StopLive();
        StopIpcFollow();
        ReloadChain();
        StatusText = $"Loaded {Symbol} · {ExpiryLabel} · {SelectedStrikeCount} strikes";
    }

    [RelayCommand]
    private void ToggleColumns() => ColumnsOpen = !ColumnsOpen;

    [RelayCommand]
    private void StartLive()
    {
        StopIpcFollow();
        if (_subscription is not null) return;
        IsLive = true;

        // API mode: soft-poll chain every ~2s
        var apiPoll = _feed.StartApiPoll(
            current: () => { lock (_chainGate) return _chain; },
            onUpdate: chain => PostUi(() =>
            {
                ApplyChain(chain, light: true);
                LiveHud = $"API {chain.Spot:F2} · {Rows.Count} rows";
                StatusText = $"Live API · {_feed.SourceLabel}";
            }),
            isActive: () => IsLive && !FollowIpc);

        if (apiPoll is not null)
        {
            _subscription = apiPoll;
            StatusText = $"Live API · soft poll 2s · {_feed.SourceLabel}";
            return;
        }

        StatusText = "Live mock · background tick + UI sample ~48ms";

        // Produce off UI thread; coalesce to ~20fps to reduce GC/freeze (docs §2.1 / §3.4)
        _subscription = Observable
            .Interval(TimeSpan.FromMilliseconds(16), Scheduler.Default)
            .Select(_ =>
            {
                lock (_chainGate) return _feed.Tick(_chain);
            })
            .Sample(UiFeed.Frame)
            .Subscribe(chain => UiFeed.Post(() =>
            {
                ApplyChain(chain, light: true);
                LiveHud = $"live {chain.Spot:F2} · {Rows.Count} rows";
            }));
    }

    [RelayCommand]
    private void StopLive()
    {
        _subscription?.Dispose();
        _subscription = null;
        IsLive = false;
        LiveHud = "";
        if (!FollowIpc && !TicketOpen)
            StatusText = _feed.IsApiLive ? "API feed paused" : "Mock feed paused";
    }

    [RelayCommand]
    private void ToggleIpcFollow()
    {
        if (FollowIpc)
        {
            StopIpcFollow();
            StatusText = "IPC follow off";
            return;
        }

        if (_ipc is null)
        {
            StatusText = "IPC feed not available";
            return;
        }

        StopLive();
        FollowIpc = true;
        StatusText = "IPC follow · spot bump + Tick (no full rebuild) @ ~48ms";

        // Heavy path avoided: don't Create() every trade — Tick around last IPC price.
        _ipcSub = _ipc.CoalescedTrades(UiFeed.Frame)
            .Subscribe(trade =>
            {
                ChainDto next;
                lock (_chainGate)
                {
                    if (!string.Equals(_chain.Symbol, trade.Symbol, StringComparison.OrdinalIgnoreCase)
                        && _feed.Underlyings.Any(u => u.Symbol == trade.Symbol))
                    {
                        _chain = _feed.Create(trade.Symbol, trade.Price, SelectedStrikeCount, expiryDate: SelectedExpiry?.D);
                    }
                    else
                    {
                        _chain = _chain with { Spot = trade.Price };
                        _chain = _feed.Tick(_chain);
                        _chain = _chain with { Spot = Math.Round(trade.Price, 2) };
                    }
                    next = _chain;
                }

                UiFeed.Post(() =>
                {
                    ApplyChain(next, light: true);
                    LiveHud = $"IPC {trade.Symbol} {trade.Price:F2} seq={trade.Sequence}";
                    StatusText = LiveHud;
                });
            });
    }

    [RelayCommand]
    private void Reload()
    {
        StopLive();
        StopIpcFollow();
        ReloadChain();
        StatusText = $"{_feed.SourceLabel} chain reloaded";
    }

    public void PlaceStub(string right, string side, double strike, double price)
    {
        var qty = Math.Max(1, TicketQty);
        var existing = Legs.FirstOrDefault(l =>
            l.Right == right && Math.Abs(l.Strike - strike) < 1e-6 && l.Side == side);

        if (existing is not null)
        {
            existing.Price = price;
            existing.Qty = qty;
        }
        else
        {
            Legs.Add(new MockOrderLegViewModel
            {
                Side = side,
                Right = right,
                Strike = strike,
                Price = price,
                Qty = qty,
                Symbol = Symbol,
                ExpiryLabel = ExpiryLabel,
            });
        }

        TicketOpen = true;
        TicketAccent = side == "BUY" ? "#166534" : "#7F1D1D";
        RecalcTicketMetrics();
        StatusText = $"Leg {side} {right} {strike:0.##} @ {price:F2}";
    }

    [RelayCommand]
    private void AddQty()
    {
        TicketQty = Math.Min(500, TicketQty + 1);
        if (Legs.Count > 0)
        {
            foreach (var leg in Legs) leg.Qty = TicketQty;
            RecalcTicketMetrics();
        }
    }

    [RelayCommand]
    private void ClearLegs()
    {
        Legs.Clear();
        TicketOpen = false;
        TicketHeadline = "";
        TicketDetails = "";
        StatusText = "Ticket cleared";
    }

    [RelayCommand]
    private void RemoveLastLeg()
    {
        if (Legs.Count == 0) return;
        Legs.RemoveAt(Legs.Count - 1);
        if (Legs.Count == 0) ClearLegs();
        else RecalcTicketMetrics();
    }

    [RelayCommand]
    private void ConfirmTicket()
    {
        if (Legs.Count == 0)
        {
            StatusText = "No legs to send";
            return;
        }

        StatusText = $"MOCK ORDER SENT · {Legs.Count} leg(s) · {TicketDebitCredit} · notional ${TicketNotional:N0}";
        Legs.Clear();
        TicketOpen = false;
    }

    [RelayCommand]
    private void CancelTicket() => ClearLegs();

    /// <summary>Called from Option Flow row click — switch symbol and highlight strike.</summary>
    public void FocusFromFlow(string symbol, double strike, string right, int? dte = null)
    {
        StopLive();
        StopIpcFollow();

        var known = SymbolOptions.FirstOrDefault(s =>
            string.Equals(s, symbol, StringComparison.OrdinalIgnoreCase));
        if (known is not null)
            SelectedSymbol = known;

        ReloadChain();

        if (dte is int dteWant && _expiries.Count > 0)
        {
            var best = _expiries.OrderBy(e => Math.Abs(e.Dte - dteWant)).FirstOrDefault();
            if (best is not null && SelectedExpiry?.D != best.D)
            {
                SelectedExpiryOption = FormatExpiry(best);
                ReloadChain();
            }
        }

        foreach (var row in Rows)
            row.IsFlowFocus = Math.Abs(row.Strike - strike) < 1e-6;

        var hit = Rows.FirstOrDefault(r => r.IsFlowFocus);
        if (known is null)
            StatusText = $"Flow {symbol} non in mock underlyings · strike {strike:0.##} su {Symbol}";
        else
            StatusText = hit is null
                ? $"Flow focus {symbol} {strike:0.##} {right.ToUpperInvariant()} (strike fuori range)"
                : $"Flow focus {symbol} {strike:0.##} {right.ToUpperInvariant()} · DTE {Dte}";
    }

    private void RecalcTicketMetrics()
    {
        if (Legs.Count == 0)
        {
            TicketHeadline = "";
            TicketDetails = "";
            TicketNotional = 0;
            TicketDebitCredit = "";
            OnPropertyChanged(nameof(TicketFooter));
            return;
        }

        var signed = 0.0;
        foreach (var leg in Legs)
        {
            var mult = leg.Side == "BUY" ? 1.0 : -1.0;
            signed += mult * leg.Qty * leg.Price * 100.0;
        }

        TicketNotional = Math.Abs(signed);
        TicketDebitCredit = signed >= 0 ? $"DEBIT ${signed:N0}" : $"CREDIT ${Math.Abs(signed):N0}";
        TicketAccent = signed >= 0 ? "#166534" : "#7F1D1D";
        TicketHeadline = $"{Legs.Count} leg{(Legs.Count > 1 ? "s" : "")} · {TicketDebitCredit}";
        TicketDetails = string.Join("  |  ", Legs.Select(l =>
            $"{l.Side} {(int)l.Qty} {l.Symbol} {l.Strike:0.##} {l.Right} @ {l.Price:F2}"));
        OnPropertyChanged(nameof(TicketFooter));
    }

    private void ReloadChain()
    {
        var under = SelectedUnderlying;
        var symbol = under.Symbol;
        _strikeStep = under.StrikeStep;
        var keepDte = SelectedExpiry?.Dte;
        var expiries = _feed.BuildExpiries(symbol).ToList();
        _expiries = expiries;
        var pick = expiries.FirstOrDefault(e => keepDte is not null && e.Dte == keepDte)
                   ?? expiries.ElementAtOrDefault(2)
                   ?? expiries[0];
        lock (_chainGate)
            _chain = _feed.Create(symbol, strikeCount: SelectedStrikeCount, expiryDate: pick.D);
        SyncSelectorsFromChain(_chain);
        ApplyChain(_chain, light: false);
    }

    private void SyncSelectorsFromChain(ChainDto chain)
    {
        _expiries = chain.Expiries.ToList();
        ExpiryOptions.Clear();
        foreach (var e in _expiries)
            ExpiryOptions.Add(FormatExpiry(e));

        var match = _expiries.FirstOrDefault(e => e.D == chain.Expiry.D) ?? _expiries.FirstOrDefault();
        SelectedExpiryOption = match is null ? "" : FormatExpiry(match);
        if (SymbolOptions.Contains(chain.Symbol))
            SelectedSymbol = chain.Symbol;
    }

    private static string FormatExpiry(ExpiryDto e) => $"{e.L} · {e.Dte}d";

    private void StopIpcFollow()
    {
        _ipcSub?.Dispose();
        _ipcSub = null;
        FollowIpc = false;
        LiveHud = "";
    }

    private void ApplyChain(ChainDto chain, bool light)
    {
        _chain = chain;
        Symbol = chain.Symbol;
        Spot = chain.Spot;
        ExpiryLabel = chain.Expiry.L;
        Dte = chain.Expiry.Dte;
        _strikeStep = _feed.Underlyings.FirstOrDefault(u => u.Symbol == chain.Symbol)?.StrikeStep ?? _strikeStep;

        // Reuse row VMs (virtualization-friendly; avoid clear/rebuild alloc storms)
        while (Rows.Count < chain.Rows.Count)
            Rows.Add(new ChainStrikeRowViewModel { Owner = this });
        while (Rows.Count > chain.Rows.Count)
            Rows.RemoveAt(Rows.Count - 1);

        for (var i = 0; i < chain.Rows.Count; i++)
        {
            Rows[i].Owner = this;
            var keepFocus = Rows[i].IsFlowFocus && Math.Abs(Rows[i].Strike - chain.Rows[i].Strike) < 1e-6;
            Rows[i].Apply(chain.Rows[i], chain.Spot, _strikeStep);
            if (!light) Rows[i].IsFlowFocus = false;
            else if (keepFocus) Rows[i].IsFlowFocus = true;
        }

        if (!light)
            _applyEpoch++;
    }

    partial void OnSymbolChanged(string value) => OnPropertyChanged(nameof(SpotHud));
    partial void OnSpotChanged(double value) => OnPropertyChanged(nameof(SpotHud));
    partial void OnExpiryLabelChanged(string value) => OnPropertyChanged(nameof(ExpiryHud));
    partial void OnDteChanged(int value) => OnPropertyChanged(nameof(ExpiryHud));

    partial void OnSelectedStrikeCountOptionChanged(string value)
    {
        if (_feed is null || IsLive || FollowIpc) return;
        if (_chain is null) return;
        ReloadChain();
    }

    private static void PostUi(Action action) => UiFeed.Post(action);

    public void Dispose()
    {
        StopLive();
        StopIpcFollow();
        GC.SuppressFinalize(this);
    }
}

public partial class MockOrderLegViewModel : ObservableObject
{
    [ObservableProperty] private string _side = "BUY";
    [ObservableProperty] private string _right = "CALL";
    [ObservableProperty] private double _strike;
    [ObservableProperty] private double _price;
    [ObservableProperty] private double _qty = 1;
    [ObservableProperty] private string _symbol = "";
    [ObservableProperty] private string _expiryLabel = "";

    public double Notional => Qty * Price * 100.0;
    public string SummaryLine => $"{Qty:0} × {Symbol} {Strike:0.#} {Right}  @ {Price:F2}";

    partial void OnQtyChanged(double value)
    {
        OnPropertyChanged(nameof(Notional));
        OnPropertyChanged(nameof(SummaryLine));
    }
    partial void OnPriceChanged(double value)
    {
        OnPropertyChanged(nameof(Notional));
        OnPropertyChanged(nameof(SummaryLine));
    }
    partial void OnSymbolChanged(string value) => OnPropertyChanged(nameof(SummaryLine));
    partial void OnStrikeChanged(double value) => OnPropertyChanged(nameof(SummaryLine));
    partial void OnRightChanged(string value) => OnPropertyChanged(nameof(SummaryLine));
}
