using System.Globalization;
using System.Text;

using Engine.Graphics;
using Engine.Serialization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemMovingBlocks : Subsystem, IUpdateable, IDrawable
{
    private static readonly int[] _drawOrders = [150];

    private BlockGeometryGenerator? _blockGeometryGenerator;

    private bool _canGenerateGeometry;

    private readonly DynamicArray<int> _indices = [];

    public readonly List<MovingBlockSet> MovingBlockSets = [];

    private readonly List<MovingBlockSet> _removing = [];

    private readonly DynamicArray<IMovingBlockSet> _result = [];

    private Shader _shader = null!;

    private readonly List<MovingBlockSet> _stopped = [];

    private SubsystemAnimatedTextures _subsystemAnimatedTextures = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private readonly DynamicArray<TerrainVertex> _vertices = new();

    public IReadOnlyList<IMovingBlockSet> ReadonlyMovingBlockSets => MovingBlockSets;

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
#if SERVER
        return;
#else
        _vertices.Clear();
        _indices.Clear();
        foreach (var movingBlockSet2 in MovingBlockSets)
        {
            DrawMovingBlockSet(camera, movingBlockSet2);
        }

        var num = 0;
        while (num < _removing.Count)
        {
            var movingBlockSet = _removing[num];
            if (movingBlockSet.RemainCounter-- > 0)
            {
                DrawMovingBlockSet(camera, movingBlockSet);
                num++;
            }
            else
            {
                _removing.RemoveAt(num);
            }
        }

        if (_vertices.Count > 0)
        {
            var viewPosition = camera.ViewPosition;
            var v = new Vector3(MathUtils.Floor(viewPosition.X), 0f, MathUtils.Floor(viewPosition.Z));
            var value = Matrix.CreateTranslation(v - viewPosition) * camera.ViewMatrix.OrientationMatrix *
                        camera.ProjectionMatrix;
            Display.BlendState = BlendState.AlphaBlend;
            Display.DepthStencilState = DepthStencilState.Default;
            Display.RasterizerState = RasterizerState.CullCounterClockwiseScissor;
            _shader.GetParameter("u_origin").SetValue(v.XZ);
            _shader.GetParameter("u_viewProjectionMatrix").SetValue(value);
            _shader.GetParameter("u_viewPosition").SetValue(camera.ViewPosition);
            _shader.GetParameter("u_texture").SetValue(_subsystemAnimatedTextures.AnimatedBlocksTexture);
            _shader.GetParameter("u_samplerState").SetValue(SamplerState.PointClamp);
            _shader.GetParameter("u_fogColor").SetValue(new Vector3(_subsystemSky.ViewFogColor));
            _shader.GetParameter("u_fogBottomTopDensity").SetValue(new Vector3(_subsystemSky.ViewFogBottom,
                _subsystemSky.ViewFogTop, _subsystemSky.ViewFogDensity));
            _shader.GetParameter("u_hazeStartDensity")
                .SetValue(new Vector2(_subsystemSky.ViewHazeStart, _subsystemSky.ViewHazeDensity));
            _shader.GetParameter("u_alphaThreshold").SetValue(0.5f);
            //_shader.GetParameter("u_fogStartInvLength").SetValue(new Vector2(_subsystemSky.ViewFogRange.X, 1f / (_subsystemSky.ViewFogRange.Y - _subsystemSky.ViewFogRange.X)));
            var vertexBuffer = new VertexBuffer(TerrainVertex.VertexDeclaration, _vertices.Count);
            var indexBuffer = new IndexBuffer(IndexFormat.ThirtyTwoBits, _indices.Count);
            vertexBuffer.SetData(_vertices.Array, 0, _vertices.Count);
            indexBuffer.SetData(_indices.Array, 0, _indices.Count);
            Display.DrawIndexed(PrimitiveType.TriangleList, _shader, vertexBuffer, indexBuffer, 0,
                indexBuffer.IndicesCount);
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
        }
#endif
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        _canGenerateGeometry = true;
        foreach (var movingBlockSet in MovingBlockSets)
        {
            var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(
                Terrain.ToCell(movingBlockSet.Position.X),
                Terrain.ToCell(movingBlockSet.Position.Z),
                false);
            if (chunkAtCell is not { State: > TerrainChunkState.InvalidContents4 })
            {
                continue;
            }

            movingBlockSet.Speed += movingBlockSet.Acceleration * _subsystemTime.GameTimeDelta;
            if (movingBlockSet.Drag != 0f)
            {
                movingBlockSet.Speed *= MathUtils.Pow(1f - movingBlockSet.Drag, _subsystemTime.GameTimeDelta);
            }

            var x = Vector3.Distance(movingBlockSet.StartPosition, movingBlockSet.Position);
            var num = Vector3.Distance(movingBlockSet.TargetPosition, movingBlockSet.Position);
            var num2 = movingBlockSet.Smoothness.X > 0f
                ? MathUtils.Saturate((MathUtils.Sqrt(x) + 0.05f) / movingBlockSet.Smoothness.X)
                : 1f;
            var num3 = movingBlockSet.Smoothness.Y > 0f
                ? MathUtils.Saturate((num + 0.05f) / movingBlockSet.Smoothness.Y)
                : 1f;
            var num4 = num2 * num3;
            var flag = false;
            var vector = num > 0f ? (movingBlockSet.TargetPosition - movingBlockSet.Position) / num : Vector3.Zero;
            var x2 = _subsystemTime.GameTimeDelta > 0f ? 0.95f / _subsystemTime.GameTimeDelta : 0f;
            var num5 = MathUtils.Min(movingBlockSet.Speed * num4, x2);
            if (num5 * _subsystemTime.GameTimeDelta >= num)
            {
                movingBlockSet.Position = movingBlockSet.TargetPosition;
                movingBlockSet.CurrentVelocity = Vector3.Zero;
                flag = true;
            }
            else
            {
                movingBlockSet.CurrentVelocity =
                    num5 / num * (movingBlockSet.TargetPosition - movingBlockSet.Position);
                movingBlockSet.Position += movingBlockSet.CurrentVelocity * _subsystemTime.GameTimeDelta;
            }

            movingBlockSet.Stop = false;
            MovingBlocksCollision(movingBlockSet);
            TerrainCollision(movingBlockSet);
            if (movingBlockSet.Stop)
            {
                if (vector.X < 0f)
                {
                    movingBlockSet.Position.X = MathUtils.Ceiling(movingBlockSet.Position.X);
                }
                else if (vector.X > 0f)
                {
                    movingBlockSet.Position.X = MathUtils.Floor(movingBlockSet.Position.X);
                }

                if (vector.Y < 0f)
                {
                    movingBlockSet.Position.Y = MathUtils.Ceiling(movingBlockSet.Position.Y);
                }
                else if (vector.Y > 0f)
                {
                    movingBlockSet.Position.Y = MathUtils.Floor(movingBlockSet.Position.Y);
                }

                if (vector.Z < 0f)
                {
                    movingBlockSet.Position.Z = MathUtils.Ceiling(movingBlockSet.Position.Z);
                }
                else if (vector.Z > 0f)
                {
                    movingBlockSet.Position.Z = MathUtils.Floor(movingBlockSet.Position.Z);
                }
            }

            if (movingBlockSet.Stop | flag)
            {
                _stopped.Add(movingBlockSet);
            }
        }

        if (CommonLib.WorkType != WorkType.Client)
        {
            foreach (var item in _stopped)
            {
                DoStop(item);
                CommonLib.Net.QueuePackage(new MovingBlockPackage(item, true));
            }
        }

        _stopped.Clear();
    }

    public event Action<IMovingBlockSet, Point3>? CollidedWithTerrain;

    public event Action<IMovingBlockSet>? Stopped;

    public IMovingBlockSet? AddMovingBlockSet(
        Vector3 position,
        Vector3 targetPosition,
        float speed,
        float acceleration,
        float drag,
        Vector2 smoothness,
        IEnumerable<MovingBlock> blocks,
        string id,
        object tag,
        bool testCollision
    )
    {
        var movingBlockSet = new MovingBlockSet
        {
            Position = position,
            StartPosition = position,
            TargetPosition = targetPosition,
            Speed = speed,
            Acceleration = acceleration,
            Drag = drag,
            Smoothness = smoothness,
            Id = id,
            Tag = tag,
            Blocks = blocks.ToList()
        };
        movingBlockSet.UpdateBox();
        if (testCollision)
        {
            MovingBlocksCollision(movingBlockSet);
            if (movingBlockSet.Stop)
            {
                return null;
            }
        }

        if (_canGenerateGeometry)
        {
            GenerateGeometry(movingBlockSet);
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            return movingBlockSet;
        }

        MovingBlockSets.Add(movingBlockSet);
        CommonLib.Net.QueuePackage(new MovingBlockPackage(movingBlockSet));

        return movingBlockSet;
    }

    public void RemoveMovingBlockSet(IMovingBlockSet movingBlockSet)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        RemoveMovingBlockSetLogic(movingBlockSet);
        CommonLib.Net.QueuePackage(new MovingBlockPackage(movingBlockSet, false));
    }

    public void RemoveMovingBlockSetLogic(IMovingBlockSet movingBlockSet)
    {
        var movingBlockSet2 = (MovingBlockSet)movingBlockSet;
        if (MovingBlockSets.Remove(movingBlockSet2))
        {
            _removing.Add(movingBlockSet2);
            movingBlockSet2.RemainCounter = 4;
        }
    }

    public void FindMovingBlocks(BoundingBox boundingBox, bool extendToFillCells, DynamicArray<IMovingBlockSet> result)
    {
        foreach (var movingBlockSet in MovingBlockSets)
        {
            if (ExclusiveBoxIntersection(boundingBox, movingBlockSet.BoundingBox(extendToFillCells)))
            {
                result.Add(movingBlockSet);
            }
        }
    }

    public IMovingBlockSet? FindMovingBlocks(string id, object? tag)
    {
        foreach (var movingBlockSet in MovingBlockSets)
        {
            if (movingBlockSet.Id == id && Equals(movingBlockSet.Tag, tag))
            {
                return movingBlockSet;
            }
        }

        return null;
    }

    public MovingBlocksRaycastResult? Raycast(Vector3 start, Vector3 end, bool extendToFillCells)
    {
        var ray = new Ray3(start, Vector3.Normalize(end - start));
        var boundingBox = new BoundingBox(Vector3.Min(start, end), Vector3.Max(start, end));
        _result.Clear();
        FindMovingBlocks(boundingBox, extendToFillCells, _result);
        var num = float.MaxValue;
        MovingBlockSet? movingBlockSet = null;
        foreach (var movingBlockSet1 in _result)
        {
            var item = (MovingBlockSet)movingBlockSet1;
            var box = item.BoundingBox(extendToFillCells);
            var num2 = ray.Intersection(box);
            if (!(num2 < num))
            {
                continue;
            }

            num = num2.Value;
            movingBlockSet = item;
        }

        if (movingBlockSet == null)
        {
            return null;
        }

        var value = default(MovingBlocksRaycastResult);
        value.Ray = ray;
        value.Distance = num;
        value.MovingBlockSet = movingBlockSet;
        return value;
    }

    public void DoStop(MovingBlockSet item)
    {
        Stopped?.Invoke(item);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemAnimatedTextures = Project.FindSubsystem<SubsystemAnimatedTextures>(true)!;
#if !SERVER
        _shader = ContentManager.Get<Shader>("Shaders/AlphaTested");
#endif
        foreach (ValuesDictionary value9 in valuesDictionary.GetValue<ValuesDictionary>("MovingBlockSets").Values)
        {
            LoadAndAddMovingItem(value9);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("MovingBlockSets", valuesDictionary2);
        var num = 0;
        foreach (var movingBlockSet in MovingBlockSets)
        {
            valuesDictionary2.SetValue(num++.ToString(CultureInfo.InvariantCulture), SaveMovingItem(movingBlockSet));
        }
    }

    public IMovingBlockSet? LoadAndAddMovingItem(ValuesDictionary value9)
    {
        var value = value9.GetValue<Vector3>("Position");
        var value2 = value9.GetValue<Vector3>("TargetPosition");
        var value3 = value9.GetValue<float>("Speed");
        var value4 = value9.GetValue<float>("Acceleration");
        var value5 = value9.GetValue<float>("Drag");
        var value6 = value9.GetValue("Smoothness", Vector2.Zero);
        var value7 = value9.GetValue("Id", string.Empty);
        var value8 = value9.GetValue("Tag", new object());
        var list = new List<MovingBlock>();
        var array = value9.GetValue<string>("Blocks").Split([';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var obj2 in array)
        {
            MovingBlock item = default;
            var array2 = obj2.Split([','], StringSplitOptions.RemoveEmptyEntries);
            item.Value = HumanReadableConverter.ConvertFromString<int>(array2[0]);
            item.Offset.X = HumanReadableConverter.ConvertFromString<int>(array2[1]);
            item.Offset.Y = HumanReadableConverter.ConvertFromString<int>(array2[2]);
            item.Offset.Z = HumanReadableConverter.ConvertFromString<int>(array2[3]);
            list.Add(item);
        }

        return AddMovingBlockSet(value, value2, value3, value4, value5, value6, list, value7, value8, false);
    }

    public static ValuesDictionary SaveMovingItem(MovingBlockSet movingBlockSet)
    {
        var valuesDictionary3 = new ValuesDictionary();
        valuesDictionary3.SetValue("Position", movingBlockSet.Position);
        valuesDictionary3.SetValue("TargetPosition", movingBlockSet.TargetPosition);
        valuesDictionary3.SetValue("Speed", movingBlockSet.Speed);
        valuesDictionary3.SetValue("Acceleration", movingBlockSet.Acceleration);
        valuesDictionary3.SetValue("Drag", movingBlockSet.Drag);
        if (movingBlockSet.Smoothness != Vector2.Zero)
        {
            valuesDictionary3.SetValue("Smoothness", movingBlockSet.Smoothness);
        }

        if (!string.IsNullOrEmpty(movingBlockSet.Id))
        {
            valuesDictionary3.SetValue("Id", movingBlockSet.Id);
        }

        if (HumanReadableConverter.IsTypeSupported(movingBlockSet.Tag.GetType()))
        {
            valuesDictionary3.SetValue("Tag", movingBlockSet.Tag);
        }
        var stringBuilder = new StringBuilder();
        foreach (var block in movingBlockSet.Blocks)
        {
            stringBuilder.Append(HumanReadableConverter.ConvertToString(block.Value));
            stringBuilder.Append(',');
            stringBuilder.Append(HumanReadableConverter.ConvertToString(block.Offset.X));
            stringBuilder.Append(',');
            stringBuilder.Append(HumanReadableConverter.ConvertToString(block.Offset.Y));
            stringBuilder.Append(',');
            stringBuilder.Append(HumanReadableConverter.ConvertToString(block.Offset.Z));
            stringBuilder.Append(';');
        }

        valuesDictionary3.SetValue("Blocks", stringBuilder.ToString());
        return valuesDictionary3;
    }

    public override void Dispose()
    {
        if (_blockGeometryGenerator is { Terrain: not null })
        {
            _blockGeometryGenerator.Terrain.Dispose();
        }
    }

    private void MovingBlocksCollision(MovingBlockSet movingBlockSet)
    {
        var boundingBox = movingBlockSet.BoundingBox(true);
        _result.Clear();
        FindMovingBlocks(boundingBox, true, _result);
        var num = 0;
        while (true)
        {
            if (num >= _result.Count)
            {
                return;
            }

            if (_result.Array[num] != movingBlockSet)
            {
                break;
            }

            num++;
        }

        movingBlockSet.Stop = true;
    }

    private void TerrainCollision(MovingBlockSet movingBlockSet)
    {
        var point = default(Point3);
        point.X = (int)MathUtils.Floor(movingBlockSet.Box.Left + movingBlockSet.Position.X);
        point.Y = (int)MathUtils.Floor(movingBlockSet.Box.Top + movingBlockSet.Position.Y);
        point.Z = (int)MathUtils.Floor(movingBlockSet.Box.Near + movingBlockSet.Position.Z);
        var point2 = default(Point3);
        point2.X = (int)MathUtils.Ceiling(movingBlockSet.Box.Right + movingBlockSet.Position.X);
        point2.Y = (int)MathUtils.Ceiling(movingBlockSet.Box.Bottom + movingBlockSet.Position.Y);
        point2.Z = (int)MathUtils.Ceiling(movingBlockSet.Box.Far + movingBlockSet.Position.Z);
        for (var i = point.X; i < point2.X; i++)
        for (var j = point.Z; j < point2.Z; j++)
        for (var k = point.Y; k < point2.Y; k++)
        {
            if (Terrain.ExtractContents(_subsystemTerrain.Terrain.GetCellValue(i, k, j)) != 0)
            {
                CollidedWithTerrain?.Invoke(movingBlockSet, new Point3(i, k, j));
            }
            else
            {
                if (!SubsystemBedrockBlockBehavior.CheckIsInTerritoriyBorder(i, j, out var territoriy))
                {
                    continue;
                }

                if (territoriy!.IsVisible)
                {
                    movingBlockSet.Stop = true;
                }
            }
        }
    }

    private void GenerateGeometry(MovingBlockSet movingBlockSet)
    {
        var point = default(Point3);
        point.X = movingBlockSet.CurrentVelocity.X > 0f
            ? (int)MathUtils.Floor(movingBlockSet.Position.X)
            : point.X = (int)MathUtils.Ceiling(movingBlockSet.Position.X);
        point.Y = movingBlockSet.CurrentVelocity.Y > 0f
            ? (int)MathUtils.Floor(movingBlockSet.Position.Y)
            : point.Y = (int)MathUtils.Ceiling(movingBlockSet.Position.Y);
        point.Z = movingBlockSet.CurrentVelocity.Z > 0f
            ? (int)MathUtils.Floor(movingBlockSet.Position.Z)
            : point.Z = (int)MathUtils.Ceiling(movingBlockSet.Position.Z);
        if (!(point != movingBlockSet.GeometryGenerationPosition))
        {
            return;
        }

        var p = new Point3(movingBlockSet.Box.Left, movingBlockSet.Box.Top, movingBlockSet.Box.Near);
        var point2 = new Point3(movingBlockSet.Box.Width, movingBlockSet.Box.Height, movingBlockSet.Box.Depth);
        point2.Y = MathUtils.Min(point2.Y, 510);
        var numk = point.Y + p.Y;
        if (_blockGeometryGenerator == null)
        {
            var x = 2;
            x = (int)MathUtils.NextPowerOf2((uint)x);
            _blockGeometryGenerator = new BlockGeometryGenerator(
                new Terrain(),
                _subsystemTerrain,
                Project.FindSubsystem<SubsystemElectricity>(true)!,
                Project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!,
                Project.FindSubsystem<SubsystemMetersBlockBehavior>(true)!,
                Project.FindSubsystem<SubsystemPalette>(true)!
            );
            for (var i = 0; i < x; i++)
            for (var j = 0; j < x; j++)
            {
                _blockGeometryGenerator.Terrain.AllocateChunk(i, j);
            }
        }

        var terrain = _subsystemTerrain.Terrain;
        for (var k = 0; k < point2.X + 2; k++)
        for (var l = 0; l < point2.Z + 2; l++)
        {
            var x2 = k + p.X + point.X - 1;
            var z = l + p.Z + point.Z - 1;
            var shaftValue = terrain.GetShaftValue(x2, z);
            _blockGeometryGenerator.Terrain.SetTemperature(k, l, Terrain.ExtractTemperature(shaftValue));
            _blockGeometryGenerator.Terrain.SetHumidity(k, l, Terrain.ExtractHumidity(shaftValue));
            for (var m = 0; m < point2.Y + 2; m++)
            {
                var y = m + p.Y + point.Y - 1;
                var light = Terrain.ExtractLight(terrain.GetCellValue(x2, y, z));
                _blockGeometryGenerator.Terrain.SetCellValueFast(k, m, l, Terrain.MakeBlockValue(0, light, 0));
            }
        }

        _blockGeometryGenerator.Terrain.SeasonTemperature = terrain.SeasonTemperature;
        _blockGeometryGenerator.Terrain.SeasonHumidity = terrain.SeasonHumidity;
        foreach (var block in movingBlockSet.Blocks)
        {
            var x3 = block.Offset.X - p.X + 1;
            var y2 = block.Offset.Y - p.Y + 1;
            var z2 = block.Offset.Z - p.Z + 1;
            var value = Terrain.ReplaceLight(light: _blockGeometryGenerator.Terrain.GetCellLightFast(x3, y2, z2),
                value: block.Value);
            _blockGeometryGenerator.Terrain.SetCellValueFast(x3, y2, z2, value);
        }

        _blockGeometryGenerator.ResetCache();
        movingBlockSet.Vertices.Clear();
        movingBlockSet.Indices.Clear();
        for (var n = 1; n < point2.X + 1; n++)
        for (var num = 1; num < point2.Y + 1; num++)
        for (var num2 = 1; num2 < point2.Z + 1; num2++)
        {
            if (num + numk > 0 && num + numk < 511)
            {
                var cellValueFast = _blockGeometryGenerator.Terrain.GetCellValueFast(n, num, num2);
                var num3 = Terrain.ExtractContents(cellValueFast);
                if (num3 != 0)
                {
                    BlocksManager.Blocks[num3].GenerateTerrainVertices(_blockGeometryGenerator,
                        movingBlockSet.Geometry, cellValueFast, n, num, num2);
                }
            }
        }

        movingBlockSet.GeometryOffset = new Vector3(p) - new Vector3(1f);
        movingBlockSet.GeometryGenerationPosition = point;
    }

    private void DrawMovingBlockSet(Camera camera, MovingBlockSet movingBlockSet)
    {
        if (_vertices.Count > 20000 || !camera.ViewFrustum.Intersection(movingBlockSet.BoundingBox(false)))
        {
            return;
        }

        GenerateGeometry(movingBlockSet);
        var count = _vertices.Count;
        var array = movingBlockSet.Indices.Array;
        _ = movingBlockSet.Indices.Count;
        var vector = movingBlockSet.Position + movingBlockSet.GeometryOffset;
        var array2 = movingBlockSet.Vertices.Array;
        var count2 = movingBlockSet.Vertices.Count;
        for (var i = 0; i < count2; i++)
        {
            var item = array2[i];
            item.X += vector.X;
            item.Y += vector.Y;
            item.Z += vector.Z;
            _vertices.Add(item);
        }

        for (var j = 0; j < movingBlockSet.Indices.Count; j++)
        {
            _indices.Add(array[j] + count);
        }
    }

    private static bool ExclusiveBoxIntersection(BoundingBox b1, BoundingBox b2)
    {
        if (b1.Max.X > b2.Min.X && b1.Min.X < b2.Max.X && b1.Max.Y > b2.Min.Y && b1.Min.Y < b2.Max.Y &&
            b1.Max.Z > b2.Min.Z)
        {
            return b1.Min.Z < b2.Max.Z;
        }

        return false;
    }

    public class MovingBlockSet : IMovingBlockSet
    {
        public float Acceleration;

        public List<MovingBlock> Blocks = [];

        public Box Box;

        public Vector3 CurrentVelocity;

        public float Drag;

        public TerrainGeometry Geometry;

        public Point3 GeometryGenerationPosition = new(int.MaxValue);

        public Vector3 GeometryOffset;

        public string Id = string.Empty;

        public readonly DynamicArray<int> Indices = [];

        public Vector3 Position;

        public int RemainCounter;

        public Vector2 Smoothness;

        public float Speed;

        public Vector3 StartPosition;

        public bool Stop;

        public required object Tag;

        public Vector3 TargetPosition;

        public readonly DynamicArray<TerrainVertex> Vertices = [];

        public MovingBlockSet()
        {
            var terrainGeometrySubset = new TerrainGeometrySubset(Vertices, Indices);
            Geometry = new TerrainGeometry
            {
                SubsetOpaque = terrainGeometrySubset,
                SubsetAlphaTest = terrainGeometrySubset,
                SubsetTransparent = terrainGeometrySubset,
                OpaqueSubsetsByFace =
                [
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset
                ],
                AlphaTestSubsetsByFace =
                [
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset
                ],
                TransparentSubsetsByFace =
                [
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset,
                    terrainGeometrySubset
                ]
            };
        }

        Vector3 IMovingBlockSet.Position => Position;

        string IMovingBlockSet.Id => Id;

        object IMovingBlockSet.Tag => Tag;

        Vector3 IMovingBlockSet.CurrentVelocity => CurrentVelocity;

        ReadOnlyList<MovingBlock> IMovingBlockSet.Blocks => new(Blocks);

        public BoundingBox BoundingBox(bool extendToFillCells)
        {
            var min = new Vector3(Position.X + Box.Left, Position.Y + Box.Top, Position.Z + Box.Near);
            var max = new Vector3(Position.X + Box.Right, Position.Y + Box.Bottom, Position.Z + Box.Far);
            if (extendToFillCells)
            {
                min.X = MathUtils.Floor(min.X);
                min.Y = MathUtils.Floor(min.Y);
                min.Z = MathUtils.Floor(min.Z);
                max.X = MathUtils.Ceiling(max.X);
                max.Y = MathUtils.Ceiling(max.Y);
                max.Z = MathUtils.Ceiling(max.Z);
            }

            return new BoundingBox(min, max);
        }

        void IMovingBlockSet.SetBlock(Point3 offset, int value)
        {
            Blocks.RemoveAll(b => b.Offset == offset);
            if (value != 0)
            {
                Blocks.Add(new MovingBlock
                {
                    Offset = offset,
                    Value = value
                });
            }

            UpdateBox();
            GeometryGenerationPosition = new Point3(int.MaxValue);
        }

        void IMovingBlockSet.Stop()
        {
            Stop = true;
        }

        public void UpdateBox()
        {
            Point3? point = null;
            Point3? point2 = null;
            foreach (var block in Blocks)
            {
                point = point.HasValue ? Point3.Min(point.Value, block.Offset) : block.Offset;
                point2 = point2.HasValue ? Point3.Max(point2.Value, block.Offset) : block.Offset;
            }

            if (point.HasValue)
            {
                if (point2 == null)
                {
                    return;
                }

                Box = new Box(point.Value.X, point.Value.Y, point.Value.Z, point2.Value.X - point.Value.X + 1,
                    point2.Value.Y - point.Value.Y + 1, point2.Value.Z - point.Value.Z + 1);
            }
            else
            {
                Box = default;
            }
        }
    }
}
