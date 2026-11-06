using System.Reflection;
using System.Runtime.CompilerServices;

using Game.Characters;
using Game.Components;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Messaging;

public sealed class ActorCapacityFeedbackTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityCapacityFeedbackReachesOnlyTheActualActorWithoutAServiceSideGui(bool egg)
    {
        using var worlds = new FeedbackWorlds();
        var behavior = worlds.CreateBehavior(worlds.Authority, egg);
        Report(behavior, worlds.Actor);
        var queued = Assert.Single(worlds.Pending);
        Assert.Same(worlds.Recipient, queued.Audience.SingleRecipient);
        Assert.False(queued.Audience.Includes(worlds.Stranger));
        worlds.Deliver();

        var toast = Assert.Single(worlds.Received);
        Assert.Equal(new GameMessageToastStyle(GameMessageTone.Normal, true, false), toast.ToastStyle);
        Assert.Equal(GameMessagePresentation.Toast, toast.Presentation);
        Assert.Null(toast.Sender);
        Assert.Empty(worlds.OtherReceived);
        Assert.Empty(worlds.Authority.FindSubsystem<SubsystemMessages>(true)!.Service.History);
        Assert.Null(worlds.Actor.Entity.FindComponent<ComponentGui>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientAndCrossWorldActorsCannotPublishAnAuthorityCapacityToast(bool egg)
    {
        using var worlds = new FeedbackWorlds();
        var clientActor = CreateCharacter(worlds.Replica, worlds.Recipient.CharacterId);
        Report(worlds.CreateBehavior(worlds.Replica, egg), clientActor);
        Report(worlds.CreateBehavior(worlds.Authority, egg), clientActor);
        Assert.Empty(worlds.Pending);
        Assert.Empty(worlds.Received);
        var wrongDirection = new MessagePackage(
            GameMessage.System("forged", presentation: GameMessagePresentation.Toast)
                with
                {
                    ToastStyle = new GameMessageToastStyle(GameMessageTone.Normal, true, false)
                }
        );
        new MessagePackageHandler().Handle(wrongDirection, worlds.AuthorityNode.CreateReceiveContext(worlds.Recipient));
        Assert.Empty(worlds.Authority.FindSubsystem<SubsystemMessages>(true)!.Service.History);
        Assert.Empty(worlds.AuthorityToasts);
    }

    [Fact]
    public void RealEggImpactAtCapacityUsesTheProjectileOwnerForItsFeedback()
    {
        using var worlds = new FeedbackWorlds();
        var behavior = (SubsystemEggBlockBehavior)worlds.CreateBehavior(worlds.Authority, true);
        var spawn = new SubsystemCreatureSpawn();
        worlds.Authority.InitializeSubsystem(spawn);
        var creatures = (Dictionary<ComponentCreature, bool>)Field(spawn, "_creatures")!;
        for (var index = 0; index < 35000; index++) { creatures.Add(new ComponentCreature(), false); }

        SetField(behavior, "_subsystemCreatureSpawn", spawn);
        SetField(
            behavior,
            "_subsystemGameInfo",
            new SubsystemGameInfo { WorldSettings = new WorldSettings { GameMode = GameMode.Creative } }
        );
        var projectile = new Projectile { Owner = worlds.Actor };

        Assert.True(behavior.OnHitAsProjectile(null, null!, projectile));

        Assert.Same(worlds.Recipient, Assert.Single(worlds.Pending).Audience.SingleRecipient);
        worlds.Deliver();
        Assert.Equal("太多生物了", Assert.Single(worlds.Received).Content.PlainText);
        Assert.Empty(worlds.OtherReceived);
    }

    private static void Report(SubsystemBlockBehavior behavior, ComponentCharacter actor)
    {
        if (behavior is SubsystemBoatBlockBehavior boat) { boat.ReportCapacityLimit(actor); }
        else { ((SubsystemEggBlockBehavior)behavior).ReportCapacityLimit(actor); }
    }

    private static ComponentCharacter CreateCharacter(RuntimeTestProject project, Guid characterId)
    {
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(data, project);
        data.CharacterId = characterId;
        var character = new ComponentCharacter { CharacterData = data };
        project.InitializeComponent(character);
        return character;
    }

    private static object? Field(object owner, string name) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);

    private static void SetField(object owner, string name, object value)
    {
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    }

    private sealed class FeedbackWorlds : IDisposable
    {
        public RuntimeTestProject Authority { get; } = new(RuntimeSide.Authority);

        public RuntimeTestProject Replica { get; } = new(RuntimeSide.Client);

        private RuntimeTestProject OtherReplica { get; } = new(RuntimeSide.Client);

        public NetNode AuthorityNode => WorldNetworkBinding.For(Authority).RequireNode();

        public Client Recipient { get; } = new(null, 1, Guid.NewGuid(), Guid.NewGuid())
            { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };

        public Client Stranger { get; } = new(null, 2, Guid.NewGuid(), Guid.NewGuid())
            { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };

        private readonly List<InProcessMessageConnection> _links = [];

        public ComponentCharacter Actor { get; }

        public List<GameMessage> Received { get; } = [];

        public List<GameMessage> OtherReceived { get; } = [];

        public List<GameMessage> AuthorityToasts { get; } = [];

        public PendingLogicalMessages Pending => (PendingLogicalMessages)Field(AuthorityNode, "_pendingPackages")!;

        public FeedbackWorlds()
        {
            Authority.AddSubsystem(new SubsystemMessages());
            Replica.AddSubsystem(new SubsystemMessages());
            OtherReplica.AddSubsystem(new SubsystemMessages());
            Authority.FindSubsystem<SubsystemMessages>(true)!.Service.ToastRequested += AuthorityToasts.Add;
            Replica.FindSubsystem<SubsystemMessages>(true)!.Service.ToastRequested += Received.Add;
            OtherReplica.FindSubsystem<SubsystemMessages>(true)!.Service.ToastRequested += OtherReceived.Add;
            Link(Replica, Recipient);
            Link(OtherReplica, Stranger);
            Actor = CreateCharacter(Authority, Recipient.CharacterId);
        }

        public SubsystemBlockBehavior CreateBehavior(RuntimeTestProject project, bool egg)
        {
            SubsystemBlockBehavior behavior = egg ? new SubsystemEggBlockBehavior() : new SubsystemBoatBlockBehavior();
            project.InitializeSubsystem(behavior);
            return behavior;
        }

        private void Link(RuntimeTestProject replica, Client recipient)
        {
            AuthorityNode.AddClient(recipient);
            var receiver = WorldNetworkBinding.For(replica).RequireNode();
            var pair = InProcessMessageConnection.CreatePair(
                AuthorityNode,
                recipient,
                receiver,
                new Client(Guid.Empty)
            );
            _links.Add(pair.ToClient);
            _links.Add(pair.ToAuthority);
        }

        public void Deliver()
        {
            typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(AuthorityNode, null);
            foreach (var project in new[] { Replica, OtherReplica })
            {
                WorldNetworkBinding.For(project).Inbox.Drain(
                    10,
                    message => new MessagePackageHandler().Handle(
                        Assert.IsType<MessagePackage>(message.Package),
                        message.Context
                    )
                );
            }
        }

        public void Dispose()
        {
            foreach (var link in _links) { link.Dispose(); }

            OtherReplica.Dispose();
            Replica.Dispose();
            Authority.Dispose();
        }
    }
}
