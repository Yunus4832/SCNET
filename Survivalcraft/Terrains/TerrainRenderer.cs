using Engine.Graphics;

namespace Game.Terrains;

public class TerrainRenderer : IDisposable
{
    private static Shader _opaqueShader = null!;

    private static Shader _alphaTestedShader = null!;

    private static Shader _transparentShader = null!;

    private static readonly DynamicArray<int> _tmpIndices = [];

    public static bool DrawChunksMap;

    public static int ChunksDrawn;

    public static int ChunkDrawCalls;

    public static int ChunkTrianglesDrawn;

    private readonly DynamicArray<TerrainChunk> _chunksToDraw = [];

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

    private readonly SubsystemSky _subsystemSky;

    private readonly SubsystemTerrain _subsystemTerrain;

    public TerrainRenderer(SubsystemTerrain subsystemTerrain)
    {
        _subsystemTerrain = subsystemTerrain;
        _subsystemSky = subsystemTerrain.Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemAnimatedTextures = subsystemTerrain.SubsystemAnimatedTextures;
        _opaqueShader = new Shader(ShaderCodeManager.GetFast("Shaders/Opaque.vsh"),
            ShaderCodeManager.GetFast("Shaders/Opaque.psh"), new ShaderMacro("Opaque"));
        _alphaTestedShader = new Shader(ShaderCodeManager.GetFast("Shaders/AlphaTested.vsh"),
            ShaderCodeManager.GetFast("Shaders/AlphaTested.psh"), new ShaderMacro("ALPHATESTED"));
        _transparentShader = new Shader(ShaderCodeManager.GetFast("Shaders/Transparent.vsh"),
            ShaderCodeManager.GetFast("Shaders/Transparent.psh"), new ShaderMacro("Transparent"));
        Display.DeviceReset += DisplayDeviceReset;
    }

    public string ChunksGpuMemoryUsage
    {
        get
        {
            var num = 0L;
            var allocatedChunks = _subsystemTerrain.Terrain.AllocatedChunks;
            foreach (var terrainChunk in allocatedChunks)
            foreach (var buffer in terrainChunk.Buffers)
            {
                num += buffer.VertexBuffer.GetGpuMemoryUsage();
                num += buffer.IndexBuffer.GetGpuMemoryUsage();
            }

            return $"{num / 1024 / 1024:0.0}MB";
        }
    }

    public void Dispose()
    {
        Display.DeviceReset -= DisplayDeviceReset;
    }

    public void PrepareForDrawing(Camera camera)
    {
        var xZ = camera.ViewPosition.XZ;
        var num = MathUtils.Sqr(_subsystemSky.VisibilityRange);
        var viewFrustum = camera.ViewFrustum;
        var gameWidgetIndex = camera.GameWidget.GameWidgetIndex;
        _chunksToDraw.Clear();
        var allocatedChunks = _subsystemTerrain.Terrain.AllocatedChunks;
        foreach (var terrainChunk in allocatedChunks)
        {
            terrainChunk.HazeEnds.TryAdd(gameWidgetIndex, 0);
            if (terrainChunk.NewGeometryData)
            {
                lock (terrainChunk.Geometry)
                {
                    if (terrainChunk.NewGeometryData)
                    {
                        terrainChunk.NewGeometryData = false;
                        SetupTerrainChunkGeometryVertexIndexBuffers(terrainChunk);
                    }
                }
            }

            terrainChunk.DrawDistanceSquared = Vector2.DistanceSquared(xZ, terrainChunk.Center);
            if (terrainChunk.DrawDistanceSquared <= num)
            {
                if (viewFrustum.Intersection(terrainChunk.BoundingBox))
                {
                    _chunksToDraw.Add(terrainChunk);
                }

                if (terrainChunk.State != TerrainChunkState.Valid)
                {
                    continue;
                }

                var num2 = terrainChunk.HazeEnds[gameWidgetIndex];
                if (num2.CloseTo(3.40282347E+38f))
                {
                    continue;
                }

                if (num2 == 0f)
                {
                    StartChunkFadeIn(camera, terrainChunk);
                }
                else
                {
                    RunChunkFadeIn(camera, terrainChunk);
                }
            }
            else
            {
                terrainChunk.HazeEnds[gameWidgetIndex] = 0f;
            }
        }

        ChunksDrawn = 0;
        ChunkDrawCalls = 0;
        ChunkTrianglesDrawn = 0;
    }

