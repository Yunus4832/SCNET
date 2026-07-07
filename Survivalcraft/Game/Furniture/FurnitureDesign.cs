using System.Globalization;
using System.Text;

using EntitySystem.TemplatesDatabase;

namespace Game;

public class FurnitureDesign
{
    public const int MinResolution = 2;

    public const int MaxDesign = 4096; //家具上限数量

    public const int MaxResolution = 256; //家具格分辨率

    public const int MaxTriangles = 65536; //家具复杂度

    public const int MaxNameLength = 100;

    private const string _typeName = "FurnitureDesign";

    private Box? _box;

    private BoundingBox[][]? _collisionBoxesByRotation;

    public bool GcUsed;

    private FurnitureGeometry? _geometry;

    private int? _hash;

    private int _index = -1;

    private BoundingBox[][]? _interactionBoxesByRotation;

    private FurnitureInteractionMode _interactionMode = FurnitureInteractionMode.None;

    public int LoadTimeLinkedDesignIndex { get; } = -1;

    private int _mainValue;

    private int _mountingFacesMask = -1;

    private int? _shadowStrengthFactor;

    private readonly SubsystemTerrain? _subsystemTerrain;

    public int TerrainUseCount;

    private BoundingBox[][]? _torchPointsByRotation;

    private int _transparentFacesMask = -1;

    private int[] _values = [];

    public FurnitureDesign(SubsystemTerrain? subsystemTerrain)
    {
        _subsystemTerrain = subsystemTerrain;
    }

    public FurnitureDesign(int index, SubsystemTerrain? subsystemTerrain, ValuesDictionary valuesDictionary)
    {
        _subsystemTerrain = subsystemTerrain;
        _index = index;
        Name = valuesDictionary.GetValue("Name", string.Empty);
        TerrainUseCount = valuesDictionary.GetValue<int>("TerrainUseCount");
        var value = valuesDictionary.GetValue<int>("Resolution");
        InteractionMode = valuesDictionary.GetValue<FurnitureInteractionMode>("InteractionMode");
        LoadTimeLinkedDesignIndex = valuesDictionary.GetValue("LinkedDesign", -1);
        var value2 = valuesDictionary.GetValue<string>("Values");
        var num = 0;
        var array = new int[value * value * value];
        var array2 = value2.Split([','], StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in array2)
        {
            var array3 = item.Split(['*'], StringSplitOptions.None);
            if (array3.Length != 2)
            {
                throw new InvalidOperationException(LanguageManager.Get(_typeName, 2));
            }

            var num2 = int.Parse(array3[0], CultureInfo.InvariantCulture);
            var num3 = int.Parse(array3[1], CultureInfo.InvariantCulture);
            var num4 = 0;
            while (num4 < num2)
            {
                array[num] = num3;
                num4++;
                num++;
            }
        }

        SetValues(value, array);
    }

    public int Resolution { get; private set; }

    public int Hash
    {
        get
        {
            _hash ??= Resolution + ((int)_interactionMode << 4);
            for (var i = 0; i < _values.Length; i++)
            {
                _hash += _values[i] * (1 + 113 * i);
            }

            return _hash!.Value;
        }
    }

    public Box Box
    {
        get
        {
            return _box ??= CalculateBox(
                new Box(0, 0, 0, Resolution, Resolution, Resolution),
                CreatePrecedingEmptySpacesArray()
            );
        }
    }

    public int ShadowStrengthFactor
    {
        get { return _shadowStrengthFactor ??= CalculateShadowStrengthFactor(); }
    }

    public bool IsLightEmitter => GetTorchPoints(0).Length != 0;

    public int MainValue
    {
        get
        {
            if (_mainValue == 0)
            {
                CalculateMainValue();
            }

            return _mainValue;
        }
    }

    public int MountingFacesMask
    {
        get
        {
            if (_mountingFacesMask < 0)
            {
                CalculateFacesMasks();
            }

            return _mountingFacesMask;
        }
    }

    public int TransparentFacesMask
    {
        get
        {
            if (_transparentFacesMask < 0)
            {
                CalculateFacesMasks();
            }

            return _transparentFacesMask;
        }
    }

    public int Index
    {
        get => _index;
        set => _index = value;
    }

    public string Name
    {
        get;
        set
        {
            if (value.Length > 0)
            {
                if (value[0] == ' ' || value[^1] == ' ')
                {
                    throw new InvalidOperationException(LanguageManager.Get(_typeName, 1));
                }

                var text = value;
                foreach (var c in text)
                {
                    if (c > '\u007f' || (!char.IsLetterOrDigit(c) && c != ' '))
                    {
                        throw new InvalidOperationException(LanguageManager.Get(_typeName, 1));
                    }
                }

                if (value.Length > 20)
                {
                    value = value.Substring(0, 20);
                }
            }

            field = value;
        }
    } = string.Empty;

    public FurnitureSet FurnitureSet { get; set; } = FurnitureSetDefault.Default;

