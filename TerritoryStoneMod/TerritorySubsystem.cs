using Engine.Core.Runtime;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game;
using Game.Cameras;
using Game.Commands;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Network.Transport;
using Game.Protocol.Connection;
using Game.Protocol.Messaging;
using Game.Subsystems;

namespace TerritoryStoneMod;

public sealed class TerritorySubsystem : Subsystem, IDrawable, IUpdateable
{
    public const string DataKey = "territories";

    public TerritoryStore Store { get; } = new();

    public int SettingsResponseVersion { get; private set; }

    public int SettingsRequestId { get; private set; }

    public (int RequestId, Guid Owner, bool Accepted, TerritorySettings Settings, string Error)? LastSettingsResponse
    {
        get;
        private set;
    }

    private IModNetwork? _network;

    private NetNode? _node;

    private TerritoryBoundaryRenderer? _boundaryRenderer;

    public TerritoryAccessRequests AccessRequests { get; } = new();

    private readonly Dictionary<Guid, TerritoryAccessRequest> _incomingRequests = new();

    private double _nextRequestCleanup;

    private readonly Dictionary<Guid, double> _submittedRequests = new();

    private TerritoryPermissionsDialog? _approvalDialog;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public string OwnerName(Guid owner) => Project.FindSubsystem<SubsystemPlayers>()?.PlayerList
                                               .GetValueOrDefault(owner)?.Name ??
                                           OnlinePlayer(owner)?.Name ?? owner.ToString("D");

    private PlayerData? OnlinePlayer(Guid id) => Project.FindSubsystem<SubsystemPlayers>()?.PlayersData
        .FirstOrDefault(player => player.PlayerGUID == id &&
                                  player.ComponentPlayer != null &&
                                  (player.IsMainPlayer || player.Client?.IsConnected == true)
        );

