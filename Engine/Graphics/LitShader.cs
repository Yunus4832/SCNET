using System.Globalization;

using Engine.Core;

namespace Engine.Graphics;

public class LitShader : Shader
{
    public readonly ShaderTransforms Transforms;

    public readonly ShaderParameter AlphaThresholdParameter;

    public readonly ShaderParameter AmbientLightColorParameter;

    public readonly ShaderParameter DiffuseLightColor1Parameter;

    public readonly ShaderParameter DiffuseLightColor2Parameter;

    public readonly ShaderParameter DiffuseLightColor3Parameter;

    public readonly ShaderParameter DirectionToLight1Parameter;

    public readonly ShaderParameter DirectionToLight2Parameter;

    public readonly ShaderParameter DirectionToLight3Parameter;

    public readonly ShaderParameter EmissionColorParameter;

    public readonly ShaderParameter FogColorParameter;

    public readonly ShaderParameter FogLengthParameter;

    public readonly ShaderParameter FogStartParameter;

    private int _instancesCount;

    public readonly int LightsCount;

    public readonly ShaderParameter MaterialColorParameter;

    public readonly ShaderParameter SamplerStateParameter;

    public readonly ShaderParameter TextureParameter;

    private readonly ShaderParameter? _time;

    public readonly bool UseFog;

    public readonly ShaderParameter WorldMatrixParameter;

    public readonly ShaderParameter WorldViewMatrixParameter;

    public readonly ShaderParameter WorldViewProjectionMatrixParameter;

    public LitShader(
        string vsc,
        string psc,
        int lightsCount,
        bool useEmissionColor,
        bool useVertexColor,
        bool useTexture,
        bool useFog,
        bool useAlphaThreshold,
        int maxInstancesCount = 1
    ) : base(
        vsc,
        psc,
        PrepareShaderMacros(
            lightsCount,
            useEmissionColor,
            useVertexColor,
            useTexture,
            useFog,
            useAlphaThreshold,
            maxInstancesCount
        )
    )
    {
        if (lightsCount is < 0 or > 3)
        {
            throw new ArgumentException(null, nameof(lightsCount));
        }

        if (maxInstancesCount is < 0 or > 32)
        {
            throw new ArgumentException(null, nameof(maxInstancesCount));
        }

        WorldMatrixParameter = GetParameter("u_worldMatrix", true);
        WorldViewMatrixParameter = GetParameter("u_worldViewMatrix", true);
        WorldViewProjectionMatrixParameter = GetParameter("u_worldViewProjectionMatrix", true);
        TextureParameter = GetParameter("u_texture", true);
        SamplerStateParameter = GetParameter("u_samplerState", true);
        MaterialColorParameter = GetParameter("u_materialColor", true);
        EmissionColorParameter = GetParameter("u_emissionColor", true);
        AlphaThresholdParameter = GetParameter("u_alphaThreshold", true);
        AmbientLightColorParameter = GetParameter("u_ambientLightColor", true);
        DiffuseLightColor1Parameter = GetParameter("u_diffuseLightColor1", true);
        DirectionToLight1Parameter = GetParameter("u_directionToLight1", true);
        DiffuseLightColor2Parameter = GetParameter("u_diffuseLightColor2", true);
        DirectionToLight2Parameter = GetParameter("u_directionToLight2", true);
        DiffuseLightColor3Parameter = GetParameter("u_diffuseLightColor3", true);
        DirectionToLight3Parameter = GetParameter("u_directionToLight3", true);
        FogStartParameter = GetParameter("u_fogStart", true);
        FogLengthParameter = GetParameter("u_fogLength", true);
        FogColorParameter = GetParameter("u_fogColor", true);
        _time = GetParameter("u_time", true);
        Transforms = new ShaderTransforms(maxInstancesCount);
        LightsCount = lightsCount;
        _instancesCount = 1;
        UseFog = useFog;
        MaterialColor = Vector4.One;
        if (useEmissionColor)
        {
            EmissionColor = Vector4.Zero;
        }

        if (lightsCount >= 1)
        {
            AmbientLightColor = new Vector3(0.2f);
            DiffuseLightColor1 = new Vector3(0.8f);
            LightDirection1 = Vector3.Normalize(new Vector3(1f, -1f, 1f));
        }

        if (lightsCount >= 2)
        {
            DiffuseLightColor2 = new Vector3(0.4f);
            LightDirection2 = Vector3.Normalize(new Vector3(-1f, -0.5f, -0.25f));
        }

        if (lightsCount >= 3)
        {
            DiffuseLightColor3 = new Vector3(0.2f);
            LightDirection3 = Vector3.Normalize(new Vector3(0f, 1f, 0f));
        }

        if (useFog)
        {
            FogLength = 100f;
        }
    }

