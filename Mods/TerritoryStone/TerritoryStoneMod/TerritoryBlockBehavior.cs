using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.Modding;
using Game.Protocol;
using Game.Protocol.Terrain;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

namespace TerritoryStoneMod;

public sealed class TerritoryBlockBehavior : SubsystemBlockBehavior
{
    private TerritorySubsystem _territories = null!;

    public event Action<ComponentCharacter, Point3>? EditRequested;

    public event Action? StoneRemoved;

    public override ResourceId[] HandledBlocks => [TerritoryBlock.Id];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _territories = Project.FindSubsystem<TerritorySubsystem>(true)!;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentCharacter player)
    {
        if (!TerritoryBlock.IsTerritoryValue(value, SubsystemTerrain.Catalog)) { return false; }

        EditRequested?.Invoke(player, new Point3(x, y, z));
        return true;
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority ||
            worldItem.Value == 0 ||
            !TerritoryBlock.IsTerritoryValue(
                SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z),
                SubsystemTerrain.Catalog
            ))
        {
            return;
        }

        _territories.PublishProjectileHit(
            new TerritoryProjectileHit(worldItem.Value, cellFace.X, cellFace.Y, cellFace.Z)
        );
    }


    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) => StoneRemoved?.Invoke();

    public override void Dispose()
    {
        EditRequested = null;
        StoneRemoved = null;
    }
}
