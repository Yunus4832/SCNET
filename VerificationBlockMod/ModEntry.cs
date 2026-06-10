using Engine.Core;

using Game.Components;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Terrains;

namespace VerificationBlockMod;

public sealed class ModEntry : IMod
{
    private float _elapsedTime;

    public void Configure(IModContext context)
    {
        context.Extensions.RegisterBlock<VerificationBlock>(
            new ResourceId(context.Manifest.ModId, "verification_block"),
            VerificationBlock.Index);

        context.Gameplay.OnCreatureInjuring(injury =>
        {
            if (injury.Health.Entity.FindComponent<ComponentPlayer>() is not null)
            {
                injury.Amount *= 0.5f;
            }
        });

        context.Gameplay.OnMinerDigging(digging =>
        {
            if (Terrain.ExtractContents(digging.CellValue) == VerificationBlock.Index)
            {
                digging.DigTimeMultiplier = 0.1f;
            }
        });

        context.Gameplay.OnBlockPlacing(placing =>
        {
            if (Terrain.ExtractContents(placing.Value) == VerificationBlock.Index)
            {
                Log.Information("Verification mod observed its block being placed.");
            }
        });

        context.Gameplay.OnTerrainCellChanging(changing =>
        {
            if (Terrain.ExtractContents(changing.OldValue) == VerificationBlock.Index ||
                Terrain.ExtractContents(changing.NewValue) == VerificationBlock.Index)
            {
                Log.Information(
                    $"Verification block terrain change at {changing.X}, {changing.Y}, {changing.Z}: " +
                    $"{changing.OldValue} -> {changing.NewValue}");
            }
        });

        context.Gameplay.OnEntityAdded(added =>
        {
            if (added.Entity.FindComponent<ComponentPlayer>() is not null)
            {
                Log.Information("Verification mod observed a player entity entering the project.");
            }
        });

        context.Gameplay.OnWorldUpdating(updating =>
        {
            _elapsedTime += updating.DeltaTime;
            if (_elapsedTime >= 30f)
            {
                _elapsedTime = 0f;
                Log.Debug("Verification mod gameplay hooks are active.");
            }
        });
    }

    public void Start(IModContext context)
    {
        Log.Information($"Started {context.Manifest.Name} {context.Manifest.Version}.");
    }

    public void Stop()
    {
        Log.Information("Stopped Verification Block mod.");
    }
}
