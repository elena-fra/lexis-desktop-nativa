using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Lexis.Contracts.Market;
using Lexis.Desktop.App.Services;

namespace Lexis.Desktop.App.ViewModels.Documents;

/// <summary>Option Flow desk — mirrors web renderOrder (filters · KPIs · feed · rail).</summary>
public partial class OptionFlowDocumentViewModel : Document, IDisposable
{
    private readonly IFlowFeed _feed;
    private readonly List<FlowRowDto> _all = new();
    private IDisposable? _liveSub;

    public ObservableCollection<FlowRowItemViewModel> Rows { get; } = new();
    public ObservableCollection<string> TickerChips { get; } = new();
    public ObservableCollection<GoldenItemViewModel> GoldenRows { get; } = new();
    public ObservableCollection<HeatStrikeViewModel> HeatStrikes { get; } = new();

    public Action<FlowRowDto>? OpenInChain { get; set; }

    [ObservableProperty] private string _statusText = "Feed · click riga → Option Chain";
    [ObservableProperty] private bool _isLive = true;
    [ObservableProperty] private bool _paused;
    [ObservableProperty] private string _liveBadge = "OPRA LIVE";

    // Filters (web ofState)
    [ObservableProperty] private string _minPremKey = "10000";
    [ObservableProperty] private string _sentiment = "all";
    [ObservableProperty] private string _moneyness = "all";
    [ObservableProperty] private string _dteFilter = "all";
    [ObservableProperty] private bool _goldenOnly;
    [ObservableProperty] private string _tickerQuery = "";

    [ObservableProperty] private FlowRowItemViewModel? _selectedRow;

    // KPIs
    [ObservableProperty] private string _kpiPremium = "$0";
    [ObservableProperty] private string _kpiCallPut = "—";
    [ObservableProperty] private string _kpiBullish = "0";
    [ObservableProperty] private string _kpiBearish = "0";
    [ObservableProperty] private string _kpiSweeps = "0";
    [ObservableProperty] private string _kpiBlocks = "0";
    [ObservableProperty] private string _kpiGolden = "0";
    [ObservableProperty] private string _kpiRows = "0";

    // Sentiment rail
    [ObservableProperty] private string _sentimentLabel = "NEUTRO";
    [ObservableProperty] private string _sentimentColor = "#D4A8B0";
    [ObservableProperty] private string _sentimentScore = "50/100 · bias premio";
    [ObservableProperty] private int _sentBull;
    [ObservableProperty] private int _sentBear;
    [ObservableProperty] private int _sentNeut;
    [ObservableProperty] private double _needleAngle = -90;

    public IBrush SentimentBrush => SolidColorBrush.Parse(SentimentColor);

    public bool Min10k => MinPremKey == "10000";
    public bool Min50k => MinPremKey == "50000";
    public bool Min100k => MinPremKey == "100000";
    public bool Min1m => MinPremKey == "1000000";
    public bool SentAll => Sentiment == "all";
    public bool SentBullOn => Sentiment == "bullish";
    public bool SentBearOn => Sentiment == "bearish";
    public bool SentNeutOn => Sentiment == "neutral";
    public bool MoneyAll => Moneyness == "all";
    public bool MoneyItm => Moneyness == "itm";
    public bool MoneyOtm => Moneyness == "otm";
    public bool DteAll => DteFilter == "all";
    public bool Dte0 => DteFilter == "0";
    public bool DteWeek => DteFilter == "week";
    public bool DteLeaps => DteFilter == "leaps";
    public bool HasGolden => GoldenRows.Count > 0;

    public OptionFlowDocumentViewModel() : this(new MockFlowFeedAdapter()) { }

    public OptionFlowDocumentViewModel(IFlowFeed feed)
    {
        _feed = feed;
        Id = "order";
        Title = "Option Flow";
        CanClose = true;
        Reload();
        StartLive();
    }

    partial void OnMinPremKeyChanged(string value)
    {
        NotifySeg();
        ApplyFilter();
    }
    partial void OnSentimentChanged(string value)
    {
        NotifySeg();
        ApplyFilter();
    }
    partial void OnMoneynessChanged(string value)
    {
        NotifySeg();
        ApplyFilter();
    }
    partial void OnDteFilterChanged(string value)
    {
        NotifySeg();
        ApplyFilter();
    }
    partial void OnGoldenOnlyChanged(bool value) => ApplyFilter();

