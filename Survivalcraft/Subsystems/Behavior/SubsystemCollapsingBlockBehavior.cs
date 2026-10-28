using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemCollapsingBlockBehavior : SubsystemBlockBehavior
{
    public const string IdString = "CollapsingBlock";

    private readonly HashSet<int> _handledBlocks = [];

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemMovingBlocks _subsystemMovingBlocks = null!;

    private SubsystemSoundMaterials _subsystemSoundMaterials = null!;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:sand"), ResourceId.Parse("game:gravel")];

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
        {
            TryCollapseColumn(new Point3(x, y, z));
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        var catalog = CurrentModRuntime.Value!.RequireWorldCatalog();
        _handledBlocks.Clear();
        foreach (var id in HandledBlocks)
        {
            _handledBlocks.Add(catalog.GetBlock(id).BlockIndex);
        }

        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true)!;
        _subsystemMovingBlocks = Project.FindSubsystem<SubsystemMovingBlocks>(true)!;
        _subsystemMovingBlocks.Stopped += MovingBlocksStopped;
        _subsystemMovingBlocks.CollidedWithTerrain += MovingBlocksCollidedWithTerrain;
    }

    private void MovingBlocksCollidedWithTerrain(IMovingBlockSet movingBlockSet, Point3 p)
    {
        if (movingBlockSet.Id != IdString)
        {
            return;
        }

        var cellValue = SubsystemTerrain.Terrain.GetCellValue(p.X, p.Y, p.Z);
        if (IsCollapseSupportBlock(cellValue))
        {
            movingBlockSet.Stop();
        }
        else if (IsCollapseDestructibleBlock(cellValue))
        {
            SubsystemTerrain.DestroyCell(0, p.X, p.Y, p.Z, 0, false, false);
        }
    }

    private void MovingBlocksStopped(IMovingBlockSet movingBlockSet)
    {
        if (movingBlockSet.Id != IdString)
        {
            return;
        }

        var p = Terrain.ToCell(
            MathUtils.Round(movingBlockSet.Position.X),
            MathUtils.Round(movingBlockSet.Position.Y),
            MathUtils.Round(movingBlockSet.Position.Z)
        );
        foreach (var block in movingBlockSet.Blocks)
        {
            var point = p + block.Offset;
            SubsystemTerrain.DestroyCell(0, point.X, point.Y, point.Z, block.Value, false, false);
        }

        _subsystemMovingBlocks.RemoveMovingBlockSet(movingBlockSet);
        if (movingBlockSet.Blocks.Count > 0)
        {
            _subsystemSoundMaterials.PlayImpactSound(movingBlockSet.Blocks[0].Value, movingBlockSet.Position, 1f);
        }
    }

    private void TryCollapseColumn(Point3 p)
    {
        if (GameManager.Role == WorldRole.RemoteClient)
        {
            return;
        }

        if (p.Y <= 0)
        {
            return;
        }

        var cellValue = SubsystemTerrain.Terrain.GetCellValue(p.X, p.Y - 1, p.Z);
        if (IsCollapseSupportBlock(cellValue))
        {
            return;
        }

        var list = new List<MovingBlock>();
        for (var i = p.Y; i < 256; i++)
        {
            var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(p.X, i, p.Z);
            if (!IsCollapsibleBlock(cellValue2))
            {
                break;
            }

            list.Add(
                new MovingBlock
                {
                    Value = cellValue2,
                    Offset = new Point3(0, i - p.Y, 0)
                }
            );
        }

        if (list.Count == 0 ||
            _subsystemMovingBlocks.AddMovingBlockSet(
                new Vector3(p),
                new Vector3(p.X, -list.Count - 1, p.Z),
                0f,
                10f,
                0.7f,
                new Vector2(0f),
                list,
                IdString,
                new object(),
                true
            ) ==
            null)
        {
            return;
        }

        foreach (var item in list)
        {
            var point = p + item.Offset;
            SubsystemTerrain.ChangeCell(point.X, point.Y, point.Z, 0);
        }
    }

    private bool IsCollapsibleBlock(int value)
    {
        return _handledBlocks.Contains(Terrain.ExtractContents(value));
    }

    private bool IsCollapseSupportBlock(int value)
    {
        var num = Terrain.ExtractContents(value);
        var block = CurrentModRuntime.Value!.RequireWorldCatalog().Blocks[num];
        return block.IsCollapseSupportBlock(SubsystemTerrain, value);
    }

    private static bool IsCollapseDestructibleBlock(int value)
    {
        var num = Terrain.ExtractContents(value);
        var block = CurrentModRuntime.Value!.RequireWorldCatalog().Blocks[num];
        return block.IsCollapseDestructibleBlock(value);
    }
}
