using Engine.Graphics;

using Game.Network.Chunks;
using Game.Terrains.Distribution;
using Game.Terrains.Rendering;

namespace Game.Terrains;

public class TerrainRenderer : IDisposable
{
    private Shader _opaqueShader;

    private Shader _alphaTestedShader;

    private Shader _transparentShader;

    private Shader _waterDepthShader;

    private readonly bool _depthFramebufferFetch;

    private readonly IComparer<TerrainChunk> _waterComparer;

    private Vector2 _waterSortCenter;

    private static readonly DynamicArray<int> _tmpIndices = [];

    public static bool DrawChunksMap;

    public static int ChunksDrawn;

    public static int ChunkDrawCalls;

    public static int ChunkTrianglesDrawn;

    private readonly DynamicArray<TerrainChunk> _chunksToDraw = [];

    private readonly Dictionary<int, DepthBufferSnapshot> _depthSnapshots = [];

    private readonly HashSet<Point2> _drawableChunks = [];

    private readonly Dictionary<Camera, TerrainVisibilityBoundary> _visibilityBoundaries = [];

    private readonly Func<Point2, bool> _isDrawable;

    private Vector2 _visibilityStartEnd;

    private readonly TerrainUploadScheduler _uploads = new();

    private readonly List<TerrainUploadScheduler.Candidate> _uploadCandidates = [];

    private readonly Dictionary<int, (Vector2 Center, float Visibility, long Frame)> _uploadViews = [];

    private readonly SamplerState _samplerState = new()
    {
        AddressModeU = TextureAddressMode.Clamp,
        AddressModeV = TextureAddressMode.Clamp,
        FilterMode = TextureFilterMode.Point,
        MaxLod = 0f
    };

    private readonly SamplerState _samplerStateMips = new()
    {
        AddressModeU = TextureAddressMode.Clamp,
        AddressModeV = TextureAddressMode.Clamp,
        FilterMode = TextureFilterMode.PointMipLinear,
        MaxLod = 4f
    };

    private readonly SubsystemAnimatedTextures _subsystemAnimatedTextures;

    private readonly SubsystemSkyPresentation _subsystemSky;

    private readonly SubsystemTerrain _subsystemTerrain;

    private readonly SubsystemTerrainPresentation _presentation;

    public TerrainRenderer(SubsystemTerrainPresentation presentation)
    {
        _presentation = presentation;
        _isDrawable = _drawableChunks.Contains;
        _depthFramebufferFetch = Display.SupportsDepthFramebufferFetch;
        _waterComparer = Comparer<TerrainChunk>.Create(CompareWaterChunks);
        _subsystemTerrain = presentation.TerrainSubsystem;
        _subsystemSky = _subsystemTerrain.Project.FindSubsystem<SubsystemSkyPresentation>(true)!;
        _subsystemAnimatedTextures = presentation.SubsystemAnimatedTextures;
        try
        {
            _opaqueShader = new Shader(
                ShaderCodeManager.GetFast("Shaders/Opaque.vsh"),
                ShaderCodeManager.GetFast("Shaders/Opaque.psh"),
                new ShaderMacro("TERRAIN_LIGHT_RESPONSE")
            );
            _alphaTestedShader = new Shader(
                ShaderCodeManager.GetFast("Shaders/AlphaTested.vsh"),
                ShaderCodeManager.GetFast("Shaders/AlphaTested.psh"),
                new ShaderMacro("ALPHATESTED"),
                new ShaderMacro("OPAQUE_CUTOUT"),
                new ShaderMacro("TERRAIN_LIGHT_RESPONSE")
            );
            var waterMacros = new List<ShaderMacro> { new("TERRAIN_LIGHT_RESPONSE") };
            if (_depthFramebufferFetch) { waterMacros.Add(new ShaderMacro("FRAMEBUFFER_DEPTH_FETCH")); }

            _transparentShader = new Shader(
                ShaderCodeManager.GetFast("Shaders/Transparent.vsh"),
                ShaderCodeManager.GetFast("Shaders/Transparent.psh"),
                waterMacros.ToArray()
            );
            _waterDepthShader = new Shader(
                ShaderCodeManager.GetFast("Shaders/Transparent.vsh"),
                ShaderCodeManager.GetFast("Shaders/DepthOnly.psh")
            );
            Display.DeviceReset += DisplayDeviceReset;
        }
        catch (Exception constructionError)
        {
            try { Dispose(); }
            catch (Exception cleanupError) { throw new AggregateException(constructionError, cleanupError); }

            throw;
        }
    }

