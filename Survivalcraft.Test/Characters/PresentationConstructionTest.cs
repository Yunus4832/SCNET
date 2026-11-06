using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.World;

namespace Survivalcraft.Test.Characters;

public sealed class PresentationConstructionTest
{
    [Theory]
    [InlineData("MaleCharacter")]
    [InlineData("FemaleCharacter")]
    [InlineData("Cow")]
    [InlineData("Wolf")]
    [InlineData("Shark_Tiger")]
    public void RealAuthorityEntityConstructionSkipsViewClassesBeforeResolvingThem(string name)
    {
        var database = LoadDatabase();
        var values = EntityValues(database, name);
        var views = ComponentValues(database, values)
            .Where(definition => definition.GetValue("ConstructionSide", "Shared") == "Presentation").ToArray();
        Assert.NotEmpty(views);
        foreach (var view in views)
        {
            view.SetValue("Class", "Unavailable.Client.Assembly." + view.DatabaseObject.Name);
            view.SetValue("IsOptional", false);
        }

        using var project = EmptyProject(database, new WorldProjectComposition(RuntimeSide.Authority, false));
        using var entity = new Entity(project, values);
        Assert.Null(entity.FindComponent<ComponentModel>());
        Assert.Null(entity.FindComponent<ComponentAimingSights>());
        Assert.Null(entity.FindComponent<ComponentBlockHighlight>());
        Assert.Null(entity.FindComponent<ComponentFirstPersonModel>());
        Assert.NotNull(entity.FindComponent<ComponentCreature>());
        Assert.NotNull(entity.FindComponent<ComponentCreaturePose>());
        Assert.NotNull(entity.FindComponent<ComponentBody>());
        if (name is "MaleCharacter" or "FemaleCharacter")
        {
            Assert.Null(entity.FindComponent<ComponentInput>());
            Assert.Null(entity.FindComponent<ComponentGui>());
        }
    }

    [Theory]
    [InlineData("MaleCharacter", "HumanModel")]
    [InlineData("FemaleCharacter", "HumanModel")]
    [InlineData("Cow", "FourLeggedModel")]
    [InlineData("Wolf", "FourLeggedModel")]
    [InlineData("Shark_Tiger", "FishModel")]
    public void ClientEntityKeepsTheActualModelsAndTheirInheritedConfiguration(string name, string modelName)
    {
        var database = LoadDatabase();
        var values = EntityValues(database, name);
        var model = values.GetValue<ValuesDictionary>(modelName);
        Assert.Equal("Presentation", model.GetValue<string>("ConstructionSide"));
        using var project = EmptyProject(database, new WorldProjectComposition(RuntimeSide.Client, true));
        using var entity = new Entity(project, values);
        Assert.NotNull(entity.FindComponent<ComponentCreatureModel>());
        Assert.Same(model, entity.FindComponent<ComponentCreatureModel>()!.ValuesDictionary);
        Assert.False(string.IsNullOrWhiteSpace(model.GetValue<string>("ModelName")));
        if (name is "MaleCharacter" or "FemaleCharacter")
        {
            Assert.NotNull(entity.FindComponent<ComponentInput>());
            Assert.NotNull(entity.FindComponent<ComponentHumanModel>());
            Assert.NotNull(entity.FindComponent<ComponentOuterClothingModel>());
            Assert.NotNull(entity.FindComponent<ComponentFirstPersonModel>());
            Assert.NotNull(entity.FindComponent<ComponentAimingSights>());
            Assert.NotNull(entity.FindComponent<ComponentBlockHighlight>());
        }
    }

    [Fact]
    public void EveryEffectiveCreatureModelInheritsPresentationOwnershipWhilePoseRemainsRules()
    {
        var database = LoadDatabase();
        var count = 0;
        var modelNames = new[] { "HumanModel", "FourLeggedModel", "FishModel", "BirdModel", "FlightlessBirdModel" };
        foreach (var template in database.Database.Root.GetExplicitNestingChildren(database.EntityTemplateType, false))
        {
            var values = EntityValues(database, template.Name);
            if (!values.ContainsKey("Creature")) { continue; }

            foreach (var modelName in modelNames)
            {
                if (!values.ContainsKey(modelName)) { continue; }

                Assert.Equal(
                    "Presentation",
                    values.GetValue<ValuesDictionary>(modelName).GetValue<string>("ConstructionSide")
                );
                count++;
            }

            var pose = values.GetValue<ValuesDictionary>("CreaturePose");
            Assert.True(new WorldProjectComposition(RuntimeSide.Authority, false).ShouldCreateComponent(pose));
        }

        Assert.True(count > 2);
    }

    private static GameDatabase LoadDatabase()
    {
        return new GameDatabase(
            XmlDatabaseSerializer.LoadDatabase(
                XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"))
            )
        );
    }

    private static ValuesDictionary EntityValues(GameDatabase database, string name)
    {
        var definition = database.Database.FindDatabaseObject(name, database.EntityTemplateType, true)!;
        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(definition);
        return values;
    }

    private static IEnumerable<ValuesDictionary> ComponentValues(GameDatabase database, ValuesDictionary entity)
    {
        return entity.Values.OfType<ValuesDictionary>()
            .Where(definition => definition.DatabaseObject.Type == database.MemberComponentTemplateType);
    }

    private static Project EmptyProject(GameDatabase database, WorldProjectComposition composition)
    {
        var template = new DatabaseObject(database.ProjectTemplateType, "PresentationConstructionTest")
        {
            NestingParent = database.Database.Root
        };
        return new Project(database, new ProjectData(database, template, null), composition: composition);
    }
}