    public LitShader(
        int lightsCount,
        bool useEmissionColor,
        bool useVertexColor,
        bool useTexture,
        bool useFog,
        bool useAlphaThreshold,
        int maxInstancesCount = 1
    ) : base(
        GetLitVshString(),
        GetLitPshString(),
        PrepareShaderMacros(lightsCount, useEmissionColor, useVertexColor, useTexture, useFog, useAlphaThreshold,
            maxInstancesCount))
    {
        if (lightsCount is < 0 or > 3)
        {
            throw new ArgumentException(null, nameof(lightsCount));
        }

        if (maxInstancesCount is < 0 or > 32)
        {
            throw new ArgumentException(null, nameof(maxInstancesCount));
        }

        WorldMatrixParameter = GetParameter("u_worldMatrix", true);
        WorldViewMatrixParameter = GetParameter("u_worldViewMatrix", true);
        WorldViewProjectionMatrixParameter = GetParameter("u_worldViewProjectionMatrix", true);
        TextureParameter = GetParameter("u_texture", true);
        SamplerStateParameter = GetParameter("u_samplerState", true);
        MaterialColorParameter = GetParameter("u_materialColor", true);
        EmissionColorParameter = GetParameter("u_emissionColor", true);
        AlphaThresholdParameter = GetParameter("u_alphaThreshold", true);
        AmbientLightColorParameter = GetParameter("u_ambientLightColor", true);
        DiffuseLightColor1Parameter = GetParameter("u_diffuseLightColor1", true);
        DirectionToLight1Parameter = GetParameter("u_directionToLight1", true);
        DiffuseLightColor2Parameter = GetParameter("u_diffuseLightColor2", true);
        DirectionToLight2Parameter = GetParameter("u_directionToLight2", true);
        DiffuseLightColor3Parameter = GetParameter("u_diffuseLightColor3", true);
        DirectionToLight3Parameter = GetParameter("u_directionToLight3", true);
        FogStartParameter = GetParameter("u_fogStart", true);
        FogLengthParameter = GetParameter("u_fogLength", true);
        FogColorParameter = GetParameter("u_fogColor", true);
        Transforms = new ShaderTransforms(maxInstancesCount);
        LightsCount = lightsCount;
        _instancesCount = 1;
        UseFog = useFog;
        MaterialColor = Vector4.One;
        if (useEmissionColor)
        {
            EmissionColor = Vector4.Zero;
        }

        if (lightsCount >= 1)
        {
            AmbientLightColor = new Vector3(0.2f);
            DiffuseLightColor1 = new Vector3(0.8f);
            LightDirection1 = Vector3.Normalize(new Vector3(1f, -1f, 1f));
        }

        if (lightsCount >= 2)
        {
            DiffuseLightColor2 = new Vector3(0.4f);
            LightDirection2 = Vector3.Normalize(new Vector3(-1f, -0.5f, -0.25f));
        }

        if (lightsCount >= 3)
        {
            DiffuseLightColor3 = new Vector3(0.2f);
            LightDirection3 = Vector3.Normalize(new Vector3(0f, 1f, 0f));
        }

        if (useFog)
        {
            FogLength = 100f;
        }
    }

    public Texture2D Texture
    {
        set => TextureParameter.SetValue(value);
    }

    public SamplerState SamplerState
    {
        set => SamplerStateParameter.SetValue(value);
    }

    public Vector4 MaterialColor
    {
        set => MaterialColorParameter.SetValue(value);
    }

    public Vector4 EmissionColor
    {
        set => EmissionColorParameter.SetValue(value);
    }

    public float AlphaThreshold
    {
        set => AlphaThresholdParameter.SetValue(value);
    }

