using Engine.Core;

using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public sealed class SubsystemBodyPackageChunkingTest
{
    [Fact]
    public void BodySnapshotRoundTripsSnapshotIdAndFullState()
    {
        var package = new SubsystemBodyPackage
        {
            PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate,
            StateTick = 12345u
        };
        package.BodyList.Add(new SubsystemBodyPackage.BodyItem
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
        });

        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        var clone = new SubsystemBodyPackage();
        clone.ReadData(new PackageStreamReader(writer.Data()));

        Assert.Equal(package.StateTick, clone.StateTick);
        var item = Assert.Single(clone.BodyList);
        Assert.Equal(7, item.CreatureId);
        Assert.Equal(new Vector3(1f, 2f, 3f), item.Position);
        Assert.Equal(Quaternion.Identity, item.Rotation);
        Assert.Equal(new Vector3(4f, 5f, 6f), item.Velocity);
        Assert.Equal(new Vector2(0.1f, 0.2f), item.LookAngles);
        Assert.Equal(new Vector3(7f, 8f, 9f), item.FlyOrder);
    }

    [Fact]
    public void FullBodySnapshotExceedingMtuCanBeSplitIntoFittingChunks()
    {
        const int maxPacketSize = 1428;
        var full = new SubsystemBodyPackage
        {
            PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate
        };
        for (var i = 0; i < 80; i++)
        {
            full.BodyList.Add(new SubsystemBodyPackage.BodyItem
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
            });
        }

        Assert.True(SerializeSize([full]) > maxPacketSize);

        var chunks = Split(full, maxPacketSize);

        Assert.True(chunks.Count > 1);
        Assert.Equal(80, chunks.Sum(chunk => chunk.BodyList.Count));
        Assert.All(chunks, chunk => Assert.True(SerializeSize([chunk]) <= maxPacketSize));
    }

    private static List<SubsystemBodyPackage> Split(SubsystemBodyPackage bodyPackage, int maxPacketSize)
    {
        var chunks = new List<SubsystemBodyPackage>();
        var chunk = new SubsystemBodyPackage
        {
            PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate
        };

        foreach (var item in bodyPackage.BodyList)
        {
            chunk.BodyList.Add(item);
            if (SerializeSize([chunk]) <= maxPacketSize)
            {
                continue;
            }

            chunk.BodyList.RemoveAt(chunk.BodyList.Count - 1);
            chunks.Add(chunk);
            chunk = new SubsystemBodyPackage
            {
                PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate
            };
            chunk.BodyList.Add(item);
        }

        if (chunk.BodyList.Count > 0)
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    private static int SerializeSize(IEnumerable<IPackage> packages)
    {
        var writer = new PackageStreamWriter
        {
            IsServer = true
        };
        foreach (var package in packages)
        {
            writer.Write(0x5A);
            writer.Write(package.ID);
            package.WriteData(writer);
        }

        return writer.Data(CommonLib.CompressionPolicy.None).Length;
    }
}
