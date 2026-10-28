using System.Diagnostics;
using System.Net;
using System.Reflection;

using Engine.Core.Mathematics;
using Engine.Network;

using Game.Managers;
using Game.Network;
using Game.Network.Transport;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class SnapshotBudgetTransportTest
{
    [Theory]
    [InlineData(1)]
    [InlineData(1300)]
    public async Task OversizedSnapshotBatchDefersWithoutBlockingLifecycleAndThenDrains(int lifecycleCount)
    {
        var node = new NetNode();
        await using var sender = new NetworkHost();
        await using var receiver = new NetworkHost();
        var previousClients = CommonLib.Net.Clients;
        var previousMode = GameManager.Role;
        var receivedPlayers = new HashSet<byte>();
        var receivedRemovals = new List<int>();
        var receivedDatagrams = 0;
        NetworkConnection? senderPeer = null;
        sender.ConnectionRequested += request => request.Accept();
        sender.Connected += peer => senderPeer = peer;
        receiver.Received += (_, data) =>
        {
            receivedDatagrams++;
            foreach (var message in ProtocolPacketCodec.Decode(data.Payload))
            {
                if (message is CharacterStatePackage character) { receivedPlayers.Add(character.ControllerSessionId); }
                else { receivedRemovals.Add(Assert.IsType<EntityRemovedPackage>(message).EntityId); }
            }
        };

        void PumpUntil(Func<bool> condition)
        {
            var timer = Stopwatch.StartNew();
            while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(5))
            {
                sender.DispatchEvents();
                receiver.DispatchEvents();
                Thread.Sleep(5);
            }

            Assert.True(condition(), "Loopback transport did not reach the expected state within five seconds.");
        }

        try
        {
            var connecting = receiver.ConnectAsync(
                new IPEndPoint(IPAddress.Loopback, sender.LocalPort),
                ReadOnlyMemory<byte>.Empty,
                CancellationToken.None
            );
            PumpUntil(() => senderPeer is not null && connecting.IsCompleted);
            await connecting;
            var observer = new Client(senderPeer, 1, Guid.NewGuid(), Guid.NewGuid(), null)
            {
                State = ClientState.Playing,
                ConnectionPhase = ConnectionPhase.Live
            };
            node.Clients.Add(observer.ID, observer);
            node.Self = node.Server = new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);
            node.Role = WorldRole.Server;
            node.CurrentStage = NetNode.Stage.Connected;
            CommonLib.Net.Role = WorldRole.Server;
            CommonLib.Net.Clients = new Dictionary<int, Client>();
            var expectedPlayers = new HashSet<byte>();
            for (byte id = 10; id < 42; id++)
            {
                var identity = new Client(null, id, Guid.NewGuid(), Guid.NewGuid(), null);
                CommonLib.Net.Clients.Add(id, identity);
                node.QueuePackage(
                    new CharacterStatePackage
                    {
                        ControllerSessionId = id,

                        PackageChangeFlag = (ComponentCharacterPackage.ChangFlag)byte.MaxValue,
                        Position = new Vector3(id, 70f, -id),
                        Rotation = Quaternion.Identity,
                        Velocity = new Vector3(id, 1f, 2f),
                        LookAngles = new Vector2(0.1f, 0.2f),
                        ChildLookAngles = new Vector2(0.3f, 0.4f)
                    },
                    PackageAudience.To(observer)
                );
                expectedPlayers.Add(id);
            }

            for (var index = 0; index < lifecycleCount; index++)
            {
                node.QueuePackage(new EntityRemovedPackage { EntityId = 4242 + index }, PackageAudience.To(observer));
            }

            var flush = typeof(NetNode).GetMethod(
                "FlushPendingPackages",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
            flush.Invoke(node, null);
            Assert.InRange(node.PendingPackageCount, 1, 31);
            PumpUntil(() => receivedRemovals.Count == lifecycleCount && receivedPlayers.Count > 0);
            Assert.Equal(Enumerable.Range(4242, lifecycleCount), receivedRemovals);
            Assert.True(receivedPlayers.Count < expectedPlayers.Count);

            for (var attempt = 0; node.PendingPackageCount > 0 && attempt < 32; attempt++)
            {
                Thread.Sleep(60);
                flush.Invoke(node, null);
                sender.DispatchEvents();
                receiver.DispatchEvents();
            }

            Assert.Equal(0, node.PendingPackageCount);
            PumpUntil(() => receivedPlayers.SetEquals(expectedPlayers));
            Assert.True(receivedDatagrams > 2);
            var fanout = node.SendStatistics.GetFanout()
                .Single(statistic => statistic.PackageType == typeof(CharacterStatePackage));
            Assert.Equal(expectedPlayers.Count, fanout.Messages);
            Assert.Equal(expectedPlayers.Count, fanout.RecipientDeliveries);
            Assert.Equal(1, fanout.MaximumFanout);
        }
        finally
        {
            CommonLib.Net.Clients = previousClients;
            CommonLib.Net.Role = previousMode;
        }
    }
}
