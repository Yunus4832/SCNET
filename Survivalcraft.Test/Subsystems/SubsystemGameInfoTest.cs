using EntitySystem.TemplatesDatabase;

using Game;
using Game.Subsystems;

namespace Survivalcraft.Test.Subsystems;

public sealed class SubsystemGameInfoTest
{
    [Fact]
    public void GameModeOverrideDoesNotReplacePersistedMode()
    {
        var subsystem = new SubsystemGameInfo
        {
            WorldSettings = new WorldSettings
            {
                Name = "TestWorld",
                GameMode = GameMode.Survival
            }
        };
        subsystem.ApplyGameModeOverride(GameMode.Creative);

        var values = new ValuesDictionary();
        subsystem.Save(values);

        Assert.Equal(GameMode.Creative, subsystem.WorldSettings.GameMode);
        Assert.Equal(GameMode.Survival, values.GetValue<GameMode>("GameMode"));
    }
}
