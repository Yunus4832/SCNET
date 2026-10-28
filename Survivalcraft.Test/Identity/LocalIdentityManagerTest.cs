using System.Security.Cryptography;
using System.Xml.Linq;

using Engine.Network.Security;

using Game.Identity;
using Game.Managers;

namespace Survivalcraft.Test.Identity;

public sealed class LocalIdentityManagerTest
{
    [Fact]
    public void PersistenceSnapshotsCannotMutateTheLiveCollectionAndMetadataHidesKeys()
    {
        LocalIdentitySettings? saved = null;
        var manager = new LocalIdentityManager(new LocalIdentitySettings(), value => saved = value);
        manager.EnsureDefaults();
        var entries = Assert.IsType<StoredLocalIdentity[]>(saved!.Identities);
        var original = entries[0];
        Assert.DoesNotContain(original.PrivateKey, original.ToString());
        entries[0] = original with { Name = "Changed by persistence callback" };
        Assert.NotEqual(entries[0].Name, manager.GetIdentities(original.Purpose)[0].Name);
        var restored = new LocalIdentityManager(saved, _ => { });
        entries[0] = original with { Name = "Changed after construction" };
        Assert.Equal("Changed by persistence callback", restored.GetIdentities(original.Purpose)[0].Name);
    }

    [Fact]
    public void InitializationCreatesIndependentDefaultsAndReloadDoesNotWrite()
    {
        var configuration = Create(out var saved);
        using var player = configuration.OpenPlayerIdentity();
        using var server = configuration.OpenServerIdentity();
        Assert.NotEqual(player.PublicIdentity.Address, server.PublicIdentity.Address);
        var reloaded = new LocalIdentityManager(
            LocalIdentityManager.ReadSettings(saved),
            _ => throw new Exception("Unexpected write")
        );
        reloaded.EnsureDefaults();
        Assert.Equal(
            configuration.GetSelectedAddress(IdentityPurpose.Player),
            reloaded.GetSelectedAddress(IdentityPurpose.Player)
        );
        Assert.Equal(
            configuration.GetSelectedAddress(IdentityPurpose.Server),
            reloaded.GetSelectedAddress(IdentityPurpose.Server)
        );
        foreach (var entry in saved.Descendants("Identity"))
        {
            using var key = ECDsa.Create();
            var bytes = Convert.FromBase64String(entry.Attribute("PrivateKey")!.Value);
            key.ImportPkcs8PrivateKey(bytes, out var consumed);
            Assert.Equal(bytes.Length, consumed);
        }
    }

    [Fact]
    public void InitializeCompletesMissingPurposeAndFailedSaveDoesNotPublishKeys()
    {
        var configuration = Create(out var saved);
        saved.Element("Server")!.Remove();
        var partial = new LocalIdentityManager(LocalIdentityManager.ReadSettings(saved), _ => { });
        partial.EnsureDefaults();
        Assert.Equal(
            configuration.GetSelectedAddress(IdentityPurpose.Player),
            partial.GetSelectedAddress(IdentityPurpose.Player)
        );
        Assert.Single(partial.GetIdentities(IdentityPurpose.Server));
        var failing = new LocalIdentityManager(new LocalIdentitySettings(), _ => throw new IOException("write failed"));
        Assert.Throws<IOException>(failing.EnsureDefaults);
        Assert.Empty(failing.GetIdentities(IdentityPurpose.Player));
    }

    [Theory]
    [InlineData(IdentityPurpose.Player)]
    [InlineData(IdentityPurpose.Server)]
    public void MultipleIdentitiesCanBeSelectedRenamedDeletedAndReloaded(IdentityPurpose purpose)
    {
        XElement? saved = null;
        var configuration = new LocalIdentityManager(
            new LocalIdentitySettings(),
            settings => saved = LocalIdentityManager.WriteSettings(settings)
        );
        configuration.EnsureDefaults();
        var original = configuration.GetSelectedAddress(purpose)!;
        var address = configuration.Create(purpose, "Second identity");
        Assert.Equal(original, configuration.GetSelectedAddress(purpose));
        Assert.Throws<InvalidOperationException>(() => configuration.Delete(purpose, original));
        configuration.Select(purpose, address);
        configuration.Rename(purpose, address, "Renamed");
        configuration.Delete(purpose, original);
        var reloaded = new LocalIdentityManager(
            LocalIdentityManager.ReadSettings(saved),
            _ => throw new Exception("Unexpected write")
        );
        reloaded.EnsureDefaults();
        var entry = Assert.Single(reloaded.GetIdentities(purpose));
        Assert.Equal("Renamed", entry.Name);
        Assert.Equal(address, reloaded.GetSelectedAddress(purpose));
        Assert.Throws<InvalidOperationException>(() => reloaded.Delete(purpose, address));
    }

