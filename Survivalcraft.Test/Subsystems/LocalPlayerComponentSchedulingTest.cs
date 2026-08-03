using System.Runtime.CompilerServices;

using Engine.Core;

using Game;
using Game.Components;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;

namespace Survivalcraft.Test.Subsystems;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NetworkWorkTypeCollection
{
    public const string Name = nameof(NetworkWorkTypeCollection);
}

[Collection(NetworkWorkTypeCollection.Name)]
public sealed class LocalPlayerComponentSchedulingTest : IDisposable
{
    private readonly WorkType _previousWorkType = CommonLib.WorkType;

    private readonly RunModeType _previousRunMode = RunMode.Value;

    public void Dispose()
    {
        CommonLib.WorkType = _previousWorkType;
        RunMode.Value = _previousRunMode;
    }

    [Fact]
    public void RemoteClientGuiAndInputAreExcludedFromScheduling()
    {
        RunMode.Value = RunModeType.Gui;
        CommonLib.WorkType = WorkType.Client;
        var player = CreatePlayer(false);
        var gui = CreateGui(player);
        var input = new ComponentInput { ComponentPlayer = player };

        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(gui));
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(input));
        Assert.False(SubsystemDrawing.ShouldScheduleDrawable(gui));
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(player));
    }

    [Fact]
    public void MainClientAndLocalPlayersKeepLocalScheduling()
    {
        RunMode.Value = RunModeType.Gui;
        CommonLib.WorkType = WorkType.Client;
        var mainPlayer = CreatePlayer(true);
        var mainGui = CreateGui(mainPlayer);
        var mainInput = new ComponentInput { ComponentPlayer = mainPlayer };

        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(mainGui));
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(mainInput));
        Assert.True(SubsystemDrawing.ShouldScheduleDrawable(mainGui));

        CommonLib.WorkType = WorkType.Local;
        var localPlayer = CreatePlayer(false);
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(
            CreateGui(localPlayer)));
    }

    [Fact]
    public void GuiServerSchedulesOnlyMainPlayerGuiAndInput()
    {
        RunMode.Value = RunModeType.Gui;
        CommonLib.WorkType = WorkType.Server;
        var mainPlayer = CreatePlayer(true);
        var remotePlayer = CreatePlayer(false);

        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(CreateGui(mainPlayer)));
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(
            new ComponentInput { ComponentPlayer = mainPlayer }));
        Assert.True(SubsystemDrawing.ShouldScheduleDrawable(CreateGui(mainPlayer)));

        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(CreateGui(remotePlayer)));
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(
            new ComponentInput { ComponentPlayer = remotePlayer }));
        Assert.False(SubsystemDrawing.ShouldScheduleDrawable(CreateGui(remotePlayer)));
    }

    [Fact]
    public void HeadlessPlayersDoNotScheduleGuiOrInputEvenWhenMarkedMain()
    {
        RunMode.Value = RunModeType.HeadlessServer;
        CommonLib.WorkType = WorkType.Server;
        var player = CreatePlayer(true);
        var gui = CreateGui(player);
        var input = new ComponentInput { ComponentPlayer = player };

        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(gui));
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(input));
        Assert.False(SubsystemDrawing.ShouldScheduleDrawable(gui));
    }

    private static ComponentPlayer CreatePlayer(bool isMainPlayer)
    {
        var playerData = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
        if (isMainPlayer)
        {
            playerData.SetMain();
        }

        return new ComponentPlayer { PlayerData = playerData };
    }

    private static ComponentGui CreateGui(ComponentPlayer player)
    {
        var gui = (ComponentGui)RuntimeHelpers.GetUninitializedObject(typeof(ComponentGui));
        gui.ComponentPlayer = player;
        return gui;
    }
}
