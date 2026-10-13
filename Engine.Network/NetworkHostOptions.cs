using System.Net;

namespace Engine.Network;

public sealed record NetworkHostOptions
{
    public IPAddress LocalIPv4Address { get; init; } = IPAddress.Any;

    public IPAddress LocalIPv6Address { get; init; } = IPAddress.IPv6Any;

    public byte ChannelCount { get; init; } = 8;

    public TimeSpan PreparationTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public bool ReuseAddress { get; init; }

    public bool ReceiveBroadcasts { get; init; }

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan DisconnectTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public int MaxPendingReliableBytes { get; init; } = 4 * 1024 * 1024;

    public int MaxDataNotifications { get; init; } = 4096;

    public int MaxNotificationBytes { get; init; } = 8 * 1024 * 1024;

    public int MaxControlNotifications { get; init; } = 1024;
}
