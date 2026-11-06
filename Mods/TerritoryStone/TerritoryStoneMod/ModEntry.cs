using System.Xml.Linq;

using EntitySystem.Core;

using Game.Modding;
using Game.Modding.Blocks;
using Game.Modding.Content;
using Game.Modding.Data;
using Game.Network.Transport;
using Game.Protocol;
using Game.Protocol.Connection;
using Game.World;

namespace TerritoryStoneMod;

public sealed class ModEntry : IMod
{
    public const string ModId = "scnet.territory";

    private IModNetwork? _network;

    private ModWorldState<PendingClientState> _clientStates = null!;

    public void Configure(IModContext context)
    {
        _clientStates = context.Worlds.Register(
            new ResourceId(context.Manifest.ModId, "pending_client"),
            static _ => new PendingClientState()
        );
        foreach (var culture in new[] { "zh-CN", "en-US", "pt-PT", "ru-RU" })
        {
            var path = $"lang/{culture}.json";
            using var stream = typeof(ModEntry).Assembly.GetManifestResourceStream(
                                   $"TerritoryStoneMod.Resources.Lang.{culture}.json"
                               ) ??
                               throw new InvalidOperationException(
                                   $"Territory language resource {culture} is missing."
                               );
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            context.Extensions.RegisterContent(new ResourceId(context.Manifest.ModId, path), path, bytes.ToArray());
        }

        context.Extensions.RegisterBlock<TerritoryBlock>(TerritoryBlock.Id);
        context.Extensions.RegisterBlockData(new ResourceId(context.Manifest.ModId, "territory_stone"), ReadBlockData);
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.DatabaseRegistryName,
            new ResourceId(context.Manifest.ModId, "territory"),
            XmlContributionMode.Patch,
            ReadDatabasePatch
        );
        TerritoryProtection.Register(context);
        TerritoryCommands.Register(context);
        context.Network.OnMessage(
            TerritoryMessages.ProjectileHit,
            message =>
            {
                if (!message.IsServer && message.Runtime.HasPresentation)
                {
                    message.Project?.FindSubsystem<TerritorySubsystem>()
                        ?.ReceiveProjectileHit(message.Read<TerritoryProjectileHit>());
                }
            }
        );
        context.Network.OnMessage(
            TerritoryMessages.AccessRequest,
            message =>
            {
                if (message.IsServer || ActiveWorld(message) is not { } world)
                {
                    return;
                }

                var notice = message.Read<TerritoryAccessRequestNotice>();
                var request = new TerritoryAccessRequest(
                    notice.Id,
                    notice.Owner,
                    notice.User,
                    notice.UserName,
                    Engine.Core.Runtime.Time.RealTime + TerritoryAccessRequests.Lifetime
                );
                world.ReceiveAccessRequest(request);
            }
        );
        context.Network.OnMessage(
            TerritoryMessages.AccessRequestRemoved,
            message =>
            {
                if (!message.IsServer && ActiveWorld(message) is { } world)
                {
                    world.RemoveAccessRequest(message.Read<TerritoryAccessRequestRemoval>().Id);
                }
            }
        );
        context.Network.OnMessage(
            TerritoryMessages.Snapshot,
            message =>
            {
                if (message.IsServer || message.Runtime.IsClosed)
                {
                    return;
                }

                var snapshot = TerritoryMessages.Restore(message.Read<TerritoryWorldSnapshot>());
                if (ActiveWorld(message) is { } world)
                {
                    world.Store.ApplySnapshot(snapshot);
                }
                else
                {
                    (_clientStates.Get(message.Runtime).Store ??= new TerritoryStore()).ApplySnapshot(snapshot);
                }
            }
        );
        context.Network.OnMessage(
            TerritoryMessages.Update,
            message =>
            {
                if (!message.IsServer && !message.Runtime.IsClosed)
                {
                    var territory = TerritoryMessages.Restore(message.Read<TerritorySnapshot>());
                    if (ActiveWorld(message) is { } world)
                    {
                        world.Store.ApplyUpdate(territory);
                    }
                    else
                    {
                        if (_clientStates.Get(message.Runtime).Store is { } pending)
                        {
                            pending.ApplyUpdate(territory);
                        }
                    }
                }
            }
        );
        context.Network.OnMessage(
            TerritoryMessages.Remove,
            message =>
            {
                if (!message.IsServer && !message.Runtime.IsClosed)
                {
                    var owner = message.Read<TerritoryRemoval>().Owner;
                    if (ActiveWorld(message) is { } world)
                    {
                        world.Store.Remove(owner);
                    }
                    else
                    {
                        if (_clientStates.Get(message.Runtime).Store is { } pending)
                        {
                            pending.Remove(owner);
                        }
                    }
                }
            }
        );
        context.Network.OnMessage(
            TerritoryMessages.Settings,
            message =>
            {
                if (!message.IsServer || ActiveWorld(message) is not { } world)
                {
                    return;
                }

                var request = message.Read<TerritorySettingsRequest>();
                var requestId = request.RequestId;
                var owner = request.Owner;
                var settings = request.Settings;
                var exists = world.Store.TryGet(owner, out var territory);
                var error = "OwnerOnly";
                var accepted = exists && TerritoryMessages.CanChangeSettings(message.Runtime, message.From, owner);
                if (accepted)
                {
                    error = world.ChangeSettings(message.From!, territory!, settings) ?? string.Empty;
                    accepted = error.Length == 0;
                }

                if (message.From != null)
                {
                    context.Network.Send(
                        message.Runtime,
                        TerritoryMessages.SettingsResult,
                        new TerritorySettingsResponse(
                            requestId,
                            owner,
                            accepted,
                            error,
                            territory?.Settings ?? new TerritorySettings()
                        ),
                        PackageAudience.To(message.From),
                        ClientState.ProjectLoaded
                    );
                }
            }
        );
        context.Network.OnMessage(
            TerritoryMessages.SettingsResult,
            message =>
            {
                if (message.IsServer || ActiveWorld(message) is not { } world)
                {
                    return;
                }

                var response = message.Read<TerritorySettingsResponse>();
                world.ReceiveSettingsResponse(
                    response.RequestId,
                    response.Owner,
                    response.Accepted,
                    response.Settings,
                    response.Error
                );
            }
        );
    }

    public void Start(IModContext context)
    {
        _network = context.Network;
        Project.BeforeSubsystemsAndEntitiesLoad += BindWorld;
    }

    public void Stop()
    {
        Project.BeforeSubsystemsAndEntitiesLoad -= BindWorld;
        _network = null;
    }

    private void BindWorld(Project project)
    {
        if (project.FindSubsystem<TerritorySubsystem>() is not { } world)
        {
            return;
        }

        var runtime = WorldRuntimeContext.For(project);
        if (!runtime.IsAuthority && _clientStates.Get(runtime).Store is { } pending)
        {
            world.Store.ApplySnapshot(pending.Territories);
            _clientStates.Get(runtime).Store = null;
        }

        if (_network is not null) { world.BindNetwork(_network); }
    }

    private static TerritorySubsystem? ActiveWorld(ModNetworkMessageContext message) =>
        message.Runtime.IsClosed ? null : message.Project?.FindSubsystem<TerritorySubsystem>();

    private static XElement ReadDatabasePatch()
    {
        using var stream = typeof(ModEntry).Assembly.GetManifestResourceStream(
                               "TerritoryStoneMod.Resources.TerritoryDatabase.xml"
                           ) ??
                           throw new InvalidOperationException("Territory database resource is missing.");
        return XElement.Load(stream);
    }

    private static string ReadBlockData()
    {
        using var stream = typeof(ModEntry).Assembly.GetManifestResourceStream(
                               "TerritoryStoneMod.Resources.BlocksData.txt"
                           ) ??
                           throw new InvalidOperationException("Territory block data resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class PendingClientState : IDisposable
    {
        public TerritoryStore? Store;

        public void Dispose() => Store = null;
    }
}
