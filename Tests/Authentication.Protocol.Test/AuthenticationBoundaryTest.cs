using System.Security.Cryptography;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Authentication.Protocol.Test;

public sealed class AuthenticationBoundaryTest
{
    [Fact]
    public void InternationalLocationPublishesOneAsciiSignatureTarget()
    {
        using var authority = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var endpoint = new AuthenticationServiceEndpoint(
            new AuthenticationServiceDescription(
                AuthenticationProtocol.Version,
                "Authority",
                "https://bücher.example/中/",
                AuthenticationPublicKey.From(authority).EncodedKey
            )
        );
        Assert.Equal("https://xn--bcher-kva.example/%E4%B8%AD/", endpoint.Description.ServiceUrl);
        Assert.Equal(
            "https://xn--bcher-kva.example/%E4%B8%AD/v1/transactions",
            endpoint.GetUri(AuthenticationProtocol.TransactionsPath).AbsoluteUri
        );
        Assert.All(endpoint.Description.ServiceUrl, character => Assert.True(char.IsAscii(character)));
    }

    [Fact]
    public void CanonicalBaseAddressMustFitTheSameBoundAsItsInput()
    {
        var oversized = "https://auth.example/" + new string('中', 300) + "/";
        Assert.True(oversized.Length < 2048);
        Assert.True(new Uri(oversized).AbsoluteUri.Length > 2048);
        Assert.Throws<AuthenticationProtocolException>(() => AuthenticationServiceEndpoint.ParseBaseUri(oversized));
        var accepted = AuthenticationServiceEndpoint.ParseBaseUri("https://auth.example/" + new string('中', 100) + "/");
        Assert.Equal(accepted, AuthenticationServiceEndpoint.ParseBaseUri(accepted.AbsoluteUri));
    }

    [Fact]
    public void OutgoingProofCannotExceedTheReceiversTokenBound()
    {
        using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var authority = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Throws<AuthenticationProtocolException>(() => AuthenticationRequestProofs.Create(
                user,
                AuthenticationPublicKey.From(authority).Address,
                new Uri("https://auth.example/v1/transactions"),
                new AuthenticationTransactionRequest(AuthenticationPurpose.Login, Grant: new string('中', 4096)),
                DateTimeOffset.UtcNow
            )
        );
    }

    [Fact]
    public async Task OrdinaryMaximumAsciiGrantCanStillBeSignedAndVerified()
    {
        using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var authority = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var address = AuthenticationPublicKey.From(authority).Address;
        var endpoint = new Uri("https://auth.example/v1/transactions");
        var data = new AuthenticationTransactionRequest(AuthenticationPurpose.Login, Grant: new string('a', 4096));
        var now = DateTimeOffset.UtcNow;
        var proof = AuthenticationRequestProofs.Create(user, address, endpoint, data, now);
        Assert.True(proof.Proof.Length <= AuthenticationProtocol.MaximumTokenLength);
        var verified =
            await AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionRequest>(
                proof,
                address,
                endpoint,
                now
            );
        Assert.True(verified.Data.Grant == data.Grant);
    }
}
