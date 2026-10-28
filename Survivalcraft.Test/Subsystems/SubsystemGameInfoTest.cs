using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Test.Subsystems;

public sealed class SubsystemGameInfoTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void HostingOverrideDoesNotChangeSavedWorldSettings(bool sendToClient, bool expected)
    {
        using var project = new Project { SendToClientMode = sendToClient };
        var subsystem = new SubsystemGameInfo { WorldSettings = new WorldSettings { RunServer = false } };
        WorldSubsystemTestSupport.Attach(project, subsystem);
        subsystem.ApplyRunServerOverride(true);
        var values = new ValuesDictionary();
        subsystem.Save(values);
        Assert.True(subsystem.WorldSettings.RunServer);
        Assert.Equal(expected, values.GetValue<bool>("RunServer"));
    }

    [Theory]
    [InlineData(false, GameMode.Survival)]
    [InlineData(true, GameMode.Creative)]
    public void GameModeOverrideUsesEffectiveModeForClientsAndPersistedModeForStorage(
        bool sendToClient,
        GameMode expected
    )
    {
        var subsystem = new SubsystemGameInfo
        {
            WorldSettings = new WorldSettings
            {
                Name = "TestWorld",
                GameMode = GameMode.Survival
            }
        };
        using var project = new Project { SendToClientMode = sendToClient };
        WorldSubsystemTestSupport.Attach(project, subsystem);
        subsystem.ApplyGameModeOverride(GameMode.Creative);

        var values = new ValuesDictionary();
        subsystem.Save(values);

        Assert.Equal(GameMode.Creative, subsystem.WorldSettings.GameMode);
        Assert.Equal(expected, values.GetValue<GameMode>("GameMode"));
    }
}
