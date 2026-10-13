namespace Rendezvous.Protocol;

public sealed record RelayDatagram(Guid RouteId, byte Side, ulong Sequence, byte[] Payload);
