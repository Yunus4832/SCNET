using System.Security.Cryptography;
using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Admission;
using Game.Managers;
using Game.Network;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Survivalcraft.Test.Admission;

public sealed class LocalPlayerAdmissionTest
{
    [Theory]
    [InlineData(ServerAdmissionMode.Open)]
    [InlineData(ServerAdmissionMode.Password)]
    [InlineData(ServerAdmissionMode.External)]
    public async Task LocalJoinUsesTheSamePolicyProofAndIsBoundToOneLoadedWorld(ServerAdmissionMode mode)
    {
        using var world = new Project();
        using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var server = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var subject = AuthenticationPublicKey.From(user).Address;
        var audience = AuthenticationPublicKey.From(server).Address;
        var policy = mode switch
        {
            ServerAdmissionMode.Open => ServerAdmissionPolicy.Open,
            ServerAdmissionMode.Password => ServerAdmissionPolicy.WithPassword("fixture-password"),
            _ => ServerAdmissionPolicy.WithAuthority(
                new AuthenticationServiceEndpoint(
                    new AuthenticationServiceDescription(
                        AuthenticationProtocol.Version,
                        "Fixture",
                        "https://auth.example/",
                        AuthenticationPublicKey.From(provider).EncodedKey
                    )
                )
            )
        };
        var now = DateTimeOffset.UtcNow;
        var attempt = new AdmissionAttempt(policy, subject, audience, new byte[32], now);
        var session = new LocalPlayerAdmission(world, attempt);
        Assert.Same(world, session.World);
        Assert.False(session.Approved);
        var approval = mode == ServerAdmissionMode.External
            ? AdmissionApprovalTokens.Issue(provider, attempt.Challenge, now)
            : string.Empty;
        Assert.True(
            session.Submit(
                new AdmissionProofPackage(
                    session.Challenge.Nonce,
                    mode == ServerAdmissionMode.Password ? "fixture-password" : string.Empty,
                    approval
                )
            )
        );
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        AdmissionResultPackage? result;
        while ((result = session.Poll(DateTimeOffset.UtcNow)) is null) { await Task.Delay(1, timeout.Token); }

        Assert.Equal(AdmissionResultCode.Accepted, result.Code);
        Assert.True(session.Approved);
        Assert.Equal(subject, session.Challenge.Subject);
        Assert.Equal(audience, session.Challenge.Server);
        Assert.False(session.Submit(new AdmissionProofPackage(session.Challenge.Nonce)));
    }

    [Fact]
    public async Task CancelledJoinDoesNotApproveAndUnansweredJoinExpires()
    {
        using var world = new Project();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var address = AuthenticationPublicKey.From(key).Address;
        var now = DateTimeOffset.UtcNow;
        var local = new LocalPlayerAdmission(
            world,
            new AdmissionAttempt(ServerAdmissionPolicy.Open, address, address, new byte[32], now)
        );
        Assert.True(local.Submit(new AdmissionProofPackage(local.Challenge.Nonce, Cancel: true)));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        AdmissionResultPackage? result;
        while ((result = local.Poll(DateTimeOffset.UtcNow)) is null) { await Task.Delay(1, timeout.Token); }

        Assert.Equal(AdmissionResultCode.Cancelled, result.Code);
        Assert.False(local.Approved);
        var unanswered = new LocalPlayerAdmission(
            world,
            new AdmissionAttempt(ServerAdmissionPolicy.Open, address, address, new byte[32], now)
        );
        Assert.Equal(
            AdmissionResultCode.Expired,
            unanswered.Poll(now.AddSeconds(AuthenticationProtocol.MaximumChallengeLifetimeSeconds + 1))!.Code
        );
        Assert.False(unanswered.Approved);
    }

    [Fact]
    public void WorldPolicyReadOnlyRequiresMetadataWithoutCreatingAProjectOrRole()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-host-policy-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var settings = ServerAdmissionPolicy.WithPassword("preflight-password");
            var admission = new ValuesDictionary();
            admission.SetValue("admission", settings.Save());
            var values = new ValuesDictionary();
            values.SetValue("game", admission);
            var extension = new XElement("ExtensionData");
            values.Save(extension);
            new XElement("Project", extension).Save(Path.Combine(directory, "Project.xml"));
            var loaded = WorldsManager.ReadAdmissionPolicy("system:" + directory);
            Assert.Equal(settings.Fingerprint(), loaded.Fingerprint());
            Assert.Equal(ServerAdmissionMode.Password, loaded.Mode);
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }
}