    private void NotifySeg()
    {
        OnPropertyChanged(nameof(Min10k));
        OnPropertyChanged(nameof(Min50k));
        OnPropertyChanged(nameof(Min100k));
        OnPropertyChanged(nameof(Min1m));
        OnPropertyChanged(nameof(SentAll));
        OnPropertyChanged(nameof(SentBullOn));
        OnPropertyChanged(nameof(SentBearOn));
        OnPropertyChanged(nameof(SentNeutOn));
        OnPropertyChanged(nameof(MoneyAll));
        OnPropertyChanged(nameof(MoneyItm));
        OnPropertyChanged(nameof(MoneyOtm));
        OnPropertyChanged(nameof(DteAll));
        OnPropertyChanged(nameof(Dte0));
        OnPropertyChanged(nameof(DteWeek));
        OnPropertyChanged(nameof(DteLeaps));
    }

    partial void OnSelectedRowChanged(FlowRowItemViewModel? value)
    {
        if (value is null) return;
        OpenInChain?.Invoke(value.Dto);
        StatusText = $"→ Chain {value.Ticker} {value.Strike:0.##}{value.Cp} · {value.Exec} · {value.Side}";
    }

    [RelayCommand]
    private void SelectRow(FlowRowItemViewModel? row)
    {
        if (row is null) return;
        SelectedRow = row;
    }

    [RelayCommand] private void SetMinPrem(string? key) { if (key is not null) MinPremKey = key; }
    [RelayCommand] private void SetSentiment(string? key) { if (key is not null) Sentiment = key; }
    [RelayCommand] private void SetMoneyness(string? key) { if (key is not null) Moneyness = key; }
    [RelayCommand] private void SetDteFilter(string? key) { if (key is not null) DteFilter = key; }
    [RelayCommand] private void ToggleGolden() => GoldenOnly = !GoldenOnly;

    [RelayCommand]
    private void TogglePause()
    {
        Paused = !Paused;
        LiveBadge = Paused ? "OPRA PAUSA" : "OPRA LIVE";
        StatusText = Paused ? "Feed in pausa" : $"{_feed.SourceLabel} · live";
    }

    [RelayCommand]
    private void ToggleTicker(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return;
        symbol = symbol.ToUpperInvariant();
        if (TickerChips.Contains(symbol))
            TickerChips.Remove(symbol);
        else
            TickerChips.Add(symbol);
        ApplyFilter();
    }

    [RelayCommand]
    private void ClearTickers()
    {
        TickerChips.Clear();
        ApplyFilter();
    }

    [RelayCommand]
    private void Reload()
    {
        _all.Clear();
        _all.AddRange(_feed.Seed(52));
        ApplyFilter();
        StatusText = $"{_feed.SourceLabel} · {_all.Count} prints · Lee-Ready (Ask=buy · Bid=sell)";
        LiveBadge = _feed.IsApiLive ? "API LIVE" : "OPRA LIVE";
    }

    [RelayCommand]
    private void StartLive()
    {
        if (_liveSub is not null) return;
        IsLive = true;
        _liveSub = _feed.StartLive(
            onRow: row =>
            {
                PostUi(() =>
                {
                    // Dedup by id when API
                    if (_all.Any(x => x.Id == row.Id)) return;
                    _all.Insert(0, row);
                    while (_all.Count > 500) _all.RemoveAt(_all.Count - 1);
                    ApplyFilter();
                });
            },
            isPaused: () => Paused);
    }

    [RelayCommand]
    private void StopLive()
    {
        _liveSub?.Dispose();
        _liveSub = null;
        IsLive = false;
        Paused = true;
        LiveBadge = "OPRA PAUSA";
    }

