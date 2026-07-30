using System.Buffers.Binary;
using System.Text;
using Lexis.Contracts.Ipc;
using Lexis.Contracts.OrderFlow;

namespace Lexis.Ipc;

/// <summary>
/// Compact binary framing for Fase 0 (docs: freeze IPC schema before shared-mem / SBE).
/// Layout: magic(4) ver(2) kind(2) seq(8) tsTicks(8) symbolLen(2) symbol utf8 | payload
/// Trade payload: price(8) size(8) aggressor(1)
/// </summary>
public static class IpcCodec
{
    private static ReadOnlySpan<byte> Magic => "LX01"u8;

    public static byte[] EncodeTrade(TradeEvent trade, long sequence)
    {
        var symbolBytes = Encoding.UTF8.GetBytes(trade.Symbol);
        if (symbolBytes.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(trade), "Symbol too long.");

        var len = 4 + 2 + 2 + 8 + 8 + 2 + symbolBytes.Length + 8 + 8 + 1;
        var buf = new byte[len];
        var w = 0;
        Magic.CopyTo(buf.AsSpan(w)); w += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(w), IpcDefaults.ProtocolVersion); w += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(w), (ushort)IpcPayloadKind.Trade); w += 2;
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(w), sequence); w += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(w), trade.Timestamp.UtcTicks); w += 8;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(w), (ushort)symbolBytes.Length); w += 2;
        symbolBytes.CopyTo(buf.AsSpan(w)); w += symbolBytes.Length;
        BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(w), trade.Price); w += 8;
        BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(w), trade.Size); w += 8;
        buf[w] = (byte)trade.Aggressor;
        return buf;
    }

    public static bool TryDecodeTrade(ReadOnlySpan<byte> data, out TradeEvent trade, out long sequence)
    {
        trade = null!;
        sequence = 0;
        if (data.Length < 4 + 2 + 2 + 8 + 8 + 2 + 8 + 8 + 1) return false;
        if (!data[..4].SequenceEqual(Magic)) return false;

        var r = 4;
        var ver = BinaryPrimitives.ReadUInt16LittleEndian(data[r..]); r += 2;
        if (ver != IpcDefaults.ProtocolVersion) return false;
        var kind = (IpcPayloadKind)BinaryPrimitives.ReadUInt16LittleEndian(data[r..]); r += 2;
        if (kind != IpcPayloadKind.Trade) return false;

        sequence = BinaryPrimitives.ReadInt64LittleEndian(data[r..]); r += 8;
        var ticks = BinaryPrimitives.ReadInt64LittleEndian(data[r..]); r += 8;
        var symLen = BinaryPrimitives.ReadUInt16LittleEndian(data[r..]); r += 2;
        if (data.Length < r + symLen + 17) return false;
        var symbol = Encoding.UTF8.GetString(data.Slice(r, symLen)); r += symLen;
        var price = BinaryPrimitives.ReadDoubleLittleEndian(data[r..]); r += 8;
        var size = BinaryPrimitives.ReadDoubleLittleEndian(data[r..]); r += 8;
        var aggressor = (AggressorSide)data[r];

        trade = new TradeEvent(
            symbol,
            price,
            size,
            new DateTimeOffset(ticks, TimeSpan.Zero),
            aggressor,
            sequence);
        return true;
    }
}
