using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using static Game.Subsystems.SubsystemMovingBlocks;

namespace Game.Subsystems;

public sealed class SubsystemMovingBlocksPresentation : Subsystem, IDrawable, IUpdateable
{
    private static readonly int[] _drawOrders = [150];

    private readonly DynamicArray<int> _indices = [];

    private readonly DynamicArray<TerrainVertex> _vertices = [];

    private readonly Dictionary<MovingBlockSet, GeometryState> _states = new(ReferenceEqualityComparer.Instance);

    private BlockGeometryGenerator? _blockGeometryGenerator;

    private Shader? _shader;

    private SubsystemMovingBlocks _moving = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemAnimatedTextures _subsystemAnimatedTextures = null!;

    private SubsystemSkyPresentation _subsystemSky = null!;

    public int[] DrawOrders => _drawOrders;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _moving = Project.FindSubsystem<SubsystemMovingBlocks>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemAnimatedTextures = Project.FindSubsystem<SubsystemAnimatedTextures>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSkyPresentation>(true)!;
        _moving.Added += OnAdded;
        _moving.Removed += OnRemoved;
        foreach (var set in _moving.MovingBlockSets) { OnAdded(set); }
    }

    private void OnAdded(MovingBlockSet set) => _states.TryAdd(set, new GeometryState());

    private void OnRemoved(MovingBlockSet set)
    {
        if (!_states.TryGetValue(set, out var state))
        {
            state = new GeometryState();
            _states.Add(set, state);
        }

        state.RemovedUpdates = 4;
    }

    public void Update(float dt)
    {
        foreach (var (set, state) in _states.ToArray())
        {
            if (state.RemovedUpdates is not { } remaining) { continue; }

            if (remaining == 0) { _states.Remove(set); }
            else { state.RemovedUpdates = remaining - 1; }
        }
    }

    public override void Dispose()
    {
        if (_moving is not null)
        {
            _moving.Added -= OnAdded;
            _moving.Removed -= OnRemoved;
        }

        _blockGeometryGenerator?.Terrain.Dispose();
        _blockGeometryGenerator = null;
        _states.Clear();
    }

    public void Draw(Camera camera, int drawOrder)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        _vertices.Clear();
        _indices.Clear();
        foreach (var (set, state) in _states)
        {
            DrawMovingBlockSet(camera, set, state);
        }

        if (_vertices.Count > 0)
        {
            _shader ??= ContentManager.Get<Shader>("Shaders/AlphaTested");
            var viewPosition = camera.ViewPosition;
            var v = new Vector3(MathUtils.Floor(viewPosition.X), 0f, MathUtils.Floor(viewPosition.Z));
            var value = Matrix.CreateTranslation(v - viewPosition) *
                        camera.ViewMatrix.OrientationMatrix *
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
            _shader.GetParameter("u_fogBottomTopDensity").SetValue(
                new Vector3(
                    _subsystemSky.ViewFogBottom,
                    _subsystemSky.ViewFogTop,
                    _subsystemSky.ViewFogDensity
                )
            );
            _shader.GetParameter("u_hazeStartDensity")
                .SetValue(new Vector2(_subsystemSky.ViewHazeStart, _subsystemSky.ViewHazeDensity));
            _shader.GetParameter("u_alphaThreshold").SetValue(0.5f);
            using var vertexBuffer = new VertexBuffer(TerrainVertex.VertexDeclaration, _vertices.Count);
            using var indexBuffer = new IndexBuffer(IndexFormat.ThirtyTwoBits, _indices.Count);
            vertexBuffer.SetData(_vertices.Array, 0, _vertices.Count);
            indexBuffer.SetData(_indices.Array, 0, _indices.Count);
            Display.DrawIndexed(
                PrimitiveType.TriangleList,
                _shader,
                vertexBuffer,
                indexBuffer,
                0,
                indexBuffer.IndicesCount
            );
        }
    }

    private void GenerateGeometry(MovingBlockSet movingBlockSet, GeometryState state)
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
        if (point == state.GeometryGenerationPosition && state.ContentRevision == movingBlockSet.ContentRevision)
        {
            return;
        }

        var p = new Point3(movingBlockSet.Box.Left, movingBlockSet.Box.Top, movingBlockSet.Box.Near);
        var point2 = new Point3(movingBlockSet.Box.Width, movingBlockSet.Box.Height, movingBlockSet.Box.Depth);
        point2.Y = MathUtils.Min(point2.Y, 254);
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
            {
                for (var j = 0; j < x; j++)
                {
                    _blockGeometryGenerator.Terrain.AllocateChunk(i, j);
                }
            }
        }

        var terrain = _subsystemTerrain.Terrain;
        for (var k = 0; k < point2.X + 2; k++)
        {
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
        }

        _blockGeometryGenerator.Terrain.SeasonTemperature = terrain.SeasonTemperature;
        _blockGeometryGenerator.Terrain.SeasonHumidity = terrain.SeasonHumidity;
        foreach (var block in movingBlockSet.Blocks)
        {
            var x3 = block.Offset.X - p.X + 1;
            var y2 = block.Offset.Y - p.Y + 1;
            var z2 = block.Offset.Z - p.Z + 1;
            var value = Terrain.ReplaceLight(
                light: _blockGeometryGenerator.Terrain.GetCellLightFast(x3, y2, z2),
                value: block.Value
            );
            _blockGeometryGenerator.Terrain.SetCellValueFast(x3, y2, z2, value);
        }

        _blockGeometryGenerator.ResetCache();
        state.Vertices.Clear();
        state.Indices.Clear();
        for (var n = 1; n < point2.X + 1; n++)
        {
            for (var num = 1; num < point2.Y + 1; num++)
            {
                for (var num2 = 1; num2 < point2.Z + 1; num2++)
                {
                    if (num + numk > 0 && num + numk < 256)
                    {
                        var cellValueFast = _blockGeometryGenerator.Terrain.GetCellValueFast(n, num, num2);
                        var num3 = Terrain.ExtractContents(cellValueFast);
                        if (num3 != 0)
                        {
                            Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(num3)
                                .GenerateTerrainVertices(
                                    _blockGeometryGenerator,
                                    state.Geometry,
                                    cellValueFast,
                                    n,
                                    num,
                                    num2
                                );
                        }
                    }
                }
            }
        }

        state.GeometryOffset = new Vector3(p) - new Vector3(1f);
        state.GeometryGenerationPosition = point;
        state.ContentRevision = movingBlockSet.ContentRevision;
    }

    private void DrawMovingBlockSet(Camera camera, MovingBlockSet movingBlockSet, GeometryState state)
    {
        if (movingBlockSet.Blocks.Count == 0 ||
            _vertices.Count > 20000 ||
            !camera.ViewFrustum.Intersection(movingBlockSet.BoundingBox(false)))
        {
            return;
        }

        GenerateGeometry(movingBlockSet, state);
        var count = _vertices.Count;
        var array = state.Indices.Array;
        _ = state.Indices.Count;
        var vector = movingBlockSet.Position + state.GeometryOffset;
        var array2 = state.Vertices.Array;
        var count2 = state.Vertices.Count;
        for (var i = 0; i < count2; i++)
        {
            var item = array2[i];
            item.X += vector.X;
            item.Y += vector.Y;
            item.Z += vector.Z;
            _vertices.Add(item);
        }

        for (var j = 0; j < state.Indices.Count; j++)
        {
            _indices.Add(array[j] + count);
        }
    }

    private sealed class GeometryState
    {
        public readonly DynamicArray<int> Indices = [];

        public readonly DynamicArray<TerrainVertex> Vertices = [];

        public TerrainGeometry Geometry;

        public Point3 GeometryGenerationPosition = new(int.MaxValue);

        public Vector3 GeometryOffset;

        public long ContentRevision = -1;

        public int? RemovedUpdates;

        public GeometryState()
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
    }
}
