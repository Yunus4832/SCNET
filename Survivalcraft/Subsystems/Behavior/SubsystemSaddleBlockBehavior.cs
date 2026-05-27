using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemSaddleBlockBehavior : SubsystemBlockBehavior
{
    private readonly Random _random = new();
    private SubsystemAudio _subsystemAudio = null!;

    public override int[] HandledBlocks => [158];

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        var bodyRaycastResult = componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction);
        if (!bodyRaycastResult.HasValue)
        {
            return false;
        }

        var componentHealth = bodyRaycastResult.Value.ComponentBody.Entity.FindComponent<ComponentHealth>();
        if (componentHealth != null && !(componentHealth.Health > 0f))
        {
            return true;
        }

        var entityTemplateName =
            bodyRaycastResult.Value.ComponentBody.Entity.ValuesDictionary.DatabaseObject.Name + "_Saddled";
        var entity = DatabaseManager.CreateEntity(Project, entityTemplateName, false);
        if (entity == null || CommonLib.WorkType == WorkType.Client)
        {
            return true;
        }

        var componentBody = entity.FindComponent<ComponentBody>(true)!;
        componentBody.Position = bodyRaycastResult.Value.ComponentBody.Position;
        componentBody.Rotation = bodyRaycastResult.Value.ComponentBody.Rotation;
        componentBody.Velocity = bodyRaycastResult.Value.ComponentBody.Velocity;
        entity.FindComponent<ComponentSpawn>(true)!.SpawnDuration = 0f;
        Project.RemoveEntity(bodyRaycastResult.Value.ComponentBody.Entity, true);
        Project.AddEntity(entity);
        _subsystemAudio.PlaySound("Audio/BlockPlaced", 1f, _random.Float(-0.1f, 0.1f), ray.Position, 1f, true);
        componentMiner.RemoveActiveTool(1);

        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
    }
}
