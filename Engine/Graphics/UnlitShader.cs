using Engine.Core;

namespace Engine.Graphics;

public class UnlitShader : Shader
{
    private readonly ShaderParameter _additiveColorParameter;

    public readonly ShaderTransforms Transforms;

    public readonly ShaderParameter AlphaThresholdParameter;

    public readonly ShaderParameter ColorParameter;

    public readonly ShaderParameter SamplerStateParameter;

    public readonly ShaderParameter TextureParameter;

    private readonly ShaderParameter? _time;

    public readonly ShaderParameter WorldViewProjectionMatrixParameter;

    public UnlitShader(
        string vsc,
        string psc,
        bool useVertexColor,
        bool useTexture,
        bool useAdditiveColor,
        bool useAlphaThreshold
    ) : base(
        vsc,
        psc,
        PrepareShaderMacros(
            useVertexColor,
            useTexture,
            useAdditiveColor,
            useAlphaThreshold
        )
    )
    {
        WorldViewProjectionMatrixParameter = GetParameter("u_worldViewProjectionMatrix", true);
        TextureParameter = GetParameter("u_texture", true);
        SamplerStateParameter = GetParameter("u_samplerState", true);
        ColorParameter = GetParameter("u_color", true);
        _additiveColorParameter = GetParameter("u_additiveColor", true);
        AlphaThresholdParameter = GetParameter("u_alphaThreshold", true);
        _time = GetParameter("u_time", true);
        Transforms = new ShaderTransforms(1);
        Color = Vector4.One;
    }

    public UnlitShader(
        bool useVertexColor,
        bool useTexture,
        bool useAdditiveColor,
        bool useAlphaThreshold
    ) : base(
        GetUnlitVshString(),
        GetUnlitPshString(),
        PrepareShaderMacros(
            useVertexColor,
            useTexture,
            useAdditiveColor,
            useAlphaThreshold
        )
    )
    {
        WorldViewProjectionMatrixParameter = GetParameter("u_worldViewProjectionMatrix", true);
        TextureParameter = GetParameter("u_texture", true);
        SamplerStateParameter = GetParameter("u_samplerState", true);
        ColorParameter = GetParameter("u_color", true);
        _additiveColorParameter = GetParameter("u_additiveColor", true);
        AlphaThresholdParameter = GetParameter("u_alphaThreshold", true);
        Transforms = new ShaderTransforms(1);
        Color = Vector4.One;
    }

    public Texture2D Texture
    {
        set => TextureParameter.SetValue(value);
    }

    public SamplerState SamplerState
    {
        set => SamplerStateParameter.SetValue(value);
    }

    public Vector4 Color
    {
        set => ColorParameter.SetValue(value);
    }

    public Vector4 AdditiveColor
    {
        set => _additiveColorParameter.SetValue(value);
    }

    public float AlphaThreshold
    {
        set => AlphaThresholdParameter.SetValue(value);
    }

    public float Time
    {
        set => _time?.SetValue(value);
    }

    public override void PrepareForDrawingOverride()
    {
        Transforms.UpdateMatrices(1, false, false, true);
        WorldViewProjectionMatrixParameter.SetValue(Transforms.WorldViewProjection, 1);
    }

    private static ShaderMacro[] PrepareShaderMacros(bool useVertexColor, bool useTexture, bool useAdditiveColor,
        bool useAlphaThreshold)
    {
        var list = new List<ShaderMacro>();
        if (useVertexColor)
        {
            list.Add(new ShaderMacro("USE_VERTEXCOLOR"));
        }

        if (useTexture)
        {
            list.Add(new ShaderMacro("USE_TEXTURE"));
        }

        if (useAdditiveColor)
        {
            list.Add(new ShaderMacro("USE_ADDITIVECOLOR"));
        }

        if (useAlphaThreshold)
        {
            list.Add(new ShaderMacro("USE_ALPHATHRESHOLD"));
        }

        return list.ToArray();
    }

    /// <summary>
    ///     获取 Unlit.vsh 着色器文件
    /// </summary>
    /// <returns>着色器字符串</returns>
    private static string GetUnlitVshString()
    {
        return new StreamReader(
                typeof(Shader).GetTypeInfo().Assembly.GetManifestResourceStream("Engine.Resources.Unlit.vsh")
                ?? throw new InvalidOperationException("Engine.Resources.Unlit.vsh not found"))
            .ReadToEnd();
    }

    /// <summary>
    ///     获取 Unlit.psh 着色器文件
    /// </summary>
    /// <returns>着色器字符串</returns>
    private static string GetUnlitPshString()
    {
        return new StreamReader(
                typeof(Shader).GetTypeInfo().Assembly
                    .GetManifestResourceStream("Engine.Resources.Unlit.psh") ??
                throw new InvalidOperationException("Engine.Resources.Unlit.psh not found "))
            .ReadToEnd();
    }
}
