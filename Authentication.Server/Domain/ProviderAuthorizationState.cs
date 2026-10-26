using System.Text.Json.Serialization;

namespace SCNET.Authentication.Server.Domain;

/// <summary>Durable provider grants are hashed; raw grant secrets are returned only to their proven subject.</summary>
public sealed class ProviderAuthorizationState
{
    [JsonRequired] public Dictionary<string, ProviderGrant> Grants { get; init; } = new(StringComparer.Ordinal);

    [JsonRequired] public HashSet<string> BannedAccounts { get; init; } = new(StringComparer.Ordinal);
}
