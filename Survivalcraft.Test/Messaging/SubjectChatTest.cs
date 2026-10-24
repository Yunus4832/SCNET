using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Security;

using EntitySystem.Core;

using Game;
using Game.Commands;
using Game.Messaging;
using Game.Network;
using Game.Network.Transport;
using Game.Players;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.Teams;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Messaging;

[Collection(NetworkWorkTypeCollection.Name)]
public sealed class SubjectChatTest : IDisposable
{
    private readonly WorkType _previousWorkType = CommonLib.WorkType;

    private readonly Dictionary<int, Client> _previousClients = CommonLib.Net.Clients;

    private readonly Client? _previousSelf = CommonLib.Net.Self;

    private readonly Client? _previousServer = CommonLib.Net.Server;

    private readonly NetNode.Stage _previousStage = CommonLib.Net.CurrentStage;

    private readonly ChatProject _project = new();

    private readonly List<OutboundPackage> _pending = (List<OutboundPackage>)typeof(NetNode)
        .GetField("_pendingPackages", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(CommonLib.Net)!;

    private readonly HashSet<PlayerAddress> _ownedSubjects = [];

    public SubjectChatTest()
    {
        CommonLib.WorkType = WorkType.Server;
        CommonLib.Net.Clients = [];
        CommonLib.Net.CurrentStage = NetNode.Stage.Connected;
    }

    [Fact]
    public void ActualSubjectAndControlledRoleAreCapturedIndependentlyOfContextAndOwnership()
    {
        var actor = User();
        actor.DisplayName = "Account";
        var owner = User();
        var role = Role(owner, "Character before rename");
        var actorSession = Session(1, actor, role.CharacterId);
        var unrelatedRole = Role(actor, "Wrong context role");
        var context = new CommandContext(
            CommandInvocationChannel.Network,
            CommandPrincipal.FromSubject(actor, unrelatedRole),
            _project
        );
        Assert.True(
            PlayerAndMessageCommandHandlers.SendChatMessage(
                context,
                new SendChatMessageCommand(GameMessageChannel.Global, "hello")
            ).Success
        );
        var sender = Assert.Single(_project.Messages.History).Sender!;
        Assert.Equal(actor.Address, sender.SubjectAddress);
        Assert.Equal("Account", sender.SubjectName);
        Assert.Equal(role.CharacterId, sender.CharacterId);
        Assert.Equal("Character before rename", sender.CharacterName);
        Assert.NotEqual(owner.Address, sender.SubjectAddress);
        actor.DisplayName = "Renamed account";
        role.Name = "Renamed character";
        actorSession.CharacterId = Guid.Empty;
        Assert.Equal("Account", sender.SubjectName);
        Assert.Equal("Character before rename", sender.CharacterName);
    }

    [Fact]
    public void CharacterlessSubjectCanChatButMutedAndBannedSubjectsCannot()
    {
        var actor = User();
        Session(1, actor);
        var context = Context(actor);
        Assert.True(
            PlayerAndMessageCommandHandlers.SendChatMessage(
                context,
                new SendChatMessageCommand(GameMessageChannel.Global, "hello")
            ).Success
        );
        var sender = Assert.Single(_project.Messages.History).Sender!;
        Assert.Equal(actor.Address, sender.SubjectAddress);
        Assert.Null(sender.CharacterId);
        Assert.Empty(sender.CharacterName);
        actor.IsMuted = true;
        Assert.Equal(
            "chat.muted",
            PlayerAndMessageCommandHandlers.SendChatMessage(
                context,
                new SendChatMessageCommand(GameMessageChannel.Global, "blocked")
            ).Code
        );
        actor.IsMuted = false;
        _project.Players.Store.Ban(actor.Address, "rule", null, DateTimeOffset.UtcNow);
        Assert.Equal(
            "chat.subject_required",
            PlayerAndMessageCommandHandlers.SendChatMessage(
                context,
                new SendChatMessageCommand(GameMessageChannel.Global, "blocked")
            ).Code
        );
        Assert.Single(_project.Messages.History);
    }

    [Fact]
    public void TeamChatRecipientsComeFromSubjectsAndIncludeCharacterlessMembers()
    {
        var actor = User();
        var teammate = User();
        var outsider = User();
        Session(1, actor);
        var memberSession = Session(2, teammate);
        var outsiderSession = Session(3, outsider);
        _project.Teams.Store.Replace(
            [new Team(Guid.NewGuid(), "Team", actor.Address, [actor.Address, teammate.Address])]
        );
        Assert.True(
            PlayerAndMessageCommandHandlers.SendChatMessage(
                Context(actor),
                new SendChatMessageCommand(GameMessageChannel.Team, "team only")
            ).Success
        );
        var outbound = Assert.Single(
            _pending,
            package => package.Package is MessagePackage message &&
                       message.GameMessage.Sender?.SubjectAddress == actor.Address
        );
        Assert.True(outbound.Audience.Includes(memberSession));
        Assert.False(outbound.Audience.Includes(outsiderSession));
        Assert.Single(_project.Messages.History);
        Assert.Equal(
            "chat.team_required",
            PlayerAndMessageCommandHandlers.SendChatMessage(
                Context(outsider),
                new SendChatMessageCommand(GameMessageChannel.Team, "no team")
            ).Code
        );
    }

    [Fact]
    public void MuteUsesActualActorRatherThanTheControlledCharactersOwner()
    {
        var actor = User();
        var owner = User();
        owner.IsMuted = true;
        var role = Role(owner, "Owned elsewhere");
        Session(1, actor, role.CharacterId);
        Assert.True(
            PlayerAndMessageCommandHandlers.SendChatMessage(
                Context(actor),
                new SendChatMessageCommand(GameMessageChannel.Global, "allowed")
            ).Success
        );
        actor.IsMuted = true;
        owner.IsMuted = false;
        Assert.Equal(
            "chat.muted",
            PlayerAndMessageCommandHandlers.SendChatMessage(
                Context(actor),
                new SendChatMessageCommand(GameMessageChannel.Global, "blocked")
            ).Code
        );
        Assert.Single(_project.Messages.History);
    }

    private CommandContext Context(WorldPlayer user) => new(
        CommandInvocationChannel.Network,
        CommandPrincipal.FromSubject(user),
        _project
    );

    private WorldPlayer User()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var address = new PlayerAddress(IdentityAddress.Encode(bytes));
        _ownedSubjects.Add(address);
        return _project.Players.Store.GetOrCreate(address);
    }

