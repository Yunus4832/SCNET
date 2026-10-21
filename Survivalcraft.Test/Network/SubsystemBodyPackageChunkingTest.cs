using Engine.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class SubsystemBodyPackageChunkingTest
{
    [Fact]
    public void BodySnapshotRoundTripsSnapshotIdAndFullState()
    {
        var package = new SubsystemBodyPackage
        {
            PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate,
            StateTick = 12345u,
            BodyList =
            [
                new SubsystemBodyPackage.BodyItem
                {
                    CreatureId = 7,
                    ChangeFlag = SubsystemBodyPackage.ChangeFlag.PositionChange |
                                 SubsystemBodyPackage.ChangeFlag.RotationChange |
                                 SubsystemBodyPackage.ChangeFlag.VelocityChange |
                                 SubsystemBodyPackage.ChangeFlag.LookAnglesChange |
                                 SubsystemBodyPackage.ChangeFlag.FlyOrderChange,
                    Position = new Vector3(1f, 2f, 3f),
                    Rotation = Quaternion.Identity,
                    Velocity = new Vector3(4f, 5f, 6f),
                    LookAngles = new Vector2(0.1f, 0.2f),
                    FlyOrder = new Vector3(7f, 8f, 9f)
                }
            ]
        };

        var writer = new ProtocolWriter();
        PackageManager.WriteMessage(package, writer);
        var clone = Assert.IsType<SubsystemBodyPackage>(
            ProtocolMessageCodec.Read(
                package.ID,
                new ProtocolReader(writer.Data())
            )
        );

        Assert.Equal(package.StateTick, clone.StateTick);
        var item = Assert.Single(clone.BodyList);
        Assert.Equal(7, item.CreatureId);
        Assert.Equal(new Vector3(1f, 2f, 3f), item.Position);
        Assert.Equal(Quaternion.Identity, item.Rotation);
        Assert.Equal(new Vector3(4f, 5f, 6f), item.Velocity);
        Assert.Equal(new Vector2(0.1f, 0.2f), item.LookAngles);
        Assert.Equal(new Vector3(7f, 8f, 9f), item.FlyOrder);
    }

    [Theory]
    [InlineData(461)]
    [InlineData(508)]
    [InlineData(1428)]
    public void FullBodySnapshotExceedingMtuCanBeSplitIntoFittingChunks(int maxPacketSize)
    {
        var items = new List<SubsystemBodyPackage.BodyItem>();
        var full = new SubsystemBodyPackage
        {
            PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate,
            StateTick = 12345u,
            BodyList = items
        };
        for (var i = 0; i < 80; i++)
        {
            items.Add(
                new SubsystemBodyPackage.BodyItem
                {
                    CreatureId = i,
                    ChangeFlag = SubsystemBodyPackage.ChangeFlag.PositionChange |
                                 SubsystemBodyPackage.ChangeFlag.RotationChange |
                                 SubsystemBodyPackage.ChangeFlag.VelocityChange |
                                 SubsystemBodyPackage.ChangeFlag.LookAnglesChange |
                                 SubsystemBodyPackage.ChangeFlag.FlyOrderChange,
                    Position = new Vector3(i, 0, 0),
                    Rotation = Quaternion.Identity,
                    Velocity = Vector3.Zero,
                    LookAngles = Vector2.Zero,
                    FlyOrder = Vector3.Zero
                }
            );
        }

        Assert.True(SerializeSize([full]) > maxPacketSize);

        var chunks = new NetNode().SplitOversizedBodyPackage(full, PackageTransportPolicy.StateStream, maxPacketSize);

        Assert.True(chunks.Count > 1);
        Assert.Equal(80, chunks.Sum(chunk => chunk.BodyList.Count));
        Assert.All(
            chunks,
            chunk =>
            {
                Assert.Equal(full.StateTick, chunk.StateTick);
                Assert.True(SubsystemBodyPackageHandler.IsNewer(chunk.StateTick, 0));
                Assert.True(SubsystemBodyPackageHandler.IsNewer(chunk.StateTick, full.StateTick - 1));
            }
        );
        Assert.All(chunks, chunk => Assert.True(SerializeSize([chunk]) <= maxPacketSize));
    }

    private static int SerializeSize(IEnumerable<IProtocolMessage> packages)
    {
        var writer = new ProtocolWriter();
        foreach (var package in packages)
        {
            writer.Write((byte)0x88);
            writer.Write(package.ID);
            PackageManager.WriteMessage(package, writer);
        }

        return writer.Data(CompressionPolicy.None).Length + sizeof(int);
    }
}
