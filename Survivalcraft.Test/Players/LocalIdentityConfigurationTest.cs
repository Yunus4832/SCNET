using System.Security.Cryptography;
using System.Xml.Linq;

using Engine.Network.Security;

using Game.Identity;

namespace Game.Test.Players;

public sealed class LocalIdentityConfigurationTest
{
    [Fact]
    public void InitializationCreatesIndependentKeysAndReloadDoesNotWrite()
    {
        XElement? saved = null;
        var configuration = new LocalIdentityConfiguration(null, xml => saved = xml);
        Assert.Throws<InvalidOperationException>(() => configuration.OpenPlayerIdentity());
        Assert.Throws<InvalidOperationException>(() => configuration.OpenServerIdentity());
        configuration.Initialize();
        using var user = configuration.OpenPlayerIdentity();
        using var server = configuration.OpenServerIdentity();
        Assert.NotEqual(user.PublicIdentity.Address, server.PublicIdentity.Address);
        Assert.NotNull(saved);
        Assert.All(
            saved.Elements(),
            entry =>
            {
                using var key = ECDsa.Create();
                var bytes = Convert.FromBase64String(entry.Attribute("PrivateKey")!.Value);
                key.ImportPkcs8PrivateKey(bytes, out var consumed);
                Assert.Equal(bytes.Length, consumed);
            }
        );
        var reloaded = new LocalIdentityConfiguration(saved, _ => throw new Exception("Unexpected write"));
        reloaded.Initialize();
        using var sameUser = reloaded.OpenPlayerIdentity();
        using var sameServer = reloaded.OpenServerIdentity();
        Assert.Equal(user.PublicIdentity.Address, sameUser.PublicIdentity.Address);
        Assert.Equal(server.PublicIdentity.Address, sameServer.PublicIdentity.Address);
    }

    [Fact]
    public void InitializationCompletesPartialConfigurationWithoutReplacingExistingKey()
    {
        var configuration = Create(out var saved);
        var address = configuration.GetAddress(IdentityPurpose.Player);
        saved.Element("Server")!.Remove();
        var partial = new LocalIdentityConfiguration(saved, _ => { });
        partial.Initialize();
        Assert.Equal(address, partial.GetAddress(IdentityPurpose.Player));
        using var server = partial.OpenServerIdentity();
        Assert.NotEqual(address, server.PublicIdentity.Address);
    }

    [Fact]
    public void FailedInitializationDoesNotPublishUnsavedKeys()
    {
        var configuration = new LocalIdentityConfiguration(null, _ => throw new IOException("write failed"));
        Assert.Throws<IOException>(configuration.Initialize);
        Assert.Null(configuration.GetAddress(IdentityPurpose.Player));
        Assert.Null(configuration.GetAddress(IdentityPurpose.Server));
    }

    [Fact]
    public void MissingMismatchedOrCorruptKeysAreRejectedWithoutWrites()
    {
        _ = Create(out var saved);
        var missing = new XElement(saved);
        missing.Element("Player")!.Attribute("PrivateKey")!.Remove();
        Assert.Throws<InvalidDataException>(() => LoadWithoutWrites(missing));
        var mismatch = new XElement(saved);
        mismatch.Element("Player")!.SetAttributeValue(
            "PrivateKey",
            saved.Element("Server")!.Attribute("PrivateKey")!.Value
        );
        Assert.Throws<CryptographicException>(() => LoadWithoutWrites(mismatch));
        var corrupt = new XElement(saved);
        corrupt.Element("Player")!.SetAttributeValue("PrivateKey", "invalid!");
        Assert.Throws<FormatException>(() => LoadWithoutWrites(corrupt));
    }

    [Fact]
    public void DuplicateEntriesAndOldIndirectReferencesAreRejected()
    {
        _ = Create(out var saved);
        saved.Add(new XElement(saved.Element("Player")!));
        Assert.Throws<InvalidDataException>(() => LoadWithoutWrites(saved));
        saved.Elements("Player").Last().Remove();
        saved.Element("Player")!.Attribute("PrivateKey")!.Remove();
        saved.Element("Player")!.SetAttributeValue("SecretReference", Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidDataException>(() => LoadWithoutWrites(saved));
    }

    [Fact]
    public void EncryptedBackupRestoresAcrossInstancesAndPreservesOtherPurpose()
    {
        var source = Create(out _);
        var destination = Create(out _);
        var backup = source.ExportBackup(IdentityPurpose.Player, "backup-fixture-password");
        var originalUser = destination.GetAddress(IdentityPurpose.Player);
        var originalServer = destination.GetAddress(IdentityPurpose.Server);
        Assert.Throws<CryptographicException>(() => destination.ImportBackup(
                IdentityPurpose.Player,
                backup,
                "wrong-password",
                originalUser
            )
        );
        Assert.Equal(originalUser, destination.GetAddress(IdentityPurpose.Player));
        Assert.Equal(
            source.GetAddress(IdentityPurpose.Player),
            destination.InspectBackup(IdentityPurpose.Player, backup, "backup-fixture-password")
        );
        Assert.Equal(originalUser, destination.GetAddress(IdentityPurpose.Player));
        Assert.Throws<InvalidOperationException>(() => destination.ImportBackup(
                IdentityPurpose.Player,
                backup,
                "backup-fixture-password",
                null
            )
        );
        destination.ImportBackup(IdentityPurpose.Player, backup, "backup-fixture-password", originalUser);
        using var restored = destination.OpenPlayerIdentity();
        Assert.Equal(source.GetAddress(IdentityPurpose.Player), restored.PublicIdentity.Address);
        Assert.Equal(originalServer, destination.GetAddress(IdentityPurpose.Server));
    }

    [Fact]
    public void FailedImportKeepsPreviousIdentity()
    {
        var source = Create(out var saved);
        var destination = LoadWithoutWrites(saved);
        var backup = source.ExportBackup(IdentityPurpose.Player, "backup-fixture-password");
        Assert.Throws<IOException>(() => destination.ImportBackup(
                IdentityPurpose.Server,
                backup,
                "backup-fixture-password",
                destination.GetAddress(IdentityPurpose.Server)
            )
        );
        Assert.Equal(source.GetAddress(IdentityPurpose.Server), destination.GetAddress(IdentityPurpose.Server));
    }

    [Fact]
    public void BackupRequiresPasswordAndRejectsTrailingData()
    {
        using var identity = NetworkIdentity.Create();
        Assert.Throws<ArgumentException>(() => IdentityBackup.Export(identity, "short"));
        var backup = IdentityBackup.Export(identity, "backup-fixture-password");
        Assert.Throws<CryptographicException>(() => IdentityBackup.Import([.. backup, 0], "backup-fixture-password"));
    }

    private static LocalIdentityConfiguration Create(out XElement saved)
    {
        XElement? document = null;
        var configuration = new LocalIdentityConfiguration(null, xml => document = xml);
        configuration.Initialize();
        saved = document!;
        return configuration;
    }

    private static LocalIdentityConfiguration LoadWithoutWrites(XElement saved)
    {
        return new LocalIdentityConfiguration(saved, _ => throw new IOException("Unexpected write"));
    }
}