    public string ChunksGpuMemoryUsage
    {
        get
        {
            var num = 0L;
            var allocatedChunks = _subsystemTerrain.Terrain.AllocatedChunks;
            foreach (var terrainChunk in allocatedChunks)
            {
                foreach (var buffer in _presentation.ChunkPresentations.Get(terrainChunk).Buffers)
                {
                    num += buffer.VertexBuffer.GetGpuMemoryUsage();
                    num += buffer.IndexBuffer.GetGpuMemoryUsage();
                }
            }

            return $"{num / 1024 / 1024:0.0}MB";
        }
    }

    public void Dispose()
    {
        Display.DeviceReset -= DisplayDeviceReset;
        _uploads.Clear();
        _uploadCandidates.Clear();
        _drawableChunks.Clear();
        _visibilityBoundaries.Clear();
        foreach (var depth in _depthSnapshots.Values) { depth.Dispose(); }

        _depthSnapshots.Clear();
        _uploadViews.Clear();
        var errors = new List<Exception>();
        foreach (var shader in new[] { _opaqueShader, _alphaTestedShader, _transparentShader, _waterDepthShader })
        {
            try { shader?.Dispose(); }
            catch (Exception error) { errors.Add(error); }
        }

        _opaqueShader = null!;
        _alphaTestedShader = null!;
        _transparentShader = null!;
        _waterDepthShader = null!;
        if (errors.Count > 0) { throw new AggregateException("Could not dispose terrain shaders.", errors); }
    }

    private void UploadReadyGeometry(Camera camera, TerrainChunk[] chunks)
    {
        var center = camera.ViewPosition.XZ;
        var visibilityRange = _subsystemSky.VisibilityRange;
        var locations = _subsystemTerrain.Demands.Locations.Values.ToArray();
        var frame = Time.FrameIndex;
        foreach (var stale in _uploadViews.Where(pair => pair.Value.Frame < frame - 1).Select(pair => pair.Key)
                     .ToArray())
        {
            _uploadViews.Remove(stale);
        }

        _uploadViews[camera.GameWidget.GameWidgetIndex] = (center, _subsystemSky.VisibilityRange, frame);
        _uploadCandidates.Clear();
        foreach (var chunk in chunks)
        {
            var presentation = _presentation.ChunkPresentations.Get(chunk);
            if (!presentation.CanUploadGeometry(chunk))
            {
                continue;
            }

            var nearest = Vector2.DistanceSquared(center, chunk.Center);
            var visible = NetworkTerrainPolicy.IsTerrainChunkRelevant(chunk.Center, center, visibilityRange);
            foreach (var location in locations)
            {
                var distance = Vector2.DistanceSquared(location.Center, chunk.Center);
                nearest = Math.Min(nearest, distance);
                visible |= NetworkTerrainPolicy.IsTerrainChunkRelevant(
                    chunk.Center,
                    location.Center,
                    location.VisibilityDistance
                );
            }

            foreach (var view in _uploadViews.Values)
            {
                var distance = Vector2.DistanceSquared(view.Center, chunk.Center);
                nearest = Math.Min(nearest, distance);
                visible |= NetworkTerrainPolicy.IsTerrainChunkRelevant(chunk.Center, view.Center, view.Visibility);
            }

            if (visible)
            {
                _uploadCandidates.Add(
                    new TerrainUploadScheduler.Candidate(chunk, !presentation.GeometryUploaded, nearest)
                );
            }
        }

        _uploads.Upload(Time.FrameIndex, _uploadCandidates, TryUploadGeometry);
    }

    private bool TryUploadGeometry(TerrainChunk chunk)
    {
        if (chunk.IsDisposed) { return false; }

        var presentation = _presentation.ChunkPresentations.Get(chunk);
        if (!Monitor.TryEnter(presentation.Geometry)) { return false; }

        try
        {
            if (!presentation.CanUploadGeometry(chunk))
            {
                return false;
            }

            SetupTerrainChunkGeometryVertexIndexBuffers(chunk);
            presentation.MarkUploaded();
            presentation.NewGeometryData = false;
            return true;
        }
        finally { Monitor.Exit(presentation.Geometry); }
    }

