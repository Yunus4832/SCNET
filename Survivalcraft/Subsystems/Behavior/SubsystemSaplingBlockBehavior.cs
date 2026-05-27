using System.Globalization;
using System.Text;

using Engine.Serialization;

using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemSaplingBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    private Dictionary<Point3, SaplingData>.ValueCollection.Enumerator _enumerator;

    private readonly Random _random = new();

    private readonly Dictionary<Point3, SaplingData> _saplings = new();

    private readonly StringBuilder _stringBuilder = new();

    private SubsystemGameInfo _subsystemGameInfo = null!;

    public override int[] HandledBlocks => [119];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var num = 0;
        while (true)
        {
            if (num >= 10)
            {
                return;
            }

            if (!_enumerator.MoveNext())
            {
                break;
            }

            MatureSapling(_enumerator.Current);
            num++;
        }

        _enumerator = _saplings.Values.GetEnumerator();
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
        if (BlocksManager.Blocks[cellContents].Transparent)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        var num = _subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative
            ? _random.Float(8f, 12f)
            : _random.Float(480f, 600f);
        AddSapling(new SaplingData
        {
            Point = new Point3(x, y, z),
            Type = (TreeType)Terrain.ExtractData(value),
            MatureTime = _subsystemGameInfo.TotalElapsedGameTime + num
        });
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        RemoveSapling(new Point3(x, y, z));
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _enumerator = _saplings.Values.GetEnumerator();
        foreach (string value in valuesDictionary.GetValue<ValuesDictionary>("Saplings").Values)
        {
            AddSapling(LoadSaplingData(value));
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Saplings", valuesDictionary2);
        var num = 0;
        foreach (var value in _saplings.Values)
        {
            valuesDictionary2.SetValue(num++.ToString(CultureInfo.InvariantCulture), SaveSaplingData(value));
        }
    }

    public SaplingData LoadSaplingData(string data)
    {
        var array = data.Split(new[] { ";" }, StringSplitOptions.None);
        if (array.Length != 3)
        {
            throw new InvalidOperationException("Invalid sapling data string.");
        }

        return new SaplingData
        {
            Point = HumanReadableConverter.ConvertFromString<Point3>(array[0]),
            Type = HumanReadableConverter.ConvertFromString<TreeType>(array[1]),
            MatureTime = HumanReadableConverter.ConvertFromString<double>(array[2])
        };
    }

    public string SaveSaplingData(SaplingData saplingData)
    {
        _stringBuilder.Length = 0;
        _stringBuilder.Append(HumanReadableConverter.ConvertToString(saplingData.Point));
        _stringBuilder.Append(';');
        _stringBuilder.Append(HumanReadableConverter.ConvertToString(saplingData.Type));
        _stringBuilder.Append(';');
        _stringBuilder.Append(HumanReadableConverter.ConvertToString(saplingData.MatureTime));
        return _stringBuilder.ToString();
    }

    public void MatureSapling(SaplingData saplingData)
    {
        if (!(_subsystemGameInfo.TotalElapsedGameTime >= saplingData.MatureTime))
        {
            return;
        }

        var x = saplingData.Point.X;
        var y = saplingData.Point.Y;
        var z = saplingData.Point.Z;
        if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(x, z, out Territoriy? territoriy))
        {
            if (!territoriy!.AllowBlockBehavior)
            {
                return;
            }
        }

        var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(x - 6, z - 6, false);
        var chunkAtCell2 = SubsystemTerrain.Terrain.GetChunkAtCell(x - 6, z + 6, false);
        var chunkAtCell3 = SubsystemTerrain.Terrain.GetChunkAtCell(x + 6, z - 6, false);
        var chunkAtCell4 = SubsystemTerrain.Terrain.GetChunkAtCell(x + 6, z + 6, false);
        if (chunkAtCell is { State: TerrainChunkState.Valid } &&
            chunkAtCell2 is { State: TerrainChunkState.Valid } &&
            chunkAtCell3 is { State: TerrainChunkState.Valid } &&
            chunkAtCell4 is { State: TerrainChunkState.Valid })
        {
            var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
            if (cellContents is 2 or 8)
            {
                if (SubsystemTerrain.Terrain.GetCellLight(x, y + 1, z) >= 9)
                {
                    var flag = false;
                    for (var i = x - 1; i <= x + 1; i++)
                    for (var j = z - 1; j <= z + 1; j++)
                    {
                        var cellContents2 = SubsystemTerrain.Terrain.GetCellContents(i, y - 1, j);
                        if (BlocksManager.Blocks[cellContents2] is not WaterBlock)
                        {
                            continue;
                        }

                        flag = true;
                        break;
                    }

                    float num;
                    if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
                    {
                        num = 1f;
                    }
                    else
                    {
                        var num2 = SubsystemTerrain.Terrain.GetTemperature(x, z) +
                                   SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
                        var num3 = SubsystemTerrain.Terrain.GetHumidity(x, z);
                        if (flag)
                        {
                            num2 = (num2 + 10) / 2;
                            num3 = MathUtils.Max(num3, 12);
                        }

                        num = 2f * PlantsManager.CalculateTreeProbability(saplingData.Type, num2, num3, y);
                    }

                    if (_random.Bool(num))
                    {
                        SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(0, 0, 0));
                        if (!GrowTree(x, y, z, saplingData.Type))
                        {
                            SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(28, 0, 0));
                        }
                    }
                    else
                    {
                        SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(28, 0, 0));
                    }
                }
                else if (_subsystemGameInfo.TotalElapsedGameTime > saplingData.MatureTime + 1200.0)
                {
                    SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(28, 0, 0));
                }
            }
            else
            {
                SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(28, 0, 0));
            }
        }
        else
        {
            saplingData.MatureTime = _subsystemGameInfo.TotalElapsedGameTime;
        }
    }

    public bool GrowTree(int x, int y, int z, TreeType treeType)
    {
        var treeBrushes = PlantsManager.GetTreeBrushes(treeType);
        for (var i = 0; i < 20; i++)
        {
            var terrainBrush = treeBrushes[_random.Int(0, treeBrushes.Count - 1)];
            var flag = true;
            var cells = terrainBrush.Cells;
            foreach (var cell in cells)
            {
                if (cell.Y < 0 || cell is { X: 0, Y: 0, Z: 0 })
                {
                    continue;
                }

                var cellContents = SubsystemTerrain.Terrain.GetCellContents(cell.X + x, cell.Y + y, cell.Z + z);
                if (cellContents == 0 || BlocksManager.Blocks[cellContents] is LeavesBlock)
                {
                    continue;
                }

                flag = false;
                break;
            }

            if (!flag)
            {
                continue;
            }

            terrainBrush.Paint(SubsystemTerrain, x, y, z);
            return true;
        }

        return false;
    }

    public void AddSapling(SaplingData saplingData)
    {
        _saplings[saplingData.Point] = saplingData;
        _enumerator = _saplings.Values.GetEnumerator();
    }

    public void RemoveSapling(Point3 point)
    {
        _saplings.Remove(point);
        _enumerator = _saplings.Values.GetEnumerator();
    }

    public class SaplingData
    {
        public double MatureTime;
        public Point3 Point;

        public TreeType Type;
    }
}
