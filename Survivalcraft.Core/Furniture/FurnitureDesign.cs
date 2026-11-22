using System.Globalization;
using System.Text;

using EntitySystem.TemplatesDatabase;

namespace Game.Furniture;

public class FurnitureDesign
{
    public const int MinResolution = 2;

    public const int MaxDesign = 4096; //家具上限数量

    public const int MaxResolution = 256; //家具格分辨率

    public const int MaxTriangles = 65536; //家具复杂度

    public const int MaxNameLength = 100;

    private Box? _box;

    private BoundingBox[][]? _collisionBoxesByRotation;

    public bool GcUsed;

    private int? _hash;

    private int _index = -1;

    private BoundingBox[][]? _interactionBoxesByRotation;

    private FurnitureInteractionMode _interactionMode = FurnitureInteractionMode.None;

    public int LoadTimeLinkedDesignIndex { get; } = -1;

    private int _mainValue;

    private int _mountingFacesMask = -1;

    private int? _shadowStrengthFactor;

    private readonly SubsystemTerrain? _subsystemTerrain;

    private readonly BlockContentCatalog? _catalog;

    private BlockContentCatalog Catalog => _catalog ??
                                           throw new InvalidOperationException(
                                               "Data-only furniture has no content catalog."
                                           );

    public int TerrainUseCount;

    private BoundingBox[][]? _torchPointsByRotation;

    private int _transparentFacesMask = -1;

    private int[] _values = [];

    public FurnitureDesign(BlockContentCatalog? catalog, SubsystemTerrain? subsystemTerrain)
    {
        if (subsystemTerrain is not null && !ReferenceEquals(catalog, subsystemTerrain.Catalog))
        {
            throw new ArgumentException("Furniture content must belong to its terrain world.", nameof(catalog));
        }

        _subsystemTerrain = subsystemTerrain;
        _catalog = catalog;
    }

