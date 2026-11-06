using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemBoatBlockBehavior : SubsystemBlockBehavior
{
    private const string _typeName = nameof(SubsystemBoatBlockBehavior);

    private SubsystemCreatureSpawn _creatureSpawn = null!;

    private SubsystemBodies _subsystemBodies = null!;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:boat")];

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
        _subsystemBodies.FindBodiesInArea(
            new Vector2(position.X, position.Z) - new Vector2(8f),
            new Vector2(position.X, position.Z) + new Vector2(8f),
            dynamicArray
        );
        if (dynamicArray.Count(b => b.Entity.ValuesDictionary.DatabaseObject.Name == "Boat") < 6)
        {
            _creatureSpawn.SpawnCreature("Boat", position, true);
            componentMiner.RemoveActiveTool(1);
        }
        else
        {
            ReportCapacityLimit(componentMiner.ComponentCharacter);
        }

        return true;
    }

    internal void ReportCapacityLimit(ComponentCharacter? actor)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).IsAuthority ||
            actor is null ||
            !ReferenceEquals(actor.Project, Project) ||
            actor.CharacterData.ControllerConnection is not { } recipient)
        {
            return;
        }

        Project.FindSubsystem<SubsystemMessages>(true)!.Service.Publish(
            GameMessage.System(
                    [
                        new MessageSegment(new LocalizedText(_typeName, "1", "Too many boats."))
                    ],
                    presentation: GameMessagePresentation.Toast
                ) with
                {
                    ToastStyle = new GameMessageToastStyle(GameMessageTone.Normal, true, false)
                },
            [recipient.ID],
            includePublisher: false
        );
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _creatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true)!;
    }
}
