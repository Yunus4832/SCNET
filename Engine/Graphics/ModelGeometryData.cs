using System.Buffers.Binary;

using Engine.Core.Mathematics;
using Engine.Media.Models;

namespace Engine.Graphics;

/// <summary>CPU preparation for the rigid model renderer; asset accessors remain graphics-independent.</summary>
public sealed class ModelGeometryData
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, "POSITION"),
        new VertexElement(12, VertexElementFormat.Vector3, "NORMAL"),
        new VertexElement(24, VertexElementFormat.Vector2, "TEXCOORD")
    );

    public required byte[] Vertices { get; init; }

    public required uint[] Indices { get; init; }

    public required BoundingBox BoundingBox { get; init; }

    public static ModelGeometryData Create(ModelData model, ModelMeshPartData primitive, IReadOnlyList<float> weights)
    {
        if (!primitive.Attributes.TryGetValue("POSITION", out var positionIndex))
        {
            throw new NotSupportedException("Rigid model rendering requires a POSITION attribute.");
        }

        var positions = model.Accessors[positionIndex];
        var normals = primitive.Attributes.TryGetValue("NORMAL", out var normalIndex)
            ? model.Accessors[normalIndex]
            : null;
        var uv = primitive.Attributes.TryGetValue("TEXCOORD_0", out var uvIndex) ? model.Accessors[uvIndex] : null;
        var sourceIndices = primitive.IndicesAccessorIndex < 0
            ? Enumerable.Range(0, positions.Count).Select(i => (uint)i).ToArray()
            : Enumerable.Range(0, model.Accessors[primitive.IndicesAccessorIndex].Count)
                .Select(i => checked((uint)model.Accessors[primitive.IndicesAccessorIndex].GetComponent(i, 0)))
                .ToArray();
        var indices = Triangles(sourceIndices, primitive.Topology);
        var actualPositions = Enumerable.Range(0, positions.Count).Select(positions.GetVector3).ToArray();
        var actualNormals = normals == null
            ? null
            : Enumerable.Range(0, normals.Count).Select(normals.GetVector3).ToArray();
        if (weights.Count > primitive.MorphTargets.Count)
        {
            throw new InvalidDataException("Morph weight count exceeds target count.");
        }

        for (var target = 0; target < weights.Count; target++)
        {
            var weight = weights[target];
            if (weight == 0)
            {
                continue;
            }

            var attributes = primitive.MorphTargets[target];
            for (var i = 0; i < actualPositions.Length; i++)
            {
                if (attributes.TryGetValue("POSITION", out var deltaPosition))
                {
                    actualPositions[i] += model.Accessors[deltaPosition].GetVector3(i) * weight;
                }

                if (actualNormals != null && attributes.TryGetValue("NORMAL", out var deltaNormal))
                {
                    actualNormals[i] += model.Accessors[deltaNormal].GetVector3(i) * weight;
                }
            }
        }

        var vertexCount = normals == null ? indices.Length : positions.Count;
        var bytes = new byte[checked(vertexCount * 32)];

        void WriteVertex(int target, int source, Vector3 normal)
        {
            var p = actualPositions[source];
            var n = normal.LengthSquared() > 0 ? Vector3.Normalize(normal) : Vector3.UnitY;
            ReadOnlySpan<float> values =
            [
                p.X, p.Y, p.Z, n.X, n.Y, n.Z,
                uv == null ? 0 : (float)uv.GetComponent(source, 0), uv == null ? 0 : (float)uv.GetComponent(source, 1)
            ];
            for (var i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(target * 32 + i * 4), values[i]);
            }
        }

        if (actualNormals != null)
        {
            for (var i = 0; i < vertexCount; i++)
            {
                WriteVertex(i, i, actualNormals[i]);
            }
        }

        for (var i = 0; i < indices.Length; i += 3)
        {
            if (actualNormals == null)
            {
                var a = (int)indices[i];
                var b = (int)indices[i + 1];
                var c = (int)indices[i + 2];
                var normal = Vector3.Cross(
                    actualPositions[b] - actualPositions[a],
                    actualPositions[c] - actualPositions[a]
                );
                for (var j = 0; j < 3; j++)
                {
                    WriteVertex(i + j, (int)indices[i + j], normal);
                    indices[i + j] = (uint)(i + j);
                }
            }

            // glTF uses counterclockwise front faces; the renderer uses clockwise indices.
            (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
        }

        return new ModelGeometryData
            { Vertices = bytes, Indices = indices, BoundingBox = new BoundingBox(actualPositions) };
    }

    private static uint[] Triangles(uint[] indices, ModelPrimitiveTopology topology)
    {
        if (topology == ModelPrimitiveTopology.Triangles)
        {
            if (indices.Length % 3 != 0)
            {
                throw new InvalidDataException("Triangle indices must be a multiple of three.");
            }

            return indices;
        }

        if (topology is not (ModelPrimitiveTopology.TriangleStrip or ModelPrimitiveTopology.TriangleFan))
        {
            throw new NotSupportedException($"Rigid model rendering does not support {topology} primitives.");
        }

        var triangles = new List<uint>();
        for (var i = 2; i < indices.Length; i++)
        {
            triangles.Add(topology == ModelPrimitiveTopology.TriangleFan ? indices[0] : indices[i - 2]);
            triangles.Add(topology == ModelPrimitiveTopology.TriangleStrip && i % 2 != 0 ? indices[i] : indices[i - 1]);
            triangles.Add(topology == ModelPrimitiveTopology.TriangleStrip && i % 2 != 0 ? indices[i - 1] : indices[i]);
        }

        return triangles.ToArray();
    }
}
