using System.Runtime.CompilerServices;

using Game;
using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public sealed class PlayerSnapshotSizeTest
{
    [Fact]
    public void AllMovementFieldsFitInitialSafeMtuWithoutCompression()
    {
        // Serialization only needs ClientId; do not initialize a game world for this size contract.
        var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
        var package = new ComponentPlayerPackage
        {
            PlayerData = player,
            Type = ComponentPlayerPackage.PlayerAction.BodyUpdate,
            PackageChangeFlag = (ComponentPlayerPackage.ChangFlag)byte.MaxValue
        };
        using var writer = new PackageStreamWriter();
        writer.Write((byte)0);
        writer.Write(package.ID);
        package.WriteData(writer);

        var datagram = CommonLib.GetWriter(writer, out _, CommonLib.CompressionPolicy.None);

        Assert.InRange(datagram.Length, 1, 507);
        Assert.Equal(PackageTransportPolicy.Snapshot, PackageTransportPolicy.Get(package));
    }
}
