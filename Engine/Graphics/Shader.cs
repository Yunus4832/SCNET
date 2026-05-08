using System.Xml.Linq;
using Engine.Core;
using Silk.NET.OpenGLES;

namespace Engine.Graphics;

public class Shader : GraphicsResource
{
    public ShaderParameter GlymulParameter = null!;

    public ShaderParameter[] Parameters = [];

    public Dictionary<string, ShaderParameter> ParametersByName = new();

    public int PixelShader;

    public string PixelShaderCode = string.Empty;

    public int Program;

    public readonly List<ShaderAttributeData> ShaderAttributeDataList = [];

    public ShaderMacro[] ShaderMacros = [];

    public readonly Dictionary<VertexDeclaration, VertexAttributeData[]> VertexAttributeDataByDeclaration = new();

    public int VertexShader;

    public string VertexShaderCode = string.Empty;

    public Shader(string vertexShaderCode, string pixelShaderCode, params ShaderMacro[] shaderMacros)
    {
        Construct(vertexShaderCode, pixelShaderCode, shaderMacros);
    }

    public string DebugName
    {
        get => string.Empty;
        set { }
    }

    public object? Tag { get; set; }

    public ReadOnlyList<ShaderParameter> ReadOnlyParameters => new(Parameters);

    private static readonly char[] _separator = ['\n'];

    private static readonly char[] _separatorArray = [' '];

