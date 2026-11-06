using Engine.Core.FileStorage;
using Engine.Core.Logging;

using EntitySystem.Core;

using Game.FileStorage;
using Game.Managers;
using Game.Startup;
using Game.Terrains;
using Game.World;

namespace Survivalcraft.Server;

internal static class ServerWorldSelection
{
    public static WorldInfo Resolve(StartupContext context, WorldRepository worlds, GameDatabase database)
    {
        var sessionInfo = context.Session;
        StartupSessionLifecycle.ConsumePending(context, StartupManager.Profiles);

        var worldArg = sessionInfo.World;
        var seedArg = sessionInfo.Seed;

        worlds.UpdateWorldsList();
        var available = worlds.WorldInfos.ToList();
        Log.Information($"Worlds directory: {GamePaths.Worlds}");
        Log.Information($"Detected worlds: {string.Join(", ", available.Select(w => w.WorldSettings.Name))}");

        var worldInfo = available.FirstOrDefault(w =>
            string.Equals(w.DirectoryName, worldArg, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.WorldSettings.Name, worldArg, StringComparison.OrdinalIgnoreCase)
        );
        if (worldInfo == null)
        {
            var worldPath = Storage.CombinePaths(GamePaths.Worlds, worldArg);
            if (Storage.DirectoryExists(worldPath))
            {
                worldInfo = worlds.GetWorldInfo(worldPath);
            }
        }

        if (worldInfo == null)
        {
            var worldSettings = new WorldSettings
            {
                Name = worldArg,
                Seed = string.IsNullOrWhiteSpace(seedArg) ? WorldSeedConverter.CreateRandomTextSeed() : seedArg,
                GameMode = sessionInfo.GameMode ?? GameMode.Survival,
                TerrainGenerationMode = sessionInfo.TerrainGenerationMode ?? TerrainGenerationMode.Continent,
                TerrainLevel = sessionInfo.TerrainLevel ?? 64
            };
            var customWorldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, worldArg);
            Log.Information($"Creating new world with seed: {worldSettings.Seed}");
            worldInfo = worlds.CreateWorld(
                database,
                worldSettings,
                new WorldHostingSettings { RunServer = true },
                customWorldDirectoryName
            );
        }
        else
        {
            if (sessionInfo.TerrainGenerationMode is not null || sessionInfo.TerrainLevel is not null)
            {
                Log.Warning("World already exists; ignoring session terrain overrides.");
            }

            if (!string.IsNullOrWhiteSpace(seedArg))
            {
                Log.Warning($"World already exists; ignoring provided seed \"{seedArg}\".");
            }

            if (!worldInfo.Hosting.RunServer)
            {
                Log.Warning(
                    $"World \"{worldInfo.WorldSettings.Name}\" is not marked as a server world. " +
                    "The server application will enable RunServer automatically."
                );
                worldInfo.Hosting.RunServer = true;
                worlds.ChangeWorld(worldInfo.DirectoryName, worldInfo.WorldSettings, worldInfo.Hosting);
            }

            worldInfo.GameModeOverride = sessionInfo.GameMode;

            Log.Information($"Using existing world seed: {worldInfo.WorldSettings.Seed}");
        }

        return worldInfo;
    }
}
