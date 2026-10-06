using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core;

using Game;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Serialization;

using LiteNetLib;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class SnapshotBudgetTransportTest
{
    [Fact]
    public void OversizedSnapshotBatchDefersWithoutBlockingLifecycleAndThenDrains()
    {
        var node = new NetNode();
        var receiverListener = new EventBasedNetListener();
        var receiver = new NetManager(receiverListener)
        {
            ChannelsCount = node.NetManager.ChannelsCount,
            UseSafeMtu = true
        };
        var previousClients = CommonLib.Net.Clients;
        var previousMode = CommonLib.WorkType;
        var receivedPlayers = new HashSet<byte>();
        var receivedRemovals = new List<int>();
        var receivedDatagrams = 0;
        NetPeer? senderPeer = null;
        node.Listener.ConnectionRequestEvent += request => request.AcceptIfKey("snapshot-budget-test");
        node.Listener.PeerConnectedEvent += peer => senderPeer = peer;
        receiverListener.NetworkReceiveEvent += (_, data, _) =>
        {
            receivedDatagrams++;
            using var reader = CommonLib.GetReader(data);
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                Assert.Equal(0x88, reader.ReadByte());
                var id = reader.ReadByte();
                if (id == (byte)PackageType.ComponentPlayer)
                {
                    var package = new ComponentPlayerPackage();
                    package.ReadData(reader);
                    receivedPlayers.Add(package.FromPlayerId);
                }
                else
                {
                    Assert.Equal((byte)PackageType.Entity, id);
                    var package = new EntityPackage();
                    package.ReadData(reader);
                    Assert.Equal(EntityPackage.EventType.Remove, package.Type);
                    receivedRemovals.Add(package.EntityId);
                }
            }
            data.Recycle();
        };

        void PumpUntil(Func<bool> condition)
        {
            var timer = Stopwatch.StartNew();
            while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(5))
            {
                node.NetManager.PollEvents();
                receiver.PollEvents();
                Thread.Sleep(5);
            }
            Assert.True(condition(), "Loopback transport did not reach the expected state within five seconds.");
        }

        try
        {
            Assert.True(node.NetManager.Start(0));
            Assert.True(receiver.Start(0));
            receiver.Connect(new IPEndPoint(IPAddress.Loopback, node.NetManager.LocalPort), "snapshot-budget-test");
            PumpUntil(() => senderPeer is not null);
            var observer = new Client(senderPeer, 1, Guid.NewGuid(), Guid.NewGuid(), null)
            {
                State = ClientState.Playing,
                ConnectionPhase = ConnectionPhase.Live
            };
            node.Clients.Add(observer.ID, observer);
            node.Self = node.Server = new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);
            node.CurrentStage = NetNode.Stage.Connected;
            CommonLib.WorkType = WorkType.Server;
            CommonLib.Net.Clients = new Dictionary<int, Client>();
            var expectedPlayers = new HashSet<byte>();
            for (byte id = 10; id < 42; id++)
            {
                var identity = new Client(null, id, Guid.NewGuid(), Guid.NewGuid(), null);
                CommonLib.Net.Clients.Add(id, identity);
                var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
                player.PlayerGUID = identity.GUID;
                node.QueuePackage(new ComponentPlayerPackage
                {
                    PlayerData = player,
                    Type = ComponentPlayerPackage.PlayerAction.BodyUpdate,
                    PackageChangeFlag = (ComponentPlayerPackage.ChangFlag)byte.MaxValue,
                    Position = new Vector3(id, 70f, -id),
                    Rotation = Quaternion.Identity,
                    Velocity = new Vector3(id, 1f, 2f),
                    LookAngles = new Vector2(0.1f, 0.2f),
                    ChildLookAngles = new Vector2(0.3f, 0.4f)
                }, PackageAudience.To(observer));
                expectedPlayers.Add(id);
            }
            node.QueuePackage(new EntityPackage(4242), PackageAudience.To(observer));
            var flush = typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!;
            flush.Invoke(node, null);
            Assert.InRange(node.PendingPackageCount, 1, 31);
            PumpUntil(() => receivedRemovals.Count == 1 && receivedPlayers.Count > 0);
            Assert.Equal(4242, Assert.Single(receivedRemovals));
            Assert.True(receivedPlayers.Count < expectedPlayers.Count);

            for (var attempt = 0; node.PendingPackageCount > 0 && attempt < 32; attempt++)
            {
                Thread.Sleep(60);
                flush.Invoke(node, null);
                node.NetManager.PollEvents();
                receiver.PollEvents();
            }
            Assert.Equal(0, node.PendingPackageCount);
            PumpUntil(() => receivedPlayers.SetEquals(expectedPlayers));
            Assert.True(receivedDatagrams > 2);
            var fanout = node.SendStatistics.GetFanout()
                .Single(statistic => statistic.PackageType == typeof(ComponentPlayerPackage));
            Assert.Equal(expectedPlayers.Count, fanout.Messages);
            Assert.Equal(expectedPlayers.Count, fanout.RecipientDeliveries);
            Assert.Equal(1, fanout.MaximumFanout);
        }
        finally
        {
            receiver.Stop();
            node.NetManager.Stop();
            CommonLib.Net.Clients = previousClients;
            CommonLib.WorkType = previousMode;
        }
    }
}