    public FurnitureDesign? LinkedDesign
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _hash = null;
        }
    }

    public FurnitureInteractionMode InteractionMode
    {
        get => _interactionMode;
        set
        {
            if (value == _interactionMode)
            {
                return;
            }

            _interactionMode = value;
            _hash = null;
        }
    }

    public FurnitureGeometry Geometry => _geometry ??= CreateGeometry();

    public int GetValue(int index)
    {
        return _values[index];
    }

    public void SetValues(int resolution, int[] values)
    {
        if (resolution is < 2 or > MaxResolution)
        {
            throw new ArgumentException(LanguageManager.Get(_typeName, 3));
        }

        if (values.Length != resolution * resolution * resolution)
        {
            throw new ArgumentException(LanguageManager.Get(_typeName, 4));
        }

        Resolution = resolution;
        if (_values.Length != resolution * resolution * resolution)
        {
            _values = new int[resolution * resolution * resolution];
        }

        values.CopyTo(_values, 0);
        _hash = null;
        _geometry = null;
        _box = null;
        _collisionBoxesByRotation = null;
        _interactionBoxesByRotation = null;
        _torchPointsByRotation = null;
        _mainValue = 0;
        _mountingFacesMask = -1;
        _transparentFacesMask = -1;
    }

    public string GetDefaultName()
    {
        if (InteractionMode == FurnitureInteractionMode.Multistate)
        {
            var count = ListChain().Count;
            if (count > 1)
            {
                return string.Format(LanguageManager.Get(_typeName, 5), count);
            }
        }
        else
        {
            if (InteractionMode == FurnitureInteractionMode.ElectricButton)
            {
                return LanguageManager.Get(_typeName, 6);
            }

            if (InteractionMode == FurnitureInteractionMode.ElectricSwitch)
            {
                return LanguageManager.Get(_typeName, 7);
            }

            if (InteractionMode != FurnitureInteractionMode.ConnectedMultistate)
            {
                return LanguageManager.Get(_typeName, 9);
            }

            var count2 = ListChain().Count;
            if (count2 > 1)
            {
                return string.Format(LanguageManager.Get(_typeName, 8), count2);
            }
        }

        return LanguageManager.Get(_typeName, 9);
    }

    public BoundingBox[] GetCollisionBoxes(int rotation)
    {
        _collisionBoxesByRotation ??= CreateCollisionAndInteractionBoxes();
        return _collisionBoxesByRotation[rotation];
    }

    public BoundingBox[] GetInteractionBoxes(int rotation)
    {
        _interactionBoxesByRotation ??= CreateCollisionAndInteractionBoxes();
        return _interactionBoxesByRotation[rotation];
    }

    public BoundingBox[] GetTorchPoints(int rotation)
    {
        _torchPointsByRotation ??= CreateTorchPoints();
        return _torchPointsByRotation[rotation];
    }

    public void Paint(int? color)
    {
        var array = new int[_values.Length];
        for (var i = 0; i < _values.Length; i++)
        {
            var num = _values[i];
            var num2 = Terrain.ExtractContents(num);
            var paintableBlock = BlocksManager.Blocks[num2] as IPaintableBlock;
            array[i] = paintableBlock?.Paint(null, num, color) ?? num;
        }

        SetValues(Resolution, array);
    }

    public void Resize(int resolution)
    {
        if (resolution is < 2 or > MaxResolution)
        {
            throw new ArgumentException(LanguageManager.Get(_typeName, 3));
        }

        if (resolution == Resolution)
        {
            return;
        }

        var array = new int[resolution * resolution * resolution];
        for (var i = 0; i < resolution; i++)
        for (var j = 0; j < resolution; j++)
        for (var k = 0; k < resolution; k++)
        {
            if (k >= 0 && k < Resolution && j >= 0 && j < Resolution && i >= 0 && i < Resolution)
            {
                array[k + j * resolution + i * resolution * resolution] =
                    _values[k + j * Resolution + i * Resolution * Resolution];
            }
        }

        SetValues(resolution, array);
    }

    public void Shift(Point3 delta)
    {
        if (!(delta != Point3.Zero))
        {
            return;
        }

        var array = new int[Resolution * Resolution * Resolution];
        for (var i = 0; i < Resolution; i++)
        for (var j = 0; j < Resolution; j++)
        for (var k = 0; k < Resolution; k++)
        {
            var num = k + delta.X;
            var num2 = j + delta.Y;
            var num3 = i + delta.Z;
            if (num >= 0 && num < Resolution && num2 >= 0 && num2 < Resolution && num3 >= 0 && num3 < Resolution)
            {
                array[num + num2 * Resolution + num3 * Resolution * Resolution] =
                    _values[k + j * Resolution + i * Resolution * Resolution];
            }
        }

        SetValues(Resolution, array);
    }

    public void Rotate(int axis, int steps)
    {
        steps %= 4;
        if (steps < 0)
        {
            steps += 4;
        }

        if (steps <= 0)
        {
            return;
        }

        var array = new int[Resolution * Resolution * Resolution];
        for (var i = 0; i < Resolution; i++)
        for (var j = 0; j < Resolution; j++)
        for (var k = 0; k < Resolution; k++)
        {
            var vector = RotatePoint(new Vector3(k, j, i) - new Vector3(Resolution / 2f - 0.5f), axis, steps) +
                         new Vector3(Resolution / 2f - 0.5f);
            var point = new Point3((int)MathUtils.Round(vector.X), (int)MathUtils.Round(vector.Y),
                (int)MathUtils.Round(vector.Z));
            if (point.X >= 0 && point.X < Resolution && point.Y >= 0 && point.Y < Resolution && point.Z >= 0 &&
                point.Z < Resolution)
            {
                array[point.X + point.Y * Resolution + point.Z * Resolution * Resolution] =
                    _values[k + j * Resolution + i * Resolution * Resolution];
            }
        }

        SetValues(Resolution, array);
    }

    public void Mirror(int axis)
    {
        var array = new int[Resolution * Resolution * Resolution];
        for (var i = 0; i < Resolution; i++)
        for (var j = 0; j < Resolution; j++)
        for (var k = 0; k < Resolution; k++)
        {
            var vector = MirrorPoint(new Vector3(k, j, i) - new Vector3(Resolution / 2f - 0.5f), axis) +
                         new Vector3(Resolution / 2f - 0.5f);
            var point = new Point3((int)MathUtils.Round(vector.X), (int)MathUtils.Round(vector.Y),
                (int)MathUtils.Round(vector.Z));
            if (point.X >= 0 && point.X < Resolution && point.Y >= 0 && point.Y < Resolution && point.Z >= 0 &&
                point.Z < Resolution)
            {
                array[point.X + point.Y * Resolution + point.Z * Resolution * Resolution] =
                    _values[k + j * Resolution + i * Resolution * Resolution];
            }
        }

        SetValues(Resolution, array);
    }

    public ValuesDictionary Save()
    {
        var stringBuilder = new StringBuilder();
        var num = _values[0];
        var num2 = 1;
        for (var i = 1; i < _values.Length; i++)
        {
            if (_values[i] != num)
            {
                stringBuilder.Append(num2.ToString(CultureInfo.InvariantCulture));
                stringBuilder.Append('*');
                stringBuilder.Append(num.ToString(CultureInfo.InvariantCulture));
                stringBuilder.Append(',');
                num = _values[i];
                num2 = 1;
            }
            else
            {
                num2++;
            }
        }

        stringBuilder.Append(num2.ToString(CultureInfo.InvariantCulture));
        stringBuilder.Append('*');
        stringBuilder.Append(num.ToString(CultureInfo.InvariantCulture));
        stringBuilder.Append(',');
        var valuesDictionary = new ValuesDictionary();
        if (!string.IsNullOrEmpty(Name))
        {
            valuesDictionary.SetValue("Name", Name);
        }

        valuesDictionary.SetValue("TerrainUseCount", TerrainUseCount);
        valuesDictionary.SetValue("Resolution", Resolution);
        valuesDictionary.SetValue("InteractionMode", _interactionMode);
        if (LinkedDesign != null)
        {
            valuesDictionary.SetValue("LinkedDesign", LinkedDesign.Index);
        }

        valuesDictionary.SetValue("Values", stringBuilder.ToString());
        return valuesDictionary;
    }

    public bool Compare(FurnitureDesign other)
    {
        if (this == other)
        {
            return true;
        }

        if (Resolution == other.Resolution &&
            InteractionMode == other.InteractionMode &&
            Hash == other.Hash &&
            Name == other.Name)
        {
            return !_values.Where((t, i) => t != other._values[i]).Any();
        }

        return false;
    }

    public bool CompareChain(FurnitureDesign other)
    {
        if (this == other)
        {
            return true;
        }

        var list = ListChain();
        var list2 = other.ListChain();
        if (list.Count != list2.Count)
        {
            return false;
        }

        return !list.Where((t, i) => !t.Compare(list2[i])).Any();
    }

    public FurnitureDesign Clone()
    {
        var furnitureDesign = new FurnitureDesign(_subsystemTerrain);
        furnitureDesign.SetValues(Resolution, _values);
        furnitureDesign.Name = Name;
        furnitureDesign.LinkedDesign = LinkedDesign;
        furnitureDesign.InteractionMode = InteractionMode;
        return furnitureDesign;
    }

    public List<FurnitureDesign> CloneChain()
    {
        var list = ListChain();
        var list2 = new List<FurnitureDesign>(list.Count);
        foreach (var item in list)
        {
            list2.Add(item.Clone());
        }

        for (var j = 0; j < list2.Count - 1; j++)
        {
            list2[j].LinkedDesign = list2[j + 1];
        }

        var furnitureDesign = list[^1].LinkedDesign;
        if (furnitureDesign == null)
        {
            return list2;
        }

        var num = list.IndexOf(furnitureDesign);
        if (num >= 0)
        {
            list2[^1].LinkedDesign = list2[num];
        }

        return list2;
    }

    public List<FurnitureDesign> ListChain()
    {
        var furnitureDesign = this;
        var hashSet = new HashSet<FurnitureDesign>();
        var list = new List<FurnitureDesign>();
        do
        {
            hashSet.Add(furnitureDesign);
            list.Add(furnitureDesign);
            furnitureDesign = furnitureDesign.LinkedDesign;
        } while (furnitureDesign != null && !hashSet.Contains(furnitureDesign));

        return list;
    }

    public static List<List<FurnitureDesign>> ListChains(IEnumerable<FurnitureDesign> designs)
    {
        var list = new List<List<FurnitureDesign>>();
        var list2 = new List<FurnitureDesign>(designs);
        while (list2.Count > 0)
        {
            var list3 = list2[0].ListChain();
            list.Add(list3);
            foreach (var item in list3)
            {
                list2.Remove(item);
            }
        }

        return list;
    }

    public byte[] CreatePrecedingEmptySpacesArray()
    {
        var array = new byte[_values.Length];
        var num = 0;
        for (var i = 0; i < Resolution; i++)
        for (var j = 0; j < Resolution; j++)
        {
            var num2 = 0;
            var num3 = 0;
            while (num3 < Resolution)
            {
                num2 = _values[num] == 0 ? num2 + 1 : 0;
                array[num] = (byte)num2;
                num3++;
                num++;
            }
        }

        return array;
    }

    public Box CalculateBox(Box box, byte[] precedingEmptySpaces)
    {
        var num = int.MaxValue;
        var num2 = int.MaxValue;
        var num3 = int.MaxValue;
        var num4 = int.MinValue;
        var num5 = int.MinValue;
        var num6 = int.MinValue;
        for (var i = box.Near; i < box.Far; i++)
        {
            var num7 = Math.Min(num3, i);
            var num8 = Math.Max(num6, i);
            var num9 = box.Top;
            var num10 = (num9 + i * Resolution) * Resolution;
            while (num9 < box.Bottom)
            {
                var num11 = box.Right - 1 - precedingEmptySpaces[num10 + box.Right - 1];
                if (num11 >= box.Left)
                {
                    num4 = Math.Max(num4, num11);
                    num2 = Math.Min(num2, num9);
                    num5 = Math.Max(num5, num9);
                    num3 = num7;
                    num6 = num8;
                    var num12 = num - 1;
                    for (var j = box.Left; j <= num12; j++)
                    {
                        if (_values[num10 + j] != 0)
                        {
                            num = Math.Min(num, j);
                            break;
                        }
                    }
                }

                num9++;
                num10 += Resolution;
            }
        }

        return new Box(num, num2, num3, num4 - num + 1, num5 - num2 + 1, num6 - num3 + 1);
    }

    private int CalculateShadowStrengthFactor()
    {
        var array = new float[Resolution * Resolution];
        var num = 0;
        for (var i = 0; i < Resolution; i++)
        for (var j = 0; j < Resolution; j++)
        {
            var x = (j + 1) / (float)Resolution;
            for (var k = 0; k < Resolution; k++)
            {
                if (!IsValueTransparent(_values[num++]))
                {
                    array[k + i * Resolution] = MathUtils.Max(array[k + i * Resolution], x);
                }
            }
        }

        var num2 = 0f;
        for (var l = 0; l < Resolution * Resolution; l++)
        {
            num2 += array[l];
        }

        num2 /= Resolution * Resolution;
        var num3 = 1.5f;
        return (int)MathUtils.Clamp(MathUtils.Round(num2 * 3f * num3), 0f, 3f);
    }

    private FurnitureGeometry CreateGeometry()
    {
        var geometry = new FurnitureGeometry();
        for (var i = 0; i < 6; i++)
        {
            var num = CellFace.OppositeFace(i);
            Point3 point;
            Point3 point2;
            Point3 point3;
            Point3 point4;
            Point3 point5;
            switch (i)
            {
                case 0:
                    point = new Point3(0, 0, 1);
                    point2 = new Point3(-1, 0, 0);
                    point3 = new Point3(0, -1, 0);
                    point4 = new Point3(Resolution, Resolution, 0);
                    point5 = new Point3(Resolution - 1, Resolution - 1, 0);
                    break;
                case 1:
                    point = new Point3(1, 0, 0);
                    point2 = new Point3(0, 0, 1);
                    point3 = new Point3(0, -1, 0);
                    point4 = new Point3(0, Resolution, 0);
                    point5 = new Point3(0, Resolution - 1, 0);
                    break;
                case 2:
                    point = new Point3(0, 0, -1);
                    point2 = new Point3(1, 0, 0);
                    point3 = new Point3(0, -1, 0);
                    point4 = new Point3(0, Resolution, Resolution);
                    point5 = new Point3(0, Resolution - 1, Resolution - 1);
                    break;
                case 3:
                    point = new Point3(-1, 0, 0);
                    point2 = new Point3(0, 0, -1);
                    point3 = new Point3(0, -1, 0);
                    point4 = new Point3(Resolution, Resolution, Resolution);
                    point5 = new Point3(Resolution - 1, Resolution - 1, Resolution - 1);
                    break;
                case 4:
                    point = new Point3(0, 1, 0);
                    point2 = new Point3(-1, 0, 0);
                    point3 = new Point3(0, 0, 1);
                    point4 = new Point3(Resolution, 0, 0);
                    point5 = new Point3(Resolution - 1, 0, 0);
                    break;
                default:
                    point = new Point3(0, -1, 0);
                    point2 = new Point3(-1, 0, 0);
                    point3 = new Point3(0, 0, -1);
                    point4 = new Point3(Resolution, Resolution, Resolution);
                    point5 = new Point3(Resolution - 1, Resolution - 1, Resolution - 1);
                    break;
            }

            var blockMesh = new BlockMesh();
            var blockMesh2 = new BlockMesh();
            for (var j = 0; j < Resolution; j++)
            {
                var array = new Cell[Resolution * Resolution];
                for (var k = 0; k < Resolution; k++)
                for (var l = 0; l < Resolution; l++)
                {
                    var num2 = j * point.X + k * point3.X + l * point2.X + point5.X;
                    var num3 = j * point.Y + k * point3.Y + l * point2.Y + point5.Y;
                    var num4 = j * point.Z + k * point3.Z + l * point2.Z + point5.Z;
                    var num5 = num2 + num3 * Resolution + num4 * Resolution * Resolution;
                    var num6 = _values[num5];
                    Cell cell = default;
                    cell.Value = num6;
                    var cell2 = cell;
                    if (j > 0 && num6 != 0)
                    {
                        var num7 = num2 - point.X + (num3 - point.Y) * Resolution +
                                   (num4 - point.Z) * Resolution * Resolution;
                        var value = _values[num7];
                        if (!IsValueTransparent(value) ||
                            Terrain.ExtractContents(num6) == Terrain.ExtractContents(value))
                        {
                            cell2.Value = 0;
                        }
                    }

                    array[l + k * Resolution] = cell2;
                }

                for (var m = 0; m < Resolution; m++)
                for (var n = 0; n < Resolution; n++)
                {
                    var value2 = array[n + m * Resolution].Value;
                    if (value2 == 0)
                    {
                        continue;
                    }

                    var point6 = FindLargestSize(array, new Point2(n, m), value2);
                    if (point6 == Point2.Zero)
                    {
                        continue;
                    }

                    MarkUsed(array, new Point2(n, m), point6);
                    var num8 = 0.0005f * Resolution;
                    var num9 = n - num8;
                    var num10 = n + point6.X + num8;
                    var num11 = m - num8;
                    var num12 = m + point6.Y + num8;
                    var x = j * point.X + num11 * point3.X + num9 * point2.X + point4.X;
                    var y = j * point.Y + num11 * point3.Y + num9 * point2.Y + point4.Y;
                    var z = j * point.Z + num11 * point3.Z + num9 * point2.Z + point4.Z;
                    var x2 = j * point.X + num11 * point3.X + num10 * point2.X + point4.X;
                    var y2 = j * point.Y + num11 * point3.Y + num10 * point2.Y + point4.Y;
                    var z2 = j * point.Z + num11 * point3.Z + num10 * point2.Z + point4.Z;
                    var x3 = j * point.X + num12 * point3.X + num10 * point2.X + point4.X;
                    var y3 = j * point.Y + num12 * point3.Y + num10 * point2.Y + point4.Y;
                    var z3 = j * point.Z + num12 * point3.Z + num10 * point2.Z + point4.Z;
                    var x4 = j * point.X + num12 * point3.X + num9 * point2.X + point4.X;
                    var y4 = j * point.Y + num12 * point3.Y + num9 * point2.Y + point4.Y;
                    var z4 = j * point.Z + num12 * point3.Z + num9 * point2.Z + point4.Z;
                    var blockMesh3 = blockMesh;
                    var num13 = Terrain.ExtractContents(value2);
                    var block = BlocksManager.Blocks[num13];
                    var num14 = block.GetFaceTextureSlot(i, value2);
                    var isEmissive = false;
                    var color = Color.White;
                    if (block is IPaintableBlock paintableBlock)
                    {
                        var paintColor = paintableBlock.GetPaintColor(value2);
                        color = SubsystemPalette.GetColor(_subsystemTerrain, paintColor);
                    }
                    else if (block is WaterBlock)
                    {
                        color = BlockColorsMap.WaterColorsMap.Lookup(12, 12);
                        num14 = 189;
                    }
                    else if (block is CarpetBlock)
                    {
                        var color2 = CarpetBlock.GetColor(Terrain.ExtractData(value2));
                        color = SubsystemPalette.GetFabricColor(_subsystemTerrain, color2);
                    }
                    else if (block is TorchBlock or WickerLampBlock)
                    {
                        isEmissive = true;
                        num14 = 31;
                    }
                    else if (block is GlassBlock)
                    {
                        blockMesh3 = blockMesh2;
                    }

                    var num15 = num14 % 16;
                    var num16 = num14 / 16;
                    var count = blockMesh3.Vertices.Count;
                    blockMesh3.Vertices.Count += 4;
                    var array2 = blockMesh3.Vertices.Array;
                    var x5 = ((n + 0.01f) / Resolution + num15) / 16f;
                    var x6 = ((n + point6.X - 0.01f) / Resolution + num15) / 16f;
                    var y5 = ((m + 0.01f) / Resolution + num16) / 16f;
                    var y6 = ((m + point6.Y - 0.01f) / Resolution + num16) / 16f;
                    array2[count] = new BlockMeshVertex
                    {
                        Position = new Vector3(x, y, z) / Resolution,
                        Color = color,
                        Face = (byte)num,
                        TextureCoordinates = new Vector2(x5, y5),
                        IsEmissive = isEmissive
                    };
                    array2[count + 1] = new BlockMeshVertex
                    {
                        Position = new Vector3(x2, y2, z2) / Resolution,
                        Color = color,
                        Face = (byte)num,
                        TextureCoordinates = new Vector2(x6, y5),
                        IsEmissive = isEmissive
                    };
                    array2[count + 2] = new BlockMeshVertex
                    {
                        Position = new Vector3(x3, y3, z3) / Resolution,
                        Color = color,
                        Face = (byte)num,
                        TextureCoordinates = new Vector2(x6, y6),
                        IsEmissive = isEmissive
                    };
                    array2[count + 3] = new BlockMeshVertex
                    {
                        Position = new Vector3(x4, y4, z4) / Resolution,
                        Color = color,
                        Face = (byte)num,
                        TextureCoordinates = new Vector2(x5, y6),
                        IsEmissive = isEmissive
                    };
                    var count2 = blockMesh3.Indices.Count;
                    blockMesh3.Indices.Count += 6;
                    var array3 = blockMesh3.Indices.Array;
                    array3[count2] = (ushort)count;
                    array3[count2 + 1] = (ushort)(count + 1);
                    array3[count2 + 2] = (ushort)(count + 2);
                    array3[count2 + 3] = (ushort)(count + 2);
                    array3[count2 + 4] = (ushort)(count + 3);
                    array3[count2 + 5] = (ushort)count;
                }
            }

            if (blockMesh.Indices.Count > 0)
            {
                blockMesh.Trim();
                blockMesh.GenerateSidesData();
                geometry.SubsetOpaqueByFace[i] = blockMesh;
            }

            if (blockMesh2.Indices.Count > 0)
            {
                blockMesh2.Trim();
                blockMesh2.GenerateSidesData();
                geometry.SubsetAlphaTestByFace[i] = blockMesh2;
            }
        }

        return geometry;
    }

    private BoundingBox[][] CreateCollisionAndInteractionBoxes()
    {
        var subdivision = CreateBoundingBoxesHelper(Box, 0, CreatePrecedingEmptySpacesArray());
        var list = new List<BoundingBox>(subdivision.Boxes.Count);
        foreach (var box in subdivision.Boxes)
        {
            var min = new Vector3(box.Left, box.Top, box.Near) / Resolution;
            var max = new Vector3(box.Right, box.Bottom, box.Far) / Resolution;
            list.Add(new BoundingBox(min, max));
        }

        var collisionBoxesByRotation = new BoundingBox[4][];
        for (var j = 0; j < 4; j++)
        {
            var m = Matrix.CreateTranslation(-0.5f, 0f, -0.5f) * Matrix.CreateRotationY(j * (float)Math.PI / 2f) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            collisionBoxesByRotation[j] = new BoundingBox[list.Count];
            for (var k = 0; k < list.Count; k++)
            {
                var v = Vector3.Transform(list[k].Min, m);
                var v2 = Vector3.Transform(list[k].Max, m);
                var boundingBox = new BoundingBox(Vector3.Min(v, v2), Vector3.Max(v, v2));
                collisionBoxesByRotation[j][k] = boundingBox;
            }
        }

        var list2 = new List<BoundingBox>(list);
        while (true)
        {
            var num = 0;
            int l;
            BoundingBox item;
            while (true)
            {
                if (num < list2.Count)
                {
                    for (l = 0; l < list2.Count; l++)
                    {
                        if (num != l)
                        {
                            var b = list2[num];
                            var b2 = list2[l];
                            item = BoundingBox.Union(b, b2);
                            var vector = item.Size();
                            if ((item.Volume() - b.Volume() - b2.Volume()) /
                                MathUtils.Min(vector.X, vector.Y, vector.Z) < 0.4f)
                            {
                                goto end_IL_0263;
                            }
                        }
                    }

                    num++;
                    continue;
                }

                var flag = false;
                for (var n = 0; n < list2.Count; n++)
                {
                    var vector2 = list2[n].Size();
                    flag |= vector2.X >= 0.6f && vector2.Y >= 0.6f;
                    flag |= vector2.X >= 0.6f && vector2.Z >= 0.6f;
                    flag |= vector2.Y >= 0.6f && vector2.Z >= 0.6f;
                }

                var minSize = flag ? 0.0625f : 0.6f;
                for (var num2 = 0; num2 < list2.Count; num2++)
                {
                    var value = list2[num2];
                    // 提取 Min 和 Max
                    var min = value.Min;
                    var max = value.Max;
                    // 确保每个轴的最小尺寸
                    EnsureMinSize(ref min.X, ref max.X, minSize);
                    EnsureMinSize(ref min.Y, ref max.Y, minSize);
                    EnsureMinSize(ref min.Z, ref max.Z, minSize);

                    // 重新赋值回 BoundingBox
                    value.Min = min;
                    value.Max = max;

                    // 更新到列表
                    list2[num2] = value;
                }

                _interactionBoxesByRotation = new BoundingBox[4][];
                for (var num3 = 0; num3 < 4; num3++)
                {
                    var m2 = Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                             Matrix.CreateRotationY(num3 * (float)Math.PI / 2f) *
                             Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                    _interactionBoxesByRotation[num3] = new BoundingBox[list2.Count];
                    for (var num4 = 0; num4 < list2.Count; num4++)
                    {
                        var v3 = Vector3.Transform(list2[num4].Min, m2);
                        var v4 = Vector3.Transform(list2[num4].Max, m2);
                        var boundingBox2 = new BoundingBox(Vector3.Min(v3, v4), Vector3.Max(v3, v4));
                        _interactionBoxesByRotation[num3][num4] = boundingBox2;
                    }
                }

                return collisionBoxesByRotation;
                end_IL_0263:
                break;
            }

            list2.RemoveAt(num);
            list2.RemoveAt(num < l ? l - 1 : l);
            list2.Add(item);
        }
    }

    private BoundingBox[][] CreateTorchPoints()
    {
        var list = new List<BoundingBox>();
        for (var i = 0; i < Resolution; i++)
        for (var j = 0; j < Resolution; j++)
        for (var k = 0; k < Resolution; k++)
        {
            var num = Terrain.ExtractContents(_values[k + j * Resolution + i * Resolution * Resolution]);
            if (num != 31 && num != 17)
            {
                continue;
            }

            var boundingBox = new BoundingBox(new Vector3(k, j, i) / Resolution,
                new Vector3(k + 1, j + 1, i + 1) / Resolution);
            var num2 = -1;
            for (var l = 0; l < list.Count; l++)
            {
                var boundingBox2 = list[l];
                var vector = boundingBox2.Size();
                var vector2 = boundingBox2.Center() - boundingBox.Center();
                vector2.X = MathUtils.Max(MathUtils.Abs(vector2.X) - vector.X / 2f, 0f);
                vector2.Y = MathUtils.Max(MathUtils.Abs(vector2.Y) - vector.Y / 2f, 0f);
                vector2.Z = MathUtils.Max(MathUtils.Abs(vector2.Z) - vector.Z / 2f, 0f);
                if (!(vector2.Length() < 0.15f))
                {
                    continue;
                }

                num2 = l;
                break;
            }

            if (num2 >= 0)
            {
                list[num2] = BoundingBox.Union(list[num2], boundingBox);
            }
            else if (list.Count < 4)
            {
                list.Add(boundingBox);
            }
        }

        var torchPointsByRotation = new BoundingBox[4][];
        for (var m = 0; m < 4; m++)
        {
            var m2 = Matrix.CreateTranslation(-0.5f, 0f, -0.5f) * Matrix.CreateRotationY(m * (float)Math.PI / 2f) *
                     Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            torchPointsByRotation[m] = new BoundingBox[list.Count];
            for (var n = 0; n < list.Count; n++)
            {
                var v = Vector3.Transform(list[n].Min, m2);
                var v2 = Vector3.Transform(list[n].Max, m2);
                torchPointsByRotation[m][n] = new BoundingBox(Vector3.Min(v, v2), Vector3.Max(v, v2));
            }
        }

        return torchPointsByRotation;
    }

    public void CalculateMainValue()
    {
        var dictionary = new Dictionary<int, int>();
        for (var i = 0; i < Resolution; i++)
        for (var j = 0; j < Resolution; j++)
        for (var num = Resolution - 1; num >= 0; num--)
        {
            var num2 = _values[j + num * Resolution + i * Resolution * Resolution];
            if (num2 != 0)
            {
                dictionary.TryGetValue(num2, out var value);
                dictionary[num2] = value + 1;
                break;
            }
        }

        var num3 = 0;
        foreach (var item in dictionary)
        {
            if (item.Value > num3)
            {
                _mainValue = item.Key;
                num3 = item.Value;
            }
        }
    }

    public void CalculateFacesMasks()
    {
        _mountingFacesMask = 0;
        _transparentFacesMask = 0;
        for (var i = 0; i < Resolution; i++)
        for (var j = 0; j < Resolution; j++)
        {
            var values = _values;
            var num = i + j * Resolution;
            _ = Resolution;
            var value = values[num + 0 * Resolution];
            var value2 = _values[i + j * Resolution + (Resolution - 1) * Resolution * Resolution];
            if (IsValueTransparent(value))
            {
                _transparentFacesMask |= 4;
            }
            else
            {
                _mountingFacesMask |= 4;
            }

            if (IsValueTransparent(value2))
            {
                _transparentFacesMask |= 1;
            }
            else
            {
                _mountingFacesMask |= 1;
            }
        }

        for (var k = 0; k < Resolution; k++)
        for (var l = 0; l < Resolution; l++)
        {
            var value3 = _values[k * Resolution + l * Resolution * Resolution];
            var value4 = _values[Resolution - 1 + k * Resolution + l * Resolution * Resolution];
            if (IsValueTransparent(value3))
            {
                _transparentFacesMask |= 8;
            }
            else
            {
                _mountingFacesMask |= 8;
            }

            if (IsValueTransparent(value4))
            {
                _transparentFacesMask |= 2;
            }
            else
            {
                _mountingFacesMask |= 2;
            }
        }

        for (var m = 0; m < Resolution; m++)
        for (var n = 0; n < Resolution; n++)
        {
            var values2 = _values;
            var num2 = m;
            _ = Resolution;
            var value5 = values2[num2 + 0 + n * Resolution * Resolution];
            var value6 = _values[m + (Resolution - 1) * Resolution + n * Resolution * Resolution];
            if (IsValueTransparent(value5))
            {
                _transparentFacesMask |= 32;
            }
            else
            {
                _mountingFacesMask |= 32;
            }

            if (IsValueTransparent(value6))
            {
                _transparentFacesMask |= 16;
            }
            else
            {
                _mountingFacesMask |= 16;
            }
        }
    }

    public Subdivision CreateBoundingBoxesHelper(Box box, int depth, byte[] precedingEmptySpaces)
    {
        var num = 0;
        Subdivision result = default;
        result.TotalVolume = box.Width * box.Height * box.Depth;
        result.MinVolume = result.TotalVolume;
        result.Boxes = new List<Box>
        {
            box
        };
        if (depth < 2)
        {
            for (var num2 = box.Bottom - 1; num2 >= box.Top + 1; num2--)
            {
                var box2 = CalculateBox(new Box(box.Left, box.Top, box.Near, box.Width, num2 - box.Top, box.Depth),
                    precedingEmptySpaces);
                var box3 = CalculateBox(new Box(box.Left, num2, box.Near, box.Width, box.Bottom - num2, box.Depth),
                    precedingEmptySpaces);
                var subdivision = CreateBoundingBoxesHelper(box2, depth + 1, precedingEmptySpaces);
                var subdivision2 = CreateBoundingBoxesHelper(box3, depth + 1, precedingEmptySpaces);
                var num3 = subdivision.Boxes.Count + subdivision2.Boxes.Count;
                var num4 = subdivision.TotalVolume + subdivision2.TotalVolume;
                var num5 = MathUtils.Min(subdivision.MinVolume, subdivision2.MinVolume);
                var num6 = num3 > result.Boxes.Count ? num4 + num : num4;
                if (num6 < result.TotalVolume || (num6 == result.TotalVolume && num5 > result.MinVolume))
                {
                    result.TotalVolume = num4;
                    result.MinVolume = num5;
                    result.Boxes = subdivision.Boxes;
                    result.Boxes.AddRange(subdivision2.Boxes);
                }
            }

            for (var i = box.Near + 1; i < box.Far; i++)
            {
                var box4 = CalculateBox(new Box(box.Left, box.Top, box.Near, box.Width, box.Height, i - box.Near),
                    precedingEmptySpaces);
                var box5 = CalculateBox(new Box(box.Left, box.Top, i, box.Width, box.Height, box.Far - i),
                    precedingEmptySpaces);
                var subdivision3 = CreateBoundingBoxesHelper(box4, depth + 1, precedingEmptySpaces);
                var subdivision4 = CreateBoundingBoxesHelper(box5, depth + 1, precedingEmptySpaces);
                var num7 = subdivision3.Boxes.Count + subdivision4.Boxes.Count;
                var num8 = subdivision3.TotalVolume + subdivision4.TotalVolume;
                var num9 = MathUtils.Min(subdivision3.MinVolume, subdivision4.MinVolume);
                var num10 = num7 > result.Boxes.Count ? num8 + num : num8;
                if (num10 < result.TotalVolume || (num10 == result.TotalVolume && num9 > result.MinVolume))
                {
                    result.TotalVolume = num8;
                    result.MinVolume = num9;
                    result.Boxes = subdivision3.Boxes;
                    result.Boxes.AddRange(subdivision4.Boxes);
                }
            }

            for (var j = box.Left + 1; j < box.Right; j++)
            {
                var box6 = CalculateBox(new Box(box.Left, box.Top, box.Near, j - box.Left, box.Height, box.Depth),
                    precedingEmptySpaces);
                var box7 = CalculateBox(new Box(j, box.Top, box.Near, box.Right - j, box.Height, box.Depth),
                    precedingEmptySpaces);
                var subdivision5 = CreateBoundingBoxesHelper(box6, depth + 1, precedingEmptySpaces);
                var subdivision6 = CreateBoundingBoxesHelper(box7, depth + 1, precedingEmptySpaces);
                var num11 = subdivision5.Boxes.Count + subdivision6.Boxes.Count;
                var num12 = subdivision5.TotalVolume + subdivision6.TotalVolume;
                var num13 = MathUtils.Min(subdivision5.MinVolume, subdivision6.MinVolume);
                var num14 = num11 > result.Boxes.Count ? num12 + num : num12;
                if (num14 < result.TotalVolume || (num14 == result.TotalVolume && num13 > result.MinVolume))
                {
                    result.TotalVolume = num12;
                    result.MinVolume = num13;
                    result.Boxes = subdivision5.Boxes;
                    result.Boxes.AddRange(subdivision6.Boxes);
                }
            }
        }

        return result;
    }

    public Point2 FindLargestSize(Cell[] surface, Point2 start, int value)
    {
        var result = Point2.Zero;
        var num = Resolution;
        for (var i = start.Y; i < Resolution; i++)
        for (var j = start.X; j <= num; j++)
        {
            if (j == num || surface[j + i * Resolution].Value != value)
            {
                num = j;
                var point = new Point2(num - start.X, i - start.Y + 1);
                if (point.X * point.Y > result.X * result.Y)
                {
                    result = point;
                }
            }
        }

        return result;
    }

    public void MarkUsed(Cell[] surface, Point2 start, Point2 size)
    {
        for (var i = start.Y; i < start.Y + size.Y; i++)
        for (var j = start.X; j < start.X + size.X; j++)
        {
            surface[j + i * Resolution].Value = 0;
        }
    }

    public static Vector3 RotatePoint(Vector3 p, int axis, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            switch (axis)
            {
                case 0:
                    p = new Vector3(p.X, p.Z, 0f - p.Y);
                    break;
                case 1:
                    p = new Vector3(0f - p.Z, p.Y, p.X);
                    break;
                default:
                    p = new Vector3(0f - p.Y, p.X, p.Z);
                    break;
            }
        }

        return p;
    }

    public static Vector3 MirrorPoint(Vector3 p, int axis)
    {
        switch (axis)
        {
            case 0:
                p = new Vector3(p.X, p.Y, 0f - p.Z);
                break;
            case 1:
                p = new Vector3(0f - p.X, p.Y, p.Z);
                break;
            default:
                p = new Vector3(0f - p.X, p.Y, p.Z);
                break;
        }

        return p;
    }

    public static void EnsureMinSize(ref float min, ref float max, float minSize)
    {
        var num = max - min;
        if (num < minSize)
        {
            var num2 = minSize - num;
            min -= num2 / 2f;
            max += num2 / 2f;
            if (min < 0f)
            {
                max -= min;
                min = 0f;
            }
            else if (max > 1f)
            {
                min -= max - 1f;
                max = 1f;
            }
        }
    }

    public static bool IsValueTransparent(int value)
    {
        if (value != 0)
        {
            return Terrain.ExtractContents(value) == 15;
        }

        return true;
    }

    public struct Cell
    {
        public int Value;
    }

    public struct Subdivision
    {
        public int TotalVolume;

        public int MinVolume;

        public List<Box> Boxes;
    }
}
