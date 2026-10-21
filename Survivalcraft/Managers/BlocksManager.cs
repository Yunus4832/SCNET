using Engine.Graphics;

namespace Game.Managers;

public static class BlocksManager
{
    private static readonly DrawBlockEnvironmentData _defaultEnvironmentData = new();

    public static void DrawMeshBlock(
        PrimitivesRenderer3D primitivesRenderer,
        BlockMesh blockMesh,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData? environmentData
    )
    {
        environmentData ??= _defaultEnvironmentData;
        var texture = environmentData.SubsystemTerrain != null
            ? environmentData.SubsystemTerrain.SubsystemAnimatedTextures.AnimatedBlocksTexture
            : BlocksTexturesManager.DefaultBlocksTexture;
        DrawMeshBlock(primitivesRenderer, blockMesh, texture, Color.White, size, ref matrix, environmentData);
    }

    public static void DrawMeshBlock(
        PrimitivesRenderer3D primitivesRenderer,
        BlockMesh? blockMesh,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData? environmentData
    )
    {
        environmentData ??= _defaultEnvironmentData;
        var texture = environmentData.SubsystemTerrain != null
            ? environmentData.SubsystemTerrain.SubsystemAnimatedTextures.AnimatedBlocksTexture
            : BlocksTexturesManager.DefaultBlocksTexture;
        DrawMeshBlock(primitivesRenderer, blockMesh, texture, color, size, ref matrix, environmentData);
    }

    public static void DrawMeshBlock(
        PrimitivesRenderer3D primitivesRenderer,
        BlockMesh? blockMesh,
        Texture2D texture,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData? environmentData
    )
    {
        environmentData ??= _defaultEnvironmentData;
        var num = LightingManager.LightIntensityByLightValue[environmentData.Light];
        var v = new Vector4(color);
        v.X *= num;
        v.Y *= num;
        v.Z *= num;
        var flag = v == Vector4.One;
        var texturedBatch3D = primitivesRenderer.TexturedBatch(
            texture,
            true,
            0,
            null,
            RasterizerState.CullCounterClockwiseScissor,
            null,
            SamplerState.PointClamp
        );
        var flag2 = false;
        var m = !environmentData.ViewProjectionMatrix.HasValue
            ? matrix
            : matrix * environmentData.ViewProjectionMatrix.Value;
        if (size.UncloseTo(1f))
        {
            m = Matrix.CreateScale(size) * m;
        }

        if (m.M14 != 0f || m.M24 != 0f || m.M34 != 0f || m.M44.UncloseTo(1f))
        {
            flag2 = true;
        }

        if (blockMesh is null)
        {
            return;
        }

        var count = blockMesh.Vertices.Count;
        var array = blockMesh.Vertices.Array;
        var count2 = blockMesh.Indices.Count;
        var array2 = blockMesh.Indices.Array;
        var triangleVertices = texturedBatch3D.TriangleVertices;
        var count3 = triangleVertices.Count;
        var count4 = triangleVertices.Count;
        triangleVertices.Count += count;
        for (var i = 0; i < count; i++)
        {
            var blockMeshVertex = array[i];
            if (flag2)
            {
                var v2 = new Vector4(blockMeshVertex.Position, 1f);
                Vector4.Transform(ref v2, ref m, out v2);
                var num2 = 1f / v2.W;
                blockMeshVertex.Position = new Vector3(v2.X * num2, v2.Y * num2, v2.Z * num2);
            }
            else
            {
                Vector3.Transform(ref blockMeshVertex.Position, ref m, out blockMeshVertex.Position);
            }

            if (flag || blockMeshVertex.IsEmissive)
            {
                triangleVertices.Array[count4++] = new VertexPositionColorTexture(
                    blockMeshVertex.Position,
                    blockMeshVertex.Color,
                    blockMeshVertex.TextureCoordinates
                );
                continue;
            }

            var color2 = new Color(
                (byte)(blockMeshVertex.Color.R * v.X),
                (byte)(blockMeshVertex.Color.G * v.Y),
                (byte)(blockMeshVertex.Color.B * v.Z),
                (byte)(blockMeshVertex.Color.A * v.W)
            );
            triangleVertices.Array[count4++] = new VertexPositionColorTexture(
                blockMeshVertex.Position,
                color2,
                blockMeshVertex.TextureCoordinates
            );
        }

        var triangleIndices = texturedBatch3D.TriangleIndices;
        var count5 = triangleIndices.Count;
        triangleIndices.Count += count2;
        for (var j = 0; j < count2; j++)
        {
            triangleIndices.Array[count5++] = (ushort)(count3 + array2[j]);
        }
    }

    internal static Vector4 TextureSlotToTextureCoords(int slot)
    {
        var num = slot % 16;
        var num2 = slot / 16;
        var x = (num + 0.001f) / 16f;
        var y = (num2 + 0.001f) / 16f;
        var z = (num + 1 - 0.001f) / 16f;
        var w = (num2 + 1 - 0.001f) / 16f;
        return new Vector4(x, y, z, w);
    }

    public static Vector4[] GetslotTexCoords(int textureSlotCount)
    {
        var totalCount = textureSlotCount * textureSlotCount;
        var slotTexCoords = new Vector4[totalCount];
        for (var i = 0; i < totalCount; i++)
        {
            var num = i % textureSlotCount;
            var num2 = i / textureSlotCount;
            var x = (num + 0.001f) / textureSlotCount;
            var y = (num2 + 0.001f) / textureSlotCount;
            var z = (num + 1 - 0.001f) / textureSlotCount;
            var w = (num2 + 1 - 0.001f) / textureSlotCount;
            slotTexCoords[i] = new Vector4(x, y, z, w);
        }

        return slotTexCoords;
    }
}
