using System.Buffers.Binary;

using Engine.Core.Mathematics;

namespace Engine.Graphics;

/// <summary>CPU geometry shared by scene nodes; graphics buffers are created only on drawing access.</summary>
public sealed class ModelPrimitive
{
    private readonly ModelGeometryData _geometry;

    private VertexBuffer? _vertices;

    private IndexBuffer? _indices;

    private bool _disposed;

    internal ModelPrimitive(ModelGeometryData geometry, int materialIndex)
    {
        _geometry = geometry;
        MaterialIndex = materialIndex;
    }

    public ReadOnlyMemory<byte> Vertices => _geometry.Vertices;

    public ReadOnlyMemory<uint> Indices => _geometry.Indices;

    public bool HasGraphicsResources => _vertices != null;

    public VertexBuffer VertexBuffer
    {
        get
        {
            EnsureGraphicsResources();
            return _vertices!;
        }
    }

    public IndexBuffer IndexBuffer
    {
        get
        {
            EnsureGraphicsResources();
            return _indices!;
        }
    }

    public int MaterialIndex { get; }

    public BoundingBox BoundingBox => _geometry.BoundingBox;

    public int StartIndex => 0;

    public int IndicesCount => _geometry.Indices.Length;

    public ushort[] GetIndices16()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_geometry.Indices.Any(index => index > ushort.MaxValue))
        {
            throw new NotSupportedException("This operation requires 16-bit mesh indices; split larger meshes first.");
        }

        return _geometry.Indices.Select(index => (ushort)index).ToArray();
    }

    private void EnsureGraphicsResources()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_vertices != null) { return; }

        VertexBuffer? vertices = null;
        IndexBuffer? indices = null;
        try
        {
            vertices = new VertexBuffer(ModelGeometryData.VertexDeclaration, _geometry.Vertices.Length / 32);
            vertices.SetData(_geometry.Vertices, 0, _geometry.Vertices.Length);
            var small = _geometry.Indices.All(index => index <= ushort.MaxValue);
            var bytes = new byte[checked(_geometry.Indices.Length * (small ? 2 : 4))];
            for (var index = 0; index < _geometry.Indices.Length; index++)
            {
                if (small)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), (ushort)_geometry.Indices[index]);
                }
                else { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), _geometry.Indices[index]); }
            }

            indices = new IndexBuffer(
                small ? IndexFormat.SixteenBits : IndexFormat.ThirtyTwoBits,
                _geometry.Indices.Length
            );
            indices.SetData(bytes, 0, bytes.Length);
            _vertices = vertices;
            _indices = indices;
            Display.DeviceLost += DropGraphicsResources;
        }
        catch
        {
            vertices?.Dispose();
            indices?.Dispose();
            throw;
        }
    }

    internal void ReleaseBuffers()
    {
        if (_disposed) { return; }

        _disposed = true;
        DropGraphicsResources();
    }

    private void DropGraphicsResources()
    {
        Display.DeviceLost -= DropGraphicsResources;
        _vertices?.Dispose();
        _indices?.Dispose();
        _vertices = null;
        _indices = null;
    }
}
