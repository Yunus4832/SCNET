using System.Diagnostics;
using Engine.Core;
using Engine.Windowing;
using Silk.NET.OpenGLES;

namespace Engine.Graphics;

internal static class GLWrapper
{
    public static GL GL = null!;

    internal static int mainFrameBuffer;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    internal static int mainColorBuffer;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

    private static int _arrayBuffer;

    private static int _elementArrayBuffer;

    private static int _texture2D;

    private static int[] _activeTexturesByUnit = null!;

    private static TextureUnit _activeTextureUnit;

    private static int _program;

    private static int _framebuffer;

    private static Vector4? _clearColor;

    private static float? _clearDepth;

    private static int? _clearStencil;

    private static TriangleFace _cullFace;

    private static FrontFaceDirection _frontFace;

    private static DepthFunction _depthFunction;

    private static int? _colorMask;

    private static bool? _depthMask;

    private static float _polygonOffsetFactor;

    private static float _polygonOffsetUnits;

    private static Vector4 _blendColor;

    private static BlendEquationModeEXT _blendEquation;

    private static BlendEquationModeEXT _blendEquationColor;

    private static BlendEquationModeEXT _blendEquationAlpha;

    private static BlendingFactor _blendFuncSource;

    private static BlendingFactor _blendFuncSourceColor;

    private static BlendingFactor _blendFuncSourceAlpha;

    private static BlendingFactor _blendFuncDestination;

    private static BlendingFactor _blendFuncDestinationColor;

    private static BlendingFactor _blendFuncDestinationAlpha;

    private static Dictionary<EnableCap, bool> _enableDisableStates = new();

    private static bool?[] _vertexAttribArray = [];

    private static RasterizerState _rasterizerState = null!;

    private static DepthStencilState _depthStencilState = null!;

    private static BlendState _blendState = null!;

    private static Dictionary<int, SamplerState> _textureSamplerStates = new();

    private static Shader _lastShader = null!;

    private static VertexDeclaration _lastVertexDeclaration = null!;

    private static IntPtr _lastVertexOffset;

    private static int _lastArrayBuffer;

    private static Viewport? _viewport;

    private static Rectangle? _scissorRectangle;

    public static bool GLExtTextureFilterAnisotropic;

    public static bool GLOesPackedDepthStencil;

    public static bool GLKhrTextureCompressionAstcLdr;

    public static int GLMaxCombinedTextureImageUnits;

    public static int GLMaxTextureSize;

    public static void Initialize()
    {
        GL = GL.GetApi(Window.View);
        mainFrameBuffer = 0;
        var bits = new int[6];
        for (var i = 0; i < 6; i++)
        {
            bits[i] = GL.GetInteger((GetPName)(i + 3410));
        }

        GL.GetInteger(GetPName.MaxTextureSize, out GLMaxTextureSize);
        var openGLVendor = $"OpenGL ES, Vendor={GL.GetStringS(StringName.Vendor) ?? string.Empty}";
        Display.DeviceDescription =
            $"{openGLVendor}, Renderer={GL.GetStringS(StringName.Renderer) ?? string.Empty}, Version={GL.GetStringS(StringName.Version) ?? string.Empty}, R={bits[0]} G={bits[1]} B={bits[2]} A={bits[3]}, D={bits[4]} S={bits[5]}, MaxTextureSize={GLMaxTextureSize}";
        Log.Information($"Initialized display device: {Display.DeviceDescription}");
        var extensions = GL.GetStringS(StringName.Extensions);
        GLExtTextureFilterAnisotropic = extensions?.Contains("GL_EXT_texture_filter_anisotropic") ?? false;
        GLOesPackedDepthStencil = extensions?.Contains("GL_OES_packed_depth_stencil") ?? false;
        GLKhrTextureCompressionAstcLdr = extensions?.Contains("GL_KHR_texture_compression_astc_ldr") ?? false;
        GLMaxCombinedTextureImageUnits = GL.GetInteger(GetPName.MaxCombinedTextureImageUnits);
    }