    public CommandResult RequestAccess(PlayerData player, string ownerName)
    {
        var matches = Store.Territories.Where(territory =>
            Guid.TryParse(ownerName, out var id)
                ? territory.Owner == id
                : string.Equals(OwnerName(territory.Owner), ownerName, StringComparison.OrdinalIgnoreCase)
        ).ToArray();
        if (matches.Length != 1)
        {
            return TerritoryCommands.Result("UnknownOwner", false);
        }

        var territory = matches[0];
        if (territory.Grants.ContainsKey(player.PlayerGUID))
        {
            return TerritoryCommands.Result("AlreadyGranted", false);
        }

        if (OnlinePlayer(territory.Owner) == null)
        {
            return TerritoryCommands.Result("OwnerOffline", false);
        }

        if (territory.Grants.Count >= 128)
        {
            return TerritoryCommands.Result("QueueFull", false);
        }

        if (!AccessRequests.TryAdd(
                territory.Owner,
                player.PlayerGUID,
                player.Name,
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

    public CommandResult DecideAccess(PlayerData owner, Guid id, string permissionText)
    {
        if (!TerritoryPermissions.TryParse(permissionText, out var permissions))
        {
            return TerritoryCommands.Result("InvalidPermissions", false);
        }

        var pending = AccessRequests.Pending.FirstOrDefault(request => request.Id == id);
        if (pending == null ||
            pending.Owner != owner.PlayerGUID ||
            !Store.TryGet(owner.PlayerGUID, out var territory) ||
            OnlinePlayer(pending.Player) == null)
        {
            return TerritoryCommands.Result("RequestExpired", false);
        }

        if (permissions != TerritoryPermission.None &&
            territory!.Grants.Count >= 128 &&
            !territory.Grants.ContainsKey(pending.Player))
        {
            return TerritoryCommands.Result("QueueFull", false);
        }

        if (!AccessRequests.TryResolve(id, owner.PlayerGUID, permissions, Time.RealTime, out var resolved))
        {
            return TerritoryCommands.Result("RequestExpired", false);
        }

        if (permissions != TerritoryPermission.None)
        {
            var existing = territory!.PermissionsFor(resolved!.Player);
            territory.SetGrant(resolved.Player, resolved.PlayerName, existing | permissions);
            PublishUpdate(territory);
        }

        SendRequest(resolved!, true);
        NotifyPlayer(
            resolved!.Player,
            permissions == TerritoryPermission.None ? "Rejected" : "Granted",
            owner.Name,
            permissions.ToString().ToLowerInvariant().Replace(" ", "")
        );
        return TerritoryCommands.Result("Processed", true);
    }

    public CommandResult ChangeGrant(PlayerData owner, Guid player, string permissionText)
    {
        if (!Store.TryGet(owner.PlayerGUID, out var territory) || !territory!.Grants.TryGetValue(player, out var grant))
        {
            return TerritoryCommands.Result("MissingGrant", false);
        }

        if (!TerritoryPermissions.TryParse(permissionText, out var permissions))
        {
            return TerritoryCommands.Result("InvalidPermissions", false);
        }

        territory.SetGrant(player, grant.Name, permissions);
        PublishUpdate(territory);
        NotifyPlayer(player, "GrantChanged", owner.Name);
        return TerritoryCommands.Result("Processed", true);
    }

    public void ReceiveAccessRequest(TerritoryAccessRequest request, bool removed)
    {
        if (removed)
        {
            _incomingRequests.Remove(request.Id);
            _submittedRequests.Remove(request.Id);
            return;
        }

        if (_incomingRequests.Count < 8 && CommonLib.MainPlayer?.PlayerData.PlayerGUID == request.Owner)
        {
            _incomingRequests[request.Id] = request;
        }
    }

    private void SendRequest(TerritoryAccessRequest request, bool removed)
    {
        if (OnlinePlayer(request.Owner) is not { } owner)
        {
            return;
        }

        if (owner.IsMainPlayer && RunMode.Value == RunModeType.Gui)
        {
            ReceiveAccessRequest(request, removed);
        }
        else if (owner.Client is { } client)
        {
            _network?.Send(
                removed ? TerritoryMessages.AccessRequestRemoved : TerritoryMessages.AccessRequest,
                writer =>
                {
                    writer.Write(request.Id);
                    if (!removed)
                    {
                        writer.Write(request.Owner);
                        writer.Write(request.Player);
                        writer.Write(request.PlayerName);
                    }
                },
                PackageAudience.To(client),
                ClientState.ProjectLoaded
            );
        }
    }

    private void NotifyPlayer(Guid playerId, string key, params string[] arguments)
    {
        if (OnlinePlayer(playerId) is not { } player)
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
        if (player.IsMainPlayer && RunMode.Value == RunModeType.Gui)
        {
            messages.DisplayLocal(message);
        }
        else if (player.Client is { } client)
        {
            messages.Publish(message, [client.ID], includePublisher: false);
        }
    }

    public void Update(float dt)
    {
        if (CommonLib.WorkType != WorkType.Client && Time.RealTime >= _nextRequestCleanup)
        {
            _nextRequestCleanup = Time.RealTime + 1;
            foreach (var request in AccessRequests.Expire(
                         Time.RealTime,
                         request =>
                             OnlinePlayer(request.Owner) == null ||
                             OnlinePlayer(request.Player) == null ||
                             !Store.TryGet(request.Owner, out _)
                     ))
            {
                SendRequest(request, true);
                NotifyPlayer(request.Player, "Expired");
            }
        }

        if (RunMode.Value != RunModeType.Gui ||
            CommonLib.MainPlayer is not { } player ||
            !ReferenceEquals(player.Project, Project))
        {
            return;
        }

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
            !DialogsManager.HasDialogs(player.GameWidget.GuiWidget) &&
            player.ComponentGui.ModalPanelWidget == null)
        {
            _approvalDialog = new TerritoryPermissionsDialog(
                next.PlayerName,
                TerritoryPermission.None,
                TerritoryPermission.All,
                selected =>
                {
                    CommandGateway.Submit(
                        player.PlayerData,
                        new DecideTerritoryAccessCommand(
                            next.Id,
                            selected.ToString().ToLowerInvariant().Replace(" ", "")
                        )
                    );
                    _submittedRequests[next.Id] = Time.RealTime + 10;
                },
                next.Id
            );
            DialogsManager.ShowDialog(player.GameWidget.GuiWidget, _approvalDialog);
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
        if (CommonLib.WorkType != WorkType.Client)
        {
            Store.Load(Project.ExtensionData.Get(ModEntry.ModId, DataKey));
        }

        if (CommonLib.WorkType == WorkType.Server && _network != null)
        {
            _node = CommonLib.Net;
            _node.OnClientBecameLive += SendSnapshot;
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        if (Project.SendToClientMode || CommonLib.WorkType == WorkType.Client)
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
        if (CommonLib.WorkType == WorkType.Server)
        {
            _network?.Send(
                TerritoryMessages.Update,
                writer => TerritoryMessages.Write(writer, territory),
                PackageAudience.Global,
                ClientState.ProjectLoaded
            );
        }
    }

    public void PublishRemoval(Guid owner)
    {
        if (CommonLib.WorkType == WorkType.Server)
        {
            _network?.Send(
                TerritoryMessages.Remove,
                writer => writer.Write(owner),
                PackageAudience.Global,
                ClientState.ProjectLoaded
            );
        }
    }

    public string? ChangeSettings(Territory territory, TerritorySettings settings)
    {
        if (CommonLib.WorkType == WorkType.Client)
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
                writer =>
                {
                    writer.Write(requestId);
                    writer.Write(territory.Owner);
                    TerritoryMessages.WriteSettings(writer, settings);
                },
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
        Guid owner,
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
            writer => TerritoryMessages.WriteSnapshot(writer, Store.Territories),
            PackageAudience.To(client),
            ClientState.ProjectLoaded
        );
    }
}
