namespace Rendezvous.Protocol;

/// <summary>Public transport configuration; an omitted host uses the configured control address host.</summary>
public sealed record RendezvousDescriptor(int UdpPort, string? UdpHost, string? RoomDirectoryPath = null);
