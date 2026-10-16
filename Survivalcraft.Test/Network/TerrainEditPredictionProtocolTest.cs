using System.Runtime.CompilerServices;

using Engine.Core;

using Game;
using Game.Network.Packages;
using Game.Network.Serialization;
using Game.Terrains;

namespace Survivalcraft.Test.Network;

public sealed class TerrainEditPredictionProtocolTest
{
    [Theory]
    [InlineData(InteractEvent.Start)]
    [InlineData(InteractEvent.Place)]
    public void InteractionRoundTripPreservesPlacementCorrelationWithoutChangingUseEvents(InteractEvent action)
    {
        var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
        var package = new ComponentPlayerPackage
        {
            PlayerData = player,
            Type = ComponentPlayerPackage.PlayerAction.InteractEvent,
            InteractEvent = action,
            PlacementRequestId = 42,
            PlacementCell = new Point3(-17, 64, 15),
            PlacementExpectedValue = Terrain.MakeBlockValue(18, 12, 5),
            NetInteractRay = new Ray3(new Vector3(1, 2, 3), Vector3.UnitZ)
        };
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var restored = new ComponentPlayerPackage();
        restored.ReadData(reader);
        Assert.Equal(action, restored.InteractEvent);
        Assert.Equal(package.NetInteractRay.Position, restored.NetInteractRay.Position);
        Assert.Equal(package.NetInteractRay.Direction, restored.NetInteractRay.Direction);
        if (action == InteractEvent.Place)
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
