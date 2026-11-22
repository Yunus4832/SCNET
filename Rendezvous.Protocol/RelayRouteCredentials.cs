namespace Rendezvous.Protocol;

/// <summary>控制通道私有凭据，密钥不得写入日志或数据通道包头。</summary>
public sealed record RelayRouteCredentials(Guid RouteId, byte Side, byte[] Key);
