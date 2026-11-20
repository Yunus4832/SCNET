using System.Text.Json;

using Engine.Core.Security;

using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Security;

namespace Authentication.Protocol.Test;

public sealed class AuthenticationInteropTest
{
    [Fact]
    public async Task OpenSslApprovalAndIndependentThumbprintsMatchTheProtocol()
    {
        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "openssl-approval.json"))
        );
        var vector = document.RootElement;
        var keys = vector.GetProperty("keys");
        foreach (var entry in keys.EnumerateObject())
        {
            var key = AuthenticationPublicKey.Parse(entry.Value.GetProperty("publicKey").GetString()!);
            Assert.Equal(entry.Value.GetProperty("identity").GetString(), key.Address);
            Assert.Equal(
                entry.Value.GetProperty("thumbprintHex").GetString(),
                Convert.ToHexStringLower(IdentityAddress.Decode(key.Address))
            );
        }

        var authority =
            AuthenticationPublicKey.Parse(keys.GetProperty("authority").GetProperty("publicKey").GetString()!);
        var challenge = new AdmissionChallenge(
            keys.GetProperty("subject").GetProperty("identity").GetString()!,
            keys.GetProperty("server").GetProperty("identity").GetString()!,
            vector.GetProperty("nonce").GetString()!,
            vector.GetProperty("channelBinding").GetString()!,
            vector.GetProperty("expiresAt").GetInt64()
        );
        var approval = await AdmissionApprovalTokens.ValidateAsync(
            vector.GetProperty("approval").GetString()!,
            authority,
            challenge,
            DateTimeOffset.FromUnixTimeSeconds(vector.GetProperty("now").GetInt64())
        );
        Assert.Equal(challenge.Subject, approval.Subject);
        Assert.Equal(challenge.Server, approval.Server);
        Assert.Equal(challenge.ExpiresAt, approval.ExpiresAt);
    }
}
