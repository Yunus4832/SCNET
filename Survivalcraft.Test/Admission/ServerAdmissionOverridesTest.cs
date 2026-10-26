using System.Security.Cryptography;
using System.Xml.Linq;

using Game.Admission;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Survivalcraft.Test.Admission;

public sealed class ServerAdmissionOverridesTest
{
    [Fact]
    public void PrivatePolicyEditingPreservesUnrelatedExtensionsAndDoesNotRequireTheirTypes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-policy-xml-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var other = new XElement(
                "Values",
                new XAttribute("Name", "other.mod"),
                new XElement(
                    "Value",
                    new XAttribute("Name", "Opaque"),
                    new XAttribute("Type", "An.Unloaded.Mod.Type"),
                    new XAttribute("Value", "preserve")
                )
            );
            var players = new XElement(
                "Values",
                new XAttribute("Name", "players"),
                new XElement(
                    "Value",
                    new XAttribute("Name", "ExistingPlayer"),
                    new XAttribute("Type", "string"),
                    new XAttribute("Value", "unchanged")
                )
            );
            var root = new XElement(
                "Project",
                new XElement("ExtensionData", new XElement("Values", new XAttribute("Name", "game"), players), other)
            );
            var policy = ServerAdmissionConfiguration.WithPassword("private-setting");
            ServerAdmissionConfiguration.ApplyWorldXml(root, policy);
            Assert.True(XNode.DeepEquals(other, root.Element("ExtensionData")!.Elements("Values").Last()));
            Assert.True(
                XNode.DeepEquals(
                    players,
                    root.Element("ExtensionData")!.Elements("Values").First().Elements("Values").First()
                )
            );
            root.Save(Path.Combine(directory, "Project.xml"));
            var read = ServerAdmissionConfiguration.ReadWorld("system:" + directory);
            Assert.Equal(policy.Fingerprint(), read.Fingerprint());
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void PasswordReferenceRoundTripsWithoutItsContentAndOnlyRuntimeDerivesTheVerifier()
    {
        var path = Path.Combine(Path.GetTempPath(), "scnet-admission-secret-" + Guid.NewGuid().ToString("N"));
        try
        {
            const string password = "private-value-with-spaces ";
            File.WriteAllText(path, password + "\n");
            var overrides = new ServerAdmissionOverrides { Mode = ServerAdmissionMode.Password, PasswordFile = path };
            var xml = overrides.ToXml();
            Assert.DoesNotContain(password, xml.ToString());
            var restored = ServerAdmissionOverrides.FromXml(XElement.Parse(xml.ToString()));
            Assert.Equal(overrides, restored);
            var policy = restored.Apply(ServerAdmissionConfiguration.Open);
            Assert.Equal(ServerAdmissionMode.Password, policy.Mode);
            Assert.True(policy.VerifyPassword(password));
            Assert.False(policy.VerifyPassword(password.Trim()));
            Assert.Equal(ServerAdmissionMode.Open, ServerAdmissionConfiguration.Open.Mode);
            File.Delete(path);
            Assert.Throws<AdmissionConfigurationException>(() => restored.Apply(ServerAdmissionConfiguration.Open));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExternalOverridesUsePublicConfigurationWithoutInstalledServiceOrNetwork()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var overrides = new ServerAdmissionOverrides
        {
            Mode = ServerAdmissionMode.External, AuthenticationUrl = "https://offline-provider.example/",
            AuthenticationKey = AuthenticationPublicKey.From(key).EncodedKey
        };
        var policy = ServerAdmissionOverrides.FromXml(overrides.ToXml()).Apply(ServerAdmissionConfiguration.Open);
        Assert.Equal(ServerAdmissionMode.External, policy.Mode);
        Assert.Equal(AuthenticationPublicKey.From(key).Address, policy.Authority!.Identity.Address);
    }

    [Fact]
    public void ExplicitModeClearsSavedCredentialIntentAndInvalidStoredModesFailClosed()
    {
        var saved = new ServerAdmissionOverrides
            { Mode = ServerAdmissionMode.Password, PasswordFile = "/private/path" };
        var replacement = saved.Merge(new ServerAdmissionOverrides { Mode = ServerAdmissionMode.Open });
        Assert.Null(replacement.PasswordFile);
        Assert.Equal(ServerAdmissionMode.Open, replacement.Apply(ServerAdmissionConfiguration.Open).Mode);
        Assert.Throws<AdmissionConfigurationException>(() => ServerAdmissionOverrides.FromXml(
                new XElement("AdmissionOverrides", new XAttribute("Mode", "invalid"))
            )
        );
        Assert.Throws<AdmissionConfigurationException>(() => new ServerAdmissionOverrides
            {
                Mode = ServerAdmissionMode.External, PasswordFile = "/private/path"
            }.Apply(ServerAdmissionConfiguration.Open)
        );
    }
}
