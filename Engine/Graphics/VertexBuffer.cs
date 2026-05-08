using System.Runtime.InteropServices;
using Engine.Core;
using Silk.NET.OpenGLES;

namespace Engine.Graphics;

public sealed class VertexBuffer : GraphicsResource
{
    internal int buffer;

    public VertexBuffer(VertexDeclaration vertexDeclaration, int verticesCount)
    {
        InitializeVertexBuffer(vertexDeclaration, verticesCount);
        AllocateBuffer();
    }

    public string DebugName
    {
        get => string.Empty;
        set { }
    }

    public VertexDeclaration VertexDeclaration { get; set; } = null!;

    public int VerticesCount { get; set; }

    public object? Tag { get; set; }

    public override void Dispose()
    {
        base.Dispose();
        DeleteBuffer();
    }

    public void SetData<T>(T[] source, int sourceStartIndex, int sourceCount, int targetStartIndex = 0) where T : struct
    {
        VerifyParametersSetData(source, sourceStartIndex, sourceCount, targetStartIndex);
        var gCHandle = GCHandle.Alloc(source, GCHandleType.Pinned);
        try
        {
            var num = Utilities.SizeOf<T>();
            var vertexStride = VertexDeclaration.VertexStride;
            GLWrapper.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
            var data = gCHandle.AddrOfPinnedObject() + sourceStartIndex * num;
            unsafe
            {
                GLWrapper.GL.BufferSubData(
                    BufferTargetARB.ArrayBuffer,
                    new IntPtr(targetStartIndex * vertexStride),
                    new UIntPtr((uint)(num * sourceCount)),
                    data.ToPointer()
                );
            }
        }
        finally
        {
            gCHandle.Free();
        }
    }

    public override void HandleDeviceLost()
    {
        DeleteBuffer();
    }

    public override void HandleDeviceReset()
    {
        AllocateBuffer();
    }

    public void AllocateBuffer()
    {
        GLWrapper.GL.GenBuffers(1, out uint uBuffer);
        buffer = (int)uBuffer;
        GLWrapper.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
        unsafe
        {
            GLWrapper.GL.BufferData(
                BufferTargetARB.ArrayBuffer,
                new UIntPtr((uint)(VertexDeclaration.VertexStride * VerticesCount)),
                null,
                BufferUsageARB.StaticDraw
            );
        }
    }

    public void DeleteBuffer()
    {
        if (buffer == 0)
        {
            return;
        }

        GLWrapper.DeleteBuffer(BufferTargetARB.ArrayBuffer, buffer);
        buffer = 0;
    }

    public override int GetGpuMemoryUsage()
    {
        return VertexDeclaration.VertexStride * VerticesCount;
    }

    public void InitializeVertexBuffer(VertexDeclaration vertexDeclaration, int verticesCount)
    {
        if (verticesCount <= 0)
        {
            throw new ArgumentException("verticesCount must be greater than 0.");
        }

        VertexDeclaration = vertexDeclaration;
        VerticesCount = verticesCount;
    }

    public void VerifyParametersSetData<T>(T[] source, int sourceStartIndex, int sourceCount, int targetStartIndex =
        0) where T : struct
    {
        VerifyNotDisposed();
        var num = Utilities.SizeOf<T>();
        var vertexStride = VertexDeclaration.VertexStride;
        if (sourceStartIndex < 0 || sourceCount < 0 || sourceStartIndex + sourceCount > source.Length)
        {
            throw new ArgumentException("Range is out of source bounds.");
        }

        if (targetStartIndex < 0 || targetStartIndex * vertexStride + sourceCount * num > VerticesCount * vertexStride)
        {
            throw new ArgumentException("Range is out of target bounds.");
        }
    }
}
