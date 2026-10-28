using Engine.Core.Runtime;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Cameras;
using Game.Commands;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Network.Transport;
using Game.Players;
using Game.Protocol.Connection;
using Game.Protocol.Identity;
using Game.Protocol.Messaging;
using Game.Rendering;
using Game.Screens;
using Game.Subsystems;
using Game.World;

namespace TerritoryStoneMod;

public sealed class TerritorySubsystem : Subsystem, IDrawable, IUpdateable
{
    public const string DataKey = "territories";

    public TerritoryStore Store { get; } = new();

    public int SettingsResponseVersion { get; private set; }

    public int SettingsRequestId { get; private set; }

    public (int RequestId, PlayerAddress Owner, bool Accepted, TerritorySettings Settings, string Error)?
        LastSettingsResponse { get; private set; }

    private IModNetwork? _network;

    private NetNode? _node;

    private TerritoryBoundaryRenderer? _boundaryRenderer;

    public TerritoryAccessRequests AccessRequests { get; } = new();

    private readonly Dictionary<Guid, TerritoryAccessRequest> _incomingRequests = new();

    private double _nextRequestCleanup;

    private readonly Dictionary<Guid, double> _submittedRequests = new();

    private TerritoryPermissionsDialog? _approvalDialog;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public string OwnerName(PlayerAddress owner)
    {
        var name = Project.FindSubsystem<SubsystemPlayers>()?.Store.Find(owner)?.DisplayName;
        var address = owner.Value[..14] + "…";
        return string.IsNullOrWhiteSpace(name) ? address : $"{name} · {address}";
    }

    private Client? OnlineUser(PlayerAddress address)
    {
        var session = PlayerSessionQuery.Find(address);
        return session?.Subject is { Ban: null } ? session : null;
    }

    private bool IsAdmitted(WorldPlayer subject)
    {
        return subject.Ban is null &&
               ReferenceEquals(
                   Project.FindSubsystem<SubsystemPlayers>()?.Store.Find(subject.Address),
                   subject
               ) &&
               ReferenceEquals(OnlineUser(subject.Address)?.Subject, subject);
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
        _submittedRequests.Remove(id);
    }

    public void ReceiveAccessRequest(TerritoryAccessRequest request)
    {
        if (_incomingRequests.Count < 8 && CommonLib.Net.Self?.PlayerAddress == request.Owner)
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

        if (ReferenceEquals(owner, CommonLib.Net.Self) && RunMode.Value == RunModeType.Gui)
        {
            if (removed) { RemoveAccessRequest(request.Id); }
            else { ReceiveAccessRequest(request); }
        }
        else
        {
            if (removed)
            {
                _network?.Send(
                    TerritoryMessages.AccessRequestRemoved,
                    new TerritoryAccessRequestRemoval(request.Id),
                    PackageAudience.To(owner),
                    ClientState.ProjectLoaded
                );
            }
            else
            {
                _network?.Send(
                    TerritoryMessages.AccessRequest,
                    new TerritoryAccessRequestNotice(request.Id, request.Owner, request.User, request.UserName),
                    PackageAudience.To(owner),
                    ClientState.ProjectLoaded
                );
            }
        }
    }

    private void NotifyUser(PlayerAddress playerId, string key, params string[] arguments)
    {
        if (OnlineUser(playerId) is not { } player)
        {
            return;
        }

        var message =
            GameMessage.System(key, presentation: GameMessagePresentation.History | GameMessagePresentation.Toast) with
            {
                LocalizationSection = "TerritoryAccess",
                LocalizationKey = key,
                LocalizationArguments = arguments
            };
        var messages = Project.FindSubsystem<SubsystemGameWidgets>(true)!.Messages;
        if (ReferenceEquals(player, CommonLib.Net.Self) && RunMode.Value == RunModeType.Gui)
        {
            messages.DisplayLocal(message);
        }
        else
        {
            messages.Publish(message, [player.ID], includePublisher: false);
        }
    }

