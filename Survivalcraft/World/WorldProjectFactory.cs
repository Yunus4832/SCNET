using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Application;
using Game.ClientContent;
using Game.Modding.Blocks;
using Game.Network;
using Game.Spawning;

namespace Game.World;

public static class WorldProjectFactory
{
    public static Project CreateClient(
        WorldRuntimeContext runtime,
        GameModRuntime mods,
        GameDatabase database,
        byte[] projectData,
        ContainerWidget gamesWidget,
        BlockIndexMapping mapping,
        ClientContentRuntime? clientContent,
        WorldInfo? worldInfo = null,
        WorldRuntimeContext? localAuthority = null,
        string? requestedCharacterName = null,
        bool isMultiplayer = false
    )
    {
        ValidateRuntime(runtime, RuntimeSide.Client);
        BlockPresentationCatalog? presentation = null;
        var bound = false;

        try
        {
            var policies = WorldProjectPolicies.Capture(Configuration.HostingConfiguration.Preferences);
            var overrides = new ValuesDictionary();
            overrides.SetValue("GameInfo", new ValuesDictionary { { "WorldDirectoryName", "NetWorld" } });
            overrides.SetValue("Views", new ValuesDictionary { { "GamesWidget", gamesWidget } });
            mods.CreateWorldCatalog(runtime, mapping, true);
            Network.Handling.ApplicationMessageHandlers.InstallWorldHandlers(
                WorldNetworkBinding.For(runtime).RequireNode(),
                mods
            );
            if (runtime.HasPresentation)
            {
                presentation = (clientContent ?? throw new InvalidOperationException("Client content is required."))
                    .CreateCatalog(WorldContentBinding.For(runtime).Catalog);
            }

            var data = new ProjectData(database, projectData, overrides, true);
            var project = new Project(
                database,
                data,
                created =>
                {
                    runtime.BindProject(created);
                    if (runtime.HasPresentation)
                    {
                        ClientApplication.BindWorld(
                            new Presentation.ClientWorldPresentation(
                                runtime,
                                created,
                                worldInfo,
                                localAuthority
                            )
                            {
                                RequestedCharacterName = requestedCharacterName
                            }
                        );
                        ClientApplication.World!.SetMultiplayer(isMultiplayer);
                    }
                },
                new WorldProjectComposition(runtime.Side, runtime.HasPresentation),
                subsystem =>
                {
                    ConfigureWorldPolicies(subsystem, policies.Spawn, policies.Interest);
                    if (subsystem is SubsystemClientModServices services)
                    {
                        services.Bind(mods.Commands, mods.ContextActions);
                    }

                    ConfigureMemoryPolicy(runtime, subsystem);
                    if (subsystem is SubsystemBlockPresentation blockPresentation)
                    {
                        blockPresentation.Bind(
                            presentation ??
                            throw new InvalidOperationException("A world presentation catalog is required.")
                        );
                        bound = true;
                    }
                }
            );
            if (presentation is not null && !bound)
            {
                project.Dispose();
                throw new InvalidOperationException("Client project has no block presentation subsystem.");
            }

            return project;
        }
        catch
        {
            if (!bound) { presentation?.Dispose(); }

            runtime.Close();
            throw;
        }
    }

    internal static void ConfigureWorldPolicies(
        Subsystem subsystem,
        CreatureSpawnPolicy spawn,
        NetworkInterestPolicy interest
    )
    {
        if (subsystem is SubsystemCreatureSpawn creatureSpawn) { creatureSpawn.ConfigurePolicy(spawn); }

        if (subsystem is SubsystemNetworkInterest networkInterest) { networkInterest.ConfigurePolicy(interest); }
    }

    private static void ConfigureMemoryPolicy(WorldRuntimeContext runtime, Subsystem subsystem)
    {
        if (subsystem is SubsystemCharacters characters)
        {
            runtime.OwnWorldResource(
                new CharacterRosterMemoryPolicy(
                    runtime,
                    characters,
                    () => Configuration.HostingConfiguration.Preferences.AutoGarbageCollect,
                    GC.Collect
                )
            );
        }
    }

    private static void ValidateRuntime(WorldRuntimeContext runtime, RuntimeSide side)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ObjectDisposedException.ThrowIf(runtime.IsClosed, runtime);
        if (runtime.Side != side || runtime.HasProjectResources)
        {
            throw new InvalidOperationException(
                "Project construction requires an unused runtime with the matching side."
            );
        }
    }
}
