using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemMatchBlockBehavior : SubsystemBlockBehavior
{
    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemExplosivesBlockBehavior _subsystemExplosivesBlockBehavior = null!;

    private SubsystemFireBlockBehavior _subsystemFireBlockBehavior = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    public override int[] HandledBlocks => [108];

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        var obj = componentMiner.Raycast(ray, RaycastMode.Digging);
        if (obj is TerrainRaycastResult result)
        {
            var cellFace = result.CellFace;
            //爆炸
            if (_subsystemExplosivesBlockBehavior.IgniteFuse(cellFace.X, cellFace.Y, cellFace.Z,
                    componentMiner.ComponentPlayer?.PlayerData))
            {
                _subsystemAudio.PlaySound("Audio/Match", 1f, _random.Float(-0.1f, 0.1f), ray.Position, 1f, true);
                componentMiner.RemoveActiveTool(1);
                return true;
            }

            //方块着火
            if (!_subsystemFireBlockBehavior.SetCellOnFire(cellFace.X, cellFace.Y, cellFace.Z, 1f, componentMiner))
            {
                return false;
            }

            _subsystemAudio.PlaySound("Audio/Match", 1f, _random.Float(-0.1f, 0.1f), ray.Position, 1f, true);
            componentMiner.RemoveActiveTool(1);
            return true;
        }

        if (obj is BodyRaycastResult raycastResult)
        {
            var componentOnFire = raycastResult.ComponentBody.Entity.FindComponent<ComponentOnFire>();
            if (componentOnFire == null)
            {
                return false;
            }

            if (_subsystemGameInfo.WorldSettings.GameMode < GameMode.Challenging || _random.Float(0f, 1f) < 0.33f)
            {
                componentOnFire.SetOnFire(componentMiner.ComponentCreature, _random.Float(6f, 8f));
            }

            _subsystemAudio.PlaySound("Audio/Match", 1f, _random.Float(-0.1f, 0.1f), ray.Position, 1f, true);
            componentMiner.RemoveActiveTool(1);
            return true;
        }

        return false;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemFireBlockBehavior = Project.FindSubsystem<SubsystemFireBlockBehavior>(true)!;
        _subsystemExplosivesBlockBehavior = Project.FindSubsystem<SubsystemExplosivesBlockBehavior>(true)!;
    }
}
