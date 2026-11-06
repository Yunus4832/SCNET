using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;
using Engine.Core.Security;

using Game.Characters;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Commands;

[Collection(NodeRoleCollection.Name)]
public class SpawnMarksTest
{
    [Theory]
    [InlineData(NodeRole.Authority, true)]
    [InlineData(NodeRole.Client, false)]
    public void SpawnCopiesAreAuthoritativeOrdinaryMarksAndNeverTrackLaterChanges(NodeRole worldRole, bool initialized)
    {
        var previous = CommonLib.Net.Role;
        var previousClients = CommonLib.Net.Clients.Values.ToArray();
        try
        {
            CommonLib.Net.Role = worldRole;
            using var project = new MarkProject(worldRole);
            var marksSubsystem = project.Marks;
            var players = new SubsystemCharacters { GlobalSpawnPosition = new Vector3(1f, 60f, 1f) };
            var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
            player.SubsystemCharacters = players;
            typeof(CharacterData).GetField(
                "<Project>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!.SetValue(player, project);
            player.CharacterId = Guid.NewGuid();
            Fixtures.CharacterTestEnvironment.SetSyntheticOwner(
                player,
                new PlayerAddress(
                    IdentityAddress.Encode(
                        Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000")
                    )
                )
            );
            var controller = new PlayerAddress(
                IdentityAddress.Encode(
                    Convert.FromHexString("0100000000000000000000000000000000000000000000000000000000000000")
                )
            );
            Fixtures.ConnectionTestEnvironment.SetRoster(
                project.Node,
                (new Dictionary<int, Client>
                    { [1] = new Client(null, 1, Guid.NewGuid(), player.CharacterId) { PlayerAddress = controller } })
                .Values
            );
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
            CommonLib.Net.Role = previous;
            Fixtures.ConnectionTestEnvironment.SetRoster(CommonLib.Net, previousClients);
        }
    }

    private sealed class MarkProject : EntitySystem.Core.Project
    {
        public SubsystemPositionMarks Marks { get; } = new();

        public NetNode Node { get; }

        public MarkProject(NodeRole role)
        {
            Node = new NetNode(
                NetNodeOptions.Default,
                true,
                Game.Network.Handling.PackageHandlerCatalog.Empty,
                role
            );
            Node.Runtime.BindProject(this);
            Fixtures.RuleHookTestEnvironment.BindEmpty(this);
            subsystems.Add(Marks);
        }
    }
}