    private void ApplyFilter()
    {
        var minPrem = MinPremKey switch
        {
            "50000" => 50_000L,
            "100000" => 100_000L,
            "1000000" => 1_000_000L,
            _ => 10_000L,
        };

        IEnumerable<FlowRowDto> q = _all;
        q = q.Where(r => r.Prem >= minPrem);
        if (Sentiment is not "all")
            q = q.Where(r => r.Sentiment == Sentiment);
        if (Moneyness is not "all")
        {
            q = q.Where(r =>
            {
                var itm = r.Type == "call" ? r.Strike < r.Spot : r.Strike > r.Spot;
                return Moneyness == "itm" ? itm : !itm;
            });
        }
        if (DteFilter is not "all")
        {
            q = q.Where(r => DteFilter switch
            {
                "0" => r.Dte == 0,
                "week" => r.Dte <= 7,
                "leaps" => r.Dte >= 365,
                _ => true,
            });
        }
        if (TickerChips.Count > 0)
            q = q.Where(r => TickerChips.Contains(r.Ticker));
        if (GoldenOnly)
            q = q.Where(r => r.Golden);

        var filtered = q.Take(180).ToList();
        var kpis = MockFlowFeed.ComputeKpis(filtered);
        KpiPremium = FormatPrem(kpis.FilteredPremium);
        KpiCallPut = kpis.CallPutRatio.ToString("0.00");
        KpiBullish = kpis.Bullish.ToString();
        KpiBearish = kpis.Bearish.ToString();
        KpiSweeps = kpis.Sweeps.ToString();
        KpiBlocks = kpis.Blocks.ToString();
        KpiGolden = kpis.Golden.ToString();
        KpiRows = kpis.Rows.ToString();

        UpdateSentiment(filtered);
        UpdateGolden(filtered);
        UpdateHeat(filtered);
        OnPropertyChanged(nameof(HasGolden));

        Rows.Clear();
        foreach (var dto in filtered)
            Rows.Add(FlowRowItemViewModel.From(dto));
    }

    private void UpdateSentiment(IReadOnlyList<FlowRowDto> rows)
    {
        long bullP = 0, bearP = 0;
        var bull = 0; var bear = 0; var neut = 0;
        foreach (var r in rows)
        {
            var p = Math.Max(0, r.Prem);
            if (r.Sentiment == "bullish") { bull++; bullP += p; }
            else if (r.Sentiment == "bearish") { bear++; bearP += p; }
            else { neut++; }
        }
        SentBull = bull; SentBear = bear; SentNeut = neut;
        var scored = bullP + bearP;
        var bias = scored > 0 ? (bullP - bearP) / (double)scored : 0;
        // Needle default points UP (neutro); -90° = BEAR, +90° = BULL (web aesthetic, corrected mapping)
        NeedleAngle = bias * 90;
        var score = (int)Math.Round(((bias + 1) / 2) * 100);
        SentimentLabel = scored == 0 ? (neut > 0 ? "NEUTRO" : "—")
            : bias > 0.18 ? "RIALZISTA" : bias < -0.18 ? "RIBASSISTA" : "NEUTRO";
        SentimentColor = scored == 0 ? "#D4A8B0"
            : bias > 0.18 ? "#86EFAC" : bias < -0.18 ? "#FCA5A5" : "#D4A8B0";
        SentimentScore = $"{score}/100 · bias premio";
        OnPropertyChanged(nameof(SentimentBrush));
    }

    private void UpdateGolden(IReadOnlyList<FlowRowDto> rows)
    {
        GoldenRows.Clear();
        foreach (var r in rows.Where(x => x.Golden).Take(8))
            GoldenRows.Add(new GoldenItemViewModel(r.Ticker, r.Strike, r.Type, r.Prem, r.Dte));
    }

    private void UpdateHeat(IReadOnlyList<FlowRowDto> rows)
    {
        HeatStrikes.Clear();
        var groups = rows
            .GroupBy(r => Math.Round(r.Strike, 0))
            .Select(g => new
            {
                Strike = g.Key,
                Call = g.Where(x => x.Type == "call").Sum(x => x.Size),
                Put = g.Where(x => x.Type == "put").Sum(x => x.Size),
            })
            .OrderByDescending(x => x.Call + x.Put)
            .Take(6)
            .ToList();
        var max = Math.Max(1, groups.Count == 0 ? 1 : groups.Max(x => x.Call + x.Put));
        foreach (var g in groups)
            HeatStrikes.Add(new HeatStrikeViewModel(g.Strike, g.Call, g.Put, (g.Call + g.Put) / (double)max));
    }

    private static string FormatPrem(long prem) =>
        prem >= 1_000_000 ? $"${prem / 1_000_000.0:0.00}M" :
        prem >= 1_000 ? $"${prem / 1_000.0:0.0}K" :
        $"${prem:N0}";

    private static void PostUi(Action action) =>
        Dispatcher.UIThread.Post(action, DispatcherPriority.Background);

