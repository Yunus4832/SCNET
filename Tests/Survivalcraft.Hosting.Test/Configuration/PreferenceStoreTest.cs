using System.Xml.Linq;

using Engine.Core.FileStorage;

using Game.Configuration;

namespace Survivalcraft.Hosting.Test.Configuration;

public sealed class PreferenceStoreTest
{
    [Fact]
    public void ServerSavePreservesPreferencesOwnedByAnotherApplication()
    {
        var owner = new Preferences();
        var configs = new Dictionary<string, string> { ["explicit"] = "runtime" };
        var store = new PreferenceStore("preferences-test:/Settings.xml", [owner], configs);
        store.Read(
            XElement.Parse(
                """
                <Settings>
                  <Setting Name="Port" Value="12345" />
                  <Setting Name="Resolution" Value="1920x1080" />
                  <Configs explicit="disk" installed="yes" />
                </Settings>
                """
            )
        );
        Assert.Equal(12345, owner.Port);
        owner.Port = 54321;
        var written = store.Write();
        Assert.Equal(
            "54321",
            (string?)written.Elements("Setting").Single(item => (string?)item.Attribute("Name") == "Port")
                .Attribute("Value")
        );
        Assert.Equal(
            "1920x1080",
            (string?)written.Elements("Setting").Single(item => (string?)item.Attribute("Name") == "Resolution")
                .Attribute("Value")
        );
        Assert.Equal("runtime", (string?)written.Element("Configs")!.Attribute("explicit"));
        Assert.Equal("yes", (string?)written.Element("Configs")!.Attribute("installed"));
    }

    [Fact]
    public void AtomicSaveLoadsTheSameActualOwnerAndLeavesNoTemporaryFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-preferences-" + Guid.NewGuid().ToString("N"));
        var scheme = "preferences-" + Guid.NewGuid().ToString("N");
        Storage.RegisterFileSystemRoot(scheme, directory);
        try
        {
            var first = new Preferences { Port = 12345 };
            new PreferenceStore(scheme + ":Settings.xml", [first]).Save();
            var second = new Preferences();
            new PreferenceStore(scheme + ":Settings.xml", [second]).Load();
            Assert.Equal(12345, second.Port);
            Assert.Single(Directory.EnumerateFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void DuplicatePreferenceOwnershipIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new PreferenceStore("preferences-test:/Settings.xml", [new Preferences(), new Preferences()])
        );
    }

    private sealed class Preferences
    {
        public int Port { get; set; } = 28889;
    }
}
