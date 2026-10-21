using System.Buffers.Binary;

using Engine.Core.Collections;
using Engine.Core.Mathematics;
using Engine.Media.Models;

namespace Engine.Graphics;

/// <summary>Shared GPU geometry for a source mesh and one immutable set of default morph weights.</summary>
public sealed class ModelMesh
{
    private readonly List<ModelPrimitive> _primitives = [];

    private readonly float[] _morphWeights;

    internal ModelMesh(ModelData data, int sourceMeshIndex, float[] morphWeights, bool keepSource)
    {
        var source = data.Meshes[sourceMeshIndex];
        Name = source.Name;
        SourceMeshIndex = sourceMeshIndex;
        _morphWeights = morphWeights;
        try
        {
            foreach (var primitive in source.MeshParts)
            {
                var geometry = ModelGeometryData.Create(data, primitive, morphWeights);
                VertexBuffer? vertices = null;
                IndexBuffer? indices = null;
                try
                {
                    vertices = new VertexBuffer(ModelGeometryData.VertexDeclaration, geometry.Vertices.Length / 32);
                    vertices.SetData(geometry.Vertices, 0, geometry.Vertices.Length);
                    var small = geometry.Indices.All(i => i <= ushort.MaxValue);
                    var bytes = new byte[checked(geometry.Indices.Length * (small ? 2 : 4))];
                    for (var i = 0; i < geometry.Indices.Length; i++)
                    {
                        if (small)
                        {
                            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), (ushort)geometry.Indices[i]);
                        }
                        else
                        {
                            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), geometry.Indices[i]);
                        }
                    }

                    indices = new IndexBuffer(
                        small ? IndexFormat.SixteenBits : IndexFormat.ThirtyTwoBits,
                        geometry.Indices.Length
                    );
                    indices.SetData(bytes, 0, bytes.Length);
                    if (keepSource)
                    {
                        vertices.Tag = geometry.Vertices;
                        indices.Tag = bytes;
                    }

                    _primitives.Add(
                        new ModelPrimitive(vertices, indices, primitive.MaterialIndex, geometry.BoundingBox)
                    );
                }
                catch
                {
                    vertices?.Dispose();
                    indices?.Dispose();
                    throw;
                }
            }
        }
        catch
        {
            ReleaseBuffers();
            throw;
        }

        BoundingBox = _primitives.Count == 0
            ? source.BoundingBox
            : _primitives.Select(p => p.BoundingBox).Aggregate(BoundingBox.Union);
    }

    public string Name { get; }

    public int SourceMeshIndex { get; }

    public ReadOnlyList<float> MorphWeights => new(_morphWeights);

    public ReadOnlyList<ModelPrimitive> Primitives => new(_primitives);

    public BoundingBox BoundingBox { get; }

    internal void ReleaseBuffers()
    {
        foreach (var primitive in _primitives)
        {
            primitive.VertexBuffer.Dispose();
            primitive.IndexBuffer.Dispose();
        }

        _primitives.Clear();
    }
}
