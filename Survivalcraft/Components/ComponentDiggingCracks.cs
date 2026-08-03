using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Components;

public class ComponentDiggingCracks : Component, IDrawable
{
    private const int _defaultSlice = 0;

    private readonly DynamicArray<TerrainChunkGeometry.Buffer> _buffers = [];

    private Texture2D _defaultTexture = null!;

    private Dictionary<Texture2D, TerrainGeometry[]> _drawItem = new();

    private ComponentMiner _componentMiner = null!;

    public TerrainGeometry? TerrainGeometry;

    private SubsystemPlayers _players = null!;

    private Point3 _point;

    private Shader _shader = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private Texture2D[] _textures = [];

    private int _value;

    public int[] DrawOrders { get; } = [200];

    public void Draw(Camera camera, int drawOrder)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        if (!_componentMiner.DigCellFace.HasValue ||
            !(_componentMiner.DigProgress > 0f) ||
            !(_componentMiner.DigTime > 0.2f))
        {
            return;
        }

        var point = _componentMiner.DigCellFace.Value.Point;
        if (CommonLib.WorkType == WorkType.Client)
        {
            if (_players.MainPlayer != null)
            {
                if (Vector3.DistanceSquared(_players.MainPlayer.GameWidget.ActiveCamera.ViewPosition,
                        new Vector3(point)) > MathUtils.Sqr(SettingsManager.Current.VisibilityRange))
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        var cellValue = _subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
        var num = Terrain.ExtractContents(cellValue);
        var block = BlocksManager.Blocks[num];
        if (cellValue != _value || point != _point)
        {
            foreach (var item in _drawItem)
            {
                var subsets = item.Value[_defaultSlice].Subsets;
                foreach (var subset in subsets)
                {
                    subset.Indices.Clear();
                    subset.Vertices.Clear();
                }
            }

            DisposeBuffers();
            block.GenerateTerrainVertices(_subsystemTerrain.BlockGeometryGenerator,
                _drawItem[_defaultTexture][_defaultSlice], cellValue, point.X, point.Y, point.Z);
            _point = point;
            _value = cellValue;
            TerrainRenderer.CompileDrawSubsets(_drawItem, _buffers, block.SetDiggingCrackingTextureTransform);
        }

        var viewPosition = camera.ViewPosition;
        var v = new Vector3(MathUtils.Floor(viewPosition.X), 0f, MathUtils.Floor(viewPosition.Z));
        var value = Matrix.CreateTranslation(v - viewPosition) * camera.ViewMatrix.OrientationMatrix *
                    camera.ProjectionMatrix;
        try
        {
            Display.BlendState = BlendState.NonPremultiplied;
            Display.DepthStencilState = DepthStencilState.Default;
            Display.RasterizerState = RasterizerState.CullCounterClockwiseScissor;
            _shader.GetParameter("u_origin").SetValue(v.XZ);
            _shader.GetParameter("u_viewProjectionMatrix").SetValue(value);
            _shader.GetParameter("u_viewPosition").SetValue(camera.ViewPosition);
            _shader.GetParameter("u_samplerState").SetValue(SamplerState.PointWrap);
            _shader.GetParameter("u_fogColor").SetValue(new Vector3(_subsystemSky.ViewFogColor));
            //new
            _shader.GetParameter("u_fogYMultiplier").SetValue(_subsystemSky.VisibilityRangeYMultiplier);
            _shader.GetParameter("u_fogBottomTopDensity").SetValue(new Vector3(_subsystemSky.ViewFogBottom,
                _subsystemSky.ViewFogTop, _subsystemSky.ViewFogDensity));
            _shader.GetParameter("u_hazeStartDensity")
                .SetValue(new Vector2(_subsystemSky.ViewHazeStart, _subsystemSky.ViewHazeDensity));
            _shader.GetParameter("u_alphaThreshold").SetValue(0.5f);
            foreach (var buffer in _buffers)
            {
                _shader.GetParameter("u_texture").SetValue(block.GetDiggingCrackingTexture(_componentMiner,
                    _componentMiner.DigProgress, cellValue, _textures));
                Display.DrawIndexed(PrimitiveType.TriangleList, _shader, buffer.VertexBuffer,
                    buffer.IndexBuffer, 0, buffer.IndexBuffer.IndicesCount);
            }
        }
        catch
        {
            // ignored
        }
    }

    private void DisposeBuffers()
    {
        foreach (var b in _buffers)
        {
            b.Dispose();
        }

        _buffers.Clear();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        _players = Project.FindSubsystem<SubsystemPlayers>(true)!;
        _defaultTexture = Project.FindSubsystem<SubsystemAnimatedTextures>(true)!.AnimatedBlocksTexture;
        _drawItem = new Dictionary<Texture2D, TerrainGeometry[]>();
        var list = new TerrainGeometry[TerrainChunk.SlicesCount];
        for (var i = 0; i < TerrainChunk.SlicesCount; i++)
        {
            var t = new TerrainGeometry(_drawItem, i);
            list[i] = t;
        }

        _drawItem.Add(_defaultTexture, list);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _componentMiner = Entity.FindComponent<ComponentMiner>(true)!;
        _shader = ContentManager.Get<Shader>("Shaders/AlphaTested");
        _textures = new Texture2D[8];
        for (var i = 0; i < 8; i++)
        {
            _textures[i] = ContentManager.Get<Texture2D>($"Textures/Cracks{i + 1}");
        }
    }

    // 暂时用不到的1.8版本的新函数
    public class Geometry : TerrainGeometry
    {
        public Geometry()
        {
            var terrainGeometrySubset = new TerrainGeometrySubset();
            SubsetOpaque = terrainGeometrySubset;
            SubsetAlphaTest = terrainGeometrySubset;
            SubsetTransparent = terrainGeometrySubset;
            OpaqueSubsetsByFace =
            [
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset
            ];
            AlphaTestSubsetsByFace =
            [
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset
            ];
            TransparentSubsetsByFace =
            [
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset,
                terrainGeometrySubset
            ];
        }
    }
}