    public static void InitializeCache()
    {
        _arrayBuffer = -1;
        _elementArrayBuffer = -1;
        _texture2D = -1;
        _activeTexturesByUnit =
        [
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1
        ];
        _activeTextureUnit = (TextureUnit)(-1);
        _program = -1;
        _framebuffer = -1;
        _clearColor = null;
        _clearDepth = null;
        _clearStencil = null;
        _cullFace = 0;
        _frontFace = 0;
        _depthFunction = (DepthFunction)(-1);
        _colorMask = null;
        _depthMask = null;
        _polygonOffsetFactor = 0f;
        _polygonOffsetUnits = 0f;
        _blendColor = new Vector4(float.MinValue);
        _blendEquation = (BlendEquationModeEXT)(-1);
        _blendEquationColor = (BlendEquationModeEXT)(-1);
        _blendEquationAlpha = (BlendEquationModeEXT)(-1);
        _blendFuncSource = (BlendingFactor)(-1);
        _blendFuncSourceColor = (BlendingFactor)(-1);
        _blendFuncSourceAlpha = (BlendingFactor)(-1);
        _blendFuncDestination = (BlendingFactor)(-1);
        _blendFuncDestinationColor = (BlendingFactor)(-1);
        _blendFuncDestinationAlpha = (BlendingFactor)(-1);
        _enableDisableStates = new Dictionary<EnableCap, bool>();
        _vertexAttribArray = new bool?[16];
        _rasterizerState = null!;
        _depthStencilState = null!;
        _blendState = null!;
        _textureSamplerStates = new Dictionary<int, SamplerState>();
        _lastShader = null!;
        _lastVertexDeclaration = null!;
        _lastVertexOffset = IntPtr.Zero;
        _lastArrayBuffer = -1;
        _viewport = null;
        _scissorRectangle = null;
    }

    public static bool Enable(EnableCap state)
    {
        if (_enableDisableStates.TryGetValue(state, out var value) && value)
        {
            return false;
        }

        GL.Enable(state);
        _enableDisableStates[state] = true;
        return true;
    }

    public static bool Disable(EnableCap state)
    {
        if (!(!_enableDisableStates.TryGetValue(state, out var value) | value))
        {
            return false;
        }

        GL.Disable(state);
        _enableDisableStates[state] = false;
        return true;
    }

    public static bool IsEnabled(EnableCap state)
    {
        if (_enableDisableStates.TryGetValue(state, out var value))
        {
            return value;
        }

        value = GL.IsEnabled(state);
        _enableDisableStates[state] = value;

        return value;
    }

    public static void ClearColor(Vector4 color)
    {
        var clearColor = _clearColor;
        if (color == clearColor)
        {
            return;
        }

        GL.ClearColor(color.X, color.Y, color.Z, color.W);
        _clearColor = color;
    }

    public static void ClearDepth(float depth)
    {
        if (depth.CloseTo(_clearDepth ?? 0))
        {
            return;
        }

        GL.ClearDepth(depth);
        _clearDepth = depth;
    }

    public static void ClearStencil(int stencil)
    {
        if (stencil == _clearStencil)
        {
            return;
        }

        GL.ClearStencil(stencil);
        _clearStencil = stencil;
    }

    public static void CullFace(TriangleFace cullFace)
    {
        if (cullFace == _cullFace)
        {
            return;
        }

        GL.CullFace(cullFace);
        _cullFace = cullFace;
    }

    public static void FrontFace(FrontFaceDirection frontFace)
    {
        if (frontFace == _frontFace)
        {
            return;
        }

        GL.FrontFace(frontFace);
        _frontFace = frontFace;
    }

    public static void DepthFunc(DepthFunction depthFunction)
    {
        if (depthFunction == _depthFunction)
        {
            return;
        }

        GL.DepthFunc(depthFunction);
        _depthFunction = depthFunction;
    }

    public static void ColorMask(int colorMask)
    {
        colorMask &= 0xF;
        if (colorMask == _colorMask)
        {
            return;
        }

        GL.ColorMask((colorMask & 8) != 0, (colorMask & 4) != 0, (colorMask & 2) != 0, (colorMask & 1) != 0);
        _colorMask = colorMask;
    }

    public static bool DepthMask(bool depthMask)
    {
        if (depthMask == _depthMask)
        {
            return false;
        }

        GL.DepthMask(depthMask);
        _depthMask = depthMask;
        return true;
    }

    public static void PolygonOffset(float factor, float units)
    {
        if (factor.CloseTo(_polygonOffsetFactor) && units.CloseTo(_polygonOffsetUnits))
        {
            return;
        }

        GL.PolygonOffset(factor, units);
        _polygonOffsetFactor = factor;
        _polygonOffsetUnits = units;
    }

    public static void BlendColor(Vector4 blendColor)
    {
        if (blendColor == _blendColor)
        {
            return;
        }

        GL.BlendColor(blendColor.X, blendColor.Y, blendColor.Z, blendColor.W);
        _blendColor = blendColor;
    }

    public static void BlendEquation(BlendEquationModeEXT blendEquation)
    {
        if (blendEquation == _blendEquation)
        {
            return;
        }

        GL.BlendEquation(blendEquation);
        _blendEquation = blendEquation;
        _blendEquationColor = (BlendEquationModeEXT)(-1);
        _blendEquationAlpha = (BlendEquationModeEXT)(-1);
    }

