using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentSimpleModel : ComponentModel
{
    private ComponentSpawn? _componentSpawn;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    public override void Animate()
    {
        if (_componentSpawn != null)
        {
            Opacity = _componentSpawn.SpawnDuration > 0f
                ? (float)MathUtils.Saturate((_subsystemGameInfo.TotalElapsedGameTime - _componentSpawn.SpawnTime) /
                                            _componentSpawn.SpawnDuration)
                : 1f;
            if (_componentSpawn.DespawnTime.HasValue)
            {
                Opacity = MathUtils.Min(Opacity.Value,
                    (float)MathUtils.Saturate(1.0 -
                                              (_subsystemGameInfo.TotalElapsedGameTime -
                                               _componentSpawn.DespawnTime.Value) / _componentSpawn.DespawnDuration));
            }
        }

        if (Model.RootBone != null)
        {
            SetBoneTransform(Model.RootBone.Index, componentFrame.Matrix);
        }

        base.Animate();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _componentSpawn = Entity.FindComponent<ComponentSpawn>();
        base.Load(valuesDictionary, idToEntityMap);
    }
}
