using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

using Game.World;

namespace Survivalcraft.Test.Subsystems;

public sealed class BuiltInPresentationCompositionTest
{
    [Theory]
    [InlineData("ScreenOverlays")]
    [InlineData("DiggingCracks")]
    public void CharacterVisualDefinitionsAreExcludedBeforeAuthorityConstruction(string name)
    {
        var document = XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"));
        var database = XmlDatabaseSerializer.LoadDatabase(document);
        var template = database.FindDatabaseObject(
            "MaleCharacter",
            database.FindDatabaseObjectType("EntityTemplate", true)!,
            true
        )!;
        var character = new ValuesDictionary();
        character.PopulateFromDatabaseObject(template);
        var definition = character.GetValue<ValuesDictionary>(name);
        Assert.Equal("Presentation", definition.GetValue<string>("ConstructionSide"));
        Assert.False(new WorldProjectComposition(RuntimeSide.Authority, false).ShouldCreateComponent(definition));
        Assert.False(new WorldProjectComposition(RuntimeSide.Client, false).ShouldCreateComponent(definition));
        Assert.True(new WorldProjectComposition(RuntimeSide.Client, true).ShouldCreateComponent(definition));
    }

    [Fact]
    public void ModelRendererIsExcludedBeforeAuthorityEntityCallbacksCanReachIt()
    {
        var document = XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"));
        var database = XmlDatabaseSerializer.LoadDatabase(document);
        var member = database.FindDatabaseObject(
            Guid.Parse("c92e59a5-e519-4c7c-b2ac-b0d3dbb2598b"),
            database.FindDatabaseObjectType("MemberSubsystemTemplate", true),
            true
        )!;
        var definition = new ValuesDictionary();
        definition.PopulateFromDatabaseObject(member);
        Assert.Equal("Game.Subsystems.SubsystemModelsRenderer", definition.GetValue<string>("Class"));
        Assert.False(new WorldProjectComposition(RuntimeSide.Authority, false).ShouldCreateSubsystem(definition));
        Assert.True(new WorldProjectComposition(RuntimeSide.Client, true).ShouldCreateSubsystem(definition));
    }
}
