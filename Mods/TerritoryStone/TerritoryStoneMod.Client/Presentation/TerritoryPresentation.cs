using Engine.Core.Mathematics;
using Engine.Core.Runtime;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Cameras;
using Game.Commands;
using Game.Components;
using Game.Managers;
using Game.Rendering;
using Game.Screens;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

namespace TerritoryStoneMod.Client.Presentation;

public sealed class TerritoryPresentation : Subsystem, IDrawable, IUpdateable
{
    private TerritorySubsystem _territories = null!;

    private TerritoryBlockBehavior _behavior = null!;

    private SubsystemTerrain _terrain = null!;

    private SubsystemModelsRenderer? _models;

    private DrawText? _drawText;

    private TerritoryBoundaryRenderer? _boundaryRenderer;

    private TerritoryPermissionsDialog? _approvalDialog;

    private readonly Dictionary<Guid, double> _submittedRequests = new();

    private bool _disposed;

    private bool _subscribed;

    private WorldRuntimeContext Runtime => WorldRuntimeContext.For(Project);

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public int[] DrawOrders => [101];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _territories = Project.FindSubsystem<TerritorySubsystem>(true)!;
        _behavior = Project.FindSubsystem<TerritoryBlockBehavior>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _models = Project.FindSubsystem<SubsystemModelsRenderer>(true)!;
        _territories.ProjectileHitReceived += ShowProjectileHit;
        _territories.ProtectionNoticeRequested += ShowProtectionNotice;
        _behavior.EditRequested += OpenSettings;
        _behavior.StoneRemoved += RemoveDrawText;
        _subscribed = true;
    }

    public void Update(float dt)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        if (!Runtime.HasPresentation ||
            Game.Network.WorldNetworkBinding.For(Runtime).Node!.Self is not { PlayerAddress: not null } session ||
            !ReferenceEquals(Game.Application.ClientApplication.World?.Project, Project))
        {
            return;
        }

        var player = Game.Players.WorldConnectionResolver.FindCharacter(session)?.ComponentCharacter;
        var parent = player?.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget ??
                     ScreensManager.CurrentScreen;

        foreach (var id in _submittedRequests.Keys
                     .Where(id => !_territories.IncomingRequests.Any(request => request.Id == id)).ToArray())
        {
            _submittedRequests.Remove(id);
        }

        if (_approvalDialog != null &&
            !_territories.IncomingRequests.Any(request => request.Id == _approvalDialog.RequestId))
        {
            DialogsManager.HideDialog(_approvalDialog);
            _approvalDialog = null;
        }

        if (_approvalDialog != null && !DialogsManager.ReadOnlyDialogs.Contains(_approvalDialog))
        {
            _approvalDialog = null;
        }

        if (_approvalDialog == null &&
            _territories.IncomingRequests.FirstOrDefault(request =>
                !_submittedRequests.TryGetValue(request.Id, out var retryAt) || retryAt <= Time.RealTime
            ) is { } next &&
            !DialogsManager.HasDialogs(null) &&
            !DialogsManager.HasDialogs(parent) &&
            player?.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget == null)
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

    public void Draw(Camera camera, int drawOrder)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        foreach (var territory in _territories.Store.Territories)
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

    private void OpenSettings(ComponentCharacter player, Point3 point)
    {
        if (_disposed || !player.IsLocallyControlled || !ReferenceEquals(player.Project, Project)) { return; }

        var gui = player.Entity.FindComponent<ComponentGui>(true)!;
        RemoveDrawText();
        var territory = _territories.Store.Territories.FirstOrDefault(item =>
            item.StonePoint == point
        );
        if (territory == null || territory.Owner != TerritoryProtection.ActorAddress(player.CharacterData))
        {
            gui.DisplaySmallMessage(
                Game.Localization.ClientLanguage.Catalog.Get("TerritorySettings", "OwnerOnly"),
                Color.Yellow,
                false,
                true
            );
            return;
        }

        gui.ModalPanelWidget = gui.ModalPanelWidget == null
            ? new TerritorySettingsWidget(
                _territories,
                territory,
                player,
                () => gui.ModalPanelWidget = null
            )
            : null;
    }

    private void ShowProjectileHit(TerritoryProjectileHit hit)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project) || _models == null || hit.Y is < 0 or > 255)
        {
            return;
        }

        var block = _terrain.Catalog.Blocks[Terrain.ExtractContents(hit.Value)];
        var ingredient = $"{block.CraftingId}:{Terrain.ExtractData(hit.Value)}";
        var text = string.Format(
            Game.Localization.ClientLanguage.Catalog.Get("SubsystemTerritoryBlockBehavior", 5),
            ingredient
        );
        if (_drawText == null)
        {
            _drawText = _models.AddDrawText(new Vector3(hit.X, hit.Y, hit.Z) + new Vector3(0.5f), text, Color.White);
        }
        else
        {
            _drawText.Text = text;
        }
    }

    private void RemoveDrawText()
    {
        if (_drawText != null)
        {
            _models?.RemoveDrawText(_drawText);
            _drawText = null;
        }
    }

    private void ShowProtectionNotice(ComponentCharacter character, string key, Territory? territory)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project) || !character.IsLocallyControlled) { return; }

        var text = Game.Localization.ClientLanguage.Catalog.Get("TerritoryProtection", key);
        if (territory is not null)
        {
            var hint = key == "StoneOwnerOnly"
                ? "OwnedBy"
                : TerritoryProtection.ActorAddress(character.CharacterData) is { } subject &&
                  territory.Grants.ContainsKey(subject)
                    ? "AuthorizedHint"
                    : "RequestHint";
            text += " " +
                    string.Format(
                        Game.Localization.ClientLanguage.Catalog.Get("TerritoryAccess", hint),
                        _territories.OwnerName(territory.Owner)
                    );
        }

        character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(text, Color.Yellow, false, true);
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_subscribed)
        {
            _territories.ProjectileHitReceived -= ShowProjectileHit;
            _territories.ProtectionNoticeRequested -= ShowProtectionNotice;
            _behavior.EditRequested -= OpenSettings;
            _behavior.StoneRemoved -= RemoveDrawText;
            _subscribed = false;
        }

        if (_approvalDialog is not null) { DialogsManager.HideDialog(_approvalDialog); }

        _approvalDialog = null;
        _submittedRequests.Clear();
        RemoveDrawText();
        _boundaryRenderer?.Dispose();
        _boundaryRenderer = null;
    }
}
