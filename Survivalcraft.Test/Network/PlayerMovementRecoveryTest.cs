using System.Runtime.CompilerServices;

using Engine.Core;

using Game;
using Game.Components;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public sealed class PlayerMovementRecoveryTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LostMovementSnapshotIsRestoredByNextUnchangedSnapshot(bool mounted)
    {
        var locomotion = new ComponentLocomotion { LookAngles = new Vector2(0.2f, 0.3f) };
        var body = new ComponentBody
        {
            Position = new Vector3(600f, 70f, -100f),
            Rotation = Quaternion.Identity,
            Velocity = Vector3.Zero,
            Locomotion = locomotion
        };
        var player = new ComponentPlayer
        {
            PlayerData = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData)),
            ComponentBody = mounted ? new ComponentBody { ParentBody = body } : body,
            ComponentLocomotion = locomotion
        };
        // Discard the first packet just as a lost datagram would be discarded.
        _ = new ComponentPlayerPackage(player, ComponentPlayerPackage.PlayerAction.BodyUpdate);
        var retry = new ComponentPlayerPackage(player, ComponentPlayerPackage.PlayerAction.BodyUpdate);
        using var writer = new PackageStreamWriter();
        retry.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var received = new ComponentPlayerPackage();

        received.ReadData(reader);

        Assert.True(received.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.PositionChange));
        Assert.True(received.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.RotationChange));
        Assert.True(received.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.VelocityChange));
        Assert.Equal(mounted, received.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.ParentBodyChange));
        Assert.Equal(body.Position, received.Position);
        Assert.Equal(body.Rotation, received.Rotation);
        Assert.Equal(Vector3.Zero, received.Velocity);
        Assert.Equal(locomotion.LookAngles, mounted ? received.ChildLookAngles : received.LookAngles);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }
}
