using System.Reflection;
using System.Runtime.CompilerServices;

using Game.Characters;
using Game.Components;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class SleepAndAutoJumpInputTest
{
    [Fact]
    public void NoStandingSurfaceReturnsUnresolvedSleepResourceAndProtocolPreservesIt()
    {
        var sleep = new ComponentSleep();
        var character = new ComponentCharacter { ComponentBody = new ComponentBody() };
        Set(sleep, "_character", character);
        Assert.False(sleep.CanSleep(out var reason));
        Assert.Equal(new LocalizedText(nameof(ComponentSleep), "1", "1"), reason);
        var package = new SleepResultPackage { EntityId = 1, Reason = reason, Result = false };
        var decoded =
            Assert.IsType<SleepResultPackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(package)));
        Assert.Equal(reason, decoded.Reason);
    }

    [Fact]
    public void AutoJumpUsesLocalInputAndRejectsStaleInputAfterControlOwnershipChanges()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, true);
        var character = new ComponentCharacter { CharacterData = data };
        project.InitializeComponent(character);
        var autoJump = new ComponentAutoJump();
        Set(autoJump, "_componentCreature", character);
        character.SetControlInput(new CharacterControlInput { AutoJumpEnabled = true });
        Assert.True(autoJump.IsEnabled);
        character.SetControlInput(new CharacterControlInput { AutoJumpEnabled = false });
        Assert.False(autoJump.IsEnabled);
        character.SetControlInput(new CharacterControlInput { AutoJumpEnabled = true });
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, false);
        Assert.False(autoJump.IsEnabled);
        Assert.Throws<InvalidOperationException>(() =>
            character.SetControlInput(new CharacterControlInput { AutoJumpEnabled = true })
        );
        Set(autoJump, "_alwaysEnabled", true);
        Assert.True(autoJump.IsEnabled);
    }

    [Fact]
    public void NonCharacterAutoJumpIsControlledOnlyByItsRuleFlag()
    {
        var autoJump = new ComponentAutoJump();
        Set(autoJump, "_componentCreature", new ComponentCreature());
        Assert.False(autoJump.IsEnabled);
        Set(autoJump, "_alwaysEnabled", true);
        Assert.True(autoJump.IsEnabled);
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
}
