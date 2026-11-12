using Game.Managers;

namespace Survivalcraft.Test.Managers;

public sealed class ShaderCodeManagerTest
{
    [Fact]
    public void ExpansionPreservesShaderDirectivesAndExpandsNestedSourcesOnce()
    {
        var sources = new Dictionary<string, string>
        {
            ["a.txt"] = "#ifdef GLSL\n#include \"b.txt\"\n#endif",
            ["b.txt"] = "highp float sharedValue;"
        };
        var result = ShaderCodeManager.ExpandIncludes(
            "// keep\n#include \"a.txt\"\nvoid main() {}",
            name => sources[name]
        );
        Assert.Equal("// keep\n#ifdef GLSL\nhighp float sharedValue;\n#endif\nvoid main() {}\n", result);
    }

    [Fact]
    public void MissingAndCyclicSourcesFailAtLoadTime()
    {
        Assert.Throws<KeyNotFoundException>(() => ShaderCodeManager.ExpandIncludes(
                "#include \"missing.txt\"",
                _ => throw new KeyNotFoundException()
            )
        );
        Assert.Throws<InvalidDataException>(() => ShaderCodeManager.ExpandIncludes(
                "#include \"a.txt\"",
                _ => "#include \"a.txt\""
            )
        );
    }

    [Theory]
    [InlineData("#include missing.txt")]
    [InlineData("#include \"\"")]
    [InlineData("#include \"missing.txt\" extra")]
    public void MalformedIncludesFailExplicitly(string source)
    {
        Assert.Throws<InvalidDataException>(() => ShaderCodeManager.ExpandIncludes(source, _ => string.Empty));
    }
}
