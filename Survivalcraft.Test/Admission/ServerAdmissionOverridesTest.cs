using System.Security.Cryptography;
using System.Xml.Linq;

using Game.Admission;
using Game.Managers;

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
            var policy = ServerAdmissionPolicy.WithPassword("private-setting");
            WorldsManager.WriteAdmissionPolicy(root, policy);
            Assert.True(XNode.DeepEquals(other, root.Element("ExtensionData")!.Elements("Values").Last()));
            Assert.True(
                XNode.DeepEquals(
                    players,
                    root.Element("ExtensionData")!.Elements("Values").First().Elements("Values").First()
                )
            );
            root.Save(Path.Combine(directory, "Project.xml"));
            var read = WorldsManager.ReadAdmissionPolicy("system:" + directory);
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
            var xml = ServerAdmissionManager.SerializeOverrides(overrides);
            Assert.DoesNotContain(password, xml.ToString());
            var restored = ServerAdmissionManager.DeserializeOverrides(XElement.Parse(xml.ToString()));
            Assert.Equal(overrides, restored);
            var policy = ServerAdmissionManager.Resolve(restored, ServerAdmissionPolicy.Open);
            Assert.Equal(ServerAdmissionMode.Password, policy.Mode);
            Assert.True(policy.VerifyPassword(password));
            Assert.False(policy.VerifyPassword(password.Trim()));
            Assert.Equal(ServerAdmissionMode.Open, ServerAdmissionPolicy.Open.Mode);
            File.Delete(path);
            Assert.Throws<AdmissionConfigurationException>(() =>
                ServerAdmissionManager.Resolve(restored, ServerAdmissionPolicy.Open)
            );
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
        var policy = ServerAdmissionManager.Resolve(
            ServerAdmissionManager.DeserializeOverrides(ServerAdmissionManager.SerializeOverrides(overrides)),
            ServerAdmissionPolicy.Open
        );
        Assert.Equal(ServerAdmissionMode.External, policy.Mode);
        Assert.Equal(AuthenticationPublicKey.From(key).Address, policy.Authority!.Identity.Address);
    }

    [Fact]
    public void ExplicitModeClearsSavedCredentialIntentAndInvalidStoredModesFailClosed()
    {
        var saved = new ServerAdmissionOverrides
            { Mode = ServerAdmissionMode.Password, PasswordFile = "/private/path" };
        var replacement = ServerAdmissionManager.Merge(
            saved,
            new ServerAdmissionOverrides { Mode = ServerAdmissionMode.Open }
        );
        Assert.Null(replacement.PasswordFile);
        Assert.Equal(
            ServerAdmissionMode.Open,
            ServerAdmissionManager.Resolve(replacement, ServerAdmissionPolicy.Open).Mode
        );
        Assert.Throws<AdmissionConfigurationException>(() => ServerAdmissionManager.DeserializeOverrides(
                new XElement("AdmissionOverrides", new XAttribute("Mode", "invalid"))
            )
        );
        Assert.Throws<AdmissionConfigurationException>(() => ServerAdmissionManager.Resolve(
                new ServerAdmissionOverrides
                {
                    Mode = ServerAdmissionMode.External, PasswordFile = "/private/path"
                },
                ServerAdmissionPolicy.Open
            )
        );
    }
}
