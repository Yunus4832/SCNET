using Game.Network;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class PlayerSnapshotSizeTest
{
    [Fact]
    public void AllMovementFieldsFitInitialSafeMtuWithoutCompression()
    {
        // A prepared identity byte is sufficient; serialization does not need a game world.
        var package = new PlayerStatePackage
        {
            PackageChangeFlag = (ComponentPlayerPackage.ChangFlag)byte.MaxValue
        };
        using var writer = new ProtocolWriter();
        writer.Write((byte)0);
        writer.Write(package.ID);
        ProtocolMessageCodec.Write(package, writer);

        var datagram = CommonLib.GetWriter(writer, out _, CompressionPolicy.None);

        Assert.InRange(datagram.Length, 1, 507);
        Assert.Equal(PackageTransportPolicy.Snapshot, PackageTransportPolicy.Get(package));
    }
}