    public FurnitureDesign(
        int index,
        BlockContentCatalog? catalog,
        SubsystemTerrain? subsystemTerrain,
        ValuesDictionary valuesDictionary
    )
        : this(catalog, subsystemTerrain)
    {
        _index = index;
        Name = valuesDictionary.GetValue("Name", string.Empty);
        TerrainUseCount = valuesDictionary.GetValue<int>("TerrainUseCount");
        var value = valuesDictionary.GetValue<int>("Resolution");
        InteractionMode = valuesDictionary.GetValue<FurnitureInteractionMode>("InteractionMode");
        LoadTimeLinkedDesignIndex = valuesDictionary.GetValue("LinkedDesign", -1);
        var value2 = valuesDictionary.GetValue<string>("Values");
        var writeIndex = 0;
        var array = new int[value * value * value];
        var array2 = value2.Split([','], StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in array2)
        {
            var array3 = item.Split(['*'], StringSplitOptions.None);
            if (array3.Length != 2)
            {
                throw new InvalidOperationException("Furniture value run must contain count and value.");
            }

            var runLength = int.Parse(array3[0], CultureInfo.InvariantCulture);
            var runValue = int.Parse(array3[1], CultureInfo.InvariantCulture);
            var runOffset = 0;
            while (runOffset < runLength)
            {
                array[writeIndex] = runValue;
                runOffset++;
                writeIndex++;
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

    public int ShadowStrengthFactor { get { return _shadowStrengthFactor ??= CalculateShadowStrengthFactor(); } }

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

    public int Index { get => _index; set => _index = value; }

    public string Name
    {
        get;
        set
        {
            if (value.Length > 0)
            {
                if (value[0] == ' ' || value[^1] == ' ')
                {
                    throw new InvalidOperationException(
                        "Furniture name must use ASCII letters, digits and internal spaces."
                    );
                }

                var text = value;
                foreach (var c in text)
                {
                    if (c > '\u007f' || (!char.IsLetterOrDigit(c) && c != ' '))
                    {
                        throw new InvalidOperationException(
                            "Furniture name must use ASCII letters, digits and internal spaces."
                        );
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

    public long Revision { get; private set; }

    public BlockContentCatalog? ContentCatalog => _catalog;

    public int GetValue(int index)
    {
        return _values[index];
    }

    public void SetValues(int resolution, int[] values)
    {
        if (resolution is < 2 or > MaxResolution)
        {
            throw new ArgumentException("Furniture resolution is out of range.");
        }

        if (values.Length != resolution * resolution * resolution)
        {
            throw new ArgumentException("Furniture values do not match the selected resolution.");
        }

        Resolution = resolution;
        if (_values.Length != resolution * resolution * resolution)
        {
            _values = new int[resolution * resolution * resolution];
        }

        values.CopyTo(_values, 0);
        _hash = null;
        Revision = unchecked(Revision + 1);
        _box = null;
        _collisionBoxesByRotation = null;
        _interactionBoxesByRotation = null;
        _torchPointsByRotation = null;
        _mainValue = 0;
        _mountingFacesMask = -1;
        _transparentFacesMask = -1;
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
            var blockValue = _values[i];
            var contents = Terrain.ExtractContents(blockValue);
            var paintableBlock = Catalog.Blocks[contents] as IPaintableBlock;
            array[i] = paintableBlock?.Paint(null, blockValue, color) ?? blockValue;
        }

        SetValues(Resolution, array);
    }

    public void Resize(int resolution)
    {
        if (resolution is < 2 or > MaxResolution)
        {
            throw new ArgumentException("Furniture resolution is out of range.");
        }

        if (resolution == Resolution)
        {
            return;
        }

        var array = new int[resolution * resolution * resolution];
        for (var i = 0; i < resolution; i++)
        {
            for (var j = 0; j < resolution; j++)
            {
                for (var k = 0; k < resolution; k++)
                {
                    if (k >= 0 && k < Resolution && j >= 0 && j < Resolution && i >= 0 && i < Resolution)
                    {
                        array[k + j * resolution + i * resolution * resolution] =
                            _values[k + j * Resolution + i * Resolution * Resolution];
                    }
                }
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
        {
            for (var j = 0; j < Resolution; j++)
            {
                for (var k = 0; k < Resolution; k++)
                {
                    var targetX = k + delta.X;
                    var targetY = j + delta.Y;
                    var targetZ = i + delta.Z;
                    if (targetX >= 0 &&
                        targetX < Resolution &&
                        targetY >= 0 &&
                        targetY < Resolution &&
                        targetZ >= 0 &&
                        targetZ < Resolution)
                    {
                        array[targetX + targetY * Resolution + targetZ * Resolution * Resolution] =
                            _values[k + j * Resolution + i * Resolution * Resolution];
                    }
                }
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
        {
            for (var j = 0; j < Resolution; j++)
            {
                for (var k = 0; k < Resolution; k++)
                {
                    var vector = RotatePoint(new Vector3(k, j, i) - new Vector3(Resolution / 2f - 0.5f), axis, steps) +
                                 new Vector3(Resolution / 2f - 0.5f);
                    var point = new Point3(
                        (int)MathUtils.Round(vector.X),
                        (int)MathUtils.Round(vector.Y),
                        (int)MathUtils.Round(vector.Z)
                    );
                    if (point.X >= 0 &&
                        point.X < Resolution &&
                        point.Y >= 0 &&
                        point.Y < Resolution &&
                        point.Z >= 0 &&
                        point.Z < Resolution)
                    {
                        array[point.X + point.Y * Resolution + point.Z * Resolution * Resolution] =
                            _values[k + j * Resolution + i * Resolution * Resolution];
                    }
                }
            }
        }

        SetValues(Resolution, array);
    }

    public void Mirror(int axis)
    {
        var array = new int[Resolution * Resolution * Resolution];
        for (var i = 0; i < Resolution; i++)
        {
            for (var j = 0; j < Resolution; j++)
            {
                for (var k = 0; k < Resolution; k++)
                {
                    var vector = MirrorPoint(new Vector3(k, j, i) - new Vector3(Resolution / 2f - 0.5f), axis) +
                                 new Vector3(Resolution / 2f - 0.5f);
                    var point = new Point3(
                        (int)MathUtils.Round(vector.X),
                        (int)MathUtils.Round(vector.Y),
                        (int)MathUtils.Round(vector.Z)
                    );
                    if (point.X >= 0 &&
                        point.X < Resolution &&
                        point.Y >= 0 &&
                        point.Y < Resolution &&
                        point.Z >= 0 &&
                        point.Z < Resolution)
                    {
                        array[point.X + point.Y * Resolution + point.Z * Resolution * Resolution] =
                            _values[k + j * Resolution + i * Resolution * Resolution];
                    }
                }
            }
        }

        SetValues(Resolution, array);
    }

    public ValuesDictionary Save()
    {
        var stringBuilder = new StringBuilder();
        var runValue = _values[0];
        var runLength = 1;
        for (var i = 1; i < _values.Length; i++)
        {
            if (_values[i] != runValue)
            {
                stringBuilder.Append(runLength.ToString(CultureInfo.InvariantCulture));
                stringBuilder.Append('*');
                stringBuilder.Append(runValue.ToString(CultureInfo.InvariantCulture));
                stringBuilder.Append(',');
                runValue = _values[i];
                runLength = 1;
            }
            else
            {
                runLength++;
            }
        }

        stringBuilder.Append(runLength.ToString(CultureInfo.InvariantCulture));
        stringBuilder.Append('*');
        stringBuilder.Append(runValue.ToString(CultureInfo.InvariantCulture));
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
        var furnitureDesign = new FurnitureDesign(_catalog, _subsystemTerrain);
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

        var linkedIndex = list.IndexOf(furnitureDesign);
        if (linkedIndex >= 0)
        {
            list2[^1].LinkedDesign = list2[linkedIndex];
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
        var cellIndex = 0;
        for (var i = 0; i < Resolution; i++)
        {
            for (var j = 0; j < Resolution; j++)
            {
                var emptyRunLength = 0;
                var x = 0;
                while (x < Resolution)
                {
                    emptyRunLength = _values[cellIndex] == 0 ? emptyRunLength + 1 : 0;
                    array[cellIndex] = (byte)emptyRunLength;
                    x++;
                    cellIndex++;
                }
            }
        }

        return array;
    }

    public Box CalculateBox(Box box, byte[] precedingEmptySpaces)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var minZ = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        var maxZ = int.MinValue;
        for (var i = box.Near; i < box.Far; i++)
        {
            var candidateMinZ = Math.Min(minZ, i);
            var candidateMaxZ = Math.Max(maxZ, i);
            var y = box.Top;
            var rowStart = (y + i * Resolution) * Resolution;
            while (y < box.Bottom)
            {
                var lastOccupiedX = box.Right - 1 - precedingEmptySpaces[rowStart + box.Right - 1];
                if (lastOccupiedX >= box.Left)
                {
                    maxX = Math.Max(maxX, lastOccupiedX);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                    minZ = candidateMinZ;
                    maxZ = candidateMaxZ;
                    var searchEndX = minX - 1;
                    for (var j = box.Left; j <= searchEndX; j++)
                    {
                        if (_values[rowStart + j] != 0)
                        {
                            minX = Math.Min(minX, j);
                            break;
                        }
                    }
                }

                y++;
                rowStart += Resolution;
            }
        }

        return new Box(minX, minY, minZ, maxX - minX + 1, maxY - minY + 1, maxZ - minZ + 1);
    }

    private int CalculateShadowStrengthFactor()
    {
        var array = new float[Resolution * Resolution];
        var cellIndex = 0;
        for (var i = 0; i < Resolution; i++)
        {
            for (var j = 0; j < Resolution; j++)
            {
                var x = (j + 1) / (float)Resolution;
                for (var k = 0; k < Resolution; k++)
                {
                    if (!IsValueTransparent(_values[cellIndex++]))
                    {
                        array[k + i * Resolution] = MathUtils.Max(array[k + i * Resolution], x);
                    }
                }
            }
        }

        var averageHeight = 0f;
        for (var l = 0; l < Resolution * Resolution; l++)
        {
            averageHeight += array[l];
        }

        averageHeight /= Resolution * Resolution;
        var shadowScale = 1.5f;
        return (int)MathUtils.Clamp(MathUtils.Round(averageHeight * 3f * shadowScale), 0f, 3f);
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
            var m = Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                    Matrix.CreateRotationY(j * (float)Math.PI / 2f) *
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
            var mergeIndex = 0;
            int l;
            BoundingBox item;
            while (true)
            {
                if (mergeIndex < list2.Count)
                {
                    for (l = 0; l < list2.Count; l++)
                    {
                        if (mergeIndex != l)
                        {
                            var b = list2[mergeIndex];
                            var b2 = list2[l];
                            item = BoundingBox.Union(b, b2);
                            var vector = item.Size();
                            if ((item.Volume() - b.Volume() - b2.Volume()) /
                                MathUtils.Min(vector.X, vector.Y, vector.Z) <
                                0.4f)
                            {
                                goto end_IL_0263;
                            }
                        }
                    }

                    mergeIndex++;
                    continue;
                }

                var hasLargeInteractionFace = false;
                for (var n = 0; n < list2.Count; n++)
                {
                    var boxSize = list2[n].Size();
                    hasLargeInteractionFace |= boxSize.X >= 0.6f && boxSize.Y >= 0.6f;
                    hasLargeInteractionFace |= boxSize.X >= 0.6f && boxSize.Z >= 0.6f;
                    hasLargeInteractionFace |= boxSize.Y >= 0.6f && boxSize.Z >= 0.6f;
                }

                var minSize = hasLargeInteractionFace ? 0.0625f : 0.6f;
                for (var resizeIndex = 0; resizeIndex < list2.Count; resizeIndex++)
                {
                    var value = list2[resizeIndex];
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
                    list2[resizeIndex] = value;
                }

                _interactionBoxesByRotation = new BoundingBox[4][];
                for (var rotation = 0; rotation < 4; rotation++)
                {
                    var m2 = Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                             Matrix.CreateRotationY(rotation * (float)Math.PI / 2f) *
                             Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                    _interactionBoxesByRotation[rotation] = new BoundingBox[list2.Count];
                    for (var boxIndex = 0; boxIndex < list2.Count; boxIndex++)
                    {
                        var v3 = Vector3.Transform(list2[boxIndex].Min, m2);
                        var v4 = Vector3.Transform(list2[boxIndex].Max, m2);
                        var boundingBox2 = new BoundingBox(Vector3.Min(v3, v4), Vector3.Max(v3, v4));
                        _interactionBoxesByRotation[rotation][boxIndex] = boundingBox2;
                    }
                }

                return collisionBoxesByRotation;
                end_IL_0263:
                break;
            }

            list2.RemoveAt(mergeIndex);
            list2.RemoveAt(mergeIndex < l ? l - 1 : l);
            list2.Add(item);
        }
    }

    private BoundingBox[][] CreateTorchPoints()
    {
        var list = new List<BoundingBox>();
        for (var i = 0; i < Resolution; i++)
        {
            for (var j = 0; j < Resolution; j++)
            {
                for (var k = 0; k < Resolution; k++)
                {
                    var contents = Terrain.ExtractContents(_values[k + j * Resolution + i * Resolution * Resolution]);
                    if (contents != 31 && contents != 17)
                    {
                        continue;
                    }

                    var boundingBox = new BoundingBox(
                        new Vector3(k, j, i) / Resolution,
                        new Vector3(k + 1, j + 1, i + 1) / Resolution
                    );
                    var clusterIndex = -1;
                    for (var l = 0; l < list.Count; l++)
                    {
                        var boundingBox2 = list[l];
                        var vector = boundingBox2.Size();
                        var distanceToBox = boundingBox2.Center() - boundingBox.Center();
                        distanceToBox.X = MathUtils.Max(MathUtils.Abs(distanceToBox.X) - vector.X / 2f, 0f);
                        distanceToBox.Y = MathUtils.Max(MathUtils.Abs(distanceToBox.Y) - vector.Y / 2f, 0f);
                        distanceToBox.Z = MathUtils.Max(MathUtils.Abs(distanceToBox.Z) - vector.Z / 2f, 0f);
                        if (!(distanceToBox.Length() < 0.15f))
                        {
                            continue;
                        }

                        clusterIndex = l;
                        break;
                    }

                    if (clusterIndex >= 0)
                    {
                        list[clusterIndex] = BoundingBox.Union(list[clusterIndex], boundingBox);
                    }
                    else if (list.Count < 4)
                    {
                        list.Add(boundingBox);
                    }
                }
            }
        }

        var torchPointsByRotation = new BoundingBox[4][];
        for (var m = 0; m < 4; m++)
        {
            var m2 = Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                     Matrix.CreateRotationY(m * (float)Math.PI / 2f) *
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
        {
            for (var j = 0; j < Resolution; j++)
            {
                for (var y = Resolution - 1; y >= 0; y--)
                {
                    var blockValue = _values[j + y * Resolution + i * Resolution * Resolution];
                    if (blockValue != 0)
                    {
                        dictionary.TryGetValue(blockValue, out var value);
                        dictionary[blockValue] = value + 1;
                        break;
                    }
                }
            }
        }

        var highestCount = 0;
        foreach (var item in dictionary)
        {
            if (item.Value > highestCount)
            {
                _mainValue = item.Key;
                highestCount = item.Value;
            }
        }
    }

    public void CalculateFacesMasks()
    {
        _mountingFacesMask = 0;
        _transparentFacesMask = 0;
        for (var i = 0; i < Resolution; i++)
        {
            for (var j = 0; j < Resolution; j++)
            {
                var values = _values;
                var frontCellIndex = i + j * Resolution;
                _ = Resolution;
                var value = values[frontCellIndex + 0 * Resolution];
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
        }

        for (var k = 0; k < Resolution; k++)
        {
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
        }

        for (var m = 0; m < Resolution; m++)
        {
            for (var n = 0; n < Resolution; n++)
            {
                var values2 = _values;
                var bottomCellX = m;
                _ = Resolution;
                var value5 = values2[bottomCellX + 0 + n * Resolution * Resolution];
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
    }

    public Subdivision CreateBoundingBoxesHelper(Box box, int depth, byte[] precedingEmptySpaces)
    {
        var extraBoxPenalty = 0;
        Subdivision result = default;
        result.TotalVolume = box.Width * box.Height * box.Depth;
        result.MinVolume = result.TotalVolume;
        result.Boxes = new List<Box>
        {
            box
        };
        if (depth < 2)
        {
            for (var splitY = box.Bottom - 1; splitY >= box.Top + 1; splitY--)
            {
                var box2 = CalculateBox(
                    new Box(box.Left, box.Top, box.Near, box.Width, splitY - box.Top, box.Depth),
                    precedingEmptySpaces
                );
                var box3 = CalculateBox(
                    new Box(box.Left, splitY, box.Near, box.Width, box.Bottom - splitY, box.Depth),
                    precedingEmptySpaces
                );
                var subdivision = CreateBoundingBoxesHelper(box2, depth + 1, precedingEmptySpaces);
                var subdivision2 = CreateBoundingBoxesHelper(box3, depth + 1, precedingEmptySpaces);
                var boxCount = subdivision.Boxes.Count + subdivision2.Boxes.Count;
                var combinedVolume = subdivision.TotalVolume + subdivision2.TotalVolume;
                var smallestVolume = MathUtils.Min(subdivision.MinVolume, subdivision2.MinVolume);
                var candidateCost = boxCount > result.Boxes.Count ? combinedVolume + extraBoxPenalty : combinedVolume;
                if (candidateCost < result.TotalVolume ||
                    (candidateCost == result.TotalVolume && smallestVolume > result.MinVolume))
                {
                    result.TotalVolume = combinedVolume;
                    result.MinVolume = smallestVolume;
                    result.Boxes = subdivision.Boxes;
                    result.Boxes.AddRange(subdivision2.Boxes);
                }
            }

            for (var i = box.Near + 1; i < box.Far; i++)
            {
                var box4 = CalculateBox(
                    new Box(box.Left, box.Top, box.Near, box.Width, box.Height, i - box.Near),
                    precedingEmptySpaces
                );
                var box5 = CalculateBox(
                    new Box(box.Left, box.Top, i, box.Width, box.Height, box.Far - i),
                    precedingEmptySpaces
                );
                var subdivision3 = CreateBoundingBoxesHelper(box4, depth + 1, precedingEmptySpaces);
                var subdivision4 = CreateBoundingBoxesHelper(box5, depth + 1, precedingEmptySpaces);
                var boxCount = subdivision3.Boxes.Count + subdivision4.Boxes.Count;
                var combinedVolume = subdivision3.TotalVolume + subdivision4.TotalVolume;
                var smallestVolume = MathUtils.Min(subdivision3.MinVolume, subdivision4.MinVolume);
                var candidateCost = boxCount > result.Boxes.Count ? combinedVolume + extraBoxPenalty : combinedVolume;
                if (candidateCost < result.TotalVolume ||
                    (candidateCost == result.TotalVolume && smallestVolume > result.MinVolume))
                {
                    result.TotalVolume = combinedVolume;
                    result.MinVolume = smallestVolume;
                    result.Boxes = subdivision3.Boxes;
                    result.Boxes.AddRange(subdivision4.Boxes);
                }
            }

            for (var j = box.Left + 1; j < box.Right; j++)
            {
                var box6 = CalculateBox(
                    new Box(box.Left, box.Top, box.Near, j - box.Left, box.Height, box.Depth),
                    precedingEmptySpaces
                );
                var box7 = CalculateBox(
                    new Box(j, box.Top, box.Near, box.Right - j, box.Height, box.Depth),
                    precedingEmptySpaces
                );
                var subdivision5 = CreateBoundingBoxesHelper(box6, depth + 1, precedingEmptySpaces);
                var subdivision6 = CreateBoundingBoxesHelper(box7, depth + 1, precedingEmptySpaces);
                var boxCount = subdivision5.Boxes.Count + subdivision6.Boxes.Count;
                var combinedVolume = subdivision5.TotalVolume + subdivision6.TotalVolume;
                var smallestVolume = MathUtils.Min(subdivision5.MinVolume, subdivision6.MinVolume);
                var candidateCost = boxCount > result.Boxes.Count ? combinedVolume + extraBoxPenalty : combinedVolume;
                if (candidateCost < result.TotalVolume ||
                    (candidateCost == result.TotalVolume && smallestVolume > result.MinVolume))
                {
                    result.TotalVolume = combinedVolume;
                    result.MinVolume = smallestVolume;
                    result.Boxes = subdivision5.Boxes;
                    result.Boxes.AddRange(subdivision6.Boxes);
                }
            }
        }

        return result;
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
        var size = max - min;
        if (size < minSize)
        {
            var missingSize = minSize - size;
            min -= missingSize / 2f;
            max += missingSize / 2f;
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

    public struct Subdivision
    {
        public int TotalVolume;

        public int MinVolume;

        public List<Box> Boxes;
    }
}
