using Engine.Core.Runtime;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Commands;
using Game.Components;
using Game.Modding;
using Game.Network;
using Game.Network.Transport;
using Game.Players;
using Game.Protocol.Connection;
using Game.Protocol.Identity;
using Game.Protocol.Messaging;
using Game.Subsystems;
using Game.World;

namespace TerritoryStoneMod;

public sealed class TerritorySubsystem : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public const string DataKey = "territories";

    private WorldRuntimeContext Runtime => WorldRuntimeContext.For(Project);

    public TerritoryStore Store { get; } = new();

    public int SettingsResponseVersion { get; private set; }

    public int SettingsRequestId { get; private set; }

    public (int RequestId, PlayerAddress Owner, bool Accepted, TerritorySettings Settings, string Error)?
        LastSettingsResponse { get; private set; }

    private IModNetwork? _network;

    private NetNode? _node;

    public TerritoryAccessRequests AccessRequests { get; } = new();

    private readonly Dictionary<Guid, TerritoryAccessRequest> _incomingRequests = new();

    private double _nextRequestCleanup;

    public IReadOnlyCollection<TerritoryAccessRequest> IncomingRequests => _incomingRequests.Values;

    public event Action<TerritoryProjectileHit>? ProjectileHitReceived;

    public event Action<ComponentCharacter, string, Territory?>? ProtectionNoticeRequested;

    public void ReceiveProjectileHit(TerritoryProjectileHit hit) => ProjectileHitReceived?.Invoke(hit);

    public bool RequestProtectionNotice(ComponentCharacter character, string key, Territory? territory)
    {
        if (!ReferenceEquals(character.Project, Project) ||
            !character.IsLocallyControlled ||
            ProtectionNoticeRequested is null)
        {
            return false;
        }

        ProtectionNoticeRequested.Invoke(character, key, territory);
        return true;
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public string OwnerName(PlayerAddress owner)
    {
        var name = Project.FindSubsystem<SubsystemPlayers>()?.Store.Find(owner)?.DisplayName;
        var address = owner.Value[..14] + "…";
        return string.IsNullOrWhiteSpace(name) ? address : $"{name} · {address}";
    }

    private WorldConnection? OnlineUser(PlayerAddress address)
    {
        var session = WorldConnectionQuery.Find(Project, address);
        return session is not null && Game.Players.WorldConnectionResolver.FindPlayer(session) is { Ban: null }
            ? session
            : null;
    }

    private bool IsAdmitted(WorldPlayer subject)
    {
        return subject.Ban is null &&
               ReferenceEquals(
                   Project.FindSubsystem<SubsystemPlayers>()?.Store.Find(subject.Address),
                   subject
               ) &&
               OnlineUser(subject.Address) is { } connection &&
               ReferenceEquals(Game.Players.WorldConnectionResolver.FindPlayer(connection), subject);
    }

    public CommandResult RequestAccess(WorldPlayer user, string ownerAddress)
    {
        if (!IsAdmitted(user)) { return TerritoryCommands.Result("Unavailable", false); }

        var matches = Store.Territories.Where(territory => territory.Owner.Value == ownerAddress).ToArray();
        if (matches.Length != 1)
        {
            return TerritoryCommands.Result("UnknownOwner", false);
        }

        var territory = matches[0];
        if (territory.Grants.ContainsKey(user.Address))
        {
            return TerritoryCommands.Result("AlreadyGranted", false);
        }

        if (OnlineUser(territory.Owner) == null)
        {
            return TerritoryCommands.Result("OwnerOffline", false);
        }

        if (territory.Grants.Count >= 128)
        {
            return TerritoryCommands.Result("QueueFull", false);
        }

        if (!AccessRequests.TryAdd(
                territory.Owner,
                user.Address,
                OwnerName(user.Address),
                Time.RealTime,
                out var request,
                out var error
            ))
        {
            return TerritoryCommands.Result(error, false);
        }

        SendRequest(request!, false);
        return TerritoryCommands.Result("Requested", true);
    }

    public CommandResult DecideAccess(WorldPlayer owner, Guid id, string permissionText)
    {
        if (!IsAdmitted(owner)) { return TerritoryCommands.Result("Unavailable", false); }

        if (!TerritoryPermissions.TryParse(permissionText, out var permissions))
        {
            return TerritoryCommands.Result("InvalidPermissions", false);
        }

        var pending = AccessRequests.Pending.FirstOrDefault(request => request.Id == id);
        if (pending == null ||
            pending.Owner != owner.Address ||
            !Store.TryGet(owner.Address, out var territory) ||
            OnlineUser(pending.User) == null)
        {
            return TerritoryCommands.Result("RequestExpired", false);
        }

        if (permissions != TerritoryPermission.None &&
            territory!.Grants.Count >= 128 &&
            !territory.Grants.ContainsKey(pending.User))
        {
            return TerritoryCommands.Result("QueueFull", false);
        }

        if (!AccessRequests.TryResolve(id, owner.Address, permissions, Time.RealTime, out var resolved))
        {
            return TerritoryCommands.Result("RequestExpired", false);
        }

        if (permissions != TerritoryPermission.None)
        {
            var existing = territory!.PermissionsFor(resolved!.User);
            territory.SetGrant(resolved.User, resolved.UserName, existing | permissions);
            PublishUpdate(territory);
        }

        SendRequest(resolved!, true);
        NotifyUser(
            resolved!.User,
            permissions == TerritoryPermission.None ? "Rejected" : "Granted",
            OwnerName(owner.Address),
            permissions.ToString().ToLowerInvariant().Replace(" ", "")
        );
        return TerritoryCommands.Result("Processed", true);
    }

    public CommandResult ChangeGrant(WorldPlayer owner, PlayerAddress player, string permissionText)
    {
        if (!IsAdmitted(owner)) { return TerritoryCommands.Result("Unavailable", false); }

        if (!Store.TryGet(owner.Address, out var territory) ||
            !territory!.Grants.TryGetValue(player, out var grant))
        {
            return TerritoryCommands.Result("MissingGrant", false);
        }

        if (!TerritoryPermissions.TryParse(permissionText, out var permissions))
        {
            return TerritoryCommands.Result("InvalidPermissions", false);
        }

        territory.SetGrant(player, grant.Name, permissions);
        PublishUpdate(territory);
        NotifyUser(player, "GrantChanged", OwnerName(owner.Address));
        return TerritoryCommands.Result("Processed", true);
    }

    public void RemoveAccessRequest(Guid id)
    {
        _incomingRequests.Remove(id);
    }

    public void ReceiveAccessRequest(TerritoryAccessRequest request)
    {
        if (_incomingRequests.Count < 8 &&
            Game.Network.WorldNetworkBinding.For(Runtime).Node!.Self?.PlayerAddress == request.Owner)
        {
            _incomingRequests[request.Id] = request;
        }
    }

    private void SendRequest(TerritoryAccessRequest request, bool removed)
    {
        if (OnlineUser(request.Owner) is not { } owner)
        {
            return;
        }

        if (removed)
        {
            _network?.Send(
                Runtime,
                TerritoryMessages.AccessRequestRemoved,
                new TerritoryAccessRequestRemoval(request.Id),
                PackageAudience.To(owner),
                ClientState.ProjectLoaded
            );
        }
        else
        {
            _network?.Send(
                Runtime,
                TerritoryMessages.AccessRequest,
                new TerritoryAccessRequestNotice(request.Id, request.Owner, request.User, request.UserName),
                PackageAudience.To(owner),
                ClientState.ProjectLoaded
            );
        }
    }

    private void NotifyUser(PlayerAddress playerId, string key, params string[] arguments)
    {
        if (OnlineUser(playerId) is not { } player)
        {
            return;
        }

        var message = Game.Messaging.GameMessageLocalization.System(
            "TerritoryAccess",
            key,
            arguments,
            presentation: GameMessagePresentation.History | GameMessagePresentation.Toast
        );
        var messages = Project.FindSubsystem<SubsystemMessages>(true)!.Service;
        messages.Publish(message, [player.ID], includePublisher: false);
    }

    public void Update(float dt)
    {
        foreach (var request in _incomingRequests.Values.Where(request => request.ExpiresAt <= Time.RealTime).ToArray())
        {
            _incomingRequests.Remove(request.Id);
        }

        if (Runtime.IsAuthority && Time.RealTime >= _nextRequestCleanup)
        {
            _nextRequestCleanup = Time.RealTime + 1;
            foreach (var request in AccessRequests.Expire(
                         Time.RealTime,
                         request =>
                             OnlineUser(request.Owner) == null ||
                             OnlineUser(request.User) == null ||
                             !Store.TryGet(request.Owner, out _)
                     ))
            {
                SendRequest(request, true);
                NotifyUser(request.User, "Expired");
            }
        }
    }


    public void PublishProjectileHit(TerritoryProjectileHit hit)
    {
        if (!Runtime.IsAuthority || Runtime.IsClosed) { return; }

        var recipients = Project.FindSubsystem<SubsystemNetworkInterest>(true)!
            .GetObserversWithin(new Engine.Core.Mathematics.Vector2(hit.X, hit.Z), 32f).ToArray();
        if (recipients.Length == 0) { return; }

        _network?.Send(
            Runtime,
            TerritoryMessages.ProjectileHit,
            hit,
            PackageAudience.To(recipients),
            ClientState.ProjectLoaded
        );
    }

    public void BindNetwork(IModNetwork network)
    {
        _network = network;
    }

    public void DetachNetwork()
    {
        if (_node != null)
        {
            _node.OnClientBecameLive -= SendSnapshot;
            _node = null;
        }

        _network = null;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (Runtime.IsAuthority)
        {
            Store.Load(Project.ExtensionData.Get(ModEntry.ModId, DataKey));
        }

        if (Runtime.IsAuthority && _network != null)
        {
            _node = Game.Network.WorldNetworkBinding.For(Runtime).Node!;
            _node.OnClientBecameLive += SendSnapshot;
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        if (Project.SendToClientMode || !Runtime.IsAuthority)
        {
            return;
        }

        var data = Project.ExtensionData.Get(ModEntry.ModId, DataKey);
        data.Clear();
        data.ApplyOverrides(Store.Save());
    }

    public override void Dispose()
    {
        DetachNetwork();
        ProjectileHitReceived = null;
        ProtectionNoticeRequested = null;
        AccessRequests.Clear();
        _incomingRequests.Clear();
        Store.Load(new ValuesDictionary());
    }

    public void PublishUpdate(Territory territory)
    {
        if (Runtime.IsAuthority)
        {
            _network?.Send(
                WorldRuntimeContext.For(Project),
                TerritoryMessages.Update,
                TerritoryMessages.Capture(territory),
                PackageAudience.Global,
                ClientState.ProjectLoaded
            );
        }
    }

    public void PublishRemoval(PlayerAddress owner)
    {
        if (Runtime.IsAuthority)
        {
            _network?.Send(
                WorldRuntimeContext.For(Project),
                TerritoryMessages.Remove,
                new TerritoryRemoval(owner),
                PackageAudience.Global,
                ClientState.ProjectLoaded
            );
        }
    }

    public string? ChangeSettings(WorldConnection? actor, Territory territory, TerritorySettings settings)
    {
        if (!TerritoryMessages.CanChangeSettings(Runtime, actor, territory.Owner) ||
            !ReferenceEquals(
                Project.FindSubsystem<SubsystemPlayers>()?.Store.Find(territory.Owner),
                actor is null ? null : Game.Players.WorldConnectionResolver.FindPlayer(actor)
            ))
        {
            return "OwnerOnly";
        }

        if (!Runtime.IsAuthority)
        {
            var server = Game.Network.WorldNetworkBinding.For(Runtime).Node!.Server;
            if (server == null || _network == null)
            {
                return "Unavailable";
            }

            SettingsRequestId = SettingsRequestId == int.MaxValue ? 1 : SettingsRequestId + 1;
            var requestId = SettingsRequestId;
            _network.Send(
                WorldRuntimeContext.For(Project),
                TerritoryMessages.Settings,
                new TerritorySettingsRequest(requestId, territory.Owner, settings),
                PackageAudience.To(server),
                ClientState.ProjectLoaded
            );
            return null;
        }

        if (!Store.TryChangeSettings(territory.Owner, settings, out var error))
        {
            return error;
        }

        PublishUpdate(territory);
        return null;
    }

    public void ReceiveSettingsResponse(
        int requestId,
        PlayerAddress owner,
        bool accepted,
        TerritorySettings settings,
        string error
    )
    {
        if (!settings.IsValid)
        {
            throw new InvalidDataException("Invalid territory settings response.");
        }

        LastSettingsResponse = (requestId, owner, accepted, settings, error);
        SettingsResponseVersion++;
    }

    private void SendSnapshot(WorldConnection client)
    {
        _network?.Send(
            WorldRuntimeContext.For(Project),
            TerritoryMessages.Snapshot,
            new TerritoryWorldSnapshot(Store.Territories.Select(TerritoryMessages.Capture).ToArray()),
            PackageAudience.To(client),
            ClientState.ProjectLoaded
        );
    }
}
