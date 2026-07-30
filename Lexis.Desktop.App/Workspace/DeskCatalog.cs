namespace Lexis.Desktop.App.Workspace;

/// <summary>Catalog of desk documents and tools openable from the menu.</summary>
public static class DeskCatalog
{
    public enum PanelKind
    {
        Document,
        Tool,
    }

    public sealed record Panel(string Id, string Title, string Blurb, PanelKind Kind);

    public static IReadOnlyList<Panel> Documents { get; } =
    [
        new("dashboard", "Riepilogo", "Conto · equity · P&L · esposizione (mock/API ready)", PanelKind.Document),
        new("gex", "GEX", "Gamma exposure profile · flip · walls (mock)", PanelKind.Document),
        new("greche", "Greche", "Greeks matrix · Δ Γ Θ ν ρ + 2nd order (mock/API ready)", PanelKind.Document),
        new("chain", "Option Chain", "TOS-style chain", PanelKind.Document),
        new("order", "Option Flow", "Unusual options activity", PanelKind.Document),
        new("graf", "Grafici", "Candlestick · LexisDesktop.Charts", PanelKind.Document),
        new("strategy", "Strategy Builder", "Leg builder", PanelKind.Document),
        new("backtest", "Backtest", "Strategy replay", PanelKind.Document),
        new("quant", "Quant & Algo", "Quant workspace", PanelKind.Document),
        new("risk", "Rischio & Payoff", "Risk / payoff", PanelKind.Document),
        new("vol", "Volatility", "IV surface", PanelKind.Document),
        new("heatmap", "Heatmap", "Strike heatmap", PanelKind.Document),
        new("scanner", "Scanner", "Idea scanner", PanelKind.Document),
        new("positions", "Posizioni", "Open positions", PanelKind.Document),
        new("portfolio", "Portafoglio", "Net Liq · BP · β-Δ · equity curve (mock/API ready)", PanelKind.Document),
        new("journal", "Mentor (AI)", "Trade mentor", PanelKind.Document),
        new("tv", "Diretta TV", "Market TV", PanelKind.Document),
    ];

    public static IReadOnlyList<Panel> Tools { get; } =
    [
        new("status", "Status / IPC", "IPC tick feed status", PanelKind.Tool),
        new("dom", "DOM", "Depth of market", PanelKind.Tool),
        new("footprint", "Footprint", "Footprint chart", PanelKind.Tool),
        new("profile", "Volume Profile", "Volume profile", PanelKind.Tool),
        new("timesales", "Time & Sales", "Executed tape", PanelKind.Tool),
    ];

    public static Panel? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return Documents.FirstOrDefault(p => p.Id == id)
            ?? Tools.FirstOrDefault(p => p.Id == id);
    }
}
