# Lexis Desktop (Avalonia path)

Parallel track for the desktop Avalonia + C++/.NET stack. **Does not modify** the web stack.

## Build

```bash
dotnet build Lexis.Desktop.sln
```

## Run (Fase 0 IPC + UI)

Preferred (visible window on Windows):

```bat
Launch-Lexis-Desktop.bat
```

**Live API (passo 2):** avvia prima lo stack web (`lexis\start-lexis.ps1`), poi la desktop.
All’avvio l’app prova `http://127.0.0.1:3001`:
- Option Flow → `GET /api/v1/flow` + SSE `flow.row`
- Option Chain → `GET /api/v1/market/chain/{symbol}` (poll 2s in Live)
- Se l’API è offline → fallback mock automatico

Credenziali default locali: utente `lexis-desktop` (auto-register).  
Override: `%AppData%\LEXIS\desktop.settings.json` oppure env `LEXIS_API_URL` / `LEXIS_USER` / `LEXIS_PASS`.

In alto nella finestra vedi lo stato: `API · …` oppure `mock (…)`.

Or manually:

```bash
# Terminal 1 — C++ synthetic tick producer (libzmq PUB, LX01)
# First build once: powershell -File Lexis.Ipc.Producer.Native/build.ps1
Lexis.Ipc.Producer.Native/bin/Lexis.Ipc.Producer.Native.exe

# Terminal 2 / Explorer — Avalonia app (NetMQ SUB)
start Lexis.Desktop.App/bin/Debug/net8.0/Lexis.Desktop.App.exe
```

In the app: open **Status / IPC** (should show tick/s). On the chain toolbar: **Follow IPC** to reprice from last trade. Menu **Desk → Option Flow** opens the mock tape (click a row → chain focus).

## Projects

| Project | Role |
|---------|------|
| `Lexis.Contracts` | Frozen contracts (A) |
| `Lexis.Pricing` | BSM + CRR (A) |
| `Lexis.Ipc` | NetMQ + binary codec LX01 (C / Fase 0 SUB) |
| `Lexis.Ipc.Producer.Native` | **C++** synthetic PUB (docs Fase 0) |
| `Lexis.Ipc.Producer` | C# fallback / reference publisher |
| `Lexis.Desktop.App` | Avalonia + Dock + consumer (B+C) · Grafici via `modulo-visivo/LexisDesktop.Charts` |

## Charts (punto 3)

Menu **Desk → Grafici** apre il pannello dockabile basato su `LexisDesktop.Charts` (ScottPlot).
Dati demo OHLC; feed live L1 in un passo successivo.

## Docs alignment

- Scheda §3.2: NetMQ/ZeroMQ first — **C++ producer**, .NET consumer
- Scheda §6 weeks 1–2: synthetic ticks validate pipeline before live Databento
- Track D: UI already on mock; IPC integration is the S3 precursor

## Note

`dotnet run` from Cursor may hide the GUI window. Prefer the `.bat` or `start` on the `.exe`.
