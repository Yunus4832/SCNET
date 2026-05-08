namespace Engine.Graphics;

public class SkyShader : Shader
{
    public readonly ShaderTransforms Transforms;

    private ShaderParameter? _alphaThresholdParameter;

    private ShaderParameter? _colorParameter;

    private ShaderParameter? _samplerStateParameter;

    private ShaderParameter? _textureParameter;

    private ShaderParameter? _worldViewProjectionMatrixParameter;

    public SkyShader(
        string vsc,
        string psc,
        bool useVertexColor,
        bool useTexture,
        bool useAlphaThreshold
    ) : base(vsc, psc, PrepareShaderMacros(useVertexColor, useTexture, useAlphaThreshold))
    {
        SetParameter();
        Transforms = new ShaderTransforms(1);
        Color = Vector4.One;
    }

    public SkyShader(
        string vsc,
        string psc,
        bool useVertexColor,
        bool useTexture,
        bool useAlphaThreshold,
        ShaderMacro[] shaderMacros
    ) : base(vsc, psc, PrepareShaderMacros(useVertexColor, useTexture, useAlphaThreshold, shaderMacros))
    {
        SetParameter();
        Transforms = new ShaderTransforms(1);
        Color = Vector4.One;
    }

    public Texture2D Texture
    {
        set => _textureParameter?.SetValue(value);
    }

    public SamplerState SamplerState
    {
        set => _samplerStateParameter?.SetValue(value);
    }

    public Vector4 Color
    {
        set => _colorParameter?.SetValue(value);
    }

    public float AlphaThreshold
    {
        set => _alphaThresholdParameter?.SetValue(value);
    }

    public void SetParameter()
    {
        _worldViewProjectionMatrixParameter = GetParameter("u_worldViewProjectionMatrix", true);
        _textureParameter = GetParameter("u_texture", true);
        _samplerStateParameter = GetParameter("u_samplerState", true);
        _colorParameter = GetParameter("u_color", true);
        _alphaThresholdParameter = GetParameter("u_alphaThreshold", true);
    }

    public override void PrepareForDrawingOverride()
    {
        Transforms.UpdateMatrices(1, false, false, true);
        _worldViewProjectionMatrixParameter?.SetValue(Transforms.WorldViewProjection, 1);
    }

    private static ShaderMacro[] PrepareShaderMacros(
        bool useVertexColor,
        bool useTexture,
        bool useAlphaThreshold)
    {
        return PrepareShaderMacros(useVertexColor, useTexture,  useAlphaThreshold, []);
    }

    private static ShaderMacro[] PrepareShaderMacros(
        bool useVertexColor,
        bool useTexture,
        bool useAlphaThreshold,
        ShaderMacro[] shaderMacros
    )
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

        if (useAlphaThreshold)
        {
            list.Add(new ShaderMacro("USE_ALPHATHRESHOLD"));
        }

        if (shaderMacros is not { Length: > 0 })
        {
            return list.ToArray();
        }

        list.AddRange(shaderMacros);

        return list.ToArray();
    }
}