    private CharacterData Role(WorldPlayer owner, string name)
    {
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        player.CharacterId = Guid.NewGuid();
        player.OwnerAddress = owner.Address;
        player.Name = name;
        player.SubsystemCharacters = _project.Characters;
        var records =
            (List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.NonPublic | BindingFlags.Instance
            )!.GetValue(_project.Characters)!;
        records.Add(player);
        return player;
    }

    private Client Session(byte id, WorldPlayer subject, Guid characterId = default)
    {
        var session = new Client(null, id, Guid.NewGuid(), characterId, _project) { PlayerAddress = subject.Address };
        CommonLib.Net.Clients[id] = session;
        if (id == 1)
        {
            CommonLib.Net.Self = session;
            CommonLib.Net.Server = session;
        }

        return session;
    }

    public void Dispose()
    {
        _pending.RemoveAll(package =>
            package.Package is MessagePackage message &&
            message.GameMessage.Sender is { } sender &&
            _ownedSubjects.Contains(sender.SubjectAddress)
        );
        CommonLib.Net.Self = _previousSelf;
        CommonLib.Net.Server = _previousServer;
        CommonLib.Net.CurrentStage = _previousStage;
        CommonLib.Net.Clients = _previousClients;
        CommonLib.WorkType = _previousWorkType;
        _project.Dispose();
    }

    private sealed class ChatProject : Project
    {
        public SubsystemPlayers Players { get; } = new();

        public SubsystemCharacters Characters { get; } = new();

        public SubsystemPositionMarks Marks { get; } = new();

        public SubsystemTeams Teams { get; }

        public GameMessageService Messages { get; }

        public ChatProject()
        {
            Messages = new GameMessageService(this);
            var widgets = new SubsystemGameWidgets();
            typeof(SubsystemGameWidgets).GetField("_messages", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(widgets, Messages);
            Teams = TeamTestSupport.CreateSubsystem(Players.Store);
            subsystems.Add(Teams);
            subsystems.Add(Players);
            subsystems.Add(Characters);
            subsystems.Add(Marks);
            WorldSubsystemTestSupport.Attach(this, Players);
            WorldSubsystemTestSupport.Attach(this, Characters);
            WorldSubsystemTestSupport.Attach(this, Marks);
            subsystems.Add(widgets);
            subsystems.Add(new SubsystemGameInfo { WorldSettings = new WorldSettings() });
        }
    }
}
