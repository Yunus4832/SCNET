using System.Globalization;

using Engine.Graphics;

namespace Game;

public class ModelShader : Shader
{
    private ShaderParameter? _alphaThresholdParameter;

    private ShaderParameter _ambientLightColorParameter = null!;

    private ShaderParameter _diffuseLightColor1Parameter = null!;

    private ShaderParameter _diffuseLightColor2Parameter = null!;

    private ShaderParameter _directionToLight1Parameter = null!;

    private ShaderParameter _directionToLight2Parameter = null!;

    private ShaderParameter _emissionColorParameter = null!;

    private ShaderParameter _fogBottomTopDensityParameter = null!;

    private ShaderParameter _fogColorParameter = null!;

    private ShaderParameter _fogYMultiplierParameter = null!;

    private ShaderParameter _hazeStartDensityParameter = null!;

    private int _instancesCount;

    private ShaderParameter _materialColorParameter = null!;

    private ShaderParameter _samplerStateParameter = null!;

    private ShaderParameter _textureParameter = null!;

    private ShaderParameter _worldMatrixParameter = null!;

    private ShaderParameter _worldUpParameter = null!;

    private ShaderParameter _worldViewProjectionMatrixParameter = null!;

    public readonly ShaderTransforms Transforms;

    public ModelShader(
        string vsc,
        string psc,
        bool useAlphaThreshold
    ) : base(vsc, psc, PrepareShaderMacros(useAlphaThreshold, 1))
    {
        SetParameter();
        Transforms = new ShaderTransforms(1);
    }

    public ModelShader(
        string vsc,
        string psc,
        bool useAlphaThreshold,
        int maxInstancesCount
    ) : base(vsc, psc, PrepareShaderMacros(useAlphaThreshold, maxInstancesCount))
    {
        SetParameter();
        Transforms = new ShaderTransforms(maxInstancesCount);
    }


    public ModelShader(
        string vsc,
        string psc,
        bool useAlphaThreshold,
        int maxInstancesCount,
        ShaderMacro[] shaderMacros
    ) : base(vsc, psc, PrepareShaderMacros(useAlphaThreshold, maxInstancesCount, shaderMacros))
    {
        SetParameter();
        Transforms = new ShaderTransforms(maxInstancesCount);
    }

    public Texture2D Texture
    {
        set => _textureParameter.SetValue(value);
    }

    public SamplerState SamplerState
    {
        set => _samplerStateParameter.SetValue(value);
    }

    public Vector4 MaterialColor
    {
        set => _materialColorParameter.SetValue(value);
    }

    public Vector4 EmissionColor
    {
        set => _emissionColorParameter.SetValue(value);
    }

    public float AlphaThreshold
    {
        set => _alphaThresholdParameter?.SetValue(value);
    }

    public Vector3 AmbientLightColor
    {
        set => _ambientLightColorParameter.SetValue(value);
    }

    public Vector3 DiffuseLightColor1
    {
        set => _diffuseLightColor1Parameter.SetValue(value);
    }

    public Vector3 DiffuseLightColor2
    {
        set => _diffuseLightColor2Parameter.SetValue(value);
    }

    public Vector3 LightDirection1
    {
        set => _directionToLight1Parameter.SetValue(-value);
    }

    public Vector3 LightDirection2
    {
        set => _directionToLight2Parameter.SetValue(-value);
    }

    public Vector3 FogColor
    {
        set => _fogColorParameter.SetValue(value);
    }

    //new
    public Vector3 FogBottomTopDensity
    {
        set => _fogBottomTopDensityParameter.SetValue(value);
    }

    public Vector2 HazeStartDensity
    {
        set => _hazeStartDensityParameter.SetValue(value);
    }

    public float FogYMultiplier
    {
        set => _fogYMultiplierParameter.SetValue(value);
    }

    public Vector3 WorldUp
    {
        set => _worldUpParameter.SetValue(value);
    }

    public int InstancesCount
    {
        get => _instancesCount;
        set
        {
            if (value < 0 || value > Transforms.MaxWorldMatrices)
            {
                throw new InvalidOperationException("Invalid instances count.");
            }

            _instancesCount = value;
        }
    }

    public void SetParameter()
    {
        _worldMatrixParameter = GetParameter("u_worldMatrix");
        _worldViewProjectionMatrixParameter = GetParameter("u_worldViewProjectionMatrix");
        _textureParameter = GetParameter("u_texture");
        _samplerStateParameter = GetParameter("u_samplerState");
        _materialColorParameter = GetParameter("u_materialColor");
        _emissionColorParameter = GetParameter("u_emissionColor");
        _alphaThresholdParameter = GetParameter("u_alphaThreshold", true);
        _ambientLightColorParameter = GetParameter("u_ambientLightColor");
        _diffuseLightColor1Parameter = GetParameter("u_diffuseLightColor1");
        _directionToLight1Parameter = GetParameter("u_directionToLight1");
        _diffuseLightColor2Parameter = GetParameter("u_diffuseLightColor2");
        _directionToLight2Parameter = GetParameter("u_directionToLight2");
        _fogColorParameter = GetParameter("u_fogColor");
        _fogBottomTopDensityParameter = GetParameter("u_fogBottomTopDensity");
        _hazeStartDensityParameter = GetParameter("u_hazeStartDensity");
        _fogYMultiplierParameter = GetParameter("u_fogYMultiplier");
        _worldUpParameter = GetParameter("u_worldUp");
    }

    public override void PrepareForDrawingOverride()
    {
        Transforms.UpdateMatrices(_instancesCount, false, false, true);
        _worldViewProjectionMatrixParameter.SetValue(Transforms.WorldViewProjection, InstancesCount);
        _worldMatrixParameter.SetValue(Transforms.World, InstancesCount);
    }

    private static ShaderMacro[] PrepareShaderMacros(
        bool useAlphaThreshold,
        int maxInstancesCount
    )
    {
        return PrepareShaderMacros(useAlphaThreshold, maxInstancesCount, []);
    }

    private static ShaderMacro[] PrepareShaderMacros(
        bool useAlphaThreshold,
        int maxInstancesCount,
        ShaderMacro[] shaderMacros
    )
    {
        var list = new List<ShaderMacro>();
        if (useAlphaThreshold)
        {
            list.Add(new ShaderMacro("ALPHATESTED"));
        }

        list.Add(new ShaderMacro("MAX_INSTANCES_COUNT", maxInstancesCount.ToString(CultureInfo.InvariantCulture)));
        if (shaderMacros is not { Length: > 0 })
        {
            return list.ToArray();
        }

        list.AddRange(shaderMacros);

        return list.ToArray();
    }
}
