using System.Reflection;

using BeaconMod;
using BeaconMod.Client.Presentation;

using Engine.Core.Mathematics;

using Game.Rendering;
using Game.Terrains;


namespace BeaconMod.Test;

public sealed class BeaconPresentationBoundaryTest
{
    [Fact]
    public void BeaconLocationsAreRuleFactsWithoutAWindowOrPresentationMode()
    {
        var behavior = new BeaconBlockBehavior();
        behavior.OnBlockGenerated(0, 1, 4, 1, true);
        behavior.OnBlockAdded(0, 0, 1, 4, 1);
        behavior.OnBlockAdded(0, 0, 17, 4, 1);
        Assert.Equal(2, behavior.Beacons.Count());
        Assert.False(typeof(IDrawable).IsAssignableFrom(typeof(BeaconBlockBehavior)));
        using var terrain = new Terrain();
        behavior.OnChunkDiscarding(terrain.AllocateChunk(0, 0));
        Assert.Equal(new Point3(17, 4, 1), Assert.Single(behavior.Beacons));
        behavior.OnBlockRemoved(0, 0, 17, 4, 1);
        Assert.Empty(behavior.Beacons);
        behavior.Dispose();
    }

    [Fact]
    public void ClientOwnerSubscribesOnceAndUnsubscribesBeforeReleasedCallbacks()
    {
        var behavior = new BeaconBlockBehavior();
        var presentation = new BeaconPresentation();
        typeof(BeaconPresentation).GetField("_behavior", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(presentation, behavior);
        var subscribe = typeof(BeaconPresentation).GetMethod(
            "Subscribe",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        subscribe.Invoke(presentation, null);
        subscribe.Invoke(presentation, null);
        var field = typeof(BeaconBlockBehavior).GetField(
            "InteractionPerformed",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        Assert.Single(((Delegate)field.GetValue(behavior)!).GetInvocationList());
        presentation.Dispose();
        presentation.Dispose();
        Assert.Null(field.GetValue(behavior));
        behavior.NotifyInteraction(new Point3(1, 2, 3));
        presentation.Draw(null!, 101);
    }

    [Fact]
    public void OnlyClientAssemblyOwnsTheVisualPaletteAndModelResource()
    {
        Assert.Null(typeof(BeaconBlock).GetField("Colors"));
        Assert.Null(typeof(BeaconBlock).GetMethod("GetEmitterColor"));
        Assert.DoesNotContain(
            typeof(BeaconBlock).Assembly.GetManifestResourceNames(),
            name => name.EndsWith(".glb", StringComparison.Ordinal)
        );
        using var model =
            typeof(BeaconClientContentModule).Assembly.GetManifestResourceStream(
                "BeaconMod.Client.Resources.Models.Beacon.glb"
            );
        Assert.NotNull(model);
        Assert.Equal(8, BeaconPalette.Colors.Length);
        for (var intensity = 0; intensity < 16; intensity++)
        {
            for (var color = 0; color < 8; color++)
            {
                var data = BeaconBlock.SetIntensity(color, intensity);
                Assert.Equal(BeaconPalette.Colors[color], BeaconPalette.GetColor(data));
                Assert.Equal(
                    Color.Lerp(BeaconPalette.Colors[color], Color.White, 0.8f),
                    BeaconPalette.GetEmitterColor(data)
                );
            }
        }
    }
}
