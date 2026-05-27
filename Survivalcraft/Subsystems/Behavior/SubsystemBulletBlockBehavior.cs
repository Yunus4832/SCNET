using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemBulletBlockBehavior : SubsystemBlockBehavior
{
    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemExplosions _subsystemExplosions = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public override int[] HandledBlocks => [];

    public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
    {
        var bulletType = BulletBlock.GetBulletType(Terrain.ExtractData(worldItem.Value));
        var result = true;
        if (!cellFace.HasValue)
        {
            return result;
        }

        var cellValue =
            _subsystemTerrain.Terrain.GetCellValue(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z);
        var num = Terrain.ExtractContents(cellValue);
        var obj = BlocksManager.Blocks[num];
        if (worldItem.Velocity.Length() > 30f)
        {
            ComponentPlayer? player = null;
            if (worldItem is Projectile proj)
            {
                player = proj.Owner?.Entity.FindComponent<ComponentPlayer>();
            }

            _subsystemExplosions.TryExplodeBlock(
                cellFace.Value.X,
                cellFace.Value.Y,
                cellFace.Value.Z, cellValue,
                player?.PlayerData
            );
        }

        if (!(obj.Density >= 1.5f) || !(worldItem.Velocity.Length() > 30f))
        {
            return result;
        }

        var num2 = 1f;
        var minDistance = 8f;
        if (bulletType == BulletBlock.BulletType.BuckshotBall)
        {
            num2 = 0.25f;
            minDistance = 4f;
        }

        if (!(_random.Float(0f, 1f) < num2))
        {
            return result;
        }

        _subsystemAudio.PlayRandomSound("Audio/Ricochets", 1f, _random.Float(-0.2f, 0.2f),
            new Vector3(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z), minDistance, true);
        result = false;

        return result;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true)!;
    }
}
