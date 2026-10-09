using Game.Managers;
using Game.Modding;
using Game.Modding.Content;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class ContentCatalogLanguageTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ModTranslationDoesNotReplaceBuiltInLanguageName(bool modFirst, bool modProvidesName)
    {
        var extensions = new ExtensionRegistry();
        var gameId = new ResourceId(new ModId("game"), "lang/en-US.json");
        var modId = new ResourceId(new ModId("example.mod"), "lang/en-US.json");
        var modOnlyLanguageId = new ResourceId(new ModId("example.mod"), "lang/fr-FR.json");
        var gameBytes = """{"Language":{"Name":"English"}}"""u8.ToArray();
        var modBytes = modProvidesName
            ? """{"Language":{"Name":"Mod English"}}"""u8.ToArray()
            : """{"Blocks":{"ExampleBlock:0":{"DisplayName":"Example"}}}"""u8.ToArray();
        var registry = extensions.GetRegistry<ContentRegistration>(ContentExtensions.RegistryName);

        if (modFirst)
        {
            registry.Register(modId.Namespace, modId, new ContentRegistration("lang/en-US.json", modBytes));
            registry.Register(gameId.Namespace, gameId, new ContentRegistration("lang/en-US.json", gameBytes));
        }
        else
        {
            registry.Register(gameId.Namespace, gameId, new ContentRegistration("lang/en-US.json", gameBytes));
            registry.Register(modId.Namespace, modId, new ContentRegistration("lang/en-US.json", modBytes));
        }

        registry.Register(modOnlyLanguageId.Namespace, modOnlyLanguageId,
            new ContentRegistration("lang/fr-FR.json", """{"Language":{"Name":"Français"}}"""u8.ToArray()));

        var catalog = ContentCatalog.Compile(extensions);
        Assert.Equal(["en-US"], catalog.LanguageTypes);
        catalog.InitializeLanguage("fr-FR");

        Assert.Equal("en-US", LanguageManager.CurrentLanguage);
        Assert.Equal("English", LanguageManager.GetLanguageDisplayName("en-US"));
    }
}
