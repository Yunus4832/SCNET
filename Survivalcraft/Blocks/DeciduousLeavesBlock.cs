namespace Game.Blocks;

public abstract class DeciduousLeavesBlock : LeavesBlock
{
    public const string TypeName = "DeciduousLeavesBlock";

    public readonly Color AutumnColor1;

    public readonly Color AutumnColor2;

    public readonly float AutumnInterval;

    public readonly float AutumnIntervalInv;

    public readonly float AutumnSpeedupFactor;

    public readonly float AutumnStart;

    public readonly float AutumnTransitionLightening;

    public readonly BlockColorsMap BlockColorsMap;

    public readonly Color SpringColor;

    public readonly float SpringInterval;

    public readonly float SpringIntervalInv;

    public readonly float SpringStart;

    public readonly float SummerInterval;

    public readonly float SummerIntervalInv;

    public readonly float SummerStart;

    public readonly float WinterInterval;

    public readonly float WinterIntervalInv;

    public readonly float WinterStart;

    public Random SharedRandom1 = new();

    protected DeciduousLeavesBlock(
        float summerStart,
        float autumnStart,
        float winterStart,
        float springStart,
        BlockColorsMap blockColorsMap,
        Color autumnColor1,
        Color autumnColor2,
        float autumnTransitionLightening
    )
    {
        SummerStart = summerStart;
        AutumnStart = autumnStart;
        WinterStart = winterStart;
        SpringStart = springStart;
        SummerInterval = IntervalUtils.Interval(summerStart, autumnStart);
        AutumnInterval = IntervalUtils.Interval(autumnStart, winterStart);
        WinterInterval = IntervalUtils.Interval(winterStart, springStart);
        SpringInterval = IntervalUtils.Interval(springStart, summerStart);
        SummerIntervalInv = 1f / SummerInterval;
        AutumnIntervalInv = 1f / AutumnInterval;
        WinterIntervalInv = 1f / WinterInterval;
        SpringIntervalInv = 1f / SpringInterval;
        BlockColorsMap = blockColorsMap;
        AutumnColor1 = autumnColor1;
        AutumnColor2 = autumnColor2;
        AutumnTransitionLightening = autumnTransitionLightening;
        AutumnSpeedupFactor = 1.33f;
        SpringColor = new Color(160, 255, 90);
    }

