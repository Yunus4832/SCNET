using System.Runtime.InteropServices;
using Engine.Core;
using Silk.NET.OpenGLES;

namespace Engine.Graphics;

public sealed class IndexBuffer : GraphicsResource
{
    internal int buffer;

    public IndexBuffer(IndexFormat indexFormat, int indicesCount)
    {
        InitializeIndexBuffer(indexFormat, indicesCount);
        AllocateBuffer();
    }

    public string DebugName
    {
        get => string.Empty;
        set { }
    }

    public IndexFormat IndexFormat { get; set; }

    public int IndicesCount { get; set; }

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
            var size = IndexFormat.GetSize();
            var data = gCHandle.AddrOfPinnedObject() + sourceStartIndex * num;
            GLWrapper.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffer);
            unsafe
            {
                GLWrapper.GL.BufferSubData(
                    BufferTargetARB.ElementArrayBuffer,
                    new IntPtr(targetStartIndex * size),
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
        GLWrapper.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffer);
        unsafe
        {
            GLWrapper.GL.BufferData(
                BufferTargetARB.ElementArrayBuffer,
                new UIntPtr((uint)(IndexFormat.GetSize() * IndicesCount)),
                null,
                GLEnum.StaticDraw
            );
        }
    }

    public void DeleteBuffer()
    {
        if (buffer == 0)
        {
            return;
        }

        GLWrapper.DeleteBuffer(BufferTargetARB.ElementArrayBuffer, buffer);
        buffer = 0;
    }

    public override int GetGpuMemoryUsage()
    {
        return IndicesCount * IndexFormat.GetSize();
    }

    public void InitializeIndexBuffer(IndexFormat indexFormat, int indicesCount)
    {
        if (indicesCount <= 0)
        {
            throw new ArgumentException("Indices count must be greater than 0.");
        }

        IndexFormat = indexFormat;
        IndicesCount = indicesCount;
    }

    public void VerifyParametersSetData<T>(T[] source, int sourceStartIndex, int sourceCount, int targetStartIndex = 0)
        where T : struct
    {
        VerifyNotDisposed();
        var num = Utilities.SizeOf<T>();
        var size = IndexFormat.GetSize();
        if (sourceStartIndex < 0 || sourceCount < 0 || sourceStartIndex + sourceCount > source.Length)
        {
            throw new ArgumentException("Range is out of source bounds.");
        }

        if (targetStartIndex < 0 || targetStartIndex * size + sourceCount * num > IndicesCount * size)
        {
            throw new ArgumentException("Range is out of target bounds.");
        }
    }
}