    public Vector3 AmbientLightColor
    {
        set => AmbientLightColorParameter.SetValue(value);
    }

    public Vector3 DiffuseLightColor1
    {
        set => DiffuseLightColor1Parameter.SetValue(value);
    }

    public Vector3 DiffuseLightColor2
    {
        set => DiffuseLightColor2Parameter.SetValue(value);
    }

    public Vector3 DiffuseLightColor3
    {
        set => DiffuseLightColor3Parameter.SetValue(value);
    }

    public Vector3 LightDirection1
    {
        set => DirectionToLight1Parameter.SetValue(-value);
    }

    public Vector3 LightDirection2
    {
        set => DirectionToLight2Parameter.SetValue(-value);
    }

    public Vector3 LightDirection3
    {
        set => DirectionToLight3Parameter.SetValue(-value);
    }

    public float FogStart
    {
        set => FogStartParameter.SetValue(value);
    }

    public float FogLength
    {
        set => FogLengthParameter.SetValue(value);
    }

    public Vector3 FogColor
    {
        set => FogColorParameter.SetValue(value);
    }

    public float Time
    {
        set => _time?.SetValue(value);
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

    public override void PrepareForDrawingOverride()
    {
        Transforms.UpdateMatrices(_instancesCount, UseFog, false, true);
        WorldViewProjectionMatrixParameter.SetValue(Transforms.WorldViewProjection, InstancesCount);
        if (LightsCount >= 1)
        {
            WorldMatrixParameter.SetValue(Transforms.World, InstancesCount);
        }

        if (UseFog)
        {
            WorldViewMatrixParameter.SetValue(Transforms.WorldView, InstancesCount);
        }
    }

    private static ShaderMacro[] PrepareShaderMacros(int lightsCount, bool useEmissionColor, bool useVertexColor,
        bool useTexture, bool useFog, bool useAlphaThreshold, int maxInstancesCount)
    {
        var list = new List<ShaderMacro>();
        if (lightsCount > 0)
        {
            list.Add(new ShaderMacro("USE_LIGHTING"));
        }

        if (lightsCount == 1)
        {
            list.Add(new ShaderMacro("ONE_LIGHT"));
        }

        if (lightsCount == 2)
        {
            list.Add(new ShaderMacro("TWO_LIGHTS"));
        }

        if (lightsCount == 3)
        {
            list.Add(new ShaderMacro("THREE_LIGHTS"));
        }

        if (useEmissionColor)
        {
            list.Add(new ShaderMacro("USE_EMISSIONCOLOR"));
        }

        if (useVertexColor)
        {
            list.Add(new ShaderMacro("USE_VERTEXCOLOR"));
        }

        if (useTexture)
        {
            list.Add(new ShaderMacro("USE_TEXTURE"));
        }

        if (useFog)
        {
            list.Add(new ShaderMacro("USE_FOG"));
        }

        if (useAlphaThreshold)
        {
            list.Add(new ShaderMacro("USE_ALPHATHRESHOLD"));
        }

        if (maxInstancesCount > 1)
        {
            list.Add(new ShaderMacro("USE_INSTANCING"));
        }

        list.Add(new ShaderMacro("MAX_INSTANCES_COUNT", maxInstancesCount.ToString(CultureInfo.InvariantCulture)));
        return list.ToArray();
    }

    /// <summary>
    ///     获取 Lit.vsh 着色器文件
    /// </summary>
    /// <returns>着色器字符串</returns>
    private static string GetLitVshString()
    {
        return new StreamReader(
                typeof(Shader).GetTypeInfo().Assembly.GetManifestResourceStream("Engine.Resources.Lit.vsh") ??
                throw new InvalidOperationException("Engine.Resources.Lit.vsh not found"))
            .ReadToEnd();
    }

    /// <summary>
    ///     获取 Lit.psh 着色器文件
    /// </summary>
    /// <returns>着色器字符串</returns>
    private static string GetLitPshString()
    {
        return new StreamReader(
                typeof(Shader).GetTypeInfo().Assembly.GetManifestResourceStream("Engine.Resources.Lit.psh") ??
                throw new InvalidOperationException("Engine.Resources.Lit.psh not found"))
            .ReadToEnd();
    }
}