    [Fact]
    public void ExplicitServerSelectionUsesRequestedKeyAndMissingKeyDoesNotFallback()
    {
        var configuration = Create(out _);
        var address = configuration.Create(IdentityPurpose.Server, "World server");
        using var identity = configuration.OpenServerIdentity(address);
        Assert.Equal(address, identity.PublicIdentity.Address);
        Assert.NotEqual(address, configuration.GetSelectedAddress(IdentityPurpose.Server));
        configuration.Delete(IdentityPurpose.Server, address);
        Assert.Throws<InvalidOperationException>(() => configuration.OpenServerIdentity(address));
    }

    [Fact]
    public void InvalidMetadataDuplicateEntriesAndMissingSelectionAreRejected()
    {
        _ = Create(out var saved);
        var duplicate = new XElement(saved);
        duplicate.Element("Player")!.Add(new XElement(duplicate.Descendants("Identity").First()));
        Assert.Throws<InvalidDataException>(() => new LocalIdentityManager(
                LocalIdentityManager.ReadSettings(duplicate),
                _ => { }
            )
        );
        var missing = new XElement(saved);
        missing.Element("Player")!.SetAttributeValue("Selected", "missing");
        Assert.Throws<InvalidDataException>(() => new LocalIdentityManager(
                LocalIdentityManager.ReadSettings(missing),
                _ => { }
            )
        );
        var corrupt = new XElement(saved);
        corrupt.Descendants("Identity").First().SetAttributeValue("PrivateKey", "invalid!");
        Assert.Throws<FormatException>(() => new LocalIdentityManager(
                LocalIdentityManager.ReadSettings(corrupt),
                _ => { }
            )
        );
        var mismatch = new XElement(saved);
        mismatch.Descendants("Identity").First().SetAttributeValue(
            "PrivateKey",
            saved.Descendants("Identity").Last().Attribute("PrivateKey")!.Value
        );
        Assert.Throws<CryptographicException>(() => new LocalIdentityManager(
                LocalIdentityManager.ReadSettings(mismatch),
                _ => { }
            )
        );
    }

    [Fact]
    public void RestoreAddsIdentityWithoutReplacingDefaultsAndRejectsDuplicates()
    {
        var source = Create(out _);
        var destination = Create(out _);
        var address = source.GetSelectedAddress(IdentityPurpose.Player)!;
        var backup = source.ExportBackup(IdentityPurpose.Player, address, "backup-fixture-password");
        var original = destination.GetSelectedAddress(IdentityPurpose.Player);
        var server = destination.GetSelectedAddress(IdentityPurpose.Server);
        Assert.Throws<CryptographicException>(() => destination.ImportBackup(
                IdentityPurpose.Player,
                backup,
                "wrong-password",
                "Restored"
            )
        );
        Assert.Equal(address, LocalIdentityManager.InspectBackup(backup, "backup-fixture-password"));
        destination.ImportBackup(IdentityPurpose.Player, backup, "backup-fixture-password", "Restored");
        Assert.Equal(original, destination.GetSelectedAddress(IdentityPurpose.Player));
        Assert.Equal(server, destination.GetSelectedAddress(IdentityPurpose.Server));
        Assert.Equal(2, destination.GetIdentities(IdentityPurpose.Player).Count);
        Assert.Throws<InvalidOperationException>(() => destination.ImportBackup(
                IdentityPurpose.Player,
                backup,
                "backup-fixture-password",
                "Duplicate"
            )
        );
        destination.Select(IdentityPurpose.Player, address);
        using var restored = destination.OpenPlayerIdentity();
        Assert.Equal(address, restored.PublicIdentity.Address);
    }

