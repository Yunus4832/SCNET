using System.Text.Json;

using Content.Packaging;

using Game.Modding;

namespace Survivalcraft.Test.Modding;

public class ModManifestTest
{
    [Theory]
    [InlineData("")]
    [InlineData(",\"scope\":null")]
    [InlineData(",\"scope\":1")]
    [InlineData(",\"scope\":\"unknown\"")]
    public void ParseRequiresExplicitScope(string declaration)
    {
        var json = """
                   {"id":"example.test","name":"Test","version":"1.0.0","canRemoveFromWorld":false
                   """ +
                   declaration +
                   "}";

        Assert.Throws<JsonException>(() => ModManifest.Parse(json));
    }

    [Theory]
    [InlineData("\"side\":\"client\"")]
    [InlineData("\"entrypoints\":{\"server\":\"Example.Server, Example\"}")]
    public void ProcessSpecificEntrypointsAreRejected(string field)
    {
        var json = """
                   {"id":"example.test","name":"Test","version":"1.0.0","scope":"both","canRemoveFromWorld":false,
                   """ +
                   field +
                   "}";
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
                              "scope": "both",
                              "canRemoveFromWorld": true,
                              "entrypoint": "Example.ModEntry, Example",
                              "dependencies": [
                                { "id": "game.content", "minimumVersion": "1.0.0" }
                              ]
                            }
                            """;

        var manifest = ModManifest.Parse(json);

        Assert.Equal(new ModId("example.content"), manifest.ModId);
        Assert.Equal(ModScope.Both, manifest.Scope);
        Assert.Equal("Example.ModEntry, Example", manifest.Entrypoint);
        Assert.True(manifest.CanRemoveFromWorld);
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
                              "scope": "both",
                              "canRemoveFromWorld": false,
                              "dependencies": [
                                { "id": "example.content" }
                              ]
                            }
                            """;

        var exception = Assert.Throws<InvalidOperationException>(() => ModManifest.Parse(json));

        Assert.Contains("cannot depend on itself", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"canRemoveFromWorld\":null")]
    [InlineData(",\"canRemoveFromWorld\":\"true\"")]
    [InlineData(",\"canRemoveFromWorld\":1")]
    public void ParseRequiresExplicitBooleanRemovalDeclaration(string declaration)
    {
        var json = """
                   {"id":"example.test","name":"Test","version":"1.0.0","scope":"both"
                   """ +
                   declaration +
                   "}";

        Assert.Throws<JsonException>(() => ModManifest.Parse(json));
    }
}
