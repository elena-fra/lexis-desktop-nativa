#include <atomic>
#include <chrono>
#include <cmath>
#include <csignal>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <random>
#include <string>
#include <thread>

#include <zmq.hpp>

#include "lexis_ipc_codec.hpp"

namespace {
std::atomic<bool> g_run{true};

void OnSignal(int) { g_run = false; }
} // namespace

int main(int argc, char** argv) {
    using namespace lexis::ipc;

    std::string endpoint = kEndpoint;
    std::string symbol = "SPY";
    int ticksPerSec = 2000;

    if (argc > 1) endpoint = argv[1];
    if (argc > 2) symbol = argv[2];
    if (argc > 3) {
        try {
            ticksPerSec = std::max(1, std::stoi(argv[3]));
        } catch (...) {
            ticksPerSec = 2000;
        }
    }

    std::signal(SIGINT, OnSignal);
    std::signal(SIGTERM, OnSignal);

    std::cout << "LEXIS IPC Producer (C++ / libzmq) — Fase 0\n"
              << "  endpoint = " << endpoint << "\n"
              << "  symbol   = " << symbol << "\n"
              << "  rate     = " << ticksPerSec << " tick/s\n"
              << "Ctrl+C to stop.\n";

    zmq::context_t ctx{1};
    zmq::socket_t pub{ctx, zmq::socket_type::pub};
    pub.bind(endpoint);

    // Slow joiner: give SUB time to connect before first frames.
    std::this_thread::sleep_for(std::chrono::milliseconds(200));

    std::mt19937 rng{7};
    std::uniform_real_distribution<double> noise{-0.5, 0.5};
    std::uniform_int_distribution<int> sizeDist{1, 40};
    std::uniform_real_distribution<double> sideFlip{0.0, 1.0};

    double price = 520.0;
    std::int64_t sequence = 0;
    std::int64_t published = 0;

    const auto start = std::chrono::steady_clock::now();
    auto lastReport = start;

    // Pace: for high rates, batch without sleeping every tick.
    const int burstEvery = std::max(1, ticksPerSec / 200);
    const auto sleepBurst = std::chrono::milliseconds(5);
    const auto sleepLow = std::chrono::milliseconds(std::max(1, 1000 / ticksPerSec));

    while (g_run) {
        price = std::max(1.0, price * (1.0 + noise(rng) * 0.0004));
        const double size = static_cast<double>(sizeDist(rng));
        const auto aggr = sideFlip(rng) < 0.5 ? Aggressor::Buy : Aggressor::Sell;
        ++sequence;

        const auto payload = EncodeTrade(
            symbol,
            std::round(price * 100.0) / 100.0,
            size,
            aggr,
            sequence,
            UtcTicksNow());

        pub.send(zmq::buffer(kTopicTrades, std::strlen(kTopicTrades)), zmq::send_flags::sndmore);
        pub.send(zmq::buffer(payload), zmq::send_flags::none);
        ++published;

        const auto now = std::chrono::steady_clock::now();
        if (now - lastReport > std::chrono::seconds(1)) {
            const double sec = std::max(
                0.001,
                std::chrono::duration<double>(now - start).count());
            std::cout << "seq=" << sequence << "  last=" << price
                      << "  ~" << static_cast<long long>(published / sec) << " tick/s avg\n";
            lastReport = now;
        }

        if (ticksPerSec <= 1000)
            std::this_thread::sleep_for(sleepLow);
        else if (published % burstEvery == 0)
            std::this_thread::sleep_for(sleepBurst);
    }

    std::cout << "Stopped. Published " << published << " trades.\n";
    return 0;
}
