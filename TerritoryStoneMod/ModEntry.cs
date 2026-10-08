using System.Xml.Linq;

using EntitySystem.Core;

using Game.Modding;
using Game.Modding.Blocks;
using Game.Modding.Content;
using Game.Modding.Data;

namespace TerritoryStoneMod;

public sealed class ModEntry : IMod
{
    public const string ModId = "scnet.territory";

    private IModNetwork? _network;
    private TerritorySubsystem? _world;
    private TerritoryStore? _pendingClientStore;

    public void Configure(IModContext context)
    {
        foreach (var culture in new[] { "zh-CN", "en-US", "pt-PT", "ru-RU" })
        {
            var path = $"lang/{culture}.json";
            using var stream = typeof(ModEntry).Assembly.GetManifestResourceStream(
                $"TerritoryStoneMod.Resources.Lang.{culture}.json") ??
                throw new InvalidOperationException($"Territory language resource {culture} is missing.");
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            context.Extensions.RegisterContent(new ResourceId(context.Manifest.ModId, path), path, bytes.ToArray());
        }

        context.Extensions.RegisterBlock<TerritoryBlock>(
            new ResourceId(context.Manifest.ModId, "territory_stone"), TerritoryBlock.Index);
        context.Extensions.RegisterBlockData(
            new ResourceId(context.Manifest.ModId, "territory_stone"), ReadBlockData);
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.DatabaseRegistryName,
            new ResourceId(context.Manifest.ModId, "territory"),
            XmlContributionMode.Patch,
            ReadDatabasePatch);
        TerritoryProtection.Register(context);
        context.Network.OnMessage(TerritoryMessages.Snapshot, message =>
        {
            if (message.IsServer || !ReferenceEquals(message.NetNode, Game.Network.CommonLib.Net))
            {
                return;
            }

            var snapshot = TerritoryMessages.ReadSnapshot(message.Reader);
            if (ActiveWorld(message) is { } world)
            {
                world.Store.ApplySnapshot(snapshot);
            }
            else
            {
                _pendingClientStore = new TerritoryStore();
                _pendingClientStore.ApplySnapshot(snapshot);
            }
        });
        context.Network.OnMessage(TerritoryMessages.Update, message =>
        {
            if (!message.IsServer && ReferenceEquals(message.NetNode, Game.Network.CommonLib.Net))
            {
                var territory = TerritoryMessages.Read(message.Reader);
                if (ActiveWorld(message) is { } world)
                {
                    world.Store.ApplyUpdate(territory);
                }
                else
                {
                    _pendingClientStore?.ApplyUpdate(territory);
                }
            }
        });
        context.Network.OnMessage(TerritoryMessages.Remove, message =>
        {
            if (!message.IsServer && ReferenceEquals(message.NetNode, Game.Network.CommonLib.Net))
            {
                var owner = message.Reader.ReadGuid();
                if (ActiveWorld(message) is { } world)
                {
                    world.Store.Remove(owner);
                }
                else
                {
                    _pendingClientStore?.Remove(owner);
                }
            }
        });
        context.Network.OnMessage(TerritoryMessages.Settings, message =>
        {
            if (!message.IsServer || ActiveWorld(message) is not { } world)
            {
                return;
            }

            var owner = message.Reader.ReadGuid();
            var applyToTeam = message.Reader.ReadBoolean();
            var showBoundary = message.Reader.ReadBoolean();
            var restrictEntry = message.Reader.ReadBoolean();
            var exists = world.Store.TryGet(owner, out var territory);
            var accepted = exists && TerritoryMessages.CanChangeSettings(message.From, owner);
            if (accepted)
            {
                world.ChangeSettings(territory!, applyToTeam, showBoundary, restrictEntry);
            }

            if (message.From != null)
            {
                context.Network.Send(TerritoryMessages.SettingsResult, writer =>
                {
                    writer.Write(owner);
                    writer.Write(accepted);
                    writer.Write(territory?.ApplyToTeam ?? false);
                    writer.Write(territory?.ShowBoundary ?? false);
                    writer.Write(territory?.RestrictEntry ?? false);
                }, Game.Network.PackageAudience.To(message.From), Game.Network.Enums.ClientState.ProjectLoaded);
            }
        });
        context.Network.OnMessage(TerritoryMessages.SettingsResult, message =>
        {
            if (message.IsServer || ActiveWorld(message) is not { } world)
            {
                return;
            }

            world.ReceiveSettingsResponse(message.Reader.ReadGuid(), message.Reader.ReadBoolean(),
                message.Reader.ReadBoolean(), message.Reader.ReadBoolean(), message.Reader.ReadBoolean());
        });
    }

    public void Start(IModContext context)
    {
        _network = context.Network;
        Project.BeforeSubsystemsAndEntitiesLoad += BindWorld;
    }

    public void Stop()
    {
        Project.BeforeSubsystemsAndEntitiesLoad -= BindWorld;
        _world?.DetachNetwork();
        _world = null;
        _pendingClientStore = null;
        _network = null;
    }

    private void BindWorld(Project project)
    {
        _world?.DetachNetwork();
        _world = project.FindSubsystem<TerritorySubsystem>();
        if (_world != null && _pendingClientStore != null)
        {
            _world.Store.ApplySnapshot(_pendingClientStore.Territories);
            _pendingClientStore = null;
        }

        if (_network != null)
        {
            _world?.BindNetwork(_network);
        }
    }

    private TerritorySubsystem? ActiveWorld(ModNetworkMessageContext message) =>
        _world != null &&
        ReferenceEquals(message.NetNode, Game.Network.CommonLib.Net) ? _world : null;

    private static XElement ReadDatabasePatch()
    {
        using var stream = typeof(ModEntry).Assembly.GetManifestResourceStream(
            "TerritoryStoneMod.Resources.TerritoryDatabase.xml") ??
            throw new InvalidOperationException("Territory database resource is missing.");
        return XElement.Load(stream);
    }

    private static string ReadBlockData()
    {
        using var stream = typeof(ModEntry).Assembly.GetManifestResourceStream(
            "TerritoryStoneMod.Resources.BlocksData.txt") ??
            throw new InvalidOperationException("Territory block data resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
