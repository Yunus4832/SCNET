using Engine.Graphics;

namespace Game.ContentReaders;

public class ShaderReader : IContentReader
{
    public override string Type => "Engine.Graphics.Shader";

    public override string[] DefaultSuffix => ["vsh", "psh"];

    public override object Get(ContentInfo[] contents)
    {
        ShaderMacro[] shaderMacros;
        if (contents[0].Filename.StartsWith("AlphaTested"))
        {
            shaderMacros = [new ShaderMacro("ALPHATESTED")];
        }
        else
        {
            shaderMacros = [];
        }

        using var vertexReader = new StreamReader(contents[0].Duplicate());
        using var pixelReader = new StreamReader(contents[1].Duplicate());
        return new Shader(
            ShaderCodeManager.ExpandIncludes(
                vertexReader.ReadToEnd(),
                ShaderCodeManager.ReadSource
            ),
            ShaderCodeManager.ExpandIncludes(
                pixelReader.ReadToEnd(),
                ShaderCodeManager.ReadSource
            ),
            shaderMacros
        );
    }
}