    public override Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z)
    {
        var data = Terrain.ExtractData(value);
        switch (GetSeason(data))
        {
            case Season.Spring:
            {
                var c3 = BlockColorsMap.Lookup(terrain, x, y, z);
                var timeOfSeason = GetTimeOfSeason(data);
                return Color.LerpNotSaturated(SpringColor, c3, timeOfSeason);
            }
            case Season.Autumn:
            {
                var c = BlockColorsMap.Lookup(terrain, x, y, z);
                var c2 = Color.LerpNotSaturated(f: MathUtils.Hash((uint)(x + 59 * y + 2497 * z)) / 4.2949673E+09f,
                    c1: AutumnColor1, c2: AutumnColor2);
                var f2 = MathUtils.Min(GetTimeOfSeason(data) * AutumnSpeedupFactor, 1f);
                return Color.MultiplyColorOnly(s: MathUtils.Lerp(1f, AutumnTransitionLightening, Hat(f2)),
                    c: Color.LerpNotSaturated(c, c2, f2));
            }
            case Season.Winter:
                return Color.White;
            case Season.Summer:
            default:
                return BlockColorsMap.Lookup(terrain, x, y, z);
        }
    }

    public override Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environmentData)
    {
        var data = Terrain.ExtractData(value);
        switch (GetSeason(data))
        {
            case Season.Spring:
            {
                var c3 = BlockColorsMap.Lookup(environmentData);
                var timeOfSeason = GetTimeOfSeason(data);
                return Color.LerpNotSaturated(SpringColor, c3, timeOfSeason);
            }
            case Season.Autumn:
            {
                var c = BlockColorsMap.Lookup(environmentData);
                var c2 = Color.Lerp(AutumnColor1, AutumnColor2, 0.5f);
                var f = MathUtils.Min(GetTimeOfSeason(data) * AutumnSpeedupFactor, 1f);
                return Color.MultiplyColorOnly(s: MathUtils.Lerp(1f, AutumnTransitionLightening, Hat(f)),
                    c: Color.LerpNotSaturated(c, c2, f));
            }
            case Season.Winter:
                return Color.White;
            case Season.Summer:
            default:
                return BlockColorsMap.Lookup(environmentData);
        }
    }

    public override bool ShouldGenerateFace(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int neighborValue,
        int x,
        int y,
        int z
    )
    {
        if (!base.ShouldGenerateFace(subsystemTerrain, face, value, neighborValue, x, y, z))
        {
            return false;
        }

        if (Terrain.ExtractContents(value) != Terrain.ExtractContents(neighborValue))
        {
            return true;
        }

        var data = Terrain.ExtractData(value);
        var data2 = Terrain.ExtractData(neighborValue);
        var num = GetSeason(data) == Season.Winter;
        var flag = GetSeason(data2) == Season.Winter;
        return num != flag;
    }

    public override BlockPlacementData GetDigValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        int toolValue,
        TerrainRaycastResult raycastResult
    )
    {
        var data = Terrain.ExtractData(value);
        if (GetSeason(data) != Season.Autumn || !(GetTimeOfSeason(data) > 0.5f))
        {
            return base.GetDigValue(subsystemTerrain, componentMiner, value, toolValue, raycastResult);
        }

        subsystemTerrain.Project
            .FindSubsystem<SubsystemParticles>(true)!
            .AddParticleSystem(new LeavesParticleSystem(
                subsystemTerrain,
                raycastResult.CellFace.Point,
                8,
                false,
                true,
                value)
            );
        var result = default(BlockPlacementData);
        result.Value = Terrain.ReplaceData(value, SetSeason(SetIsShaken(data, true), Season.Winter));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        if (Terrain.ExtractContents(newValue) == Terrain.ExtractContents(oldValue))
        {
            showDebris = false;
        }
        else if (SharedRandom.Bool(0.25f))
        {
            dropValues.Add(new BlockDropValue
            {
                Value = 23,
                Count = 1
            });
            showDebris = true;
        }
        else
        {
            // 获取季节信息
            var season = GetSeason(Terrain.ExtractData(oldValue));
            // 获取方块的content信息
            var blockContents = Terrain.ExtractContents(oldValue);
            // 获取树叶的季节中的时间并位移
            // 季节中时间被保存在方块的14~16位里 (所以树叶在一个季节里共有7个状态)
            var timeOfSeason = (Terrain.ExtractData(oldValue) & 7) << 14;
            // 将季节加进获取到的content信息里
            // 季节信息被保存在方块的17~18位里
            var dropVaule = ((int)season << 17) | blockContents | timeOfSeason;
            dropValues.Add(new BlockDropValue
            {
                Value = dropVaule,
                Count = 1
            });
            showDebris = true;
        }
    }

    public override bool CanAutoStack(int value1, int value2)
    {
        return Terrain.ExtractContents(value1) == Terrain.ExtractContents(value2) &&
               GetSeason(Terrain.ExtractData(value1)) == GetSeason(Terrain.ExtractData(value2));
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var displayName = base.GetDisplayName(subsystemTerrain, value);
        var season = GetSeason(Terrain.ExtractData(value));
        var seasonPrefix = season switch
        {
            Season.Summer => LanguageControl.Get("DeciduousLeavesBlock", 0),
            Season.Autumn => LanguageControl.Get("DeciduousLeavesBlock", 1),
            Season.Winter => LanguageControl.Get("DeciduousLeavesBlock", 2),
            _ => LanguageControl.Get("DeciduousLeavesBlock", 3)
        };

        return seasonPrefix + displayName;
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetSeason(SetTimeOfSeason(0, 0f), Season.Spring));
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetSeason(SetTimeOfSeason(0, 0f), Season.Summer));
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetSeason(SetTimeOfSeason(0, 0.999f), Season.Autumn));
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetSeason(SetTimeOfSeason(0, 0f), Season.Winter));
    }

    public override int GetShadowStrength(int value)
    {
        var season = GetSeason(Terrain.ExtractData(value));

        var shadowStrength = season switch
        {
            Season.Winter => base.GetShadowStrength(value) / 3,
            Season.Spring => base.GetShadowStrength(value) / 2,
            _ => base.GetShadowStrength(value)
        };

        return shadowStrength;
    }


    public virtual float GetLeafDropProbability(int value)
    {
        var data = Terrain.ExtractData(value);
        var season = GetSeason(data);

        var probability = season switch
        {
            Season.Summer => 0.015f,
            Season.Autumn => MathUtils.Lerp(0.04f, 0.16f, GetTimeOfSeason(data)),
            _ => 0f
        };

        return probability;
    }


    public virtual int SetTimeOfYear(int data, float timeOfYear)
    {
        var num = IntervalUtils.Interval(SummerStart, timeOfYear);
        int num2;
        if (num < SummerInterval)
        {
            num2 = SetSeason(SetTimeOfSeason(data, num * SummerIntervalInv), Season.Summer);
        }
        else
        {
            var num3 = IntervalUtils.Interval(AutumnStart, timeOfYear);
            if (num3 < AutumnInterval)
            {
                num2 = SetSeason(SetTimeOfSeason(data, num3 * AutumnIntervalInv), Season.Autumn);
            }
            else
            {
                var num4 = IntervalUtils.Interval(WinterStart, timeOfYear);
                if (num4 < WinterInterval)
                {
                    num2 = SetSeason(SetTimeOfSeason(data, num4 * WinterIntervalInv), Season.Winter);
                }
                else
                {
                    var num5 = IntervalUtils.Interval(SpringStart, timeOfYear);
                    num2 = SetSeason(SetTimeOfSeason(data, num5 * SpringIntervalInv), Season.Spring);
                }
            }
        }

        if (!GetIsShaken(data))
        {
            return num2;
        }

        if (GetSeason(num2) == Season.Autumn)
        {
            return data;
        }

        if (GetSeason(num2) != Season.Winter)
        {
            num2 = SetIsShaken(num2, false);
        }

        return num2;
    }

    public static Season GetSeason(int data)
    {
        return (Season)((data >> 3) & 3);
    }

    public static int SetSeason(int data, Season season)
    {
        return (data & -25) | ((int)(season & Season.Spring) << 3);
    }

    public static float GetTimeOfSeason(int data)
    {
        return (data & 7) / 7f;
    }

    public static int SetTimeOfSeason(int data, float timeOfSeason)
    {
        var num = (int)(MathUtils.Clamp(timeOfSeason, 0f, 0.999f) * 8f);
        return (data & -8) | (num & 7);
    }

    public static bool GetIsShaken(int data)
    {
        return (data & 0x20) != 0;
    }

    public static int SetIsShaken(int data, bool isManuallyCleared)
    {
        return (data & -33) | (isManuallyCleared ? 32 : 0);
    }

    public static float Hat(float f)
    {
        return 1f - 2f * MathUtils.Abs(f - 0.5f);
    }
}