    public void PrepareForDrawing(Camera camera)
    {
        var xZ = camera.ViewPosition.XZ;
        var num = MathUtils.Sqr(_subsystemSky.VisibilityRange);
        var viewFrustum = camera.ViewFrustum;
        _chunksToDraw.Clear();
        _drawableChunks.Clear();
        var allocatedChunks = _subsystemTerrain.Terrain.AllocatedChunks;
        UploadReadyGeometry(camera, allocatedChunks);
        foreach (var chunk in allocatedChunks)
        {
            if (!_presentation.ChunkPresentations.TryGet(chunk, out var presentation) ||
                !ClientChunkDerivationPipeline.CanDraw(_subsystemTerrain.ContentRole, chunk, presentation))
            {
                continue;
            }

            _drawableChunks.Add(chunk.Coords);
            presentation.DrawDistanceSquared = Vector2.DistanceSquared(xZ, chunk.Center);
            if (NetworkTerrainPolicy.GetChunkDistanceSquared(chunk.Center, xZ) <= num &&
                viewFrustum.Intersection(chunk.BoundingBox))
            {
                _chunksToDraw.Add(chunk);
            }
        }

        if (!_visibilityBoundaries.TryGetValue(camera, out var boundary))
        {
            boundary = new TerrainVisibilityBoundary();
            _visibilityBoundaries.Add(camera, boundary);
        }

        boundary.Update(xZ, _subsystemSky.VisibilityRange, Time.FrameDuration, _isDrawable);
        _visibilityStartEnd = new Vector2(boundary.DisplayDistance * 0.8f, boundary.DisplayDistance);
        _subsystemSky.ViewVisibilityStartEnd = _visibilityStartEnd;

        ChunksDrawn = 0;
        ChunkDrawCalls = 0;
        ChunkTrianglesDrawn = 0;
    }

    public void DrawOpaque(Camera camera)
    {
        var viewPosition = camera.ViewPosition;
        var v = new Vector3(MathUtils.Floor(viewPosition.X), 0f, MathUtils.Floor(viewPosition.Z));
        var value = Matrix.CreateTranslation(v - viewPosition) *
                    camera.ViewMatrix.OrientationMatrix *
                    camera.ProjectionMatrix;
        Display.BlendState = BlendState.Opaque;
        Display.DepthStencilState = DepthStencilState.Default;
        Display.RasterizerState = RasterizerState.CullCounterClockwiseScissor;
        _opaqueShader.GetParameter("u_origin", true).SetValue(v.XZ);
        _opaqueShader.GetParameter("u_viewProjectionMatrix", true).SetValue(value);
        _opaqueShader.GetParameter("u_viewPosition", true).SetValue(viewPosition);
        _opaqueShader.GetParameter("u_samplerState", true)
            .SetValue(
                Configuration.ClientConfiguration.Preferences.TerrainMipmapsEnabled
                    ? _samplerStateMips
                    : _samplerState
            );
        LightingManager.BindTerrainEnvironment(_opaqueShader, _subsystemTerrain);
        _subsystemSky.BindAtmosphere(_opaqueShader, camera);
        foreach (var terrainChunk in _chunksToDraw)
        {
            var num3 = 16;
            if (viewPosition.Z > terrainChunk.BoundingBox.Min.Z)
            {
                num3 |= 1;
            }

            if (viewPosition.X > terrainChunk.BoundingBox.Min.X)
            {
                num3 |= 2;
            }

            if (viewPosition.Z < terrainChunk.BoundingBox.Max.Z)
            {
                num3 |= 4;
            }

            if (viewPosition.X < terrainChunk.BoundingBox.Max.X)
            {
                num3 |= 8;
            }

            DrawTerrainChunkGeometrySubsets(_opaqueShader, terrainChunk, num3);
            ChunksDrawn++;
        }
    }