    public void Construct(string vertexShaderCode, string pixelShaderCode, params ShaderMacro[] shaderMacros)
    {
        try
        {
            InitializeShader(vertexShaderCode, pixelShaderCode, shaderMacros);
            CompileShaders();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        DeleteShaders();
    }

    public void PrepareForDrawing()
    {
        GlymulParameter.SetValue(Display.RenderTarget != null ? -1f : 1f);
        PrepareForDrawingOverride();
    }

    public VertexAttributeData[] GetVertexAttribData(VertexDeclaration vertexDeclaration)
    {
        if (VertexAttributeDataByDeclaration.TryGetValue(vertexDeclaration, out var value))
        {
            return value;
        }

        value = new VertexAttributeData[8];
        foreach (var shaderAttributeDatum in ShaderAttributeDataList)
        {
            VertexElement? vertexElement = null;
            foreach (var element in vertexDeclaration.elements)
            {
                if (element.Semantic != shaderAttributeDatum.Semantic)
                {
                    continue;
                }

                vertexElement = element;
                break;
            }

            if (vertexElement is null)
            {
                throw new InvalidOperationException(
                    $"VertexElement not found for shader attribute \"{shaderAttributeDatum.Semantic}\".");
            }

            value[shaderAttributeDatum.Location] = new VertexAttributeData
            {
                Size = vertexElement.Format.GetElementsCount(),
                Offset = vertexElement.Offset
            };
            GLWrapper.TranslateVertexElementFormat(
                vertexElement.Format,
                out var type,
                out value[shaderAttributeDatum.Location].Normalize
            );
            value[shaderAttributeDatum.Location].Type = (VertexAttribPointerType)type;
        }

        VertexAttributeDataByDeclaration.Add(vertexDeclaration, value);

        return value;
    }

    public static void ParseShaderMetadata(
        string shaderCode,
        Dictionary<string, string> semanticsByAttribute,
        Dictionary<string, string> samplersByTexture
    )
    {
        var array = shaderCode.Split('\n');
        for (var i = 0; i < array.Length; i++)
        {
            try
            {
                var text = array[i];
                text = text.Trim();
                if (!text.StartsWith("//"))
                {
                    continue;
                }

                text = text[2..].TrimStart();
                if (!text.StartsWith("<") || !text.EndsWith("/>"))
                {
                    continue;
                }

                var xElement = XElement.Parse(text);
                if (xElement.Name == "Semantic")
                {
                    if (xElement.Attribute("Attribute") == null)
                    {
                        throw new InvalidOperationException(
                            "Missing \"Attribute\" attribute in shader metadata.");
                    }

                    if (xElement.Attribute("Name") == null)
                    {
                        throw new InvalidOperationException("Missing \"Name\" attribute in shader metadata.");
                    }

                    semanticsByAttribute.Add(
                        xElement.Attribute("Attribute")?.Value ?? string.Empty,
                        xElement.Attribute("Name")?.Value ?? string.Empty
                    );
                }
                else
                {
                    if (!(xElement.Name == "Sampler"))
                    {
                        throw new InvalidOperationException("Unrecognized shader metadata node.");
                    }

                    if (xElement.Attribute("Texture") == null)
                    {
                        throw new InvalidOperationException(
                            "Missing \"Texture\" attribute in shader metadata.");
                    }

                    if (xElement.Attribute("Name") == null)
                    {
                        throw new InvalidOperationException("Missing \"Name\" attribute in shader metadata.");
                    }

                    samplersByTexture.Add(
                        xElement.Attribute("Texture")?.Value ?? string.Empty,
                        xElement.Attribute("Name")?.Value ?? string.Empty
                    );
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error in shader metadata, line {i + 1}. {ex.Message}");
            }
        }
    }

    public string PrependShaderMacros(string shaderCode, ShaderMacro[] shaderMacros, bool isVertexShader)
    {
        var str = string.Empty;

        if (!shaderCode.StartsWith("#version "))
        {
            str += "#version 100" + Environment.NewLine;
        }
        else
        {
            var versioncode = shaderCode.Split(_separator)[0];
            var versionnum = versioncode.Split(_separatorArray)[1];

#if ANDROID
            if (int.Parse(versionnum) >= 300 || versioncode.EndsWith("es"))
            {
                str += $"#version {versionnum} es" + Environment.NewLine;
            }
            else
            {
                str += $"#version {versionnum}" + Environment.NewLine;
            }
#endif
#if DESKTOP
            str += $"#version {versionnum}" + Environment.NewLine;
#endif
            shaderCode = "//" + shaderCode;
        }

        str = str + "#define GLSL" + Environment.NewLine;
        if (isVertexShader)
        {
            str = !Display.UseReducedZRange
                ? str +
                  "#define OPENGL_POSITION_FIX gl_Position.y *= u_glymul; gl_Position.z = 2.0 * gl_Position.z - gl_Position.w;" +
                  Environment.NewLine
                : str + "#define OPENGL_POSITION_FIX gl_Position.y *= u_glymul;" + Environment.NewLine;
            str = str + "uniform float u_glymul;" + Environment.NewLine;
        }

        str = shaderMacros.Aggregate(str,
            (current, shaderMacro) =>
                current + "#define " + shaderMacro.Name + " " + shaderMacro.Value + Environment.NewLine);

        str = str + "#line 1" + Environment.NewLine;
        return str + shaderCode;
    }

    public override void HandleDeviceLost()
    {
        DeleteShaders();
    }

    public override void HandleDeviceReset()
    {
        CompileShaders();
    }

    public void CompileShaders()
    {
        DeleteShaders();
        var dictionary = new Dictionary<string, string>();
        var dictionary2 = new Dictionary<string, string>();
        ParseShaderMetadata(VertexShaderCode, dictionary, dictionary2);
        ParseShaderMetadata(PixelShaderCode, dictionary, dictionary2);
        var @string = PrependShaderMacros(VertexShaderCode, ShaderMacros, true);
        var string2 = PrependShaderMacros(PixelShaderCode, ShaderMacros, false);
        var uVertexShader = GLWrapper.GL.CreateShader(ShaderType.VertexShader);
        VertexShader = (int)uVertexShader;
        GLWrapper.GL.ShaderSource(uVertexShader, @string);
        GLWrapper.GL.CompileShader(uVertexShader);
        GLWrapper.GL.GetShader(uVertexShader, ShaderParameterName.CompileStatus, out var @params);
        if (@params != 1)
        {
            var shaderInfoLog = GLWrapper.GL.GetShaderInfoLog(uVertexShader);
            throw new InvalidOperationException($"Error compiling vertex shader.\n{shaderInfoLog}");
        }

        var uPixelShader = GLWrapper.GL.CreateShader(ShaderType.FragmentShader);
        PixelShader = (int)uPixelShader;
        GLWrapper.GL.ShaderSource(uPixelShader, string2);
        GLWrapper.GL.CompileShader(uPixelShader);
        GLWrapper.GL.GetShader(uPixelShader, ShaderParameterName.CompileStatus, out var params2);
        if (params2 != 1)
        {
            var shaderInfoLog2 = GLWrapper.GL.GetShaderInfoLog(uPixelShader);
            throw new InvalidOperationException($"Error compiling pixel shader.\n{shaderInfoLog2}");
        }

        var uProgram = GLWrapper.GL.CreateProgram();
        Program = (int)uProgram;
        GLWrapper.GL.AttachShader(uProgram, uVertexShader);
        GLWrapper.GL.AttachShader(uProgram, uPixelShader);
        GLWrapper.GL.LinkProgram(uProgram);
        GLWrapper.GL.GetProgram(uProgram, ProgramPropertyARB.LinkStatus, out var params3);
        if (params3 != 1)
        {
            var programInfoLog = GLWrapper.GL.GetProgramInfoLog(uProgram);
            throw new InvalidOperationException($"Error linking program.\n{programInfoLog}");
        }

        GLWrapper.GL.GetProgram(uProgram, ProgramPropertyARB.ActiveAttributes, out var params4);
        for (var i = 0; i < params4; i++)
        {
            GLWrapper.GL.GetActiveAttrib(
                uProgram,
                (uint)i,
                256,
                out _,
                out _,
                out AttributeType _,
                out string name
            );
            var attribLocation = GLWrapper.GL.GetAttribLocation(uProgram, name);
            if (!dictionary.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException(
                    $"Attribute \"{name}\" has no semantic defined in shader metadata.");
            }

            ShaderAttributeDataList.Add(new ShaderAttributeData
            {
                Location = attribLocation,
                Semantic = value
            });
        }

        GLWrapper.GL.GetProgram(uProgram, ProgramPropertyARB.ActiveUniforms, out var params5);
        var list = new List<ShaderParameter>();
        var dictionary3 = new Dictionary<string, ShaderParameter>();
        for (var j = 0; j < params5; j++)
        {
            GLWrapper.GL.GetActiveUniform(
                uProgram,
                (uint)j,
                256,
                out _,
                out var size2,
                out UniformType type2,
                out string name
            );
            var uniformLocation = GLWrapper.GL.GetUniformLocation(uProgram, name);
            var shaderParameterType = GLWrapper.TranslateActiveUniformType(type2);
            var num = name.IndexOf('[');
            if (num >= 0)
            {
                name = name.Remove(num, name.Length - num);
            }

            var shaderParameter = new ShaderParameter(this, name, shaderParameterType, size2)
            {
                location = uniformLocation
            };

            dictionary3.Add(shaderParameter.Name, shaderParameter);
            list.Add(shaderParameter);
            if (shaderParameterType != ShaderParameterType.Texture2D)
            {
                continue;
            }

            if (!dictionary2.TryGetValue(shaderParameter.Name, out var value2))
            {
                throw new InvalidOperationException(
                    $"Texture \"{shaderParameter.Name}\" has no sampler defined in shader metadata.");
            }

            var shaderParameter2 = new ShaderParameter(this, value2, ShaderParameterType.Sampler2D, 1)
            {
                location = int.MaxValue
            };

            dictionary3.Add(value2, shaderParameter2);
            list.Add(shaderParameter2);
        }

        if (Parameters.Length != 0)
        {
            foreach (var item in dictionary3)
            {
                if (ParametersByName.TryGetValue(item.Key, out var value3))
                {
                    value3.location = item.Value.location;
                }
            }

            var parameters = Parameters;
            foreach (var p in parameters)
            {
                p.isChanged = true;
            }
        }
        else
        {
            Parameters = list.ToArray();
            ParametersByName = dictionary3;
        }

        GlymulParameter = GetParameter("u_glymul");
        if (GlymulParameter.Type != 0)
        {
            throw new InvalidOperationException("u_glymul parameter has invalid type.");
        }
    }

    public void DeleteShaders()
    {
        if (Program != 0)
        {
            if (VertexShader != 0)
            {
                GLWrapper.GL.DetachShader((uint)Program, (uint)VertexShader);
            }

            if (PixelShader != 0)
            {
                GLWrapper.GL.DetachShader((uint)Program, (uint)PixelShader);
            }

            GLWrapper.DeleteProgram(Program);
            Program = 0;
        }

        if (VertexShader != 0)
        {
            GLWrapper.GL.DeleteShader((uint)VertexShader);
            VertexShader = 0;
        }

        if (PixelShader != 0)
        {
            GLWrapper.GL.DeleteShader((uint)PixelShader);
            PixelShader = 0;
        }
    }

    public ShaderParameter GetParameter(string name, bool allowNull = false)
    {
        if (ParametersByName.TryGetValue(name, out var value))
        {
            return value;
        }

        if (allowNull)
        {
            return new ShaderParameter("null", ShaderParameterType.Null);
        }

        throw new InvalidOperationException($"Parameter \"{name}\" not found.");
    }

    public override int GetGpuMemoryUsage()
    {
        return 16384;
    }

    public virtual void PrepareForDrawingOverride()
    {
    }

    public void InitializeShader(string vertexShaderCode, string pixelShaderCode, ShaderMacro[] shaderMacros)
    {
        VertexShaderCode = vertexShaderCode;
        PixelShaderCode = pixelShaderCode;
        ShaderMacros = (ShaderMacro[])shaderMacros.Clone();
    }

    public struct ShaderAttributeData
    {
        public string Semantic;

        public int Location;
    }

    public struct VertexAttributeData
    {
        public int Size;

        public VertexAttribPointerType Type;

        public bool Normalize;

        public int Offset;
    }
}
