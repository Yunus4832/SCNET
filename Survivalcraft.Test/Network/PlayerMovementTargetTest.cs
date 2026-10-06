using Game.Components;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class PlayerMovementTargetTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DelayedSnapshotsCannotTargetTheWrongBodyAcrossMountTransitions(bool currentlyMounted,
        bool packetMounted)
    {
        var mount = new ComponentBody();
        var player = new ComponentBody { ParentBody = currentlyMounted ? mount : null };

        var target = ComponentPlayerPackageHandler.ResolveMovementBody(player, packetMounted);

        if (currentlyMounted != packetMounted)
        {
            Assert.Null(target);
        }
        else
        {
            Assert.Same(currentlyMounted ? mount : player, target);
        }
    }
}
