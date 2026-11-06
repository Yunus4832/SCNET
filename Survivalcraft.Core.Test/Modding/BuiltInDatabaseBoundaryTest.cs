using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

using Game.World;

namespace Survivalcraft.Core.Test.Modding;

public sealed class BuiltInDatabaseBoundaryTest
{
    [Theory]
    [InlineData("Audio")]
    [InlineData("AmbientSounds")]
    [InlineData("AnimatedTextures")]
    [InlineData("BlocksTexture")]
    [InlineData("Drawing")]
    [InlineData("GameWidgets")]
    [InlineData("Particles")]
    [InlineData("Postprocessor")]
    [InlineData("Shadows")]
    [InlineData("ButtonBlockBehavior")]
    [InlineData("SwitchBlockBehavior")]
    [InlineData("Gui")]
    public void PresentationServicesAreExcludedBeforeAuthorityTypeResolution(string service)
    {
        var root = XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"));
        var component = service == "Gui";
        var templateKind = component ? "ComponentTemplate" : "SubsystemTemplate";
        var template = root.Descendants(templateKind)
            .Single(node => (string?)node.Attribute("Name") == service);
        var database = XmlDatabaseSerializer.LoadDatabase(root);
        var definition = database.FindDatabaseObject(
            Guid.Parse((string)template.Attribute("Guid")!),
            database.FindDatabaseObjectType(templateKind, true),
            true
        )!;
        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(definition);
        foreach (var (side, presentation, expected) in new[]
                 {
                     (RuntimeSide.Authority, false, false), (RuntimeSide.Client, false, false),
                     (RuntimeSide.Client, true, true)
                 })
        {
            var composition = new WorldProjectComposition(side, presentation);
            Assert.Equal(
                expected,
                component
                    ? composition.ShouldCreateComponent(values)
                    : composition.ShouldCreateSubsystem(values)
            );
        }
    }

    [Fact]
    public void BuiltInDatabaseLoadsAllStoredValueTypesWithoutPresentationAssemblies()
    {
        var root = XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"));
        var database = XmlDatabaseSerializer.LoadDatabase(root);
        Assert.NotNull(database.Root);
        foreach (var type in root.Descendants().Attributes("Type").Select(attribute => attribute.Value).Distinct())
        {
            Assert.NotNull(TemplateTypeNames.Resolve(type));
        }

        var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName().Name).ToHashSet();
        Assert.DoesNotContain("Engine", loaded);
        Assert.DoesNotContain("Survivalcraft", loaded);
        Assert.DoesNotContain("Survivalcraft.Client", loaded);
    }
}
