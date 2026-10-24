using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;
using Engine.Core.Security;

using Game;
using Game.Network;
using Game.Protocol.Identity;
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
        var previousClients = CommonLib.Net.Clients;
        try
        {
            CommonLib.WorkType = workType;
            using var project = new MarkProject();
            var marksSubsystem = project.Marks;
            var players = new SubsystemCharacters { GlobalSpawnPosition = new Vector3(1f, 60f, 1f) };
            var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
            player.SubsystemCharacters = players;
            typeof(CharacterData).GetField(
                "<Project>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!.SetValue(player, project);
            player.CharacterId = Guid.NewGuid();
            player.OwnerAddress = new PlayerAddress(
                IdentityAddress.Encode(
                    Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000")
                )
            );
            var controller = new PlayerAddress(
                IdentityAddress.Encode(
                    Convert.FromHexString("0100000000000000000000000000000000000000000000000000000000000000")
                )
            );
            CommonLib.Net.Clients = new Dictionary<int, Client>
                { [1] = new Client(null, 1, Guid.NewGuid(), player.CharacterId, null) { PlayerAddress = controller } };
            var marks = marksSubsystem.GetPrivateMarks(controller);
            player.InitializeSpawnMarks();
            Assert.Empty(marks.Names);
            Assert.Empty(marksSubsystem.PublicMarks.Names);
            var original = new Vector3(2f, 65f, 3f);
            player.SpawnPosition = original;
            player.InitializeSpawnMarks();
            Assert.Equal(initialized, marks.TryGet("spawn", out var personal));
            Assert.Equal(initialized, marksSubsystem.PublicMarks.TryGet("spawn", out var shared));
            if (!initialized)
            {
                return;
            }

            Assert.Equal(original, personal);
            Assert.Empty(marksSubsystem.GetPrivateMarks(player.OwnerAddress).Names);
            Assert.Equal(original, shared);
            Assert.Equal(new Vector3(1f, 60f, 1f), players.GlobalSpawnPosition);
            player.SpawnPosition = new Vector3(10f, 70f, 20f);
            player.InitializeSpawnMarks();
            Assert.True(marks.TryGet("spawn", out personal));
            Assert.Equal(original, personal);
            Assert.True(marksSubsystem.PublicMarks.TryGet("spawn", out shared));
            Assert.Equal(original, shared);
            var overwritten = new Vector3(30f, 65f, 30f);
            marks.Set("SPAWN", overwritten);
            marksSubsystem.PublicMarks.Set("spawn", overwritten);
            player.InitializeSpawnMarks();
            Assert.True(marks.TryGet("spawn", out personal));
            Assert.Equal(overwritten, personal);
            var restored = new PositionMarks();
            restored.Load(marksSubsystem.PublicMarks.Save());
            Assert.True(restored.TryGet("spawn", out shared));
            Assert.Equal(overwritten, shared);
            Assert.Equal(new Vector3(10f, 70f, 20f), player.SpawnPosition);
        }
        finally
        {
            CommonLib.WorkType = previous;
            CommonLib.Net.Clients = previousClients;
        }
    }

    private sealed class MarkProject : EntitySystem.Core.Project
    {
        public SubsystemPositionMarks Marks { get; } = new();

        public MarkProject() => subsystems.Add(Marks);
    }
}
