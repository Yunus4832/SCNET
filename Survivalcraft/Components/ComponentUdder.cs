using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentUdder : Component
{
    private ComponentCreature _componentCreature = null!;

    private double _lastMilkingTime;

    private float _milkRegenerationTime;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    public bool HasMilk
    {
        get
        {
            if (!(_lastMilkingTime < 0.0))
            {
                return _subsystemGameInfo.TotalElapsedGameTime - _lastMilkingTime >= _milkRegenerationTime;
            }

            return true;
        }
    }

    public bool Milk(ComponentMiner? milker)
    {
        if (milker != null)
        {
            Entity.FindComponent<ComponentHerdBehavior>()
                ?.CallNearbyCreaturesHelp(milker.ComponentCreature, 20f, 20f, true);
        }

        if (HasMilk)
        {
            _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
            _lastMilkingTime = _subsystemGameInfo.TotalElapsedGameTime;
            return true;
        }

        _componentCreature.ComponentCreatureSounds.PlayPainSound();
        return false;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _milkRegenerationTime = valuesDictionary.GetValue<float>("MilkRegenerationTime");
        _lastMilkingTime = valuesDictionary.GetValue<double>("LastMilkingTime");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("LastMilkingTime", _lastMilkingTime);
    }
}
