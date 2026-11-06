using System.Xml.Linq;

using Engine.Core.Reflection;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.Hosting;
using Game.Subsystems;

namespace Survivalcraft.Test.Characters;

public sealed class CharacterTemplateTest
{
    [Theory]
    [InlineData("MaleCharacter")]
    [InlineData("FemaleCharacter")]
    public void CharacterOverridesSurviveEntitySerializationThroughInheritedTemplate(string templateName)
    {
        var database = LoadDatabase();
        var template = database.Database.FindDatabaseObject(templateName, database.EntityTemplateType, true)!;
        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(template);
        Assert.False(values.ContainsKey("Player"));
        var character = values.GetValue<ValuesDictionary>("Character");
        Assert.Equal(typeof(ComponentCharacter).FullName, character.GetValue<string>("Class"));
        Assert.Equal(Guid.Empty, character.GetValue<Guid>("CharacterId"));

        var id = Guid.NewGuid();
        values.ApplyOverrides(
            new ValuesDictionary
            {
                { "Character", new ValuesDictionary { { "CharacterId", id } } }
            }
        );
        var node = new XElement("Entity");
        new EntityData { Id = 1, ValuesDictionary = values }.Save(node);
        var restored = new EntityData(database, node);
        Assert.Equal(templateName, restored.ValuesDictionary.DatabaseObject.Name);
        Assert.Equal(
            id,
            restored.ValuesDictionary.GetValue<ValuesDictionary>("Character").GetValue<Guid>("CharacterId")
        );
    }

    [Fact]
    public void ProjectTemplateSeparatesPlayerRecordsAndCharacterCollections()
    {
        var database = LoadDatabase();
        var template = database.Database.FindDatabaseObject("GameProject", database.ProjectTemplateType, true)!;
        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(template);
        var players = values.GetValue<ValuesDictionary>("Players");
        Assert.Equal(typeof(SubsystemPlayers).FullName, players.GetValue<string>("Class"));
        Assert.False(players.ContainsKey("Characters"));
        Assert.False(players.ContainsKey("OfflineCharacters"));
        var characters = values.GetValue<ValuesDictionary>("Characters");
        Assert.Equal(typeof(SubsystemCharacters).FullName, characters.GetValue<string>("Class"));
        Assert.Empty(characters.GetValue<ValuesDictionary>("Characters"));
        Assert.Empty(characters.GetValue<ValuesDictionary>("OfflineCharacters"));
        Assert.Contains("Players", characters.GetValue<string>("Dependencies"));
        Assert.Equal(
            typeof(SubsystemPositionMarks).FullName,
            values.GetValue<ValuesDictionary>("PositionMarks").GetValue<string>("Class")
        );
    }

    [Fact]
    public void AllInheritedComponentAndSubsystemClassBindingsResolve()
    {
        var database = LoadDatabase();
        foreach (var type in new[] { database.EntityTemplateType, database.ProjectTemplateType })
        {
            foreach (var template in database.Database.Root.GetExplicitNestingChildren(type, false))
            {
                var values = new ValuesDictionary();
                values.PopulateFromDatabaseObject(template);
                foreach (var component in values.Values.OfType<ValuesDictionary>())
                {
                    if (!component.ContainsKey("Class")) { continue; }

                    var name = component.GetValue<string>("Class");
                    if (string.IsNullOrEmpty(name)) { continue; }

                    var owners = new[]
                    {
                        typeof(SubsystemTime).Assembly, typeof(SubsystemWorldHosting).Assembly,
                        typeof(ComponentGui).Assembly, typeof(Game.Managers.ContentManager).Assembly
                    }.Distinct();
                    var candidates = owners.Select(owner => owner.GetType(name, false)).Where(type => type is not null)
                        .ToArray();
                    var expectedOwner = Assert.Single(candidates);
                    var binding = RuntimeTypeResolver.FindType(name)!;
                    Assert.Same(expectedOwner, binding);
                    Assert.Same(binding, RuntimeTypeResolver.FindType(name));
                    Assert.True(
                        typeof(Component).IsAssignableFrom(binding) || typeof(Subsystem).IsAssignableFrom(binding),
                        $"{template.Name}/{component.DatabaseObject.Name} has invalid class binding {name}."
                    );
                }
            }
        }
    }

    [Fact]
    public void TeamsHaveTheirOwnSubsystemAndPersistenceSchema()
    {
        var database = LoadDatabase();
        var template = database.Database.FindDatabaseObject("GameProject", database.ProjectTemplateType, true)!;
        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(template);
        var players = values.GetValue<ValuesDictionary>("Players");
        Assert.False(players.ContainsKey("Teams"));
        Assert.False(players.ContainsKey("ServerGroups"));
        var teams = values.GetValue<ValuesDictionary>("Teams");
        Assert.Equal(typeof(SubsystemTeams).FullName, teams.GetValue<string>("Class"));
        Assert.Equal("Players", teams.GetValue<string>("Dependencies"));
        Assert.Empty(teams.GetValue<ValuesDictionary>("Teams"));
    }

    private static GameDatabase LoadDatabase()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml");
        return new GameDatabase(XmlDatabaseSerializer.LoadDatabase(XElement.Load(path)));
    }
}
