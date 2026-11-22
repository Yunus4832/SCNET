namespace Rendezvous.Protocol;

/// <summary>公开的传输配置，省略主机时沿用控制地址的主机。</summary>
public sealed record RendezvousDescriptor(int UdpPort, string? UdpHost, string? RoomDirectoryPath = null);
