using Game.Terrains;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainDerivedRevisionsTest
{
    [Fact]
    public void BoundaryInvalidationRetainsDirectInputsAndRequiresOnlyPropagation()
    {
        var revisions = new TerrainDerivedRevisions();
        var first = revisions.Capture();
        Assert.True(revisions.PublishHeights(first));
        Assert.True(revisions.PublishLightSeed(first));
        Assert.True(revisions.PublishLight(first));
        revisions.InvalidatePropagation();
        Assert.True(revisions.HasCurrentHeights);
        Assert.True(revisions.HasCurrentLightSeed);
        Assert.False(revisions.HasCurrentLight);
        Assert.False(revisions.PublishLight(first));
        var current = revisions.Capture();
        Assert.Equal(first.Seed, current.Seed);
        var published = revisions.LightRevision;
        Assert.True(revisions.PublishLight(current, false));
        Assert.Equal(published, revisions.LightRevision);
        Assert.True(revisions.HasCurrentLight);
    }

    [Fact]
    public void PendingPropagationKeepsItsSeedReadyAndRejectsOldCompletions()
    {
        var revisions = new TerrainDerivedRevisions();
        var request = revisions.Capture();
        Assert.True(revisions.PublishHeights(request));
        Assert.True(revisions.PublishLightSeed(request));
        Assert.True(revisions.PublishLight(request));
        revisions.InvalidatePropagation();
        Assert.True(revisions.HasCurrentLightSeed);
        Assert.False(revisions.HasCurrentLight);
        Assert.False(revisions.PublishLight(request));
        Assert.True(revisions.PublishLight(revisions.Capture()));
        Assert.True(revisions.HasCurrentLight);
        revisions.InvalidateLight();
        Assert.False(revisions.HasCurrentLightSeed);
        Assert.False(revisions.HasCurrentLight);
    }

    [Fact]
    public void ContentInvalidationRejectsHeightAndLightCompletionsAlreadyInFlight()
    {
        var revisions = new TerrainDerivedRevisions();
        var old = revisions.Capture();
        revisions.InvalidateHeightsAndLight();
        Assert.False(revisions.PublishHeights(old));
        Assert.False(revisions.PublishLight(old));
        Assert.False(revisions.HasCurrentHeights);
        Assert.False(revisions.HasCurrentLight);
        var current = revisions.Capture();
        Assert.True(revisions.PublishHeights(current));
        Assert.True(revisions.HasCurrentHeights);
        Assert.False(revisions.HasCurrentLight);
        revisions.PublishLightSeed(current);
        Assert.True(revisions.PublishLight(current));
        Assert.True(revisions.HasCurrentLight);
    }

    [Fact]
    public void NewSkyInputDoesNotInvalidateHeightsButRejectsOldLightCompletion()
    {
        var revisions = new TerrainDerivedRevisions();
        var old = revisions.Capture();
        Assert.True(revisions.PublishHeights(old));
        revisions.PublishLightSeed(old);
        Assert.True(revisions.PublishLight(old));
        var heightRevision = revisions.HeightRevision;
        revisions.InvalidateLight();
        Assert.True(revisions.HasCurrentHeights);
        Assert.False(revisions.HasCurrentLight);
        Assert.False(revisions.PublishLight(old));
        revisions.PublishLightSeed(revisions.Capture());
        Assert.True(revisions.PublishLight(revisions.Capture()));
        Assert.Equal(heightRevision, revisions.HeightRevision);
    }

    [Fact]
    public void IndependentWorldsAndNeighborChangesDoNotShareCompletionState()
    {
        var logic = new TerrainDerivedRevisions();
        var visual = new TerrainDerivedRevisions();
        var request = logic.Capture();
        Assert.True(logic.PublishHeights(request));
        logic.PublishLightSeed(request);
        Assert.True(logic.PublishLight(request));
        var before = logic.LightRevision;
        Assert.True(logic.PublishLight(logic.Capture()));
        Assert.True(logic.LightRevision > before);
        Assert.True(logic.HasCurrentLight);
        Assert.False(visual.HasCurrentHeights);
        Assert.False(visual.HasCurrentLight);
        visual.InvalidateLight();
        Assert.True(logic.HasCurrentLight);
    }
}
