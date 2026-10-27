using Game.Network;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class PlayerSnapshotSizeTest
{
    [Fact]
    public void AllMovementFieldsFitInitialSafeMtuWithoutCompression()
    {
        // A prepared identity byte is sufficient; serialization does not need a game world.
        var package = new CharacterStatePackage
        {
            PackageChangeFlag = (ComponentCharacterPackage.ChangFlag)byte.MaxValue
        };
        var writer = ProtocolPacketCodec.Encode([package], CompressionPolicy.None);

        var datagram = CommonLib.GetWriter(writer);

        Assert.InRange(datagram.Length, 1, 507);
        Assert.Equal(PackageTransportPolicy.Snapshot, PackageTransportPolicy.Get(package));
    }
}