    public static void BlendEquationSeparate(
        BlendEquationModeEXT blendEquationColor,
        BlendEquationModeEXT blendEquationAlpha
    )
    {
        if (blendEquationColor == _blendEquationColor && blendEquationAlpha == _blendEquationAlpha)
        {
            return;
        }

        GL.BlendEquationSeparate(blendEquationColor, blendEquationAlpha);
        _blendEquationColor = blendEquationColor;
        _blendEquationAlpha = blendEquationAlpha;
        _blendEquation = (BlendEquationModeEXT)(-1);
    }

    public static void BlendFunc(BlendingFactor blendFuncSource, BlendingFactor blendFuncDestination)
    {
        if (blendFuncSource == _blendFuncSource && blendFuncDestination == _blendFuncDestination)
        {
            return;
        }

        GL.BlendFunc(blendFuncSource, blendFuncDestination);
        _blendFuncSource = blendFuncSource;
        _blendFuncDestination = blendFuncDestination;
        _blendFuncSourceColor = (BlendingFactor)(-1);
        _blendFuncSourceAlpha = (BlendingFactor)(-1);
        _blendFuncDestinationColor = (BlendingFactor)(-1);
        _blendFuncDestinationAlpha = (BlendingFactor)(-1);
    }

    public static void BlendFuncSeparate(
        BlendingFactor blendFuncSourceColor,
        BlendingFactor blendFuncDestinationColor,
        BlendingFactor blendFuncSourceAlpha,
        BlendingFactor blendFuncDestinationAlpha
    )
    {
        if (blendFuncSourceColor == _blendFuncSourceColor &&
            blendFuncDestinationColor == _blendFuncDestinationColor &&
            blendFuncSourceAlpha == _blendFuncSourceAlpha &&
            blendFuncDestinationAlpha == _blendFuncDestinationAlpha)
        {
            return;
        }

        GL.BlendFuncSeparate(blendFuncSourceColor, blendFuncDestinationColor, blendFuncSourceAlpha,
            blendFuncDestinationAlpha);
        _blendFuncSourceColor = blendFuncSourceColor;
        _blendFuncSourceAlpha = blendFuncSourceAlpha;
        _blendFuncDestinationColor = blendFuncDestinationColor;
        _blendFuncDestinationAlpha = blendFuncDestinationAlpha;
        _blendFuncSource = (BlendingFactor)(-1);
        _blendFuncDestination = (BlendingFactor)(-1);
    }

    public static void VertexAttribArray(int index, bool enable)
    {
        if (enable && (!_vertexAttribArray[index].HasValue || !_vertexAttribArray[index]!.Value))
        {
            GL.EnableVertexAttribArray((uint)index);
            _vertexAttribArray[index] = true;
        }
        else if (!enable && (!_vertexAttribArray[index].HasValue || _vertexAttribArray[index]!.Value))
        {
            GL.DisableVertexAttribArray((uint)index);
            _vertexAttribArray[index] = false;
        }
    }

    public static void BindTexture(TextureTarget target, int texture, bool forceBind)
    {
        BindTexture(target, (uint)texture, forceBind);
    }

    public static void BindTexture(TextureTarget target, uint texture, bool forceBind)
    {
        if (target != TextureTarget.Texture2D)
        {
            GL.BindTexture(target, texture);
            return;
        }

        if (!forceBind && texture == _texture2D)
        {
            return;
        }

        GL.BindTexture(target, texture);
        _texture2D = (int)texture;
        if (_activeTextureUnit < 0)
        {
            return;
        }

        _activeTexturesByUnit[(int)(_activeTextureUnit - 33984)] = (int)texture;
    }

    public static void ActiveTexture(TextureUnit textureUnit)
    {
        if (textureUnit == _activeTextureUnit)
        {
            return;
        }

        GL.ActiveTexture(textureUnit);
        _activeTextureUnit = textureUnit;
    }

    public static void BindBuffer(BufferTargetARB target, int buffer)
    {
        BindBuffer(target, (uint)buffer);
    }

    public static void BindBuffer(BufferTargetARB target, uint buffer)
    {
        switch (target)
        {
            case BufferTargetARB.ArrayBuffer:
                if (buffer != _arrayBuffer)
                {
                    GL.BindBuffer(target, buffer);
                    _arrayBuffer = (int)buffer;
                }

                break;
            case BufferTargetARB.ElementArrayBuffer:
                if (buffer != _elementArrayBuffer)
                {
                    GL.BindBuffer(target, buffer);
                    _elementArrayBuffer = (int)buffer;
                }

                break;
            default:
                GL.BindBuffer(target, buffer);
                break;
        }
    }