    public void DrawAlphaTested(Camera camera)
    {
        var viewPosition = camera.ViewPosition;
        var v = new Vector3(MathUtils.Floor(viewPosition.X), 0f, MathUtils.Floor(viewPosition.Z));
        var value = Matrix.CreateTranslation(v - viewPosition) *
                    camera.ViewMatrix.OrientationMatrix *
                    camera.ProjectionMatrix;
        Display.BlendState = BlendState.Opaque;
        Display.DepthStencilState = DepthStencilState.Default;
        Display.RasterizerState = RasterizerState.CullCounterClockwiseScissor;
        _alphaTestedShader.GetParameter("u_origin", true).SetValue(v.XZ);
        _alphaTestedShader.GetParameter("u_viewProjectionMatrix", true).SetValue(value);
        _alphaTestedShader.GetParameter("u_viewPosition", true).SetValue(viewPosition);
        _alphaTestedShader.GetParameter("u_samplerState", true)
            .SetValue(
                Configuration.ClientConfiguration.Preferences.TerrainMipmapsEnabled
                    ? _samplerStateMips
                    : _samplerState
            );
        LightingManager.BindTerrainEnvironment(_alphaTestedShader, _subsystemTerrain);
        _subsystemSky.BindAtmosphere(_alphaTestedShader, camera);
        _alphaTestedShader.GetParameter("u_alphaThreshold").SetValue(0.5f);
        foreach (var terrainChunk in _chunksToDraw)
        {
            const int subsetsMask = 32;
            DrawTerrainChunkGeometrySubsets(_alphaTestedShader, terrainChunk, subsetsMask);
        }
    }

    public void DrawTransparent(Camera camera)
    {
        if (!HasWaterGeometry()) { return; }

        if (!_depthFramebufferFetch)
        {
            var viewport = Display.Viewport;
            var view = camera.GameWidget.GameWidgetIndex;
            var format = Display.RenderTarget?.DepthFormat ?? DepthFormat.Depth24Stencil8;
            if (!_depthSnapshots.TryGetValue(view, out var depth) ||
                depth.Texture.Width != viewport.Width ||
                depth.Texture.Height != viewport.Height ||
                depth.Format != format)
            {
                _depthSnapshots.Remove(view);
                depth?.Dispose();
                depth = new DepthBufferSnapshot(viewport.Width, viewport.Height, format);
                _depthSnapshots[view] = depth;
            }

            depth.Capture();
            _transparentShader.GetParameter("u_sceneDepth").SetValue(depth.Texture);
            _transparentShader.GetParameter("u_sceneDepthSampler").SetValue(SamplerState.PointClamp);
            _transparentShader.GetParameter("u_depthUvScale").SetValue(
                new Vector2(0.5f, Display.RenderTarget == null ? 0.5f : -0.5f)
            );
        }

        var projection = camera.ProjectionMatrix;
        _transparentShader.GetParameter("u_depthProjection").SetValue(
            new Vector4(projection.M33, projection.M43, projection.M34, projection.M44)
        );
        _transparentShader.GetParameter("u_viewDirection").SetValue(camera.ViewDirection);
        _transparentShader.GetParameter("u_depthScaleBias").SetValue(
            Display.UseReducedZRange ? new Vector2(2f, -1f) : new Vector2(1f, 0f)
        );
        _transparentShader.GetParameter("u_underWater").SetValue(_subsystemSky.ViewUnderWaterDepth > 0f ? 1f : 0f);

        var viewPosition = camera.ViewPosition;
        var v = new Vector3(MathUtils.Floor(viewPosition.X), 0f, MathUtils.Floor(viewPosition.Z));
        var value = Matrix.CreateTranslation(v - viewPosition) *
                    camera.ViewMatrix.OrientationMatrix *
                    camera.ProjectionMatrix;
        Display.BlendState = BlendState.AlphaBlend;
        Display.DepthStencilState = DepthStencilState.DepthRead;
        Display.RasterizerState = _subsystemSky.ViewUnderWaterDepth > 0f
            ? RasterizerState.CullClockwiseScissor
            : RasterizerState.CullCounterClockwiseScissor;
        _transparentShader.GetParameter("u_origin", true).SetValue(v.XZ);
        _transparentShader.GetParameter("u_viewProjectionMatrix", true).SetValue(value);
        _transparentShader.GetParameter("u_viewPosition", true).SetValue(viewPosition);
        _transparentShader.GetParameter("u_samplerState", true)
            .SetValue(
                Configuration.ClientConfiguration.Preferences.TerrainMipmapsEnabled
                    ? _samplerStateMips
                    : _samplerState
            );
        LightingManager.BindTerrainEnvironment(_transparentShader, _subsystemTerrain);
        _subsystemSky.BindAtmosphere(_transparentShader, camera);
        _waterSortCenter = camera.ViewPosition.XZ;
        Array.Sort(_chunksToDraw.Array, 0, _chunksToDraw.Count, _waterComparer);
        foreach (var terrainChunk in _chunksToDraw)
        {
            const int subsetsMask = 64;
            DrawTerrainChunkGeometrySubsets(_transparentShader, terrainChunk, subsetsMask);
        }

        Display.DepthStencilState = DepthStencilState.Default;
        Display.BlendState = BlendState.DepthOnly;
        _waterDepthShader.GetParameter("u_origin").SetValue(v.XZ);
        _waterDepthShader.GetParameter("u_viewProjectionMatrix").SetValue(value);
        foreach (var chunk in _chunksToDraw)
        {
            DrawTerrainChunkGeometrySubsets(_waterDepthShader, chunk, 64, false);
        }

        Display.BlendState = BlendState.AlphaBlend;
    }

