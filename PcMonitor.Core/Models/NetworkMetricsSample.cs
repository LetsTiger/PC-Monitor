using PcMonitor.Core.Metrics;

namespace PcMonitor.Core.Models;

public sealed record NetworkMetricsSample(
    DateTimeOffset Timestamp,

    string? GatewayAddress,
    bool GatewaySuccess,
    long? GatewayLatencyMilliseconds,
    double GatewayPacketLossPercent,
    double? GatewayJitterMilliseconds,
    int GatewayConsecutiveFailures,

    string InternetTarget,
    bool InternetSuccess,
    long? InternetLatencyMilliseconds,
    double InternetPacketLossPercent,
    double? InternetJitterMilliseconds,
    int InternetConsecutiveFailures) : IMetricSample;