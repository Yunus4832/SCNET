using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemBoatBlockBehavior : SubsystemBlockBehavior
{
    private const string _typeName = "SubsystemBoatBlockBehavior";

    private SubsystemCreatureSpawn _creatureSpawn = null!;

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemBodies _subsystemBodies = null!;

    public override int[] HandledBlocks => [178];

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        _ = componentMiner.Inventory;
        if (Terrain.ExtractContents(componentMiner.ActiveBlockValue) != 178)
        {
            return false;
        }

        var terrainRaycastResult = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
        if (!terrainRaycastResult.HasValue)
        {
            return false;
        }

        var position = terrainRaycastResult.Value.HitPoint();
        var dynamicArray = new DynamicArray<ComponentBody>();
        _subsystemBodies.FindBodiesInArea(new Vector2(position.X, position.Z) - new Vector2(8f),
            new Vector2(position.X, position.Z) + new Vector2(8f), dynamicArray);
        if (dynamicArray.Count(b => b.Entity.ValuesDictionary.DatabaseObject.Name == "Boat") < 6)
        {
            _creatureSpawn.SpawnCreature("Boat", position, true);
            componentMiner.RemoveActiveTool(1);
        }
        else
        {
            componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                LanguageControl.Get(_typeName, 1),
                Color.White,
                true,
                false
            );
        }

        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _creatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true)!;
    }
}
