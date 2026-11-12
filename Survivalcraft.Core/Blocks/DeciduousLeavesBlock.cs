namespace Game.Blocks;

public abstract class DeciduousLeavesBlock : LeavesBlock
{
    public const string TypeName = nameof(DeciduousLeavesBlock);


    public readonly float AutumnInterval;

    public readonly float AutumnIntervalInv;

    public readonly float AutumnSpeedupFactor;

    public readonly float AutumnStart;


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
        float springStart
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
        AutumnSpeedupFactor = 1.33f;
    }


    public override BlockDigData GetDigValue(
        SubsystemTerrain subsystemTerrain,
        int value,
        int toolValue,
        TerrainRaycastResult raycastResult
    )
    {
        var data = Terrain.ExtractData(value);
        if (GetSeason(data) != Season.Autumn || !(GetTimeOfSeason(data) > 0.5f))
        {
            return base.GetDigValue(subsystemTerrain, value, toolValue, raycastResult);
        }

        var result = default(BlockDigData);
        result.Value = Terrain.ReplaceData(value, SetSeason(SetIsShaken(data, true), Season.Winter));
        result.CellFace = raycastResult.CellFace;
        result.Kind = BlockDigOutcomeKind.LeavesShaken;
        return result;
    }

    public static bool IsShakeTransition(int oldValue, int newValue)
    {
        var oldData = Terrain.ExtractData(oldValue);
        var newData = Terrain.ExtractData(newValue);
        return Terrain.ExtractContents(oldValue) != 0 &&
               Terrain.ExtractContents(oldValue) == Terrain.ExtractContents(newValue) &&
               GetSeason(oldData) == Season.Autumn &&
               GetTimeOfSeason(oldData) > 0.5f &&
               GetSeason(newData) == Season.Winter &&
               GetIsShaken(newData);
    }

    public override bool AllowsDestructionDebris(int oldValue, int newValue) =>
        Terrain.ExtractContents(oldValue) != Terrain.ExtractContents(newValue);

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        showDebris = AllowsDestructionDebris(oldValue, newValue);
        if (!showDebris) { return; }

        if (SharedRandom.Bool(0.25f))
        {
            dropValues.Add(
                new BlockDropValue
                {
                    Value = 23,
                    Count = 1
                }
            );
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
            dropValues.Add(
                new BlockDropValue
                {
                    Value = dropVaule,
                    Count = 1
                }
            );
        }
    }

    public override bool CanAutoStack(int value1, int value2)
    {
        return Terrain.ExtractContents(value1) == Terrain.ExtractContents(value2) &&
               GetSeason(Terrain.ExtractData(value1)) == GetSeason(Terrain.ExtractData(value2));
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, SetSeason(SetTimeOfSeason(0, 0f), Season.Spring));
        yield return Terrain.MakeBlockValue(BlockIndex, SetSeason(SetTimeOfSeason(0, 0f), Season.Summer));
        yield return Terrain.MakeBlockValue(BlockIndex, SetSeason(SetTimeOfSeason(0, 0.999f), Season.Autumn));
        yield return Terrain.MakeBlockValue(BlockIndex, SetSeason(SetTimeOfSeason(0, 0f), Season.Winter));
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
