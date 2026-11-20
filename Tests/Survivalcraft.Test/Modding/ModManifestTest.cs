using System.Text.Json;

using Game.Modding;

namespace Survivalcraft.Test.Modding;

public class ModManifestTest
{
    [Theory]
    [InlineData("\"side\":\"client\"")]
    [InlineData("\"entrypoints\":{\"server\":\"Example.Server, Example\"}")]
    public void ProcessSpecificEntrypointsAreRejected(string field)
    {
        var json = "{\"id\":\"example.test\",\"name\":\"Test\",\"version\":\"1.0.0\"," + field + "}";
        Assert.Throws<JsonException>(() => ModManifest.Parse(json));
    }

    [Fact]
    public void ParseReadsManifestPropertiesAndDependencies()
    {
        const string json = """
                            {
                              "id": "example.content",
                              "name": "Example Content",
                              "version": "1.2.0",
                              "entrypoint": "Example.ModEntry, Example",
                              "dependencies": [
                                { "id": "game.content", "minimumVersion": "1.0.0" }
                              ]
                            }
                            """;

        var manifest = ModManifest.Parse(json);

        Assert.Equal(new ModId("example.content"), manifest.ModId);
        Assert.Equal("Example.ModEntry, Example", manifest.Entrypoint);
        Assert.Equal("game.content", Assert.Single(manifest.RequiredDependencies).Id);
    }

    [Fact]
    public void ParseRejectsSelfDependency()
    {
        const string json = """
                            {
                              "id": "example.content",
                              "name": "Example Content",
                              "version": "1.0.0",
                              "dependencies": [
                                { "id": "example.content" }
                              ]
                            }
                            """;

        var exception = Assert.Throws<InvalidOperationException>(() => ModManifest.Parse(json));

        Assert.Contains("cannot depend on itself", exception.Message);
    }
}
