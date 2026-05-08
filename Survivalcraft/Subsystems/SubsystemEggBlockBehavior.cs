using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemEggBlockBehavior : SubsystemBlockBehavior
{
    private readonly EggBlock _eggBlock = (EggBlock)BlocksManager.Blocks[118];

    private readonly Random _random = new();

    private SubsystemCreatureSpawn _subsystemCreatureSpawn = null!;
    private SubsystemGameInfo _subsystemGameInfo = null!;

    public override int[] HandledBlocks => [];

    public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
    {
        var data = Terrain.ExtractData(worldItem.Value);
        var isCooked = EggBlock.GetIsCooked(data);
        var isLaid = EggBlock.GetIsLaid(data);
        if (isCooked || (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
                         !(_random.Float(0f, 1f) <= (isLaid ? 0.15f : 1f))))
        {
            return true;
        }

        const int maxCount = 35 * 1000;
        if (_subsystemCreatureSpawn.Creatures.Count < maxCount)
        {
            var eggType = _eggBlock.GetEggType(data);
            _subsystemCreatureSpawn.CreateEntity(eggType.TemplateName, worldItem.Position, null, null);
        }
        else
        {
            ((worldItem as Projectile)?.Owner as ComponentPlayer)?.ComponentGui.DisplaySmallMessage("太多生物了",
                Color.White, true, false);
        }

        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true)!;
    }
}