    public void DrawOpaque(Camera camera)
    {
        var gameWidgetIndex = camera.GameWidget.GameWidgetIndex;
        var viewPosition = camera.ViewPosition;
        var v = new Vector3(MathUtils.Floor(viewPosition.X), 0f, MathUtils.Floor(viewPosition.Z));
        var value = Matrix.CreateTranslation(v - viewPosition) * camera.ViewMatrix.OrientationMatrix *
                    camera.ProjectionMatrix;
        Display.BlendState = BlendState.Opaque;
        Display.DepthStencilState = DepthStencilState.Default;
        Display.RasterizerState = RasterizerState.CullCounterClockwiseScissor;
        _opaqueShader.GetParameter("u_origin", true).SetValue(v.XZ);
        _opaqueShader.GetParameter("u_viewProjectionMatrix", true).SetValue(value);
        _opaqueShader.GetParameter("u_viewPosition", true).SetValue(viewPosition);
        _opaqueShader.GetParameter("u_samplerState", true)
            .SetValue(SettingsManager.Current.TerrainMipmapsEnabled ? _samplerStateMips : _samplerState);
        _opaqueShader.GetParameter("u_fogYMultiplier", true).SetValue(_subsystemSky.VisibilityRangeYMultiplier);
        _opaqueShader.GetParameter("u_fogColor", true).SetValue(new Vector3(_subsystemSky.ViewFogColor));
        _opaqueShader.GetParameter("u_fogBottomTopDensity").SetValue(new Vector3(_subsystemSky.ViewFogBottom,
            _subsystemSky.ViewFogTop, _subsystemSky.ViewFogDensity));
        var parameter = _opaqueShader.GetParameter("u_hazeStartDensity");
        var point = Terrain.ToChunk(camera.ViewPosition.XZ);
        _subsystemTerrain.Terrain.GetChunkAtCoords(point.X, point.Y);
        foreach (var terrainChunk in _chunksToDraw)
        {
            var num = MathUtils.Min(terrainChunk.HazeEnds[gameWidgetIndex],
                _subsystemSky.ViewHazeStart + 1f / _subsystemSky.ViewHazeDensity);
            var num2 = MathUtils.Min(_subsystemSky.ViewHazeStart, num - 1f);
            parameter.SetValue(new Vector2(num2, 1f / (num - num2)));
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
        var gameWidgetIndex = camera.GameWidget.GameWidgetIndex;
        var viewPosition = camera.ViewPosition;
        var v = new Vector3(MathUtils.Floor(viewPosition.X), 0f, MathUtils.Floor(viewPosition.Z));
        var value = Matrix.CreateTranslation(v - viewPosition) * camera.ViewMatrix.OrientationMatrix *
                    camera.ProjectionMatrix;
        Display.BlendState = BlendState.Opaque;
        Display.DepthStencilState = DepthStencilState.Default;
        Display.RasterizerState = RasterizerState.CullCounterClockwiseScissor;
        _alphaTestedShader.GetParameter("u_origin", true).SetValue(v.XZ);
        _alphaTestedShader.GetParameter("u_viewProjectionMatrix", true).SetValue(value);
        _alphaTestedShader.GetParameter("u_viewPosition", true).SetValue(viewPosition);
        _alphaTestedShader.GetParameter("u_samplerState", true)
            .SetValue(SettingsManager.Current.TerrainMipmapsEnabled ? _samplerStateMips : _samplerState);
        _alphaTestedShader.GetParameter("u_fogYMultiplier", true).SetValue(_subsystemSky.VisibilityRangeYMultiplier);
        _alphaTestedShader.GetParameter("u_fogColor", true).SetValue(new Vector3(_subsystemSky.ViewFogColor));
        _alphaTestedShader.GetParameter("u_fogBottomTopDensity").SetValue(new Vector3(_subsystemSky.ViewFogBottom,
            _subsystemSky.ViewFogTop, _subsystemSky.ViewFogDensity));
        _alphaTestedShader.GetParameter("u_alphaThreshold").SetValue(0.5f);
        var parameter = _alphaTestedShader.GetParameter("u_hazeStartDensity");
        foreach (var terrainChunk in _chunksToDraw)
        {
            var num = MathUtils.Min(terrainChunk.HazeEnds[gameWidgetIndex],
                _subsystemSky.ViewHazeStart + 1f / _subsystemSky.ViewHazeDensity);
            var num2 = MathUtils.Min(_subsystemSky.ViewHazeStart, num - 1f);
            parameter.SetValue(new Vector2(num2, 1f / (num - num2)));
            const int subsetsMask = 32;
            DrawTerrainChunkGeometrySubsets(_alphaTestedShader, terrainChunk, subsetsMask);
        }
    }

    public void DrawTransparent(Camera camera)
    {
        var gameWidgetIndex = camera.GameWidget.GameWidgetIndex;
        var viewPosition = camera.ViewPosition;
        var v = new Vector3(MathUtils.Floor(viewPosition.X), 0f, MathUtils.Floor(viewPosition.Z));
        var value = Matrix.CreateTranslation(v - viewPosition) * camera.ViewMatrix.OrientationMatrix *
                    camera.ProjectionMatrix;
        Display.BlendState = BlendState.AlphaBlend;
        Display.DepthStencilState = DepthStencilState.Default;
        Display.RasterizerState = _subsystemSky.ViewUnderWaterDepth > 0f
            ? RasterizerState.CullClockwiseScissor
            : RasterizerState.CullCounterClockwiseScissor;
        _transparentShader.GetParameter("u_origin", true).SetValue(v.XZ);
        _transparentShader.GetParameter("u_viewProjectionMatrix", true).SetValue(value);
        _transparentShader.GetParameter("u_viewPosition", true).SetValue(viewPosition);
        _transparentShader.GetParameter("u_samplerState", true)
            .SetValue(SettingsManager.Current.TerrainMipmapsEnabled ? _samplerStateMips : _samplerState);
        _transparentShader.GetParameter("u_fogYMultiplier", true).SetValue(_subsystemSky.VisibilityRangeYMultiplier);
        _transparentShader.GetParameter("u_fogColor", true).SetValue(new Vector3(_subsystemSky.ViewFogColor));
        _transparentShader.GetParameter("u_fogBottomTopDensity").SetValue(new Vector3(_subsystemSky.ViewFogBottom,
            _subsystemSky.ViewFogTop, _subsystemSky.ViewFogDensity));
        var parameter = _transparentShader.GetParameter("u_hazeStartDensity");
        for (var i = 0; i < _chunksToDraw.Count; i++)
        {
            var terrainChunk = _chunksToDraw[i];
            var num = MathUtils.Min(terrainChunk.HazeEnds[gameWidgetIndex],
                _subsystemSky.ViewHazeStart + 1f / _subsystemSky.ViewHazeDensity);
            var num2 = MathUtils.Min(_subsystemSky.ViewHazeStart, num - 1f);
            parameter.SetValue(new Vector2(num2, 1f / (num - num2)));
            var subsetsMask = 64;
            DrawTerrainChunkGeometrySubsets(_transparentShader, terrainChunk, subsetsMask);
        }
    }

    public void DisplayDeviceReset()
    {
        _subsystemTerrain.TerrainUpdater.DowngradeAllChunksState(TerrainChunkState.InvalidVertices1, false);
        var allocatedChunks = _subsystemTerrain.Terrain.AllocatedChunks;
        foreach (var terrainChunk in allocatedChunks)
        {
            DisposeTerrainChunkGeometryVertexIndexBuffers(terrainChunk);
        }
    }

    public void DisposeTerrainChunkGeometryVertexIndexBuffers(TerrainChunk chunk)
    {
        foreach (var buffer in chunk.Buffers)
        {
            buffer.Dispose();
        }

        chunk.Buffers.Clear();
        chunk.InvalidateSliceContentsHashes();
    }

    private void SetupTerrainChunkGeometryVertexIndexBuffers(TerrainChunk chunk)
    {
        DisposeTerrainChunkGeometryVertexIndexBuffers(chunk);
        CompileDrawSubsets(chunk.Draws, chunk.Buffers);
        chunk.CopySliceContentsHashes();
    }

    public static void CompileDrawSubsets(
        Dictionary<Texture2D, TerrainGeometry[]> list,
        DynamicArray<TerrainChunkGeometry.Buffer> buffers,
        Func<TerrainVertex, TerrainVertex>? vertexTransform = null
    )
    {
        foreach (var item in list)
        {
            var geometry = item.Value;
            var num = 0;
            while (num < 224)
            {
                var num2 = 0;
                var num3 = 0;
                int i;
                for (i = num; i < 224; i++)
                {
                    var num4 = i / 32;
                    var num5 = i % 32;
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
                        Texture = item.Key,
                        VertexBuffer = new VertexBuffer(TerrainVertex.VertexDeclaration, num2)
                    };
                    buffers.Add(buffer);
                    var num6 = 0;
                    var num7 = 0;
                    for (var j = num; j < i; j++)
                    {
                        var num8 = j / 32;
                        var num9 = j % 32;
                        var terrainGeometrySubset2 = geometry[num9].Subsets[num8];
                        if (num9 == 0 || j == num)
                        {
                            buffer.SubsetIndexBufferStarts[num8] = num7;
                        }

                        if (terrainGeometrySubset2.Indices.Count > 0)
                        {
                            _tmpIndices.Count = terrainGeometrySubset2.Indices.Count;
                            ShiftIndices(terrainGeometrySubset2.Indices.Array, _tmpIndices.Array, num6,
                                terrainGeometrySubset2.Indices.Count);
                            buffer.IndexBuffer.SetData(_tmpIndices.Array, 0, _tmpIndices.Count, num7);
                            num7 += _tmpIndices.Count;
                        }

                        if (terrainGeometrySubset2.Vertices.Count > 0)
                        {
                            buffer.VertexBuffer.SetData(terrainGeometrySubset2.Vertices.Array, 0,
                                terrainGeometrySubset2.Vertices.Count, num6);
                            num6 += terrainGeometrySubset2.Vertices.Count;
                        }

                        if (num9 == 31 || j == i - 1)
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
        foreach (var buffer in chunk.Buffers)
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

                        Display.DrawIndexed(PrimitiveType.TriangleList, shader, buffer.VertexBuffer, buffer.IndexBuffer,
                            num, num2 - num);
                        ChunkTrianglesDrawn += (num2 - num) / 3;
                        ChunkDrawCalls++;
                    }

                    num = 2147483647;
                }
            }
        }
    }

