using System.Runtime.InteropServices;
using Engine.Core;
using Engine.Windowing;
using Silk.NET.OpenGLES;

namespace Engine.Graphics;

public static class Display
{
    private static bool _useReducedZRange;

    private static RenderTarget2D? _renderTarget;

    private static RasterizerState _rasterizerState = RasterizerState.CullCounterClockwise;

    private static DepthStencilState _depthStencilState = DepthStencilState.Default;

    private static BlendState _blendState = BlendState.Opaque;

    public static int DrawCallCount;

    public static int LastDrawCallCount;

    public static string DeviceDescription = string.Empty;

    public static bool UseReducedZRange
    {
        get => _useReducedZRange;
        set
        {
            if (value == _useReducedZRange)
            {
                return;
            }

            _useReducedZRange = value;
            foreach (var resource in GraphicsResource.resources)
            {
                (resource as Shader)?.CompileShaders();
            }
        }
    }

    public static Point2 BackbufferSize { get; private set; }

    public static Viewport Viewport { get; set; }

    public static Rectangle ScissorRectangle { get; set; }

    public static RasterizerState RasterizerState
    {
        get => _rasterizerState;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _rasterizerState = value;
            value.isLocked = true;
        }
    }

    public static DepthStencilState DepthStencilState
    {
        get => _depthStencilState;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _depthStencilState = value;
            value.isLocked = true;
        }
    }

    public static BlendState BlendState
    {
        get => _blendState;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _blendState = value;
            value.isLocked = true;
        }
    }

    public static RenderTarget2D? RenderTarget
    {
        get => _renderTarget;
        set
        {
            _renderTarget = value;
            if (value != null)
            {
                Viewport = new Viewport(0, 0, value.Width, value.Height);
                ScissorRectangle = new Rectangle(0, 0, value.Width, value.Height);
            }
            else
            {
                Viewport = new Viewport(0, 0, BackbufferSize.X, BackbufferSize.Y);
                ScissorRectangle = new Rectangle(0, 0, BackbufferSize.X, BackbufferSize.Y);
            }
        }
    }

    public static event Action? DeviceLost;

    public static event Action? DeviceReset;

    public static void DrawUser<T>(PrimitiveType primitiveType, Shader shader, VertexDeclaration vertexDeclaration,
        T[] vertexData, int startVertex, int verticesCount) where T : struct
    {
        VerifyParametersDrawUser(primitiveType, shader, vertexDeclaration, vertexData, startVertex, verticesCount);
        var gCHandle = GCHandle.Alloc(vertexData, GCHandleType.Pinned);
        try
        {
            GLWrapper.ApplyRenderTarget(RenderTarget);
            GLWrapper.ApplyViewportScissor(Viewport, ScissorRectangle, RasterizerState.ScissorTestEnable);
            GLWrapper.ApplyShaderAndBuffers(
                shader,
                vertexDeclaration,
                gCHandle.AddrOfPinnedObject() + startVertex * vertexDeclaration.VertexStride,
                0,
                null
            );
            GLWrapper.ApplyRasterizerState(RasterizerState);
            GLWrapper.ApplyDepthStencilState(DepthStencilState);
            GLWrapper.ApplyBlendState(BlendState);
            GLWrapper.GL.DrawArrays(GLWrapper.TranslatePrimitiveType(primitiveType), startVertex, (uint)verticesCount);
        }
        finally
        {
            DrawCallCount++;
            gCHandle.Free();
        }
    }

    public static void DrawUserIndexed<T>(PrimitiveType primitiveType, Shader shader,
        VertexDeclaration vertexDeclaration, T[] vertexData, int startVertex, int verticesCount, int[] indexData,
        int startIndex, int indicesCount) where T : struct
    {
        VerifyParametersDrawUserIndexed(primitiveType, shader, vertexDeclaration, vertexData, startVertex,
            verticesCount, indexData, startIndex, indicesCount);
        var gCHandle = GCHandle.Alloc(vertexData, GCHandleType.Pinned);
        var gCHandle2 = GCHandle.Alloc(indexData, GCHandleType.Pinned);
        try
        {
            GLWrapper.ApplyRenderTarget(RenderTarget);
            GLWrapper.ApplyViewportScissor(Viewport, ScissorRectangle, RasterizerState.ScissorTestEnable);
            GLWrapper.ApplyShaderAndBuffers(shader, vertexDeclaration, gCHandle.AddrOfPinnedObject(), 0, 0);
            GLWrapper.ApplyRasterizerState(RasterizerState);
            GLWrapper.ApplyDepthStencilState(DepthStencilState);
            GLWrapper.ApplyBlendState(BlendState);
            var indices = gCHandle2.AddrOfPinnedObject() + 2 * startIndex;
            unsafe
            {
                GLWrapper.GL.DrawElements(
                    GLWrapper.TranslatePrimitiveType(primitiveType),
                    (uint)indicesCount,
                    GLEnum.UnsignedInt,
                    indices.ToPointer()
                );
            }
        }
        finally
        {
            DrawCallCount++;
            gCHandle.Free();
            gCHandle2.Free();
        }
    }

    public static void Draw(PrimitiveType primitiveType, Shader shader, VertexBuffer vertexBuffer, int startVertex,
        int verticesCount)
    {
        VerifyParametersDraw(primitiveType, shader, vertexBuffer, startVertex, verticesCount);
        GLWrapper.ApplyRenderTarget(RenderTarget);
        GLWrapper.ApplyViewportScissor(Viewport, ScissorRectangle, RasterizerState.ScissorTestEnable);
        GLWrapper.ApplyShaderAndBuffers(shader, vertexBuffer.VertexDeclaration, IntPtr.Zero, vertexBuffer.buffer,
            null);
        GLWrapper.ApplyRasterizerState(RasterizerState);
        GLWrapper.ApplyDepthStencilState(DepthStencilState);
        GLWrapper.ApplyBlendState(BlendState);
        DrawCallCount++;
        GLWrapper.GL.DrawArrays(GLWrapper.TranslatePrimitiveType(primitiveType), startVertex, (uint)verticesCount);
    }

    public static void DrawIndexed(PrimitiveType primitiveType, Shader shader, VertexBuffer vertexBuffer,
        IndexBuffer indexBuffer, int startIndex, int indicesCount)
    {
        VerifyParametersDrawIndexed(primitiveType, shader, vertexBuffer, indexBuffer, startIndex, indicesCount);
        GLWrapper.ApplyRenderTarget(RenderTarget);
        GLWrapper.ApplyViewportScissor(Viewport, ScissorRectangle, RasterizerState.ScissorTestEnable);
        GLWrapper.ApplyShaderAndBuffers(shader, vertexBuffer.VertexDeclaration, IntPtr.Zero, vertexBuffer.buffer,
            indexBuffer.buffer);
        GLWrapper.ApplyRasterizerState(RasterizerState);
        GLWrapper.ApplyDepthStencilState(DepthStencilState);
        GLWrapper.ApplyBlendState(BlendState);
        DrawCallCount++;
        var indices = new IntPtr(startIndex * indexBuffer.IndexFormat.GetSize());
        unsafe
        {
            GLWrapper.GL.DrawElements(
                GLWrapper.TranslatePrimitiveType(primitiveType),
                (uint)indicesCount,
                GLWrapper.TranslateIndexFormat(indexBuffer.IndexFormat),
                indices.ToPointer()
            );
        }
    }

    public static void Clear(Vector4? color, float? depth = null, int? stencil = null)
    {
        GLWrapper.Clear(RenderTarget, color, depth, stencil);
    }

    public static void ResetGLStateCache()
    {
        GLWrapper.InitializeCache();
    }

    internal static void Initialize()
    {
        GLWrapper.Initialize();
        GLWrapper.InitializeCache();
        Resize();
    }

    internal static void Dispose()
    {
    }

    internal static void BeforeFrame()
    {
    }

    internal static void AfterFrame()
    {
        LastDrawCallCount = DrawCallCount;
        DrawCallCount = 0;
    }

    internal static void Resize()
    {
        BackbufferSize = new Point2(Window.Size.X, Window.Size.Y);
        Viewport = new Viewport(0, 0, Window.Size.X, Window.Size.Y);
        ScissorRectangle = new Rectangle(0, 0, Window.Size.X, Window.Size.Y);
    }

    public static long GetGpuMemoryUsage()
    {
        long num = 8 * BackbufferSize.X * BackbufferSize.Y;
        foreach (var resource in GraphicsResource.resources)
        {
            num += resource.GetGpuMemoryUsage();
        }

        return num;
    }

    public static void Clear(Color? color, float? depth = null, int? stencil = null)
    {
        Clear(color.HasValue ? new Vector4?(new Vector4(color.Value)) : null, depth, stencil);
    }

    internal static void VerifyParametersDrawUser<T>(
        PrimitiveType primitiveType,
        Shader shader,
        VertexDeclaration vertexDeclaration,
        T[] vertexData,
        int startVertex,
        int verticesCount
    ) where T : struct
    {
        shader.VerifyNotDisposed();
        var num = Utilities.SizeOf<T>();

        if (vertexDeclaration.VertexStride / num * num != vertexDeclaration.VertexStride)
        {
            throw new InvalidOperationException(
                $"Vertex is not an integer multiple of array element, vertex stride is {vertexDeclaration.VertexStride}, array element is {num}.");
        }

        if (startVertex < 0 || verticesCount < 0 || startVertex + verticesCount > vertexData.Length)
        {
            throw new ArgumentException("Vertices range is out of bounds.");
        }
    }

    internal static void VerifyParametersDrawUserIndexed<T>(
        PrimitiveType primitiveType,
        Shader shader,
        VertexDeclaration vertexDeclaration,
        T[] vertexData,
        int startVertex,
        int verticesCount,
        int[] indexData,
        int startIndex,
        int indicesCount
    ) where T : struct
    {
        shader.VerifyNotDisposed();
        var num = Utilities.SizeOf<T>();
        if (vertexDeclaration.VertexStride / num * num != vertexDeclaration.VertexStride)
        {
            throw new InvalidOperationException(
                $"Vertex is not an integer multiple of array element, vertex stride is {vertexDeclaration.VertexStride}, array element is {num}.");
        }

        if (startVertex < 0 || verticesCount < 0 || startVertex + verticesCount > vertexData.Length)
        {
            throw new ArgumentException("Vertices range is out of bounds.");
        }

        if (startIndex < 0 || indicesCount < 0 || startIndex + indicesCount > indexData.Length)
        {
            throw new ArgumentException("Indices range is out of bounds.");
        }
    }

    internal static void VerifyParametersDraw(
        PrimitiveType primitiveType,
        Shader shader,
        VertexBuffer vertexBuffer,
        int startVertex,
        int verticesCount
    )
    {
        shader.VerifyNotDisposed();
        vertexBuffer.VerifyNotDisposed();
        if (startVertex < 0 || verticesCount < 0 || startVertex + verticesCount > vertexBuffer.VerticesCount)
        {
            throw new ArgumentException("Vertices range is out of bounds.");
        }
    }

    internal static void VerifyParametersDrawIndexed(
        PrimitiveType primitiveType,
        Shader shader,
        VertexBuffer vertexBuffer,
        IndexBuffer indexBuffer,
        int startIndex,
        int indicesCount
    )
    {
        shader.VerifyNotDisposed();
        vertexBuffer.VerifyNotDisposed();
        indexBuffer.VerifyNotDisposed();
        if (startIndex < 0 || indicesCount < 0 || startIndex + indicesCount > indexBuffer.IndicesCount)
        {
            throw new ArgumentException("Indices range is out of bounds.");
        }
    }

    internal static void HandleDeviceLost()
    {
        foreach (var resource in GraphicsResource.resources)
        {
            resource.HandleDeviceLost();
        }

        DeviceLost?.Invoke();
    }

    internal static void HandleDeviceReset()
    {
        foreach (var resource in GraphicsResource.resources)
        {
            resource.HandleDeviceReset();
        }

        DeviceReset?.Invoke();
    }
}
