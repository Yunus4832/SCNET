namespace Rendezvous.Protocol;

/// <summary>用于注册单个参与者传输端点的短期凭证。</summary>
public sealed record RegistrationTicket(string Token, DateTimeOffset ExpiresAt);
