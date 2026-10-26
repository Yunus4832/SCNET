using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using Game.Characters;
using Game.Components;

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
        var player = new ComponentCharacter
        {
            CharacterData = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData)),
            ComponentBody = mounted ? new ComponentBody { ParentBody = body } : body,
            ComponentLocomotion = locomotion
        };
        // Discard the first packet just as a lost datagram would be discarded.
        _ = CharacterPackageBuilder.Capture(player, ComponentCharacterPackage.CharacterAction.BodyUpdate);
        var retry = CharacterPackageBuilder.Capture(player, ComponentCharacterPackage.CharacterAction.BodyUpdate);
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(retry, writer);
        using var reader = new ProtocolReader(writer.Data());
        var received = Assert.IsType<CharacterStatePackage>(ProtocolMessageCodec.Read(retry.ID, reader));

        Assert.True(received.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.PositionChange));
        Assert.True(received.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.RotationChange));
        Assert.True(received.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.VelocityChange));
        Assert.Equal(mounted, received.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.ParentBodyChange));
        Assert.Equal(body.Position, received.Position);
        Assert.Equal(body.Rotation, received.Rotation);
        Assert.Equal(Vector3.Zero, received.Velocity);
        Assert.Equal(locomotion.LookAngles, mounted ? received.ChildLookAngles : received.LookAngles);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }
}
