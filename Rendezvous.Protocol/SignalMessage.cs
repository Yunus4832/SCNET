namespace Rendezvous.Protocol;

/// <summary>有大小限制的控制数据报，票据不得写入日志。</summary>
public sealed record SignalMessage(
    string Operation,
    Guid RequestId,
    Guid SessionId = default,
    Guid ParticipantId = default,
    string Ticket = "",
    string? Endpoint = null,
    DateTimeOffset? ExpiresAt = null,
    string? Error = null,
    string? Challenge = null,
    string[]? LocalEndpoints = null
);