    [Fact]
    public void FailedMutationsLeaveAllIdentitiesAndSelectionUnchanged()
    {
        var source = Create(out var saved);
        var address = source.Create(IdentityPurpose.Player, "Second");
        // The original document snapshot remains unchanged after subsequent mutations.
        var latest = new XElement(saved);
        var failing = new LocalIdentityManager(
            LocalIdentityManager.ReadSettings(latest),
            _ => throw new IOException("write failed")
        );
        var original = failing.GetSelectedAddress(IdentityPurpose.Player)!;
        Assert.Throws<IOException>(() => failing.Create(IdentityPurpose.Player, "Unsaved"));
        Assert.Throws<IOException>(() => failing.Rename(IdentityPurpose.Player, original, "Unsaved"));
        Assert.Equal(original, failing.GetSelectedAddress(IdentityPurpose.Player));
        Assert.Single(failing.GetIdentities(IdentityPurpose.Player));
        var backup = source.ExportBackup(IdentityPurpose.Player, address, "backup-fixture-password");
        Assert.Throws<IOException>(() => failing.ImportBackup(
                IdentityPurpose.Player,
                backup,
                "backup-fixture-password",
                "Unsaved"
            )
        );
        Assert.Single(failing.GetIdentities(IdentityPurpose.Player));
    }

    [Fact]
    public void FailedSelectionAndDeletionDoNotChangePersistedState()
    {
        XElement? saved = null;
        var source = new LocalIdentityManager(
            new LocalIdentitySettings(),
            settings => saved = LocalIdentityManager.WriteSettings(settings)
        );
        source.EnsureDefaults();
        var address = source.Create(IdentityPurpose.Server, "Second");
        var failing = new LocalIdentityManager(
            LocalIdentityManager.ReadSettings(saved),
            _ => throw new IOException("write failed")
        );
        Assert.Throws<IOException>(() => failing.Select(IdentityPurpose.Server, address));
        Assert.Throws<IOException>(() => failing.Delete(IdentityPurpose.Server, address));
        Assert.Equal(
            source.GetSelectedAddress(IdentityPurpose.Server),
            failing.GetSelectedAddress(IdentityPurpose.Server)
        );
        Assert.Equal(source.GetIdentities(IdentityPurpose.Server), failing.GetIdentities(IdentityPurpose.Server));
    }

    [Fact]
    public void NamesAndCollectionLimitsAreEnforcedWithoutRemovingDefaults()
    {
        var configuration = Create(out _);
        Assert.Throws<ArgumentException>(() => configuration.Create(IdentityPurpose.Player, " "));
        Assert.Throws<ArgumentException>(() => configuration.Create(IdentityPurpose.Player, new string('x', 81)));
        Assert.Throws<ArgumentException>(() => configuration.Create(IdentityPurpose.Player, "line\nbreak"));
        for (var i = 1; i < LocalIdentityManager.MaximumIdentityCount; i++)
        {
            configuration.Create(IdentityPurpose.Player, "Identity " + i);
        }

        Assert.Throws<InvalidOperationException>(() => configuration.Create(IdentityPurpose.Player, "Too many"));
        Assert.Equal(
            LocalIdentityManager.MaximumIdentityCount,
            configuration.GetIdentities(IdentityPurpose.Player).Count
        );
        using var selected = configuration.OpenPlayerIdentity();
        Assert.Equal(configuration.GetSelectedAddress(IdentityPurpose.Player), selected.PublicIdentity.Address);
    }

    [Fact]
    public void BackupRequiresPasswordAndRejectsTrailingData()
    {
        using var identity = NetworkIdentity.Create();
        Assert.Throws<ArgumentException>(() => IdentityBackup.Export(identity, "short"));
        var backup = IdentityBackup.Export(identity, "backup-fixture-password");
        Assert.Throws<CryptographicException>(() => IdentityBackup.Import([.. backup, 0], "backup-fixture-password"));
    }

    private static LocalIdentityManager Create(out XElement saved)
    {
        XElement? document = null;
        var configuration = new LocalIdentityManager(
            new LocalIdentitySettings(),
            settings => document = LocalIdentityManager.WriteSettings(settings)
        );
        configuration.EnsureDefaults();
        saved = document!;
        return configuration;
    }
}
