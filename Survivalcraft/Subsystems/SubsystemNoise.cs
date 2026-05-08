using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemNoise : Subsystem
{
    private readonly DynamicArray<ComponentBody> _componentBodies = new();
    private SubsystemBodies _subsystemBodies = null!;

    public void MakeNoise(Vector3 position, float loudness, float range)
    {
        MakeNoisepublic(null, position, loudness, range);
    }

    public void MakeNoise(ComponentBody sourceBody, float loudness, float range)
    {
        MakeNoisepublic(sourceBody, sourceBody.Position, loudness, range);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
    }

    private void MakeNoisepublic(ComponentBody? sourceBody, Vector3 position, float loudness, float range)
    {
        var num = range * range;
        _componentBodies.Clear();
        _subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), range, _componentBodies);
        for (var i = 0; i < _componentBodies.Count; i++)
        {
            var componentBody = _componentBodies.Array[i];
            if (componentBody == sourceBody ||
                !(Vector3.DistanceSquared(componentBody.Position, position) < num))
            {
                continue;
            }

            foreach (var item in componentBody.Entity.FindComponents<INoiseListener>())
            {
                item?.HearNoise(sourceBody, position, loudness);
            }
        }
    }
}