    private int CompareWaterChunks(TerrainChunk first, TerrainChunk second)
    {
        return Vector2.DistanceSquared(_waterSortCenter, second.Center)
            .CompareTo(Vector2.DistanceSquared(_waterSortCenter, first.Center));
    }

    private bool HasWaterGeometry()
    {
        foreach (var chunk in _chunksToDraw)
        {
            foreach (var buffer in _presentation.ChunkPresentations.Get(chunk).Buffers)
            {
                if (buffer.SubsetIndexBufferEnds[6] > buffer.SubsetIndexBufferStarts[6]) { return true; }
            }
        }

        return false;
    }

    public void DisplayDeviceReset()
    {
        _subsystemTerrain.WorkExecutor.EnterMain();
        try
        {
            _presentation.MeshUpdater.RequestAllMeshesRebuild(false);
            var allocatedChunks = _subsystemTerrain.Terrain.AllocatedChunks;
            foreach (var terrainChunk in allocatedChunks)
            {
                _presentation.ChunkPresentations.Get(terrainChunk).GeometryUploaded = false;
                _presentation.ChunkPresentations.Get(terrainChunk).NewGeometryData = false;
                if (_subsystemTerrain.ContentRole == TerrainContentRole.Replica)
                {
                    _presentation.ChunkPresentations.Get(terrainChunk).RequestMeshRebuild();
                }

                DisposeTerrainChunkGeometryVertexIndexBuffers(terrainChunk);
            }
        }
        finally
        {
            _subsystemTerrain.WorkExecutor.LeaveMain();
        }
    }

    public void DisposeTerrainChunkGeometryVertexIndexBuffers(TerrainChunk chunk)
    {
        foreach (var buffer in _presentation.ChunkPresentations.Get(chunk).Buffers)
        {
            buffer.Dispose();
        }

        _presentation.ChunkPresentations.Get(chunk).Buffers.Clear();
    }

    private void SetupTerrainChunkGeometryVertexIndexBuffers(TerrainChunk chunk)
    {
        DisposeTerrainChunkGeometryVertexIndexBuffers(chunk);
        CompileDrawSubsets(
            _presentation.ChunkPresentations.Get(chunk).Draws,
            _presentation.ChunkPresentations.Get(chunk).Buffers
        );
    }

