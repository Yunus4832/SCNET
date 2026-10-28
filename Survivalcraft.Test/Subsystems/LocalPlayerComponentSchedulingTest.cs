using System.Runtime.CompilerServices;

using Engine.Core.Runtime;

using Game.Characters;
using Game.Components;
using Game.Managers;
using Game.Network;
using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Test.Subsystems;

[Collection(WorldRoleCollection.Name)]
public sealed class LocalPlayerComponentSchedulingTest : IDisposable
{
    private readonly WorldRole _previousWorldRole = GameManager.Role;

    private readonly RunModeType _previousRunMode = RunMode.Value;

    public void Dispose()
    {
        CommonLib.Net.Role = _previousWorldRole;
        RunMode.Value = _previousRunMode;
    }

    [Fact]
    public void RemoteClientGuiAndInputAreExcludedFromScheduling()
    {
        RunMode.Value = RunModeType.Gui;
        CommonLib.Net.Role = WorldRole.RemoteClient;
        var player = CreatePlayer(false);
        var gui = CreateGui(player);
        var input = new ComponentInput { ComponentCharacter = player };

        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(gui));
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(input));
        Assert.False(SubsystemDrawing.ShouldScheduleDrawable(gui));
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(player));
    }

    [Fact]
    public void OnlyMainGuiPlayerKeepsLocalScheduling()
    {
        RunMode.Value = RunModeType.Gui;
        CommonLib.Net.Role = WorldRole.RemoteClient;
        var localCharacter = CreatePlayer(true);
        var mainGui = CreateGui(localCharacter);
        var mainInput = new ComponentInput { ComponentCharacter = localCharacter };

        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(mainGui));
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(mainInput));
        Assert.True(SubsystemDrawing.ShouldScheduleDrawable(mainGui));

        CommonLib.Net.Role = WorldRole.SinglePlayer;
        var localLocalCharacter = CreatePlayer(true);
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(CreateGui(localLocalCharacter)));
        var additionalLocalPlayer = CreatePlayer(false);
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(CreateGui(additionalLocalPlayer)));
    }

    [Fact]
    public void GuiServerSchedulesOnlyLocalCharacterGuiAndInput()
    {
        RunMode.Value = RunModeType.Gui;
        CommonLib.Net.Role = WorldRole.Server;
        var localCharacter = CreatePlayer(true);
        var remotePlayer = CreatePlayer(false);

        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(CreateGui(localCharacter)));
        Assert.True(
            SubsystemUpdate.ShouldScheduleUpdateable(new ComponentInput { ComponentCharacter = localCharacter })
        );
        Assert.True(SubsystemDrawing.ShouldScheduleDrawable(CreateGui(localCharacter)));

        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(CreateGui(remotePlayer)));
        Assert.False(
            SubsystemUpdate.ShouldScheduleUpdateable(new ComponentInput { ComponentCharacter = remotePlayer })
        );
        Assert.False(SubsystemDrawing.ShouldScheduleDrawable(CreateGui(remotePlayer)));
    }

    [Fact]
    public void HeadlessPlayersDoNotScheduleGuiOrInputEvenWhenMarkedMain()
    {
        RunMode.Value = RunModeType.HeadlessServer;
        CommonLib.Net.Role = WorldRole.Server;
        var player = CreatePlayer(true);
        var gui = CreateGui(player);
        var input = new ComponentInput { ComponentCharacter = player };

        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(gui));
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(input));
        Assert.False(SubsystemDrawing.ShouldScheduleDrawable(gui));
    }

    private static ComponentCharacter CreatePlayer(bool isLocalCharacter)
    {
        var characterData = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        if (isLocalCharacter)
        {
            // This fixture tests scheduling flags, including deliberately invalid Headless UI states.
            typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(characterData, true);
        }

        return new ComponentCharacter { CharacterData = characterData };
    }

    private static ComponentGui CreateGui(ComponentCharacter player)
    {
        var gui = (ComponentGui)RuntimeHelpers.GetUninitializedObject(typeof(ComponentGui));
        gui.ComponentCharacter = player;
        return gui;
    }
}
