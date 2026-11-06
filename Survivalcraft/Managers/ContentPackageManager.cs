using Game.Application;
using Game.Content;

namespace Game.Managers;

public static class ContentPackageManager
{
    private const string _typeName = nameof(ContentPackageManager);

    public static string GetTypeDescription(ContentType type)
    {
        return type switch
        {
            ContentType.World => Localization.ClientLanguage.Catalog.Get(_typeName, "World"),
            ContentType.BlocksTexture => Localization.ClientLanguage.Catalog.Get(_typeName, "Blocks Texture"),
            ContentType.CharacterSkin => Localization.ClientLanguage.Catalog.Get(_typeName, "Character Skin"),
            ContentType.FurniturePack => Localization.ClientLanguage.Catalog.Get(_typeName, "Furniture Pack"),
            _ => string.Empty
        };
    }

    public static void DeleteContent(ContentType type, string name)
    {
        switch (type)
        {
            case ContentType.World:
                ClientApplication.Worlds.DeleteWorld(name);
                break;
            case ContentType.BlocksTexture:
                BlocksTexturesManager.DeleteBlocksTexture(name);
                break;
            case ContentType.CharacterSkin:
                CharacterSkinsManager.DeleteCharacterSkin(name);
                break;
            case ContentType.FurniturePack:
                FurniturePacksManager.DeleteFurniturePack(name);
                break;
            default:
                throw new InvalidOperationException(Localization.ClientLanguage.Catalog.Get(_typeName, 4));
        }
    }
}
