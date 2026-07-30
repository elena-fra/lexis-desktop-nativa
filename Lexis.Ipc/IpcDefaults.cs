namespace Lexis.Ipc;

/// <summary>Fase 0 defaults — NetMQ PUB/SUB on loopback (Scheda §3.2).</summary>
public static class IpcDefaults
{
    public const string Endpoint = "tcp://127.0.0.1:5556";
    public const string TopicTrades = "trades";
    public const ushort ProtocolVersion = 1;
}
