using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentMount : Component
{
    public ComponentBody ComponentBody { get; set; } = null!;

    public Vector3 MountOffset { get; set; }

    public Vector3 DismountOffset { get; set; }

    public ComponentRider? Rider => ComponentBody.ChildBodies
        .Select(childBody => childBody.Entity.FindComponent<ComponentRider>())
        .OfType<ComponentRider>().FirstOrDefault();

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        ComponentBody = Entity.FindComponent<ComponentBody>(true)!;
        MountOffset = valuesDictionary.GetValue<Vector3>("MountOffset");
        DismountOffset = valuesDictionary.GetValue<Vector3>("DismountOffset");
    }
}
