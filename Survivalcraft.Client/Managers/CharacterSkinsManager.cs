using Engine.Graphics;
using Engine.Media.Images;

using EntitySystem.TemplatesDatabase;

using Game.Application;

namespace Game.Managers;

public static class CharacterSkinsManager
{
    private static readonly List<string> _characterSkinNames = [];

    private static readonly Dictionary<CharacterSex, Model> _playerModels = new();

    private static readonly Dictionary<CharacterSex, Model> _outerClothingModels = new();

    public static ReadOnlyList<string> ReadOnlyCharacterSkinsNames => new(_characterSkinNames);

    public static event Action<string>? CharacterSkinDeleted;

    public static Content.CharacterSkinStore CreateStore() => new(ClientApplication.Paths.CharacterSkins);

    public static void Initialize()
    {
        Storage.CreateDirectory(ClientApplication.Paths.CharacterSkins);
    }

    /// <summary>
    ///     是否为Pak资源
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static bool IsBuiltIn(string name)
    {
        return name.StartsWith('$');
    }

    public static CharacterSex? GetCharacterSex(string name)
    {
        name = name.ToLower();
        if (name.Contains("female") || name.Contains("girl") || name.Contains("woman"))
        {
            return CharacterSex.Female;
        }

        if (name.Contains("male") || name.Contains("boy") || name.Contains("man"))
        {
            return CharacterSex.Male;
        }

        return null;
    }

    private static string GetAssetPath(string name)
    {
        return CreateStore().TryGetFileName(name, out var path)
            ? path
            : throw new InvalidOperationException($"Skin '{name}' does not exist.");
    }

    public static string GetDisplayName(string name)
    {
        if (!IsBuiltIn(name))
        {
            return ContentAssetStore.GetDisplayName(
                ClientApplication.Paths.CharacterSkins,
                name,
                Storage.GetExtension(GetAssetPath(name))
            );
        }

        return BuiltInCharacterSkins.GetDefaultName(name);
    }

    public static DateTime GetCreationDate(string name)
    {
        try
        {
            if (CreateStore().TryGetFileName(name, out var fileName))
            {
                return Storage.GetFileLastWriteTime(fileName);
            }
        }
        catch
        {
            // ignored
        }

        return new DateTime(2000, 1, 1);
    }

    /// <summary>
    ///     根据名称返回皮肤的Image资源
    /// </summary>
    private static bool GetCharacterSkinImage(string name, out Texture2D? image)
    {
        image = null!;
        try
        {
            if (CreateStore().TryGetFileName(name, out var fileName))
            {
                //在Storage里面
                if (Storage.FileExists(fileName))
                {
                    using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
                    Content.CharacterSkinStore.Validate(stream);
                    stream.Position = 0L;
                    image = Texture2D.Load(stream);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                image = ContentManager.Get<Texture2D>("Textures/Creatures/Human" + name[1..].Replace(" ", ""));
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Texture2D? LoadTexture(string name, bool throwIfNull = true)
    {
        if (!GetCharacterSkinImage(name, out var image) && throwIfNull)
        {
            throw new InvalidOperationException("ChactacterSkin not found");
        }

        return image;
    }

    public static string ImportCharacterSkin(string name, Stream stream)
    {
        var ex = ContentNameRules.Verify(name);
        if (ex != null)
        {
            throw ex;
        }

        return ContentAssetStore.Install(
            ClientApplication.Paths.CharacterSkins,
            null,
            Storage.GetFileNameWithoutExtension(name),
            stream,
            Content.CharacterSkinStore.Validate
        );
    }

    public static string ReplaceCharacterSkin(string assetKey, string displayName, Stream stream) =>
        ContentAssetStore.Replace(
            ClientApplication.Paths.CharacterSkins,
            null,
            assetKey,
            displayName,
            stream,
            Content.CharacterSkinStore.Validate
        );

    public static void DeleteCharacterSkin(string name)
    {
        try
        {
            if (!CreateStore().TryGetFileName(name, out var fileName))
            {
                return;
            }

            ContentAssetStore.Delete(ClientApplication.Paths.CharacterSkins, name, Storage.GetExtension(fileName));
            CharacterSkinDeleted?.Invoke(name);
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser($"Unable to delete character skin \"{name}\"", e);
        }
    }

    public static void UpdateCharacterSkinsList()
    {
        _characterSkinNames.Clear();
        _characterSkinNames.AddRange(BuiltInCharacterSkins.Definitions.Select(definition => definition.Name));
        foreach (var item in Storage.ListFileNames(ClientApplication.Paths.CharacterSkins))
        {
            if (ContentAssetStore.IsImageComplete(ClientApplication.Paths.CharacterSkins, item))
            {
                _characterSkinNames.Add(Storage.GetFileNameWithoutExtension(item));
            }
        }
    }

    public static Model GetCharacterModel(CharacterSex characterSex)
    {
        if (_playerModels.TryGetValue(characterSex, out var value))
        {
            return value;
        }

        var valuesDictionary = characterSex switch
        {
            CharacterSex.Male => DatabaseManager.FindEntityValuesDictionary("MaleCharacter", true)!,
            CharacterSex.Female => DatabaseManager.FindEntityValuesDictionary("FemaleCharacter", true)!,
            _ => throw new InvalidOperationException("Unknown player class.")
        };

        value = ContentManager.Get<Model>(
            valuesDictionary.GetValue<ValuesDictionary>("HumanModel")
                .GetValue<string>("ModelName")
        );
        _playerModels.Add(characterSex, value);

        return value;
    }

    public static Model GetOuterClothingModel(CharacterSex characterSex)
    {
        if (_outerClothingModels.TryGetValue(characterSex, out var value))
        {
            return value;
        }

        var valuesDictionary = characterSex switch
        {
            CharacterSex.Male => DatabaseManager.FindEntityValuesDictionary("MaleCharacter", true)!,
            CharacterSex.Female => DatabaseManager.FindEntityValuesDictionary("FemaleCharacter", true)!,
            _ => throw new InvalidOperationException("Unknown player class.")
        };

        value = ContentManager.Get<Model>(
            valuesDictionary.GetValue<ValuesDictionary>("OuterClothingModel")
                .GetValue<string>("ModelName")
        );
        _outerClothingModels.Add(characterSex, value);

        return value;
    }

    public class NetCharacterSkin
    {
        public Dictionary<string, Image> Cache = new();
    }
}
