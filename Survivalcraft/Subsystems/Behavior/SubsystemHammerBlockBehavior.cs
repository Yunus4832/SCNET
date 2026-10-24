using EntitySystem.TemplatesDatabase;

using Game.Network;

namespace Game.Subsystems;

public class SubsystemHammerBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemFurnitureBlockBehavior _subsystemFurnitureBlockBehavior = null!;

    public override ResourceId[] HandledBlocks => [];

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        var terrainRaycastResult = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
        if (!terrainRaycastResult.HasValue)
        {
            return false;
        }

        if (componentMiner.ComponentCharacter != null &&
            (CommonLib.WorkType == WorkType.Local ||
             componentMiner.ComponentCharacter.CharacterData.IsLocalCharacter))
        {
            _subsystemFurnitureBlockBehavior.ScanDesign(
                terrainRaycastResult.Value.CellFace,
                ray.Direction,
                componentMiner
            );
        }

        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemFurnitureBlockBehavior = Project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
    }
}
