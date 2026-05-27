using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemPlantBlockBehavior : SubsystemPollableBlockBehavior //IUpdateable
{
    private readonly Random _random = new();

    private SubsystemCellChangeQueue _subsystemCellChangeQueue = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemSeasons _subsystemSeasons = null!;

    private SubsystemTime _subsystemTime = null!;

    public override int[] HandledBlocks =>
    [
        19,
        20,
        24,
        25,
        28,
        99,
        131,
        244,
        132,
        174,
        204
    ];

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        Lifecycle(value, x, y, z, true);
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var num = Terrain.ExtractContents(SubsystemTerrain.Terrain.GetCellValue(x, y, z));
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
        var num2 = Terrain.ExtractContents(cellValue);
        switch (num)
        {
            case 131:
            case 244:
                if (num2 != 8 && num2 != 2 && num2 != 168)
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
            case 132:
            {
                var block = BlocksManager.Blocks[num2];
                if (block.IsFaceTransparent(SubsystemTerrain, 4, cellValue) && !(block is FenceBlock))
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
            }
            default:
                if (num2 != 8 && num2 != 2 && num2 != 7 && num2 != 168)
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
        }
    }

    public override void OnPoll(int value, int x, int y, int z, int pollPass)
    {
        if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode != EnvironmentBehaviorMode.Living)
        {
            return;
        }

        Grow(value, x, y, z, pollPass);
        Lifecycle(value, x, y, z, false);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemCellChangeQueue = Project.FindSubsystem<SubsystemCellChangeQueue>(true)!;
        _subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true)!;
    }

    private void Grow(int value, int x, int y, int z, int pollPass)
    {
        if (y is <= 0 or >= 255)
        {
            return;
        }

        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        if (num == 19)
        {
            GrowTallGrass(value, x, y, z, pollPass);
            return;
        }

        if (block is FlowerBlock)
        {
            GrowFlower(value, x, y, z, pollPass);
            return;
        }

        switch (num)
        {
            case 174:
                GrowRye(value, x, y, z, pollPass);
                break;
            case 204:
                GrowCotton(value, x, y, z, pollPass);
                break;
            case 131:
                GrowPumpkin(value, x, y, z, pollPass);
                break;
        }
    }

    private void GrowTallGrass(int value, int x, int y, int z, int pollPass)
    {
        var data = Terrain.ExtractData(value);
        if (!TallGrassBlock.GetIsSmall(data) ||
            Terrain.ExtractLight(SubsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z)) < 9)
        {
            return;
        }

        var data2 = TallGrassBlock.SetIsSmall(data, false);
        var value2 = Terrain.ReplaceData(value, data2);
        _subsystemCellChangeQueue.QueueCellChange(x, y, z, value2);
    }

    private void GrowFlower(int value, int x, int y, int z, int pollPass)
    {
        var data = Terrain.ExtractData(value);
        if (!FlowerBlock.GetIsSmall(data) ||
            Terrain.ExtractLight(SubsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z)) < 9)
        {
            return;
        }

        var data2 = FlowerBlock.SetIsSmall(data, false);
        var value2 = Terrain.ReplaceData(value, data2);
        _subsystemCellChangeQueue.QueueCellChange(x, y, z, value2);
    }

    private void GrowRye(int value, int x, int y, int z, int pollPass)
    {
        if (Terrain.ExtractLight(SubsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z)) < 9)
        {
            return;
        }

        var data = Terrain.ExtractData(value);
        var size = RyeBlock.GetSize(data);
        if (size == 7)
        {
            return;
        }

        if (RyeBlock.GetIsWild(data))
        {
            if (size >= 7)
            {
                return;
            }

            var data2 = RyeBlock.SetSize(RyeBlock.SetIsWild(data, true), size + 1);
            var value2 = Terrain.ReplaceData(value, data2);
            _subsystemCellChangeQueue.QueueCellChange(x, y, z, value2);

            return;
        }

        var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
        if (Terrain.ExtractContents(cellValueFast) == 168)
        {
            var data3 = Terrain.ExtractData(cellValueFast);
            var hydration = SoilBlock.GetHydration(data3);
            var nitrogen = SoilBlock.GetNitrogen(data3);
            var num = SubsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                      SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            var num2 = 4;
            var num3 = 0.8f;
            if (nitrogen > 0)
            {
                num2--;
                num3 -= 0.4f;
            }

            if (hydration)
            {
                num2--;
                num3 -= 0.4f;
            }

            if (num <= 4)
            {
                num2 += 4;
            }

            if (pollPass % MathUtils.Max(num2, 1) != 0 && num3 < 1f)
            {
                return;
            }

            var data4 = RyeBlock.SetSize(data, MathUtils.Min(size + 1, 7));
            if (_random.Float(0f, 1f) < num3 && size == 3)
            {
                data4 = RyeBlock.SetIsWild(data4, true);
            }

            var value3 = Terrain.ReplaceData(value, data4);
            _subsystemCellChangeQueue.QueueCellChange(x, y, z, value3);
            if (size + 1 != 7)
            {
                return;
            }

            var data5 = SoilBlock.SetNitrogen(data3, MathUtils.Max(nitrogen - 1, 0));
            var value4 = Terrain.ReplaceData(cellValueFast, data5);
            _subsystemCellChangeQueue.QueueCellChange(x, y - 1, z, value4);
        }
        else
        {
            var value5 = Terrain.ReplaceData(value, RyeBlock.SetIsWild(data, true));
            _subsystemCellChangeQueue.QueueCellChange(x, y, z, value5);
        }
    }

    private void GrowCotton(int value, int x, int y, int z, int pollPass)
    {
        if (Terrain.ExtractLight(SubsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z)) < 9)
        {
            return;
        }

        var data = Terrain.ExtractData(value);
        var size = CottonBlock.GetSize(data);
        if (size >= 2)
        {
            return;
        }

        if (CottonBlock.GetIsWild(data))
        {
            var data2 = CottonBlock.SetSize(CottonBlock.SetIsWild(data, true), size + 1);
            var value2 = Terrain.ReplaceData(value, data2);
            _subsystemCellChangeQueue.QueueCellChange(x, y, z, value2);

            return;
        }

        var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
        if (Terrain.ExtractContents(cellValueFast) == 168)
        {
            var data3 = Terrain.ExtractData(cellValueFast);
            var hydration = SoilBlock.GetHydration(data3);
            var nitrogen = SoilBlock.GetNitrogen(data3);
            var num = SubsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                      SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            var num2 = 8;
            var num3 = 0.8f;
            if (nitrogen > 0)
            {
                num2 -= 2;
                num3 -= 0.4f;
            }

            if (hydration)
            {
                num2 -= 2;
                num3 -= 0.4f;
            }

            if (num <= 4)
            {
                num2 += 8;
            }

            if (pollPass % MathUtils.Max(num2, 1) != 0 && num3 < 1f)
            {
                return;
            }

            var data4 = CottonBlock.SetSize(data, MathUtils.Min(size + 1, 2));
            if (_random.Float(0f, 1f) < num3 && size == 1)
            {
                data4 = CottonBlock.SetIsWild(data4, true);
            }

            var value3 = Terrain.ReplaceData(value, data4);
            _subsystemCellChangeQueue.QueueCellChange(x, y, z, value3);
            if (size + 1 != 2)
            {
                return;
            }

            var data5 = SoilBlock.SetNitrogen(data3, MathUtils.Max(nitrogen - 1, 0));
            var value4 = Terrain.ReplaceData(cellValueFast, data5);
            _subsystemCellChangeQueue.QueueCellChange(x, y - 1, z, value4);
        }
        else
        {
            var value5 = Terrain.ReplaceData(value, CottonBlock.SetIsWild(data, true));
            _subsystemCellChangeQueue.QueueCellChange(x, y, z, value5);
        }
    }

    private void GrowPumpkin(int value, int x, int y, int z, int pollPass)
    {
        if (Terrain.ExtractLight(SubsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z)) < 9)
        {
            return;
        }

        var data = Terrain.ExtractData(value);
        var size = BasePumpkinBlock.GetSize(data);
        if (BasePumpkinBlock.GetIsDead(data) || size >= 7)
        {
            return;
        }

        var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
        var num = Terrain.ExtractContents(cellValueFast);
        var data2 = Terrain.ExtractData(cellValueFast);
        var flag = num == 168 && SoilBlock.GetHydration(data2);
        var num2 = num == 168 ? SoilBlock.GetNitrogen(data2) : 0;
        var num3 = SubsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                   SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
        var num4 = 4;
        var num5 = 0.15f;
        if (num == 168)
        {
            num4--;
            num5 -= 0.05f;
        }

        if (num2 > 0)
        {
            num4--;
            num5 -= 0.05f;
        }

        if (flag)
        {
            num4--;
            num5 -= 0.05f;
        }

        if (num3 <= 4)
        {
            num4 += 5;
        }

        if (pollPass % MathUtils.Max(num4, 1) != 0 && num5 < 1f)
        {
            return;
        }

        var data3 = BasePumpkinBlock.SetSize(data, MathUtils.Min(size + 1, 7));
        if (_random.Float(0f, 1f) < num5)
        {
            data3 = BasePumpkinBlock.SetIsDead(data3, true);
        }

        var value2 = Terrain.ReplaceData(value, data3);
        _subsystemCellChangeQueue.QueueCellChange(x, y, z, value2);
        if (num != 168 || size + 1 != 7)
        {
            return;
        }

        var data4 = SoilBlock.SetNitrogen(data2, MathUtils.Max(num2 - 3, 0));
        var value3 = Terrain.ReplaceData(cellValueFast, data4);
        _subsystemCellChangeQueue.QueueCellChange(x, y - 1, z, value3);
    }

    private void Lifecycle(int value, int x, int y, int z, bool applyImmediately)
    {
    }
}
