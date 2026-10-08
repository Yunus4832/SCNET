using System.Xml.Linq;

using Engine.Core;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

using TerritoryStoneMod;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public class TerritorySubsystemTest
{
    [Fact]
    public void ExternalSubsystemLoadsSavesAndDisposesWorldOwnedData()
    {
        var previousWorkType = CommonLib.WorkType;
        CommonLib.WorkType = WorkType.Local;
        try
        {
            var (database, template) = CreateDatabase();
            var owner = Guid.NewGuid();
            ProjectData saved;
            TerritorySubsystem subsystem;
            using (var project = new Project(database, new ProjectData(database, template, null)))
            {
                subsystem = project.FindSubsystem<TerritorySubsystem>(true)!;
                Assert.True(subsystem.Store.TryCreate(owner, new Point3(1, 64, 2)));
                saved = project.Save();
                Assert.Single(saved.ExtensionData.GetValue<ValuesDictionary>(ModEntry.ModId)
                    .GetValue<ValuesDictionary>(TerritorySubsystem.DataKey));
                Assert.False(saved.ValuesDictionary.ContainsKey("Territory"));

                project.SendToClientMode = true;
                Assert.Empty(project.Save().ExtensionData);
            }

            Assert.Empty(subsystem.Store.Territories);
            var serialized = new XElement("Project");
            saved.Save(serialized);
            using var restored = new Project(database, new ProjectData(database, serialized, null, false));
            var territory = Assert.Single(restored.FindSubsystem<TerritorySubsystem>(true)!.Store.Territories);
            Assert.Equal(owner, territory.Owner);
            Assert.Equal(new Point3(1, 64, 2), territory.StonePoint);
        }
        finally
        {
            CommonLib.WorkType = previousWorkType;
        }
    }

    private static (GameDatabase Database, DatabaseObject Template) CreateDatabase()
    {
        var names = new[]
        {
            "Folder", "ProjectTemplate", "MemberSubsystemTemplate", "SubsystemTemplate", "EntityTemplate",
            "MemberComponentTemplate", "ComponentTemplate", "ParameterSet", "Parameter"
        };
        var types = names.Select(name =>
            new DatabaseObjectType(name, name, name, 0, name == "Parameter", false, 64, false)).ToArray();
        foreach (var type in types)
        {
            type.InitializeRelations(types, types, types[^1]);
        }

        var root = new DatabaseObject(types[0], "Root");
        var database = new Database(root, types);
        var template = new DatabaseObject(types[1], "TestProject") { NestingParent = root };
        var member = new DatabaseObject(types[2], "Territory") { NestingParent = template };
        _ = new DatabaseObject(types[^1], "Class", "TerritoryStoneMod.TerritorySubsystem")
        {
            NestingParent = member
        };
        _ = new DatabaseObject(types[^1], "IsOptional", false) { NestingParent = member };
        return (new GameDatabase(database), template);
    }
}