    public void Dispose()
    {
        StopLive();
        GC.SuppressFinalize(this);
    }
}

public partial class FlowRowItemViewModel : ObservableObject
{
    public FlowRowDto Dto { get; private init; } = null!;

    public string Timestamp => Dto.Timestamp;
    public string Ticker => Dto.Ticker;
    public string SpotLabel => Dto.Spot.ToString("0.00");
    public string ExpLabel => $"{Dto.Exp}";
    public string DteLabel => $"{Dto.Dte}g";
    public string ExpDte => $"{Dto.Exp} {Dto.Dte}g";
    public double Strike => Dto.Strike;
    public string StrikeLabel => Dto.Strike.ToString("0.##");
    public string Cp => Dto.Type == "call" ? "C" : "P";
    public IBrush CpBrush => Dto.Type == "call"
        ? SolidColorBrush.Parse("#86EFAC")
        : SolidColorBrush.Parse("#FCA5A5");
    public string SizeOi => $"{Dto.Size:N0}/{Dto.Oi:N0}";
    public string PremLabel => Dto.Prem >= 1_000_000 ? $"${Dto.Prem / 1_000_000.0:0.00}M" :
        Dto.Prem >= 1_000 ? $"${Dto.Prem / 1_000.0:0.0}K" : $"${Dto.Prem:N0}";
    public bool PremHot => Dto.Prem >= 100_000;
    public string Exec => Dto.Exec;
    public string Side => Dto.Side;
    public string ConfLabel => Dto.Confidence is int c ? $"CS {c}" : "CS —";
    public string ExecLine => $"{Dto.Exec}  {Dto.Side}  {(Dto.Confidence is int c ? $"CS {c}" : "CS —")}{(Dto.Golden ? "  ★" : "")}";
    public bool Golden => Dto.Golden;
    public bool Anomaly => Dto.Anomaly;
    public string DirLabel => Dto.Sentiment switch
    {
        "bullish" => "▲ Rial.",
        "bearish" => "▼ Rib.",
        _ => "• Neut.",
    };
    public IBrush RowAccent => Dto.Sentiment switch
    {
        "bullish" => SolidColorBrush.Parse("#22C55E"),
        "bearish" => SolidColorBrush.Parse("#EF4444"),
        _ => SolidColorBrush.Parse("#D4A8B0"),
    };
    public IBrush RowBg => Dto.Golden
        ? SolidColorBrush.Parse("#1A1520")
        : Dto.Sentiment == "bullish" ? SolidColorBrush.Parse("#0F1A14")
        : Dto.Sentiment == "bearish" ? SolidColorBrush.Parse("#1A1212")
        : SolidColorBrush.Parse("#100E14");
    public IBrush ExecBg => Dto.Exec switch
    {
        "SWEEP" => SolidColorBrush.Parse("#3A2A32"),
        "BLOCK" => SolidColorBrush.Parse("#2A2430"),
        _ => SolidColorBrush.Parse("#1E1A22"),
    };
    public IBrush SideBg => Dto.Side switch
    {
        "ASK" => SolidColorBrush.Parse("#1A2E22"),
        "BID" => SolidColorBrush.Parse("#2E1A18"),
        _ => SolidColorBrush.Parse("#2A2430"),
    };
    public IBrush SideFg => Dto.Side switch
    {
        "ASK" => SolidColorBrush.Parse("#86EFAC"),
        "BID" => SolidColorBrush.Parse("#FCA5A5"),
        _ => SolidColorBrush.Parse("#D4A8B0"),
    };

    public static FlowRowItemViewModel From(FlowRowDto dto) => new() { Dto = dto };
}

public sealed record GoldenItemViewModel(string Ticker, double Strike, string Type, long Prem, int Dte)
{
    public string Line => $"{Ticker}  {Strike:0.##}{(Type == "call" ? "C" : "P")}  ·  {Dte}g";
    public string PremLabel => Prem >= 1_000_000 ? $"${Prem / 1_000_000.0:0.00}M" : $"${Prem / 1_000.0:0.0}K";
}

public sealed record HeatStrikeViewModel(double Strike, int Call, int Put, double Intensity)
{
    public string Label => Strike.ToString("0");
    public string Bar => new string('█', Math.Max(1, (int)Math.Round(Intensity * 10)));
}
