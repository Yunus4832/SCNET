using EntitySystem.TemplatesDatabase;

using Game.World;

namespace Survivalcraft.Core.Test.World;

public sealed class WorldProjectCompositionTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, "Shared", true)]
    [InlineData(RuntimeSide.Client, "Shared", true)]
    [InlineData(RuntimeSide.Authority, "Authority", true)]
    [InlineData(RuntimeSide.Client, "Authority", false)]
    [InlineData(RuntimeSide.Authority, "Client", false)]
    [InlineData(RuntimeSide.Client, "Client", true)]
    public void ConstructionUsesExplicitOwnershipRatherThanUpdateSide(
        RuntimeSide side,
        string constructionSide,
        bool expected
    )
    {
        var definition = new ValuesDictionary();
        definition.SetValue("ConstructionSide", constructionSide);
        definition.SetValue("UpdateSide", "Authority");
        definition.SetValue("Class", "Unavailable.Presentation.Type");
        var composition = new WorldProjectComposition(side, side == RuntimeSide.Client);
        Assert.Equal(expected, composition.ShouldCreateComponent(definition));
        Assert.Equal(expected, composition.ShouldCreateSubsystem(definition));
        Assert.Equal("Unavailable.Presentation.Type", definition.GetValue<string>("Class"));
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, false, false)]
    [InlineData(RuntimeSide.Authority, true, false)]
    [InlineData(RuntimeSide.Client, false, false)]
    [InlineData(RuntimeSide.Client, true, true)]
    public void PresentationRequiresBothAClientWorldAndPresentationCapability(
        RuntimeSide side,
        bool hasPresentation,
        bool expected
    )
    {
        var definition = new ValuesDictionary();
        definition.SetValue("ConstructionSide", "Presentation");
        definition.SetValue("Class", "Unavailable.Presentation.Type");
        var composition = new WorldProjectComposition(side, hasPresentation);
        Assert.Equal(expected, composition.ShouldCreateComponent(definition));
        Assert.Equal(expected, composition.ShouldCreateSubsystem(definition));
        definition.SetValue("ConstructionSide", "Client");
        Assert.Equal(side == RuntimeSide.Client, composition.ShouldCreateComponent(definition));
    }

    [Fact]
    public void UnknownOwnershipFailsInsteadOfSilentlyDroppingContent()
    {
        var definition = new ValuesDictionary();
        definition.SetValue("ConstructionSide", "Unknown");
        var composition = new WorldProjectComposition(RuntimeSide.Authority, false);
        Assert.Throws<InvalidOperationException>(() => composition.ShouldCreateComponent(definition));
    }
}
