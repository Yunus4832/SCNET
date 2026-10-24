using Engine.Core.Mathematics;

using Game.Terrains;

namespace Survivalcraft.Test.Network;

public sealed class TerrainEditPredictionProtocolTest
{
    [Theory]
    [InlineData(InteractionMessageKind.Use)]
    [InlineData(InteractionMessageKind.Place)]
    public void InteractionRoundTripPreservesPlacementCorrelationWithoutChangingUseEvents(InteractionMessageKind action)
    {
        var package = new CharacterInteractionPackage
        {
            InteractEvent = action,
            PlacementRequestId = 42,
            PlacementCell = new Point3(-17, 64, 15),
            PlacementExpectedValue = Terrain.MakeBlockValue(18, 12, 5),
            NetInteractRay = new Ray3(new Vector3(1, 2, 3), Vector3.UnitZ)
        };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        using var reader = new ProtocolReader(writer.Data());
        var restored = Assert.IsType<CharacterInteractionPackage>(ProtocolMessageCodec.Read(package.ID, reader));
        Assert.Equal(action, restored.InteractEvent);
        Assert.Equal(package.NetInteractRay.Position, restored.NetInteractRay.Position);
        Assert.Equal(package.NetInteractRay.Direction, restored.NetInteractRay.Direction);
        if (action == InteractionMessageKind.Place)
        {
            Assert.Equal(package.PlacementRequestId, restored.PlacementRequestId);
            Assert.Equal(package.PlacementCell, restored.PlacementCell);
            Assert.Equal(package.PlacementExpectedValue, restored.PlacementExpectedValue);
        }
        else
        {
            Assert.Equal(0, restored.PlacementRequestId);
        }
    }
}
