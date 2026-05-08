using EntitySystem.TemplatesDatabase;
using Game.NetWork;

namespace Game.Subsystems;

public class SubsystemHammerBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemFurnitureBlockBehavior _subsystemFurnitureBlockBehavior = null!;

    public override int[] HandledBlocks => [];

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        var terrainRaycastResult = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
        if (!terrainRaycastResult.HasValue)
        {
            return false;
        }

        if (componentMiner.ComponentPlayer != null && (CommonLib.WorkType == WorkType.Local || componentMiner.ComponentPlayer.PlayerData.IsMainPlayer))
        {
            _subsystemFurnitureBlockBehavior.ScanDesign(terrainRaycastResult.Value.CellFace, ray.Direction,
                componentMiner);
        }

        return true;

    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemFurnitureBlockBehavior = Project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
    }
}
