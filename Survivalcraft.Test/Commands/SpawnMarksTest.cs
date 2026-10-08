using System.Runtime.CompilerServices;

using Engine.Core;

using Game;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Commands;

[Collection(NetworkWorkTypeCollection.Name)]
public class SpawnMarksTest
{
    [Theory]
    [InlineData(WorkType.Local, true)]
    [InlineData(WorkType.Server, true)]
    [InlineData(WorkType.Client, false)]
    public void SpawnCopiesAreAuthoritativeOrdinaryMarksAndNeverTrackLaterChanges(WorkType workType, bool initialized)
    {
        var previous = CommonLib.WorkType;
        try
        {
            CommonLib.WorkType = workType;
            var players = new SubsystemPlayers { GlobalSpawnPosition = new Vector3(1f, 60f, 1f) };
            var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
            player.SubsystemPlayers = players;
            player.InitializeSpawnMarks();
            Assert.Empty(player.PrivateMarks.Names);
            Assert.Empty(players.PublicMarks.Names);
            var original = new Vector3(2f, 65f, 3f);
            player.SpawnPosition = original;
            player.InitializeSpawnMarks();
            Assert.Equal(initialized, player.PrivateMarks.TryGet("spawn", out var personal));
            Assert.Equal(initialized, players.PublicMarks.TryGet("spawn", out var shared));
            if (!initialized)
            {
                return;
            }

            Assert.Equal(original, personal);
            Assert.Equal(original, shared);
            Assert.Equal(new Vector3(1f, 60f, 1f), players.GlobalSpawnPosition);
            player.SpawnPosition = new Vector3(10f, 70f, 20f);
            player.InitializeSpawnMarks();
            Assert.True(player.PrivateMarks.TryGet("spawn", out personal));
            Assert.Equal(original, personal);
            Assert.True(players.PublicMarks.TryGet("spawn", out shared));
            Assert.Equal(original, shared);
            var overwritten = new Vector3(30f, 65f, 30f);
            player.PrivateMarks.Set("SPAWN", overwritten);
            players.PublicMarks.Set("spawn", overwritten);
            player.InitializeSpawnMarks();
            Assert.True(player.PrivateMarks.TryGet("spawn", out personal));
            Assert.Equal(overwritten, personal);
            var restored = new PositionMarks();
            restored.Load(players.PublicMarks.Save());
            Assert.True(restored.TryGet("spawn", out shared));
            Assert.Equal(overwritten, shared);
            Assert.Equal(new Vector3(10f, 70f, 20f), player.SpawnPosition);
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
    }
}
