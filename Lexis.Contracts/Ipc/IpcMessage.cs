namespace Lexis.Contracts.Ipc;

/// <summary>
/// Cross-process message envelope — frozen IPC boundary (Mappa dipendenze §1 / Scheda §3.2).
/// Payload serialization (SBE / Cap'n Proto / FlatBuffers / shared-mem layout) is decided in Fase 0.
/// For now this is the logical schema Avalonia and the bus will share.
/// </summary>
public enum IpcPayloadKind : ushort
{
    Heartbeat = 0,
    Trade = 1,
    BookSnapshot = 2,
    BookDelta = 3,
    ChainQuote = 4,
    FootprintBar = 5,
    OrderState = 6,
    Fill = 7,
}

public sealed record IpcEnvelope(
    IpcPayloadKind Kind,
    long Sequence,
    DateTimeOffset TimestampUtc,
    string Symbol,
    ReadOnlyMemory<byte> Payload);
