# Lexis.Ipc.Producer.Native (C++)

Fase 0 synthetic trade publisher — **C++ / libzmq**, same wire format as `Lexis.Ipc` (.NET SUB).

Docs: Scheda tecnica §3.2 / §6 weeks 1–2 (producer C++ → NetMQ/ZeroMQ → .NET).

## Build (Windows + VS Build Tools)

```powershell
cd Lexis.Ipc.Producer.Native
./build.ps1
```

Output: `bin/Lexis.Ipc.Producer.Native.exe` (+ `libzmq*.dll`).

## Run

```bat
Lexis.Ipc.Producer.Native.exe [endpoint] [symbol] [ticksPerSec]
```

Defaults: `tcp://127.0.0.1:5556`  `SPY`  `2000`

Then start the Avalonia app — Status/IPC should show Receiving: YES.

## Note

The old C# `Lexis.Ipc.Producer` remains as a reference/fallback; the launch bat uses this native binary.
