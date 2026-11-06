using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components.Behavior;

namespace Game.Components;

public class ComponentBehaviorSelector : Component, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private readonly List<ComponentBehavior> _behaviors = [];

    private ComponentCreature _componentCreature = null!;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        ComponentBehavior? componentBehavior = null;
        if (_componentCreature.ComponentHealth.Health > 0f)
        {
            var num = 0f;
            foreach (var behavior in _behaviors)
            {
                var importanceLevel = behavior.ImportanceLevel;
                if (!(importanceLevel > num))
                {
                    continue;
                }

                num = importanceLevel;
                componentBehavior = behavior;
            }
        }

        foreach (var behavior2 in _behaviors)
        {
            if (behavior2 == componentBehavior)
            {
                if (!behavior2.IsActive)
                {
                    behavior2.IsActive = true;
                }
            }
            else if (behavior2.IsActive)
            {
                behavior2.IsActive = false;
            }
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;

        foreach (var item in Entity.FindComponents<ComponentBehavior>().OfType<ComponentBehavior>())
        {
            _behaviors.Add(item);
        }
    }
}
