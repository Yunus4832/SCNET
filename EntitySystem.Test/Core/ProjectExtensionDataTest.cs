using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Test.Core;

public class ProjectExtensionDataTest
{
    [Fact]
    public void OwnersAndDatasetsAreIndependent()
    {
        var store = new ProjectExtensionData();
        store.Get("game", "marks").SetValue("value", 1);
        store.Get("example", "marks").SetValue("value", 2);
        store.Get("example", "territories").SetValue("value", 3);

        Assert.Equal(1, store.Get("game", "marks").GetValue<int>("value"));
        Assert.Equal(2, store.Get("example", "marks").GetValue<int>("value"));
        Assert.Equal(3, store.Get("example", "territories").GetValue<int>("value"));
        Assert.Throws<ArgumentException>(() => store.Get(" ", "marks"));
        Assert.Throws<ArgumentException>(() => store.Get("game", ""));
    }

    [Fact]
    public void LoadAndSaveIsolateNestedDictionaries()
    {
        var store = new ProjectExtensionData();
        var nested = new ValuesDictionary();
        nested.SetValue("value", 1);
        store.Get("example", "data").SetValue("nested", nested);
        var snapshot = store.Save();
        nested.SetValue("value", 2);

        var restored = new ProjectExtensionData();
        restored.Load(snapshot);
        snapshot.GetValue<ValuesDictionary>("example").Clear();
        Assert.Equal(1, restored.Get("example", "data").GetValue<ValuesDictionary>("nested").GetValue<int>("value"));
        restored.Load(new ValuesDictionary());
        Assert.Empty(restored.Get("example", "data"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectRoundTripsBusinessDataOutsideSubsystemsAndEntities(bool messagePack)
    {
        var (database, template) = CreateDatabase();
        using var project = new Project(database, new ProjectData(database, template, null));
        project.ExtensionData.Get("example", "counter").SetValue("count", 42);
        var saved = project.Save();
        var restoredData = RoundTrip(database, saved, messagePack);
        Assert.Empty(restoredData.ValuesDictionary);
        Assert.Empty(restoredData.EntityDataList.EntitiesData);
        using var restored = new Project(database, restoredData);
        Assert.Equal(42, restored.ExtensionData.Get("example", "counter").GetValue<int>("count"));
        project.ExtensionData.Get("example", "counter").SetValue("count", 43);
        Assert.Equal(42, restored.ExtensionData.Get("example", "counter").GetValue<int>("count"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyProjectsAndClientSnapshotsContainNoBusinessData(bool messagePack)
    {
        var (database, template) = CreateDatabase();
        using var project = new Project(database, new ProjectData(database, template, null));
        Assert.Empty(RoundTrip(database, project.Save(), messagePack).ExtensionData);
        project.ExtensionData.Get("example", "secret").SetValue("value", "server-only");
        project.SendToClientMode = true;
        Assert.Empty(RoundTrip(database, project.Save(), messagePack).ExtensionData);
        project.SendToClientMode = false;
        Assert.Equal("server-only", RoundTrip(database, project.Save(), messagePack).ExtensionData
            .GetValue<ValuesDictionary>("example").GetValue<ValuesDictionary>("secret").GetValue<string>("value"));
    }

    private static ProjectData RoundTrip(GameDatabase database, ProjectData saved, bool messagePack)
    {
        if (messagePack)
        {
            var root = new ValuesDictionary();
            saved.Save(root);
            return new ProjectData(database, root.ToMessagePack(), null, false);
        }

        var node = new XElement("Project");
        saved.Save(node);
        Assert.Null(node.Element("Subsystems")!.Element("ExtensionData"));
        return new ProjectData(database, XElement.Parse(node.ToString()), null, false);
    }

    private static (GameDatabase Database, DatabaseObject Template) CreateDatabase()
    {
        var names = new[]
        {
            "Folder", "ProjectTemplate", "MemberSubsystemTemplate", "SubsystemTemplate", "EntityTemplate",
            "MemberComponentTemplate", "ComponentTemplate", "ParameterSet", "Parameter"
        };
        var types = names.Select(name => new DatabaseObjectType(name, name, name, 0, name == "Parameter", false, 64, false)).ToArray();
        foreach (var type in types)
        {
            type.InitializeRelations([types[0]], null, types[^1]);
        }

        var root = new DatabaseObject(types[0], "Root");
        var database = new Database(root, types);
        var template = new DatabaseObject(types[1], "TestProject") { NestingParent = root };
        return (new GameDatabase(database), template);
    }
}
