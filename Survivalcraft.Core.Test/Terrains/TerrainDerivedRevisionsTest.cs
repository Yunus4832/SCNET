using Game.Terrains;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainDerivedRevisionsTest
{
    [Fact]
    public void PartialPropagationDoesNotExposeUnstableLightAsCurrentOrLoseInputCompletion()
    {
        var revisions = new TerrainDerivedRevisions();
        var request = revisions.Capture();
        Assert.True(revisions.PublishHeights(request));
        Assert.True(revisions.PublishSunlight(request));
        Assert.True(revisions.PublishLight(request));
        revisions.BeginLightWrite();
        Assert.True(revisions.HasCompletedLightInputs);
        Assert.False(revisions.HasCurrentLight);
        revisions.RecordLightChange();
        Assert.False(revisions.HasCurrentLight);
        revisions.EndLightWrite();
        Assert.True(revisions.HasCurrentLight);
        revisions.BeginLightWrite();
        revisions.InvalidateLight();
        revisions.EndLightWrite();
        Assert.False(revisions.HasCurrentLight);
        Assert.False(revisions.HasCompletedLightInputs);
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
        revisions.PublishSunlight(current);
        Assert.True(revisions.PublishLight(current));
        Assert.True(revisions.HasCurrentLight);
    }

    [Fact]
    public void NewSkyInputDoesNotInvalidateHeightsButRejectsOldLightCompletion()
    {
        var revisions = new TerrainDerivedRevisions();
        var old = revisions.Capture();
        Assert.True(revisions.PublishHeights(old));
        revisions.PublishSunlight(old);
        Assert.True(revisions.PublishLight(old));
        var heightRevision = revisions.HeightRevision;
        revisions.InvalidateLight();
        Assert.True(revisions.HasCurrentHeights);
        Assert.False(revisions.HasCurrentLight);
        Assert.False(revisions.PublishLight(old));
        revisions.PublishSunlight(revisions.Capture());
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
        logic.PublishSunlight(request);
        Assert.True(logic.PublishLight(request));
        var before = logic.LightRevision;
        logic.RecordLightChange();
        Assert.True(logic.LightRevision > before);
        Assert.True(logic.HasCurrentLight);
        Assert.False(visual.HasCurrentHeights);
        Assert.False(visual.HasCurrentLight);
        visual.InvalidateLight();
        Assert.True(logic.HasCurrentLight);
    }
}
