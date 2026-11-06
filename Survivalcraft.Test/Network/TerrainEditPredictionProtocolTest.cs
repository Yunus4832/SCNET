using Engine.Core.Mathematics;

namespace Survivalcraft.Test.Network;

public sealed class TerrainEditPredictionProtocolTest
{
    [Theory]
    [InlineData(InteractionMessageKind.Use)]
    [InlineData(InteractionMessageKind.Place)]
    public void InteractionRoundTripPreservesPlacementCorrelationWithoutChangingUseEvents(InteractionMessageKind action)
    {
        CharacterInteractionPackage package = action == InteractionMessageKind.Place
            ? new CharacterPlacePackage
            {
                ControllerClientId = 7, PlacementRequestId = 42, PlacementCell = new Point3(-3, 64, 2),
                PlacementExpectedValue = 100
            }
            : new CharacterUsePackage { ControllerClientId = 7 };
        var encoded = ProtocolMessageCodec.Encode(package);
        var restored = Assert.IsAssignableFrom<CharacterInteractionPackage>(ProtocolMessageCodec.Decode(encoded));
        Assert.Equal(action, restored.InteractEvent);
        Assert.Equal(package.ControllerClientId, restored.ControllerClientId);
        Assert.Equal(encoded, ProtocolMessageCodec.Encode(restored));
        if (restored is CharacterPlacePackage place) { Assert.Equal(42, place.PlacementRequestId); }
        else { Assert.Null(typeof(CharacterUsePackage).GetProperty("PlacementRequestId")); }
    }
}
