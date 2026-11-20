using System.Xml.Linq;

using Game.Configuration;
using Game.Managers;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class PreferenceOwnershipTest
{
    [Fact]
    public void FlatPreferenceKeysHaveExactlyOneRealOwner()
    {
        var document = SettingsManager.WriteSettings();
        var names = document.Elements("Setting").Select(element => (string)element.Attribute("Name")!).ToArray();
        Assert.Equal(59, names.Length);
        Assert.Contains(nameof(HostPreferences.ConsoleLanguageType), names);
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("Survivalcraft.Client", typeof(ClientPreferences).Assembly.GetName().Name);
        Assert.Equal("Survivalcraft.Hosting", typeof(HostPreferences).Assembly.GetName().Name);
        Assert.DoesNotContain(
            typeof(HostPreferences).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "Survivalcraft" or "Survivalcraft.Client"
        );
    }

    [Fact]
    public void ExistingFlatDocumentRoutesToActualOwnersAndRetainsConfigOverridePriority()
    {
        var saved = SettingsManager.WriteSettings();
        var config = AppConfigManager.Values.ToArray();
        try
        {
            AppConfigManager.Set("Language", "startup");
            SettingsManager.ReadSettings(
                new XElement(
                    "Settings",
                    new XElement("Setting", new XAttribute("Name", "SoundsVolume"), new XAttribute("Value", "2")),
                    new XElement("Setting", new XAttribute("Name", "ServerPort"), new XAttribute("Value", "30001")),
                    new XElement(
                        "Setting",
                        new XAttribute("Name", "IsolatedStorageMigrationCounter"),
                        new XAttribute("Value", "7")
                    ),
                    new XElement("Configs", new XAttribute("Language", "disk"))
                )
            );
            Assert.Equal(1f, ClientConfiguration.Preferences.SoundsVolume);
            Assert.Equal(30001, HostingConfiguration.Preferences.ServerPort);
            Assert.Equal(7, SettingsManager.Application.IsolatedStorageMigrationCounter);
            Assert.Equal("startup", AppConfigManager.Values["Language"]);
            var roundTrip = SettingsManager.WriteSettings();
            Assert.Equal(
                "30001",
                (string)roundTrip.Elements("Setting")
                    .Single(element => (string?)element.Attribute("Name") == "ServerPort").Attribute("Value")!
            );
        }
        finally
        {
            SettingsManager.ReadSettings(saved);
            AppConfigManager.Values.Clear();
            foreach (var entry in config) { AppConfigManager.Values.Add(entry.Key, entry.Value); }
        }
    }

    [Fact]
    public void HostCredentialsAreGeneratedAndRetainedByTheHostPreferenceInstance()
    {
        var preferences = new HostPreferences();
        Assert.True(preferences.EnsureHttpCommandAccessToken());
        var token = preferences.HttpCommandAccessToken;
        Assert.True(token.Length >= 32);
        Assert.False(preferences.EnsureHttpCommandAccessToken());
        Assert.Equal(token, preferences.HttpCommandAccessToken);
    }
}
