using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemWhistleBlockBehavior : SubsystemBlockBehavior
{
    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemNoise _subsystemNoise = null!;

    public override int[] HandledBlocks => [160];

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        _subsystemAudio.PlayRandomSound("Audio/Whistle", 1f, _random.Float(-0.2f, 0f), ray.Position, 4f, true);
        _subsystemNoise.MakeNoise(componentMiner.ComponentCreature.ComponentBody, 0.5f, 30f);
        var dynamicArray = new DynamicArray<ComponentBody>();
        _subsystemBodies.FindBodiesAroundPoint(
            new Vector2(componentMiner.ComponentCreature.ComponentBody.Position.X,
                componentMiner.ComponentCreature.ComponentBody.Position.Z), 64f, dynamicArray);
        var num = float.PositiveInfinity;
        var list = new List<ComponentBody>();
        foreach (var item in dynamicArray)
        {
            var componentSummonBehavior = item.Entity.FindComponent<ComponentSummonBehavior>();
            if (componentSummonBehavior is not { IsEnabled: true })
            {
                continue;
            }

            var num2 = Vector3.Distance(item.Position, componentMiner.ComponentCreature.ComponentBody.Position);
            if (num2 > 4f && componentSummonBehavior.SummonTarget == null)
            {
                list.Add(item);
                num = MathUtils.Min(num, num2);
            }
            else
            {
                componentSummonBehavior.SummonTarget = componentMiner.ComponentCreature.ComponentBody;
            }
        }

        foreach (var item2 in list)
        {
            var componentSummonBehavior2 = item2.Entity.FindComponent<ComponentSummonBehavior>();
            if (componentSummonBehavior2 != null &&
                Vector3.Distance(item2.Position, componentMiner.ComponentCreature.ComponentBody.Position) < num + 4f)
            {
                componentSummonBehavior2.SummonTarget = componentMiner.ComponentCreature.ComponentBody;
            }
        }

        componentMiner.DamageActiveTool(1);
        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
    }
}
