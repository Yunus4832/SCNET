using System.Xml.Linq;

using Engine.Core.Reflection;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game;
using Game.Creatures;
using Game.World;

namespace Survivalcraft.Test.Modding;

public sealed class TemplateTypeResolutionTest
{
    [Fact]
    public void BuiltInTemplateDatabaseResolvesStoredValuesAndRequiredTemplateImplementations()
    {
        _ = typeof(GameEntry).Assembly.GetTypes();
        var root = XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"));
        var database = XmlDatabaseSerializer.LoadDatabase(root);
        var gameDatabase = new GameDatabase(database);
        foreach (var node in root.Descendants())
        {
            if ((string?)node.Attribute("Type") is { Length: > 0 } typeName)
            {
                Assert.NotNull(TemplateTypeNames.Resolve(typeName));
            }
        }

        var templates = database.Root.GetExplicitNestingChildren(gameDatabase.EntityTemplateType, false)
            .Concat(database.Root.GetExplicitNestingChildren(gameDatabase.ProjectTemplateType, false));
        foreach (var template in templates)
        {
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(template);
            foreach (var item in values.Values.OfType<ValuesDictionary>())
            {
                var name = item.GetValue<string>("Class", string.Empty);
                if (name.Length == 0) { continue; }

                var optional = item.GetValue<bool>("IsOptional", false);
                var type = RuntimeTypeResolver.FindType(name, !optional);
                if (!optional) { Assert.NotNull(type); }
            }
        }
    }

    [Fact]
    public void TemplateValuesRoundTripThroughCurrentDomainTypeNames()
    {
        var values = new ValuesDictionary();
        values.SetValue("Mode", GameMode.Creative);
        values.SetValue("Category", CreatureCategory.Bird);
        var root = new XElement("Values");
        values.Save(root);
        var restored = new ValuesDictionary();
        restored.ApplyOverrides(root);
        Assert.Equal(GameMode.Creative, restored.GetValue<GameMode>("Mode"));
        Assert.Equal(CreatureCategory.Bird, restored.GetValue<CreatureCategory>("Category"));
    }
}
