using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemSaddleBlockBehavior : SubsystemBlockBehavior
{
    private readonly Random _random = new();

    private SubsystemSpatialSoundRequests _soundRequests = null!;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:saddle")];

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
        if (entity == null || !WorldRuntimeContext.For(Project).IsAuthority)
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
        _soundRequests.RequestSound("Audio/BlockPlaced", 1f, _random.Float(-0.1f, 0.1f), ray.Position, 1f, true);
        componentMiner.RemoveActiveTool(1);

        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _soundRequests = Project.FindSubsystem<SubsystemSpatialSoundRequests>(true)!;
    }
}