    public static void BindFramebuffer(uint framebuffer)
    {
        if (framebuffer == _framebuffer)
        {
            return;
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        _framebuffer = (int)framebuffer;
    }

    public static void UseProgram(uint program)
    {
        if (program == _program)
        {
            return;
        }

        GL.UseProgram(program);
        _program = (int)program;
    }

    public static void DeleteProgram(int program)
    {
        if (_program == program)
        {
            _program = -1;
        }

        GL.DeleteProgram((uint)program);
    }

    public static void DeleteTexture(int texture)
    {
        if (_texture2D == texture)
        {
            _texture2D = -1;
        }

        for (var i = 0; i < _activeTexturesByUnit.Length; i++)
        {
            if (_activeTexturesByUnit[i] != texture)
            {
                continue;
            }

            _activeTexturesByUnit[i] = -1;
        }

        _textureSamplerStates.Remove(texture);
        GL.DeleteTexture((uint)texture);
    }

    public static void DeleteFramebuffer(int framebuffer)
    {
        if (_framebuffer == framebuffer)
        {
            _framebuffer = -1;
        }

        var uFramebuffer = (uint)framebuffer;
        GL.DeleteFramebuffers(1, ref uFramebuffer);
    }

    public static void DeleteBuffer(BufferTargetARB target, int buffer)
    {
        if (target == BufferTargetARB.ArrayBuffer)
        {
            if (_arrayBuffer == buffer)
            {
                _arrayBuffer = -1;
            }

            if (_lastArrayBuffer == buffer)
            {
                _lastArrayBuffer = -1;
            }
        }

        if (target == BufferTargetARB.ElementArrayBuffer && _elementArrayBuffer == buffer)
        {
            _elementArrayBuffer = -1;
        }

        var uBuffer = (uint)buffer;
        GL.DeleteBuffers(1, ref uBuffer);
    }

    public static void ApplyViewportScissor(Viewport viewport, Rectangle scissorRectangle, bool isScissorEnabled)
    {
        if (!_viewport.HasValue ||
            viewport.X != _viewport.Value.X ||
            viewport.Y != _viewport.Value.Y ||
            viewport.Width != _viewport.Value.Width ||
            viewport.Height != _viewport.Value.Height)
        {
            var y = Display.RenderTarget is null
                ? Display.BackbufferSize.Y - viewport.Y - viewport.Height
                : viewport.Y;
            GL.Viewport(viewport.X, y, (uint)viewport.Width, (uint)viewport.Height);
        }

        if (!_viewport.HasValue ||
            !viewport.MinDepth.CloseTo(_viewport.Value.MinDepth) ||
            !viewport.MaxDepth.CloseTo(_viewport.Value.MaxDepth)
           )
        {
            GL.DepthRange(viewport.MinDepth, viewport.MaxDepth);
        }

        _viewport = viewport;
        if (!isScissorEnabled)
        {
            return;
        }

        if (_scissorRectangle.HasValue)
        {
            var value = scissorRectangle;
            var scissorRectangle2 = _scissorRectangle;
            if (!(value != scissorRectangle2))
            {
                return;
            }
        }

        if (Display.RenderTarget == null)
        {
            scissorRectangle.Top = Display.BackbufferSize.Y - scissorRectangle.Top - scissorRectangle.Height;
        }

        GL.Scissor(scissorRectangle.Left, scissorRectangle.Top, (uint)scissorRectangle.Width,
            (uint)scissorRectangle.Height);
        _scissorRectangle = scissorRectangle;
    }

    public static void ApplyRasterizerState(RasterizerState state)
    {
        if (state == _rasterizerState)
        {
            return;
        }

        _rasterizerState = state;
        switch (state.CullMode)
        {
            case CullMode.None:
                Disable(EnableCap.CullFace);
                break;
            case CullMode.CullClockwise:
                Enable(EnableCap.CullFace);
                CullFace(TriangleFace.Back);
                FrontFace(Display.RenderTarget != null ? FrontFaceDirection.CW : FrontFaceDirection.Ccw);
                break;
            case CullMode.CullCounterClockwise:
                Enable(EnableCap.CullFace);
                CullFace(TriangleFace.Back);
                FrontFace(Display.RenderTarget != null ? FrontFaceDirection.Ccw : FrontFaceDirection.CW);
                break;
        }

        if (state.ScissorTestEnable)
        {
            Enable(EnableCap.ScissorTest);
        }
        else
        {
            Disable(EnableCap.ScissorTest);
        }

        if (state.DepthBias != 0f || state.SlopeScaleDepthBias != 0f)
        {
            Enable(EnableCap.PolygonOffsetFill);
            PolygonOffset(state.SlopeScaleDepthBias, state.DepthBias);
        }
        else
        {
            Disable(EnableCap.PolygonOffsetFill);
        }
    }

    public static void ApplyDepthStencilState(DepthStencilState state)
    {
        if (state == _depthStencilState)
        {
            return;
        }

        _depthStencilState = state;
        if (state.DepthBufferTestEnable || state.DepthBufferWriteEnable)
        {
            Enable(EnableCap.DepthTest);
            if (state.DepthBufferTestEnable)
            {
                DepthFunc((DepthFunction)TranslateCompareFunction(state.DepthBufferFunction));
            }
            else
            {
                DepthFunc(DepthFunction.Always);
            }

            DepthMask(state.DepthBufferWriteEnable);
        }
        else
        {
            Disable(EnableCap.DepthTest);
        }
    }

    public static void ApplyBlendState(BlendState state)
    {
        if (state == _blendState)
        {
            return;
        }

        _blendState = state;
        if (state is
            {
                ColorBlendFunction: BlendFunction.Add,
                ColorSourceBlend: Blend.One,
                ColorDestinationBlend: Blend.Zero,
                AlphaBlendFunction: BlendFunction.Add,
                AlphaSourceBlend: Blend.One,
                AlphaDestinationBlend: Blend.Zero
            }
           )
        {
            Disable(EnableCap.Blend);
            return;
        }

        var colorBlendFunc = (BlendEquationModeEXT)TranslateBlendFunction(state.ColorBlendFunction);
        var alphaBlendFunc = (BlendEquationModeEXT)TranslateBlendFunction(state.AlphaBlendFunction);
        var colorSourceBlend = (BlendingFactor)TranslateBlend(state.ColorSourceBlend);
        var colorDestinationBlend = (BlendingFactor)TranslateBlend(state.ColorDestinationBlend);
        var alphaSourceBlend = (BlendingFactor)TranslateBlend(state.AlphaSourceBlend);
        var alphaDestinationBlend = (BlendingFactor)TranslateBlend(state.AlphaDestinationBlend);
        if (colorBlendFunc == alphaBlendFunc && colorSourceBlend == alphaSourceBlend &&
            colorDestinationBlend == alphaDestinationBlend)
        {
            BlendEquation(colorBlendFunc);
            BlendFunc(colorSourceBlend, colorDestinationBlend);
        }
        else
        {
            BlendEquationSeparate(colorBlendFunc, alphaBlendFunc);
            BlendFuncSeparate(colorSourceBlend, colorDestinationBlend, alphaSourceBlend, alphaDestinationBlend);
        }

        BlendColor(state.BlendFactor);
        Enable(EnableCap.Blend);
    }

    public static void ApplyRenderTarget(RenderTarget2D? renderTarget)
    {
        if (renderTarget != null)
        {
            BindFramebuffer((uint)renderTarget.frameBuffer);
        }
        else
        {
            BindFramebuffer((uint)mainFrameBuffer);
        }
    }

    public static unsafe void ApplyShaderAndBuffers(
        Shader shader,
        VertexDeclaration vertexDeclaration,
        IntPtr vertexOffset,
        int arrayBuffer,
        int? elementArrayBuffer
    )
    {
        shader.PrepareForDrawing();
        BindBuffer(BufferTargetARB.ArrayBuffer, (uint)arrayBuffer);
        if (elementArrayBuffer.HasValue)
        {
            BindBuffer(BufferTargetARB.ElementArrayBuffer, (uint)elementArrayBuffer.Value);
        }

        UseProgram((uint)shader.Program);
        if (shader != _lastShader || vertexOffset != _lastVertexOffset || arrayBuffer != _lastArrayBuffer ||
            vertexDeclaration.elements != _lastVertexDeclaration.elements)
        {
            var vertexAttribData = shader.GetVertexAttribData(vertexDeclaration);
            for (var i = 0; i < vertexAttribData.Length; i++)
            {
                if (vertexAttribData[i].Size != 0)
                {
                    GL.VertexAttribPointer(
                        (uint)i,
                        vertexAttribData[i].Size,
                        vertexAttribData[i].Type,
                        vertexAttribData[i].Normalize,
                        (uint)vertexDeclaration.VertexStride,
                        (vertexOffset + vertexAttribData[i].Offset).ToPointer()
                    );
                    VertexAttribArray(i, true);
                }
                else
                {
                    VertexAttribArray(i, false);
                }
            }

            _lastShader = shader;
            _lastVertexDeclaration = vertexDeclaration;
            _lastVertexOffset = vertexOffset;
            _lastArrayBuffer = arrayBuffer;
        }

        var num = 0;
        var num2 = 0;
        ShaderParameter shaderParameter;
        while (true)
        {
            if (num2 >= shader.Parameters.Length)
            {
                return;
            }

            shaderParameter = shader.Parameters[num2];
            if (shaderParameter.isChanged)
            {
                switch (shaderParameter.Type)
                {
                    case ShaderParameterType.Float:
                        GL.Uniform1(shaderParameter.location, (uint)shaderParameter.Count, shaderParameter.value);
                        shaderParameter.isChanged = false;
                        break;
                    case ShaderParameterType.Vector2:
                        GL.Uniform2(shaderParameter.location, (uint)shaderParameter.Count, shaderParameter.value);
                        shaderParameter.isChanged = false;
                        break;
                    case ShaderParameterType.Vector3:
                        GL.Uniform3(shaderParameter.location, (uint)shaderParameter.Count, shaderParameter.value);
                        shaderParameter.isChanged = false;
                        break;
                    case ShaderParameterType.Vector4:
                        GL.Uniform4(shaderParameter.location, (uint)shaderParameter.Count, shaderParameter.value);
                        shaderParameter.isChanged = false;
                        break;
                    case ShaderParameterType.Matrix:
                        GL.UniformMatrix4(shaderParameter.location, (uint)shaderParameter.Count, false,
                            shaderParameter.value);
                        shaderParameter.isChanged = false;
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported shader parameter type.");
                    case ShaderParameterType.Texture2D:
                    case ShaderParameterType.Sampler2D:
                        break;
                }
            }

            if (shaderParameter.Type == ShaderParameterType.Texture2D)
            {
                if (num >= 8)
                {
                    throw new InvalidOperationException("Too many simultaneous textures.");
                }

                ActiveTexture((TextureUnit)(33984 + num));
                if (shaderParameter.isChanged)
                {
                    GL.Uniform1(shaderParameter.location, num);
                }

                var obj = shader.Parameters[num2 + 1];
                var texture2D = (Texture2D?)shaderParameter.resource;
                var samplerState = (SamplerState?)obj.resource;
                if (texture2D != null)
                {
                    if (samplerState == null)
                    {
                        break;
                    }

                    if (_activeTexturesByUnit[num] != texture2D.texture)
                    {
                        BindTexture(TextureTarget.Texture2D, (uint)texture2D.texture, true);
                    }

                    if (!_textureSamplerStates.TryGetValue(texture2D.texture, out var value) ||
                        value != samplerState)
                    {
                        BindTexture(TextureTarget.Texture2D, (uint)texture2D.texture, false);
                        if (GLExtTextureFilterAnisotropic)
                        {
                            GL.TexParameter(
                                TextureTarget.Texture2D,
                                TextureParameterName.TextureMaxAnisotropy,
                                samplerState.FilterMode == TextureFilterMode.Anisotropic
                                    ? samplerState.MaxAnisotropy
                                    : 1f
                            );
                        }

                        GL.TexParameter(
                            TextureTarget.Texture2D,
                            TextureParameterName.TextureMinFilter,
                            (int)TranslateTextureFilterModeMin(samplerState.FilterMode, texture2D.MipLevelsCount > 1)
                        );
                        GL.TexParameter(
                            TextureTarget.Texture2D,
                            TextureParameterName.TextureMagFilter,
                            (int)TranslateTextureFilterModeMag(samplerState.FilterMode)
                        );
                        GL.TexParameter(
                            TextureTarget.Texture2D,
                            TextureParameterName.TextureWrapS,
                            (int)TranslateTextureAddressMode(samplerState.AddressModeU)
                        );
                        GL.TexParameter(
                            TextureTarget.Texture2D,
                            TextureParameterName.TextureWrapT,
                            (int)TranslateTextureAddressMode(samplerState.AddressModeV)
                        );
                        GL.TexParameter(
                            TextureTarget.Texture2D,
                            TextureParameterName.TextureMinLod,
                            samplerState.MinLod
                        );
                        GL.TexParameter(
                            TextureTarget.Texture2D,
                            TextureParameterName.TextureMaxLod,
                            samplerState.MaxLod
                        );
                        _textureSamplerStates[texture2D.texture] = samplerState;
                    }
                }
                else if (_activeTexturesByUnit[num] != 0)
                {
                    BindTexture(TextureTarget.Texture2D, 0, true);
                }

                num++;
                shaderParameter.isChanged = false;
            }

            num2++;
        }

        throw new InvalidOperationException(
            $"Associated SamplerState is not set for texture \"{shaderParameter.Name}\".");
    }

    public static void Clear(RenderTarget2D? renderTarget, Vector4? color, float? depth, int? stencil)
    {
        ClearBufferMask clearBufferMask = 0;
        if (color.HasValue)
        {
            clearBufferMask |= ClearBufferMask.ColorBufferBit;
            ClearColor(color.Value);
            ColorMask(15);
        }

        if (depth.HasValue)
        {
            clearBufferMask |= ClearBufferMask.DepthBufferBit;
            ClearDepth(depth.Value);
            if (DepthMask(true))
            {
                _depthStencilState = null!;
            }
        }

        if (stencil.HasValue)
        {
            clearBufferMask |= ClearBufferMask.StencilBufferBit;
            ClearStencil(stencil.Value);
        }

        if (clearBufferMask == 0)
        {
            return;
        }

        ApplyRenderTarget(renderTarget);
        if (Disable(EnableCap.ScissorTest))
        {
            _rasterizerState = null!;
        }

        GL.Clear(clearBufferMask);
    }

    public static void HandleContextLost()
    {
        try
        {
            Log.Information("Device lost");
            Display.HandleDeviceLost();
            GC.Collect();
            InitializeCache();
            Display.Resize();
            Display.HandleDeviceReset();
            Log.Information("Device reset");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to recreate graphics resources. Reason: {0}", ex.Message);
        }
    }

    public static void TranslateVertexElementFormat(
        VertexElementFormat vertexElementFormat,
        out GLEnum type,
        out bool normalize
    )
    {
        switch (vertexElementFormat)
        {
            case VertexElementFormat.Single:
            case VertexElementFormat.Vector2:
            case VertexElementFormat.Vector3:
            case VertexElementFormat.Vector4:
                type = GLEnum.Float;
                normalize = false;
                break;
            case VertexElementFormat.Byte4:
                type = GLEnum.UnsignedByte;
                normalize = false;
                break;
            case VertexElementFormat.NormalizedByte4:
                type = GLEnum.UnsignedByte;
                normalize = true;
                break;
            case VertexElementFormat.Short2:
                type = GLEnum.Short;
                normalize = false;
                break;
            case VertexElementFormat.NormalizedShort2:
                type = GLEnum.Short;
                normalize = true;
                break;
            case VertexElementFormat.Short4:
                type = GLEnum.Short;
                normalize = false;
                break;
            case VertexElementFormat.NormalizedShort4:
                type = GLEnum.Short;
                normalize = true;
                break;
            default:
                throw new InvalidOperationException("Unsupported vertex element format.");
        }
    }

    public static GLEnum TranslateIndexFormat(IndexFormat indexFormat)
    {
        return indexFormat switch
        {
            IndexFormat.SixteenBits => GLEnum.UnsignedShort,
            IndexFormat.ThirtyTwoBits => GLEnum.UnsignedInt,
            _ => throw new InvalidOperationException("Unsupported index format.")
        };
    }

    public static ShaderParameterType TranslateActiveUniformType(UniformType type)
    {
        return TranslateActiveUniformType((GLEnum)type);
    }

    public static ShaderParameterType TranslateActiveUniformType(GLEnum type)
    {
        return type switch
        {
            GLEnum.Float => ShaderParameterType.Float,
            GLEnum.FloatVec2 => ShaderParameterType.Vector2,
            GLEnum.FloatVec3 => ShaderParameterType.Vector3,
            GLEnum.FloatVec4 => ShaderParameterType.Vector4,
            GLEnum.FloatMat4 => ShaderParameterType.Matrix,
            GLEnum.Sampler2D => ShaderParameterType.Texture2D,
            _ => throw new InvalidOperationException("Unsupported shader parameter type.")
        };
    }

    public static GLEnum TranslatePrimitiveType(PrimitiveType primitiveType)
    {
        return primitiveType switch
        {
            PrimitiveType.LineList => GLEnum.Lines,
            PrimitiveType.LineStrip => GLEnum.LineStrip,
            PrimitiveType.TriangleList => GLEnum.Triangles,
            PrimitiveType.TriangleStrip => GLEnum.TriangleStrip,
            _ => throw new InvalidOperationException("Unsupported primitive type.")
        };
    }

    public static GLEnum TranslateTextureFilterModeMin(TextureFilterMode filterMode, bool isMipmapped)
    {
        return filterMode switch
        {
            TextureFilterMode.Point => !isMipmapped ? GLEnum.Nearest : GLEnum.NearestMipmapNearest,
            TextureFilterMode.Linear or TextureFilterMode.Anisotropic => !isMipmapped
                ? GLEnum.Linear
                : GLEnum.LinearMipmapLinear,
            TextureFilterMode.PointMipLinear => !isMipmapped ? GLEnum.Nearest : GLEnum.NearestMipmapLinear,
            TextureFilterMode.LinearMipPoint => !isMipmapped ? GLEnum.Linear : GLEnum.LinearMipmapNearest,
            TextureFilterMode.MinPointMagLinearMipPoint => !isMipmapped ? GLEnum.Nearest : GLEnum.NearestMipmapNearest,
            TextureFilterMode.MinPointMagLinearMipLinear => !isMipmapped ? GLEnum.Nearest : GLEnum.NearestMipmapLinear,
            TextureFilterMode.MinLinearMagPointMipPoint => !isMipmapped ? GLEnum.Linear : GLEnum.LinearMipmapNearest,
            TextureFilterMode.MinLinearMagPointMipLinear => !isMipmapped ? GLEnum.Linear : GLEnum.LinearMipmapLinear,
            _ => throw new InvalidOperationException("Unsupported texture filter mode.")
        };
    }

    public static GLEnum TranslateTextureFilterModeMag(TextureFilterMode filterMode)
    {
        return filterMode switch
        {
            TextureFilterMode.Point => GLEnum.Nearest,
            TextureFilterMode.Linear => GLEnum.Linear,
            TextureFilterMode.Anisotropic => GLEnum.Linear,
            TextureFilterMode.PointMipLinear => GLEnum.Nearest,
            TextureFilterMode.LinearMipPoint => GLEnum.Nearest,
            TextureFilterMode.MinPointMagLinearMipPoint => GLEnum.Linear,
            TextureFilterMode.MinPointMagLinearMipLinear => GLEnum.Linear,
            TextureFilterMode.MinLinearMagPointMipPoint => GLEnum.Nearest,
            TextureFilterMode.MinLinearMagPointMipLinear => GLEnum.Nearest,
            _ => throw new InvalidOperationException("Unsupported texture filter mode.")
        };
    }

    public static GLEnum TranslateTextureAddressMode(TextureAddressMode addressMode)
    {
        return addressMode switch
        {
            TextureAddressMode.Clamp => GLEnum.ClampToEdge,
            TextureAddressMode.Wrap => GLEnum.Repeat,
            _ => throw new InvalidOperationException("Unsupported texture address mode.")
        };
    }

    public static GLEnum TranslateCompareFunction(CompareFunction compareFunction)
    {
        return compareFunction switch
        {
            CompareFunction.Always => GLEnum.Always,
            CompareFunction.Equal => GLEnum.Equal,
            CompareFunction.Greater => GLEnum.Greater,
            CompareFunction.GreaterEqual => GLEnum.Gequal,
            CompareFunction.Less => GLEnum.Less,
            CompareFunction.LessEqual => GLEnum.Lequal,
            CompareFunction.Never => GLEnum.Never,
            CompareFunction.NotEqual => GLEnum.Notequal,
            _ => throw new InvalidOperationException("Unsupported texture address mode.")
        };
    }

    public static GLEnum TranslateBlendFunction(BlendFunction blendFunction)
    {
        return blendFunction switch
        {
            BlendFunction.Add => GLEnum.FuncAdd,
            BlendFunction.Subtract => GLEnum.FuncSubtract,
            BlendFunction.ReverseSubtract => GLEnum.FuncReverseSubtract,
            _ => throw new InvalidOperationException("Unsupported blend function.")
        };
    }

    public static GLEnum TranslateBlend(Blend blend)
    {
        return blend switch
        {
            Blend.Zero => GLEnum.False,
            Blend.One => GLEnum.One,
            Blend.SourceColor => GLEnum.SrcColor,
            Blend.InverseSourceColor => GLEnum.OneMinusSrcColor,
            Blend.DestinationColor => GLEnum.DstColor,
            Blend.InverseDestinationColor => GLEnum.OneMinusDstColor,
            Blend.SourceAlpha => GLEnum.SrcAlpha,
            Blend.InverseSourceAlpha => GLEnum.OneMinusSrcAlpha,
            Blend.DestinationAlpha => GLEnum.DstAlpha,
            Blend.InverseDestinationAlpha => GLEnum.OneMinusDstAlpha,
            Blend.BlendFactor => GLEnum.ConstantColor,
            Blend.InverseBlendFactor => GLEnum.OneMinusConstantColor,
            Blend.SourceAlphaSaturation => GLEnum.SrcAlphaSaturate,
            _ => throw new InvalidOperationException("Unsupported blend.")
        };
    }

    public static GLEnum TranslateDepthFormat(DepthFormat depthFormat)
    {
        return depthFormat switch
        {
            DepthFormat.Depth16 => GLEnum.DepthComponent16,
            DepthFormat.Depth24Stencil8 => GLEnum.Depth24Stencil8,
            _ => throw new InvalidOperationException("Unsupported DepthFormat.")
        };
    }

    [Conditional("DEBUG")]
    public static void CheckGlError()
    {
    }
}
