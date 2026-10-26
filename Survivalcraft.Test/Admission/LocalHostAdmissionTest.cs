using System.Security.Cryptography;
using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

using Game.Admission;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Survivalcraft.Test.Admission;

public sealed class LocalHostAdmissionTest
{
    [Theory]
    [InlineData(ServerAdmissionMode.Open)]
    [InlineData(ServerAdmissionMode.Password)]
    [InlineData(ServerAdmissionMode.External)]
    public async Task GuiHostMustPassTheSamePolicyAndAuthorizationCannotMoveBetweenWorldsOrIdentities(
        ServerAdmissionMode mode
    )
    {
        using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var server = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var subject = AuthenticationPublicKey.From(user).Address;
        var audience = AuthenticationPublicKey.From(server).Address;
        var configuration = mode switch
        {
            ServerAdmissionMode.Open => ServerAdmissionConfiguration.Open,
            ServerAdmissionMode.Password => ServerAdmissionConfiguration.WithPassword("gui-host-password"),
            _ => ServerAdmissionConfiguration.WithAuthority(
                new AuthenticationServiceEndpoint(
                    new AuthenticationServiceDescription(
                        AuthenticationProtocol.Version,
                        "Provider",
                        "https://auth.example/",
                        AuthenticationPublicKey.From(provider).EncodedKey
                    )
                )
            )
        };
        var session = new LocalHostAdmissionSession(
            "world-one",
            configuration,
            subject,
            audience,
            DateTimeOffset.UtcNow
        );
        Assert.False(session.Authorizes("world-one", configuration, subject, audience, DateTimeOffset.UtcNow));
        var approval = mode == ServerAdmissionMode.External
            ? AdmissionApprovalTokens.Issue(provider, session.Challenge, DateTimeOffset.UtcNow)
            : string.Empty;
        Assert.True(
            session.Submit(
                new AdmissionProofPackage(
                    session.Challenge.Nonce,
                    mode == ServerAdmissionMode.Password ? "gui-host-password" : string.Empty,
                    approval
                )
            )
        );
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        AdmissionResultPackage? result;
        while ((result = session.Poll(DateTimeOffset.UtcNow)) is null) { await Task.Delay(1, deadline.Token); }

        Assert.Equal(AdmissionResultCode.Accepted, result.Code);
        Assert.True(
            session.Authorizes(
                "world-one",
                ServerAdmissionConfiguration.Load(configuration.Save()),
                subject,
                audience,
                DateTimeOffset.UtcNow
            )
        );
        Assert.False(session.Authorizes("world-two", configuration, subject, audience, DateTimeOffset.UtcNow));
        Assert.False(session.Authorizes("world-one", configuration, audience, audience, DateTimeOffset.UtcNow));
        Assert.False(session.Authorizes("world-one", configuration, subject, subject, DateTimeOffset.UtcNow));
        Assert.False(
            session.Authorizes(
                "world-one",
                ServerAdmissionConfiguration.WithPassword("changed"),
                subject,
                audience,
                DateTimeOffset.UtcNow
            )
        );
        Assert.False(
            session.Authorizes("world-one", configuration, subject, audience, DateTimeOffset.UtcNow.AddMinutes(11))
        );
        Assert.False(session.Submit(new AdmissionProofPackage(session.Challenge.Nonce)));
    }

    [Fact]
    public async Task CancelledLocalHostNeverReceivesAStartPermit()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var address = AuthenticationPublicKey.From(key).Address;
        var session = new LocalHostAdmissionSession(
            "world",
            ServerAdmissionConfiguration.Open,
            address,
            address,
            DateTimeOffset.UtcNow
        );
        Assert.True(session.Submit(new AdmissionProofPackage(session.Challenge.Nonce, Cancel: true)));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        AdmissionResultPackage? result;
        while ((result = session.Poll(DateTimeOffset.UtcNow)) is null) { await Task.Delay(1, deadline.Token); }

        Assert.Equal(AdmissionResultCode.Cancelled, result.Code);
        Assert.False(session.Approved);
        Assert.False(
            session.Authorizes("world", ServerAdmissionConfiguration.Open, address, address, DateTimeOffset.UtcNow)
        );
    }

    [Fact]
    public void PreflightReadsOnlyPrivateWorldMetadataWithoutCreatingAProjectOrRole()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-host-policy-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var settings = ServerAdmissionConfiguration.WithPassword("preflight-password");
            var admission = new ValuesDictionary();
            admission.SetValue("admission", settings.Save());
            var values = new ValuesDictionary();
            values.SetValue("game", admission);
            var extension = new XElement("ExtensionData");
            values.Save(extension);
            new XElement("Project", extension).Save(Path.Combine(directory, "Project.xml"));
            var loaded = ServerAdmissionConfiguration.ReadWorld("system:" + directory);
            Assert.Equal(settings.Fingerprint(), loaded.Fingerprint());
            Assert.Equal(ServerAdmissionMode.Password, loaded.Mode);
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }
}