    private void StartChunkFadeIn(Camera camera, TerrainChunk chunk)
    {
        var viewPosition = camera.ViewPosition;
        var v = new Vector2(chunk.Origin.X, chunk.Origin.Y);
        var v2 = new Vector2(chunk.Origin.X + 16, chunk.Origin.Y);
        var v3 = new Vector2(chunk.Origin.X, chunk.Origin.Y + 16);
        var v4 = new Vector2(chunk.Origin.X + 16, chunk.Origin.Y + 16);
        var x = Vector2.Distance(viewPosition.XZ, v);
        var x2 = Vector2.Distance(viewPosition.XZ, v2);
        var x3 = Vector2.Distance(viewPosition.XZ, v3);
        var x4 = Vector2.Distance(viewPosition.XZ, v4);
        chunk.HazeEnds[camera.GameWidget.GameWidgetIndex] = MathUtils.Max(MathUtils.Min(x, x2, x3, x4), 0.001f);
    }

    private void RunChunkFadeIn(Camera camera, TerrainChunk chunk)
    {
        chunk.HazeEnds[camera.GameWidget.GameWidgetIndex] += 32f * Time.FrameDuration;
        if (chunk.HazeEnds[camera.GameWidget.GameWidgetIndex] >= _subsystemSky.VisibilityRange)
        {
            chunk.HazeEnds[camera.GameWidget.GameWidgetIndex] = 3.40282347E+38f;
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
