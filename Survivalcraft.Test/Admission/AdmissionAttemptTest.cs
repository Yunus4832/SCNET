using System.Security.Cryptography;
using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

using Game.Admission;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Survivalcraft.Test.Admission;

public sealed class AdmissionAttemptTest
{
    private static readonly DateTimeOffset _now = DateTimeOffset.FromUnixTimeSeconds(1792840000);

    [Fact]
    public async Task OpenAdmissionStillConsumesItsBoundConnectionAttemptExactlyOnce()
    {
        var attempt = Attempt(ServerAdmissionConfiguration.Open);
        var proof = new AdmissionProofPackage(attempt.Challenge.Nonce);
        var replies = await Task.WhenAll(attempt.AuthorizeAsync(proof, _now), attempt.AuthorizeAsync(proof, _now));
        Assert.Single(replies.Where(reply => reply.Code == AdmissionResultCode.Accepted));
        Assert.Single(replies.Where(reply => reply.Code == AdmissionResultCode.Denied));
    }

    [Fact]
    public async Task PasswordIsPrivateAndWrongPasswordIsRateLimitedBeforeRetry()
    {
        const string password = "a-test-join-password";
        var configuration = ServerAdmissionConfiguration.WithPassword(password);
        var data = configuration.Save();
        var xml = new XElement("Admission");
        data.Save(xml);
        Assert.DoesNotContain(password, xml.ToString());
        var reloaded = new ValuesDictionary();
        reloaded.ApplyOverrides(XElement.Parse(xml.ToString()));
        var attempt = Attempt(ServerAdmissionConfiguration.Load(reloaded));
        Assert.Equal(ServerAdmissionMode.Password, attempt.Describe().Mode);
        Assert.Empty(attempt.Describe().AuthorityPublicKey);
        var proof = new AdmissionProofPackage(attempt.Challenge.Nonce, "wrong");
        Assert.Equal(AdmissionResultCode.IncorrectPassword, (await attempt.AuthorizeAsync(proof, _now)).Code);
        var correct = proof with { Password = password };
        Assert.Equal(AdmissionResultCode.RateLimited, (await attempt.AuthorizeAsync(correct, _now.AddSeconds(1))).Code);
        Assert.Equal(AdmissionResultCode.Accepted, (await attempt.AuthorizeAsync(correct, _now.AddSeconds(2))).Code);
        Assert.Equal(AdmissionResultCode.Denied, (await attempt.AuthorizeAsync(correct, _now.AddSeconds(3))).Code);
    }

    [Fact]
    public async Task ExternalApprovalCannotBeReusedOnAnotherConnectionOrMixedWithPassword()
    {
        using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var endpoint = new AuthenticationServiceEndpoint(
            new AuthenticationServiceDescription(
                AuthenticationProtocol.Version,
                "Test authority",
                "https://auth.example/",
                AuthenticationPublicKey.From(provider).EncodedKey
            )
        );
        var configuration =
            ServerAdmissionConfiguration.Load(ServerAdmissionConfiguration.WithAuthority(endpoint).Save());
        var attempt = Attempt(configuration);
        var approval = AdmissionApprovalTokens.Issue(provider, attempt.Challenge, _now);
        var accepted = await attempt.AuthorizeAsync(
            new AdmissionProofPackage(attempt.Challenge.Nonce, Approval: approval),
            _now
        );
        Assert.Equal(AdmissionResultCode.Accepted, accepted.Code);
        var other = Attempt(configuration);
        var replay = await other.AuthorizeAsync(
            new AdmissionProofPackage(other.Challenge.Nonce, Approval: approval),
            _now
        );
        Assert.Equal(AdmissionResultCode.ApprovalRejected, replay.Code);
        var mixed = Attempt(configuration);
        var valid = AdmissionApprovalTokens.Issue(provider, mixed.Challenge, _now);
        Assert.Equal(
            AdmissionResultCode.ApprovalRejected,
            (await mixed.AuthorizeAsync(
                new AdmissionProofPackage(mixed.Challenge.Nonce, "not-a-mode-combination", valid),
                _now
            )).Code
        );
    }

    [Fact]
    public async Task CancelledAndExpiredAttemptsNeverGrantAdmission()
    {
        var cancelled = Attempt(ServerAdmissionConfiguration.Open);
        var proof = new AdmissionProofPackage(cancelled.Challenge.Nonce, Cancel: true);
        Assert.Equal(AdmissionResultCode.Cancelled, (await cancelled.AuthorizeAsync(proof, _now)).Code);
        Assert.Equal(
            AdmissionResultCode.Denied,
            (await cancelled.AuthorizeAsync(proof with { Cancel = false }, _now)).Code
        );
        var expired = Attempt(ServerAdmissionConfiguration.Open);
        Assert.Equal(
            AdmissionResultCode.Expired,
            (await expired.AuthorizeAsync(new AdmissionProofPackage(expired.Challenge.Nonce), _now.AddMinutes(10))).Code
        );
    }

    [Fact]
    public void CorruptedAdmissionConfigurationNeverDowngradesToOpen()
    {
        var data = new ValuesDictionary();
        data.SetValue("Mode", 255);
        Assert.Throws<InvalidDataException>(() => ServerAdmissionConfiguration.Load(data));
        data.SetValue("Mode", (int)ServerAdmissionMode.Password);
        Assert.Throws<InvalidDataException>(() => ServerAdmissionConfiguration.Load(data));
        data.SetValue("Mode", (int)ServerAdmissionMode.Open);
        data.SetValue("PasswordSalt", "unexpected");
        Assert.Throws<InvalidDataException>(() => ServerAdmissionConfiguration.Load(data));
    }

    private static AdmissionAttempt Attempt(ServerAdmissionConfiguration configuration)
    {
        using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var server = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new AdmissionAttempt(
            configuration,
            AuthenticationPublicKey.From(user).Address,
            AuthenticationPublicKey.From(server).Address,
            RandomNumberGenerator.GetBytes(32),
            _now
        );
    }
}
