using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemPlantBlockBehavior : SubsystemPollableBlockBehavior //IUpdateable
{
    private readonly Random _random = new();

    private SubsystemCellChangeQueue _subsystemCellChangeQueue = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemSeasons _subsystemSeasons = null!;

    private SubsystemTime _subsystemTime = null!;

    public override ResourceId[] HandledBlocks =>
    [
        ResourceId.Parse("game:tall_grass"), ResourceId.Parse("game:red_flower"),
        ResourceId.Parse("game:purple_flower"), ResourceId.Parse("game:white_flower"),
        ResourceId.Parse("game:dry_bush"), ResourceId.Parse("game:large_dry_bush"), ResourceId.Parse("game:pumpkin"),
        ResourceId.Parse("game:rotten_pumpkin"), ResourceId.Parse("game:jack_o_lantern"), ResourceId.Parse("game:rye"),
        ResourceId.Parse("game:cotton")
    ];

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        Lifecycle(value, x, y, z, true);
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var plantContents = Terrain.ExtractContents(SubsystemTerrain.Terrain.GetCellValue(x, y, z));
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
        var supportContents = Terrain.ExtractContents(cellValue);
        switch (plantContents)
        {
            case 131:
            case 244:
                if (supportContents != 8 && supportContents != 2 && supportContents != 168)
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
            case 132:
            {
                var block = WorldContentBinding.For(Project).Catalog.Blocks[supportContents];
                if (block.IsFaceTransparent(SubsystemTerrain, 4, cellValue) && !(block is FenceBlock))
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
            }
            default:
                if (supportContents != 8 && supportContents != 2 && supportContents != 7 && supportContents != 168)
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

        var contents = Terrain.ExtractContents(value);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[contents];
        if (contents == 19)
        {
            GrowTallGrass(value, x, y, z, pollPass);
            return;
        }

        if (block is FlowerBlock)
        {
            GrowFlower(value, x, y, z, pollPass);
            return;
        }

        switch (contents)
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
            !SubsystemTerrain.TryGetEffectiveLight(x, y + 1, z, out var light) ||
            light < 9)
        {
            return;
        }

        var data2 = TallGrassBlock.SetIsSmall(data, false);
        var value2 = Terrain.ReplaceData(value, data2);
        if (!_subsystemCellChangeQueue.TryQueueCellChange(x, y, z, value2)) { return; }
    }

    private void GrowFlower(int value, int x, int y, int z, int pollPass)
    {
        var data = Terrain.ExtractData(value);
        if (!FlowerBlock.GetIsSmall(data) ||
            !SubsystemTerrain.TryGetEffectiveLight(x, y + 1, z, out var light) ||
            light < 9)
        {
            return;
        }

        var data2 = FlowerBlock.SetIsSmall(data, false);
        var value2 = Terrain.ReplaceData(value, data2);
        if (!_subsystemCellChangeQueue.TryQueueCellChange(x, y, z, value2)) { return; }
    }

    private void GrowRye(int value, int x, int y, int z, int pollPass)
    {
        if (!SubsystemTerrain.TryGetEffectiveLight(x, y + 1, z, out var light) || light < 9)
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
            if (!_subsystemCellChangeQueue.TryQueueCellChange(x, y, z, value2)) { return; }

            return;
        }

        var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
        if (Terrain.ExtractContents(cellValueFast) == 168)
        {
            var data3 = Terrain.ExtractData(cellValueFast);
            var hydration = SoilBlock.GetHydration(data3);
            var nitrogen = SoilBlock.GetNitrogen(data3);
            var temperature = SubsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                              SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            var growthInterval = 4;
            var wildChance = 0.8f;
            if (nitrogen > 0)
            {
                growthInterval--;
                wildChance -= 0.4f;
            }

            if (hydration)
            {
                growthInterval--;
                wildChance -= 0.4f;
            }

            if (temperature <= 4)
            {
                growthInterval += 4;
            }

            if (pollPass % MathUtils.Max(growthInterval, 1) != 0 && wildChance < 1f)
            {
                return;
            }

            var data4 = RyeBlock.SetSize(data, MathUtils.Min(size + 1, 7));
            if (_random.Float(0f, 1f) < wildChance && size == 3)
            {
                data4 = RyeBlock.SetIsWild(data4, true);
            }

            var value3 = Terrain.ReplaceData(value, data4);
            if (size + 1 != 7)
            {
                if (!_subsystemCellChangeQueue.TryQueueCellChange(x, y, z, value3)) { return; }

                return;
            }

            var data5 = SoilBlock.SetNitrogen(data3, MathUtils.Max(nitrogen - 1, 0));
            var value4 = Terrain.ReplaceData(cellValueFast, data5);
            if (!_subsystemCellChangeQueue.TryQueueCellChanges(
                    [(new Point3(x, y, z), value3), (new Point3(x, y - 1, z), value4)]
                )) { return; }
        }
        else
        {
            var value5 = Terrain.ReplaceData(value, RyeBlock.SetIsWild(data, true));
            if (!_subsystemCellChangeQueue.TryQueueCellChange(x, y, z, value5)) { return; }
        }
    }

    private void GrowCotton(int value, int x, int y, int z, int pollPass)
    {
        if (!SubsystemTerrain.TryGetEffectiveLight(x, y + 1, z, out var light) || light < 9)
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
            if (!_subsystemCellChangeQueue.TryQueueCellChange(x, y, z, value2)) { return; }

            return;
        }

        var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
        if (Terrain.ExtractContents(cellValueFast) == 168)
        {
            var data3 = Terrain.ExtractData(cellValueFast);
            var hydration = SoilBlock.GetHydration(data3);
            var nitrogen = SoilBlock.GetNitrogen(data3);
            var temperature = SubsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                              SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            var growthInterval = 8;
            var wildChance = 0.8f;
            if (nitrogen > 0)
            {
                growthInterval -= 2;
                wildChance -= 0.4f;
            }

            if (hydration)
            {
                growthInterval -= 2;
                wildChance -= 0.4f;
            }

            if (temperature <= 4)
            {
                growthInterval += 8;
            }

            if (pollPass % MathUtils.Max(growthInterval, 1) != 0 && wildChance < 1f)
            {
                return;
            }

            var data4 = CottonBlock.SetSize(data, MathUtils.Min(size + 1, 2));
            if (_random.Float(0f, 1f) < wildChance && size == 1)
            {
                data4 = CottonBlock.SetIsWild(data4, true);
            }

            var value3 = Terrain.ReplaceData(value, data4);
            if (size + 1 != 2)
            {
                if (!_subsystemCellChangeQueue.TryQueueCellChange(x, y, z, value3)) { return; }

                return;
            }

            var data5 = SoilBlock.SetNitrogen(data3, MathUtils.Max(nitrogen - 1, 0));
            var value4 = Terrain.ReplaceData(cellValueFast, data5);
            if (!_subsystemCellChangeQueue.TryQueueCellChanges(
                    [(new Point3(x, y, z), value3), (new Point3(x, y - 1, z), value4)]
                )) { return; }
        }
        else
        {
            var value5 = Terrain.ReplaceData(value, CottonBlock.SetIsWild(data, true));
            if (!_subsystemCellChangeQueue.TryQueueCellChange(x, y, z, value5)) { return; }
        }
    }

    private void GrowPumpkin(int value, int x, int y, int z, int pollPass)
    {
        if (!SubsystemTerrain.TryGetEffectiveLight(x, y + 1, z, out var light) || light < 9)
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
        var soilContents = Terrain.ExtractContents(cellValueFast);
        var data2 = Terrain.ExtractData(cellValueFast);
        var isHydrated = soilContents == 168 && SoilBlock.GetHydration(data2);
        var nitrogen = soilContents == 168 ? SoilBlock.GetNitrogen(data2) : 0;
        var temperature = SubsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                          SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
        var growthInterval = 4;
        var deathChance = 0.15f;
        if (soilContents == 168)
        {
            growthInterval--;
            deathChance -= 0.05f;
        }

        if (nitrogen > 0)
        {
            growthInterval--;
            deathChance -= 0.05f;
        }

        if (isHydrated)
        {
            growthInterval--;
            deathChance -= 0.05f;
        }

        if (temperature <= 4)
        {
            growthInterval += 5;
        }

        if (pollPass % MathUtils.Max(growthInterval, 1) != 0 && deathChance < 1f)
        {
            return;
        }

        var data3 = BasePumpkinBlock.SetSize(data, MathUtils.Min(size + 1, 7));
        if (_random.Float(0f, 1f) < deathChance)
        {
            data3 = BasePumpkinBlock.SetIsDead(data3, true);
        }

        var value2 = Terrain.ReplaceData(value, data3);
        if (soilContents != 168 || size + 1 != 7)
        {
            if (!_subsystemCellChangeQueue.TryQueueCellChange(x, y, z, value2)) { return; }

            return;
        }

        var data4 = SoilBlock.SetNitrogen(data2, MathUtils.Max(nitrogen - 3, 0));
        var value3 = Terrain.ReplaceData(cellValueFast, data4);
        if (!_subsystemCellChangeQueue.TryQueueCellChanges(
                [(new Point3(x, y, z), value2), (new Point3(x, y - 1, z), value3)]
            )) { return; }
    }

    private void Lifecycle(int value, int x, int y, int z, bool applyImmediately)
    {
    }
}
