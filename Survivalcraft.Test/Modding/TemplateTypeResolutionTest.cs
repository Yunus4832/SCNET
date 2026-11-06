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
    public void AllCreatureDefinitionsHaveIndependentGameplayPoseAndMatchingBodyProfile()
    {
        var root = XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"));
        var database = new GameDatabase(XmlDatabaseSerializer.LoadDatabase(root));
        var profiles = new Dictionary<string, string>
        {
            ["HumanModel"] = "Human", ["FourLeggedModel"] = "FourLegged", ["FishModel"] = "Fish",
            ["BirdModel"] = "Bird", ["FlightlessBirdModel"] = "FlightlessBird"
        };
        var count = 0;
        foreach (var definition in database.Database.Root.GetExplicitNestingChildren(
                     database.EntityTemplateType,
                     false
                 ))
        {
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(definition);
            if (!values.ContainsKey("Creature")) { continue; }

            var pose = values.GetValue<ValuesDictionary>("CreaturePose");
            Assert.Equal("Game.Components.ComponentCreaturePose", pose.GetValue<string>("Class"));
            foreach (var profile in profiles)
            {
                if (values.ContainsKey(profile.Key))
                {
                    Assert.Equal(profile.Value, pose.GetValue<object>("Kind").ToString());
                    if (profile.Key == "HumanModel")
                    {
                        Assert.Equal(
                            values.GetValue<ValuesDictionary>(profile.Key).GetValue<float>("WalkAnimationSpeed"),
                            pose.GetValue<float>("FootstepRate")
                        );
                    }
                }
            }

            count++;
        }

        Assert.True(count > 0);
    }

    [Fact]
    public void EveryBuiltInEffectStateHasItsPresentationComponent()
    {
        var root = XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"));
        var database = new GameDatabase(XmlDatabaseSerializer.LoadDatabase(root));
        var covered = new Dictionary<string, int>
        {
            ["OnFire"] = 0, ["Shapeshifter"] = 0, ["Health"] = 0, ["Damage"] = 0, ["Sickness"] = 0, ["Flu"] = 0,
            ["Sleep"] = 0, ["VitalStats"] = 0, ["CreatureSounds"] = 0, ["Body"] = 0, ["Miner"] = 0, ["Level"] = 0,
            ["Clothing"] = 0, ["Boat"] = 0, ["Character"] = 0, ["Intro"] = 0, ["Locomotion"] = 0,
            ["CetaceanBreatheBehavior"] = 0
        };
        foreach (var template in database.Database.Root.GetExplicitNestingChildren(database.EntityTemplateType, false))
        {
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(template);
            foreach (var name in covered.Keys.ToArray())
            {
                if (!values.ContainsKey(name)) { continue; }

                var state = values.GetValue<ValuesDictionary>(name);
                var presentationName = name.EndsWith("Behavior", StringComparison.Ordinal) ? name[..^8] : name;
                var presentation = values.GetValue<ValuesDictionary>(presentationName + "Presentation");
                Assert.Equal(
                    "Game.Components.Component" + presentationName + "Presentation",
                    presentation.GetValue<string>("Class")
                );
                Assert.True(presentation.GetValue<int>("LoadOrder") > state.GetValue<int>("LoadOrder"));
                covered[name]++;
            }
        }

        Assert.All(covered.Values, count => Assert.True(count > 0));
    }

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