    public void Update(float dt)
    {
        if (GameManager.Role != WorldRole.RemoteClient && Time.RealTime >= _nextRequestCleanup)
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

        if (RunMode.Value != RunModeType.Gui ||
            CommonLib.Net.Self is not { PlayerAddress: not null } session ||
            !ReferenceEquals(GameManager.Project, Project))
        {
            return;
        }

        var player = session.CharacterData?.ComponentCharacter;
        var parent = player?.GameWidget.GuiWidget ?? ScreensManager.CurrentScreen;

        foreach (var request in _incomingRequests.Values.Where(request => request.ExpiresAt <= Time.RealTime).ToArray())
        {
            _incomingRequests.Remove(request.Id);
            _submittedRequests.Remove(request.Id);
        }

        if (_approvalDialog != null && !_incomingRequests.ContainsKey(_approvalDialog.RequestId))
        {
            DialogsManager.HideDialog(_approvalDialog);
            _approvalDialog = null;
        }

        if (_approvalDialog != null && !DialogsManager.ReadOnlyDialogs.Contains(_approvalDialog))
        {
            _approvalDialog = null;
        }

        if (_approvalDialog == null &&
            _incomingRequests.Values.FirstOrDefault(request =>
                !_submittedRequests.TryGetValue(request.Id, out var retryAt) || retryAt <= Time.RealTime
            ) is { } next &&
            !DialogsManager.HasDialogs(null) &&
            !DialogsManager.HasDialogs(parent) &&
            player?.ComponentGui.ModalPanelWidget == null)
        {
            _approvalDialog = new TerritoryPermissionsDialog(
                next.UserName,
                TerritoryPermission.None,
                TerritoryPermission.All,
                selected =>
                {
                    CommandGateway.SubmitSession(
                        session,
                        Project,
                        new DecideTerritoryAccessCommand(
                            next.Id,
                            selected.ToString().ToLowerInvariant().Replace(" ", "")
                        )
                    );
                    _submittedRequests[next.Id] = Time.RealTime + 10;
                },
                next.Id
            );
            DialogsManager.ShowDialog(parent, _approvalDialog);
        }
    }

    // Draw after sky (5) and transparent terrain (100), which otherwise cover
    // this depth-read-only overlay. Keep later weather/screen effects intact.
    public int[] DrawOrders => [101];

    public void Draw(Camera camera, int drawOrder)
    {
        foreach (var territory in Store.Territories)
        {
            if (!territory.Settings.ShowBoundary)
            {
                continue;
            }

            var bounds = territory.Bounds;
            var dx = Math.Max(bounds.Min.X - camera.ViewPosition.X, Math.Max(0, camera.ViewPosition.X - bounds.Max.X));
            var dz = Math.Max(bounds.Min.Z - camera.ViewPosition.Z, Math.Max(0, camera.ViewPosition.Z - bounds.Max.Z));
            if (dx * dx + dz * dz > 24 * 24 || !camera.ViewFrustum.Intersection(bounds))
            {
                continue;
            }

            _boundaryRenderer ??= new TerritoryBoundaryRenderer();
            _boundaryRenderer.Draw(territory, camera);
        }
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
        if (GameManager.Role != WorldRole.RemoteClient)
        {
            Store.Load(Project.ExtensionData.Get(ModEntry.ModId, DataKey));
        }

        if (GameManager.Role == WorldRole.Server && _network != null)
        {
            _node = CommonLib.Net;
            _node.OnClientBecameLive += SendSnapshot;
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        if (Project.SendToClientMode || GameManager.Role == WorldRole.RemoteClient)
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
        AccessRequests.Clear();
        _incomingRequests.Clear();
        _submittedRequests.Clear();
        if (_approvalDialog != null)
        {
            DialogsManager.HideDialog(_approvalDialog);
            _approvalDialog = null;
        }

        _boundaryRenderer?.Dispose();
        _boundaryRenderer = null;
        Store.Load(new ValuesDictionary());
    }

    public void PublishUpdate(Territory territory)
    {
        if (GameManager.Role == WorldRole.Server)
        {
            _network?.Send(
                TerritoryMessages.Update,
                TerritoryMessages.Capture(territory),
                PackageAudience.Global,
                ClientState.ProjectLoaded
            );
        }
    }

    public void PublishRemoval(PlayerAddress owner)
    {
        if (GameManager.Role == WorldRole.Server)
        {
            _network?.Send(
                TerritoryMessages.Remove,
                new TerritoryRemoval(owner),
                PackageAudience.Global,
                ClientState.ProjectLoaded
            );
        }
    }

    public string? ChangeSettings(Client? actor, Territory territory, TerritorySettings settings)
    {
        if (!TerritoryMessages.CanChangeSettings(actor, territory.Owner) ||
            !ReferenceEquals(
                Project.FindSubsystem<SubsystemPlayers>()?.Store.Find(territory.Owner),
                actor?.Subject
            ))
        {
            return "OwnerOnly";
        }

        if (GameManager.Role == WorldRole.RemoteClient)
        {
            var server = CommonLib.Net.Server;
            if (server == null || _network == null)
            {
                return "Unavailable";
            }

            SettingsRequestId = SettingsRequestId == int.MaxValue ? 1 : SettingsRequestId + 1;
            var requestId = SettingsRequestId;
            _network.Send(
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

    private void SendSnapshot(Client client)
    {
        _network?.Send(
            TerritoryMessages.Snapshot,
            new TerritoryWorldSnapshot(Store.Territories.Select(TerritoryMessages.Capture).ToArray()),
            PackageAudience.To(client),
            ClientState.ProjectLoaded
        );
    }
}
