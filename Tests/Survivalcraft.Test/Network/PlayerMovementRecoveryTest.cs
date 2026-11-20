using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using Game.Characters;
using Game.Components;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class PlayerMovementRecoveryTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LostMovementSnapshotIsRestoredByNextUnchangedSnapshot(bool mounted)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
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
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player.CharacterData, project);
        // Discard the first packet just as a lost datagram would be discarded.
        _ = CharacterPackageBuilder.Capture(player, ComponentCharacterPackage.CharacterAction.BodyUpdate);
        var retry = CharacterPackageBuilder.Capture(player, ComponentCharacterPackage.CharacterAction.BodyUpdate);
        var writer = ProtocolMessageCodec.Encode(retry);
        var reader = writer;
        var received = Assert.IsType<CharacterStatePackage>(ProtocolMessageCodec.Decode(reader));

        Assert.True(received.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.PositionChange));
        Assert.True(received.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.RotationChange));
        Assert.True(received.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.VelocityChange));
        Assert.Equal(mounted, received.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.ParentBodyChange));
        Assert.Equal(body.Position, received.Position);
        Assert.Equal(body.Rotation, received.Rotation);
        Assert.Equal(Vector3.Zero, received.Velocity);
        Assert.Equal(locomotion.LookAngles, mounted ? received.ChildLookAngles : received.LookAngles);
    }
}
