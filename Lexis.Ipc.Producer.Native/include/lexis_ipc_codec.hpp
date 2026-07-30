#pragma once

#include <cstdint>
#include <cstring>
#include <string>
#include <vector>
#include <chrono>

// Wire format must match Lexis.Ipc.IpcCodec (desktop/.NET):
// magic(4) ver(u16) kind(u16) seq(i64) tsTicks(i64) symbolLen(u16) symbol utf8
// price(f64) size(f64) aggressor(u8)
// All multi-byte integers little-endian. Topic frame = "trades".

namespace lexis::ipc {

inline constexpr char kEndpoint[] = "tcp://127.0.0.1:5556";
inline constexpr char kTopicTrades[] = "trades";
inline constexpr std::uint16_t kProtocolVersion = 1;
inline constexpr std::uint16_t kKindTrade = 1;

enum class Aggressor : std::uint8_t { Unknown = 0, Buy = 1, Sell = 2 };

// .NET DateTimeOffset.UtcTicks: 100ns since 0001-01-01 UTC
inline std::int64_t UtcTicksNow() {
    using namespace std::chrono;
    // Unix epoch 1970-01-01 as DateTime ticks = 621355968000000000
    constexpr std::int64_t kUnixEpochTicks = 621355968000000000LL;
    const auto now = system_clock::now().time_since_epoch();
    const auto ns = duration_cast<nanoseconds>(now).count();
    return kUnixEpochTicks + ns / 100;
}

inline void WriteU16(std::vector<std::uint8_t>& b, std::uint16_t v) {
    b.push_back(static_cast<std::uint8_t>(v & 0xff));
    b.push_back(static_cast<std::uint8_t>((v >> 8) & 0xff));
}

inline void WriteI64(std::vector<std::uint8_t>& b, std::int64_t v) {
    for (int i = 0; i < 8; ++i)
        b.push_back(static_cast<std::uint8_t>((v >> (8 * i)) & 0xff));
}

inline void WriteF64(std::vector<std::uint8_t>& b, double v) {
    std::uint64_t u = 0;
    static_assert(sizeof(double) == 8, "double must be 8 bytes");
    std::memcpy(&u, &v, 8);
    for (int i = 0; i < 8; ++i)
        b.push_back(static_cast<std::uint8_t>((u >> (8 * i)) & 0xff));
}

inline std::vector<std::uint8_t> EncodeTrade(
    const std::string& symbol,
    double price,
    double size,
    Aggressor aggressor,
    std::int64_t sequence,
    std::int64_t utcTicks) {
    std::vector<std::uint8_t> buf;
    buf.reserve(4 + 2 + 2 + 8 + 8 + 2 + symbol.size() + 8 + 8 + 1);
    buf.push_back('L');
    buf.push_back('X');
    buf.push_back('0');
    buf.push_back('1');
    WriteU16(buf, kProtocolVersion);
    WriteU16(buf, kKindTrade);
    WriteI64(buf, sequence);
    WriteI64(buf, utcTicks);
    WriteU16(buf, static_cast<std::uint16_t>(symbol.size()));
    buf.insert(buf.end(), symbol.begin(), symbol.end());
    WriteF64(buf, price);
    WriteF64(buf, size);
    buf.push_back(static_cast<std::uint8_t>(aggressor));
    return buf;
}

} // namespace lexis::ipc
