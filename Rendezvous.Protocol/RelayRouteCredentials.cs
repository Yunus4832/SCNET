namespace Rendezvous.Protocol;

/// <summary>Private control-channel credentials; never log or send the key in a data-plane header.</summary>
public sealed record RelayRouteCredentials(Guid RouteId, byte Side, byte[] Key);
