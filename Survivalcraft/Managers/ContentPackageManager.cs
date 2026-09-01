namespace Game.Managers;

public static class ContentPackageManager
{
    private const string _typeName = nameof(ContentPackageManager);

    public static string GetTypeDescription(ContentType type)
    {
        return type switch
        {
            ContentType.World => LanguageManager.Get(_typeName, "World"),
            ContentType.BlocksTexture => LanguageManager.Get(_typeName, "Blocks Texture"),
            ContentType.CharacterSkin => LanguageManager.Get(_typeName, "Character Skin"),
            ContentType.FurniturePack => LanguageManager.Get(_typeName, "Furniture Pack"),
            _ => string.Empty
        };
    }

    public static Exception? VerifyContentName(string name)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            return new InvalidOperationException(LanguageManager.Get(_typeName, 1));
        }

        return trimmedName.Length > 50
            ? new InvalidOperationException(LanguageManager.Get(_typeName, 2))
            : null;
    }

    public static void DeleteContent(ContentType type, string name)
    {
        switch (type)
        {
            case ContentType.World:
                WorldsManager.DeleteWorld(name);
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
                throw new InvalidOperationException(LanguageManager.Get(_typeName, 4));
        }
    }

}
