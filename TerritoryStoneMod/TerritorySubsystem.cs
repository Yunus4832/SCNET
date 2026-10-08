using Engine.Core;
using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game;
using Game.Cameras;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;

namespace TerritoryStoneMod;

public sealed class TerritorySubsystem : Subsystem, IDrawable
{
    public const string DataKey = "territories";

    public TerritoryStore Store { get; } = new();

    public int SettingsResponseVersion { get; private set; }

    public (Guid Owner, bool Accepted, bool ApplyToTeam, bool ShowBoundary, bool RestrictEntry)? LastSettingsResponse
    {
        get;
        private set;
    }

    private IModNetwork? _network;
    private NetNode? _node;
    private PrimitivesRenderer3D? _primitives;

    public int[] DrawOrders => [2];

    public void Draw(Camera camera, int drawOrder)
    {
        foreach (var territory in Store.Territories)
        {
            if (!territory.ShowBoundary)
            {
                continue;
            }

            var min = new Vector3(territory.Origin.X - 16, territory.StonePoint.Y, territory.Origin.Y - 16);
            var max = new Vector3(territory.Origin.X + 17, territory.StonePoint.Y + 32, territory.Origin.Y + 17);
            var viewBounds = new BoundingBox(min - new Vector3(16, 0, 16), max + new Vector3(16, 0, 16));
            if (!viewBounds.Contains(camera.ViewPosition))
            {
                continue;
            }

            _primitives ??= new PrimitivesRenderer3D();
            _primitives.FlatBatch().QueueBoundingBox(new BoundingBox(min, max),
                territory.Owner == camera.GameWidget.PlayerData.PlayerGUID ? Color.Green : Color.Yellow);
            _primitives.Flush(camera.ViewProjectionMatrix);
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
        Store.Load(new ValuesDictionary());
    }

    public void PublishUpdate(Territory territory)
    {
        if (CommonLib.WorkType == WorkType.Server)
        {
            _network?.Send(TerritoryMessages.Update, writer => TerritoryMessages.Write(writer, territory),
                PackageAudience.Global, ClientState.ProjectLoaded);
        }
    }

    public void PublishRemoval(Guid owner)
    {
        if (CommonLib.WorkType == WorkType.Server)
        {
            _network?.Send(TerritoryMessages.Remove, writer => writer.Write(owner),
                PackageAudience.Global, ClientState.ProjectLoaded);
        }
    }

    public void ChangeSettings(Territory territory, bool applyToTeam, bool showBoundary, bool restrictEntry)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            var server = CommonLib.Net.Server;
            if (server == null)
            {
                return;
            }

            _network?.Send(TerritoryMessages.Settings, writer =>
            {
                writer.Write(territory.Owner);
                writer.Write(applyToTeam);
                writer.Write(showBoundary);
                writer.Write(restrictEntry);
            }, PackageAudience.To(server), ClientState.ProjectLoaded);
            return;
        }

        territory.ApplyToTeam = applyToTeam;
        territory.ShowBoundary = showBoundary;
        territory.RestrictEntry = restrictEntry;
        PublishUpdate(territory);
    }

    public void ReceiveSettingsResponse(Guid owner, bool accepted, bool applyToTeam, bool showBoundary,
        bool restrictEntry)
    {
        LastSettingsResponse = (owner, accepted, applyToTeam, showBoundary, restrictEntry);
        SettingsResponseVersion++;
    }

    private void SendSnapshot(Client client)
    {
        _network?.Send(TerritoryMessages.Snapshot, writer => TerritoryMessages.WriteSnapshot(writer, Store.Territories),
            PackageAudience.To(client), ClientState.ProjectLoaded);
    }
}