    public static void CompileDrawSubsets(
        Dictionary<Texture2D, TerrainGeometry[]> list,
        DynamicArray<TerrainChunkGeometry.Buffer> buffers,
        Func<TerrainVertex, TerrainVertex>? vertexTransform = null
    )
    {
        foreach (var (key, geometry) in list)
        {
            var num = 0;
            var geometryItemsCount = 7 * TerrainChunk.SlicesCount;
            while (num < geometryItemsCount)
            {
                var num2 = 0;
                var num3 = 0;
                int i;
                for (i = num; i < geometryItemsCount; i++)
                {
                    var num4 = i / TerrainChunk.SlicesCount;
                    var num5 = i % TerrainChunk.SlicesCount;
                    var terrainGeometrySubset = geometry[num5].Subsets[num4];
                    if (vertexTransform != null)
                    {
                        var tmpList = new DynamicArray<TerrainVertex>();
                        foreach (var terrainVertex in terrainGeometrySubset.Vertices)
                        {
                            var vertex = vertexTransform(terrainVertex);
                            tmpList.Add(vertex);
                        }

                        terrainGeometrySubset.Vertices = tmpList;
                    }

                    if (num2 + terrainGeometrySubset.Vertices.Count > 65535 && i > num)
                    {
                        break;
                    }

                    num2 += terrainGeometrySubset.Vertices.Count;
                    num3 += terrainGeometrySubset.Indices.Count;
                }

                if (num2 > 0 && num3 > 0)
                {
                    var buffer = new TerrainChunkGeometry.Buffer
                    {
                        IndexBuffer = new IndexBuffer(IndexFormat.ThirtyTwoBits, num3),
                        Texture = key,
                        VertexBuffer = new VertexBuffer(TerrainVertex.VertexDeclaration, num2)
                    };
                    buffers.Add(buffer);
                    var num6 = 0;
                    var num7 = 0;
                    for (var j = num; j < i; j++)
                    {
                        var num8 = j / TerrainChunk.SlicesCount;
                        var num9 = j % TerrainChunk.SlicesCount;
                        var terrainGeometrySubset2 = geometry[num9].Subsets[num8];
                        if (num9 == 0 || j == num)
                        {
                            buffer.SubsetIndexBufferStarts[num8] = num7;
                        }

                        if (terrainGeometrySubset2.Indices.Count > 0)
                        {
                            _tmpIndices.Count = terrainGeometrySubset2.Indices.Count;
                            ShiftIndices(
                                terrainGeometrySubset2.Indices.Array,
                                _tmpIndices.Array,
                                num6,
                                terrainGeometrySubset2.Indices.Count
                            );
                            buffer.IndexBuffer.SetData(_tmpIndices.Array, 0, _tmpIndices.Count, num7);
                            num7 += _tmpIndices.Count;
                        }

                        if (terrainGeometrySubset2.Vertices.Count > 0)
                        {
                            buffer.VertexBuffer.SetData(
                                terrainGeometrySubset2.Vertices.Array,
                                0,
                                terrainGeometrySubset2.Vertices.Count,
                                num6
                            );
                            num6 += terrainGeometrySubset2.Vertices.Count;
                        }

                        if (num9 == TerrainChunk.SlicesCount - 1 || j == i - 1)
                        {
                            buffer.SubsetIndexBufferEnds[num8] = num7;
                        }
                    }
                }

                num = i;
            }
        }
    }

    private void DrawTerrainChunkGeometrySubsets(
        Shader shader,
        TerrainChunk chunk,
        int subsetsMask,
        bool applyTexture = true
    )
    {
        foreach (var buffer in _presentation.ChunkPresentations.Get(chunk).Buffers)
        {
            var num = 2147483647;
            var num2 = 0;
            for (var i = 0; i < 8; i++)
            {
                if (i < 7 && (subsetsMask & (1 << i)) != 0)
                {
                    if (buffer.SubsetIndexBufferEnds[i] <= 0)
                    {
                        continue;
                    }

                    if (num == 2147483647)
                    {
                        num = buffer.SubsetIndexBufferStarts[i];
                    }

                    num2 = buffer.SubsetIndexBufferEnds[i];
                }
                else
                {
                    if (num2 > num)
                    {
                        if (applyTexture)
                        {
                            shader.GetParameter("u_texture", true).SetValue(buffer.Texture);
                        }

                        Display.DrawIndexed(
                            PrimitiveType.TriangleList,
                            shader,
                            buffer.VertexBuffer,
                            buffer.IndexBuffer,
                            num,
                            num2 - num
                        );
                        ChunkTrianglesDrawn += (num2 - num) / 3;
                        ChunkDrawCalls++;
                    }

                    num = 2147483647;
                }
            }
        }
    }

    private static void ShiftIndices(int[] source, int[] destination, int shift, int count)
    {
        for (var i = 0; i < count; i++)
        {
            destination[i] = source[i] + shift;
        }
    }
}
