using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

using Game.Admission;
using Game.Managers;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldMetadataPersistenceTest
{
    [Fact]
    public void SettingsAndAdmissionCommitTogetherWithoutClosingTheDurabilityStreamEarly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-world-metadata-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var settings = new WorldSettings { Name = "Original", GameMode = GameMode.Creative };
            var values = new ValuesDictionary();
            settings.Save(values, false);
            var gameInfo = new XElement("Values", new XAttribute("Name", "GameInfo"));
            values.Save(gameInfo);
            var extension = new XElement(
                "Values",
                new XAttribute("Name", "unloaded.fixture"),
                new XElement(
                    "Value",
                    new XAttribute("Name", "Opaque"),
                    new XAttribute("Type", "Unloaded.Mod.Type"),
                    new XAttribute("Value", "preserve")
                )
            );
            var root = new XElement(
                "Project",
                new XElement("Subsystems", gameInfo),
                new XElement("ExtensionData", extension)
            );
            root.Save(Path.Combine(directory, "Project.xml"));
            var policy = ServerAdmissionPolicy.WithPassword("fixture-password");
            settings.Name = "Updated";
            WorldsManager.ChangeWorld("system:" + directory, settings, policy);
            var saved = XElement.Load(Path.Combine(directory, "Project.xml"));
            var savedInfo = new ValuesDictionary();
            savedInfo.ApplyOverrides(saved.Element("Subsystems")!.Element("Values")!);
            Assert.Equal("Updated", savedInfo.GetValue<string>("WorldName"));
            Assert.True(WorldsManager.ReadAdmissionPolicy("system:" + directory).VerifyPassword("fixture-password"));
            Assert.True(
                XNode.DeepEquals(
                    extension,
                    saved.Element("ExtensionData")!.Elements("Values")
                        .Single(item => (string?)item.Attribute("Name") == "unloaded.fixture")
                )
            );
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }
}
