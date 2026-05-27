using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Components;

public class ComponentBehaviorSelector : Component, IUpdateable
{
    private readonly List<ComponentBehavior> _behaviors = [];

    private ComponentCreature _componentCreature = null!;

    public bool IsDisableBehavior
    {
        get;
        set
        {
            field = value;
            if (value)
            {
                return;
            }

            foreach (var b in _behaviors)
            {
                b.IsActive = false;
            }
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (IsDisableBehavior)
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
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        if (CommonLib.WorkType == WorkType.Client)
        {
            IsDisableBehavior = true;
        }

        foreach (var item in Entity.FindComponents<ComponentBehavior>().OfType<ComponentBehavior>())
        {
            _behaviors.Add(item);
        }
    }
}
