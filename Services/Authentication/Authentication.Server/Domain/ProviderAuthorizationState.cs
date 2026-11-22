using System.Text.Json.Serialization;

namespace SCNET.Authentication.Server.Domain;

/// <summary>持久化时只保存提供方授权凭据的哈希，原始授权密钥只返回给已验证的主体。</summary>
public sealed class ProviderAuthorizationState
{
    [JsonRequired] public Dictionary<string, ProviderGrant> Grants { get; init; } = new(StringComparer.Ordinal);

    [JsonRequired] public HashSet<string> BannedAccounts { get; init; } = new(StringComparer.Ordinal);
}
