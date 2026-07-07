using Engine.Graphics;
using Engine.Media;

using EntitySystem.TemplatesDatabase;

namespace Game.Managers;

public static class CharacterSkinsManager
{
    private static readonly List<string> _characterSkinNames = [];

    private static readonly Dictionary<PlayerClass, Model> _playerModels = new();

    private static readonly Dictionary<PlayerClass, Model> _outerClothingModels = new();

    public static readonly List<string> WaitReplyList = [];

    public static ReadOnlyList<string> ReadOnlyCharacterSkinsNames => new(_characterSkinNames);

    public static event Action<string>? CharacterSkinDeleted;

    public static void Initialize()
    {
        Storage.CreateDirectory(GamePaths.CharacterSkins);
    }

    /// <summary>
    /// 是否为Pak资源
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static bool IsBuiltIn(string name)
    {
        return name.StartsWith('$');
    }

    /// <summary>
    /// 是否具有皮肤资源
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static bool HasSkinRes(string name)
    {
        return IsBuiltIn(name) || GetFileName(name, out _);
    }

    /// <summary>
    /// 保存bytes皮肤数据为文件
    /// </summary>
    /// <param name="name"></param>
    /// <param name="skinData"></param>
    public static void SaveSkinToFile(string name, byte[] skinData)
    {
        var path = Path.Combine(GamePaths.CharacterSkins, name);
        using var s = Storage.OpenFile(path, OpenFileMode.CreateOrOpen);
        s.Write(skinData, 0, skinData.Length);
    }

    public static PlayerClass? GetPlayerClass(string name)
    {
        name = name.ToLower();
        if (name.Contains("female") || name.Contains("girl") || name.Contains("woman"))
        {
            return PlayerClass.Female;
        }

        if (name.Contains("male") || name.Contains("boy") || name.Contains("man"))
        {
            return PlayerClass.Male;
        }

        return null;
    }

    /// <summary>
    /// 获取Storage中的皮肤文件路径
    /// </summary>
    /// <param name="name"></param>
    /// <param name="filename"></param>
    /// <returns>皮肤文件的绝对路径</returns>
    public static bool GetFileName(string name, out string filename)
    {
        filename = string.Empty;
        if (IsBuiltIn(name))
        {
            return false;
        }

        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        filename = Storage.CombinePaths(GamePaths.CharacterSkins, name);
        return Storage.FileExists(filename);
    }

    public static string GetDisplayName(string name)
    {
        if (!IsBuiltIn(name))
        {
            return Storage.GetFileNameWithoutExtension(name);
        }

        if (name.Contains("Female"))
        {
            if (name.Contains('1'))
            {
                return "Doris";
            }

            if (name.Contains('2'))
            {
                return "Mabel";
            }

            return name.Contains('3') ? "Ada" : "Shirley";
        }

        if (name.Contains('1'))
        {
            return "Walter";
        }

        if (name.Contains('2'))
        {
            return "Basil";
        }

        return name.Contains('3') ? "Geoffrey" : "Zachary";
    }

    public static DateTime GetCreationDate(string name)
    {
        try
        {
            if (GetFileName(name, out var fileName))
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
    /// 根据名称返回皮肤的Image资源
    /// </summary>
    private static bool GetCharacterSkinImage(string name, out Texture2D? image)
    {
        image = null!;
        try
        {
            if (GetFileName(name, out var fileName))
            {
                //在Storage里面
                if (Storage.FileExists(fileName))
                {
                    using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
                    ValidateCharacterSkin(stream);
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
        var ex = ExternalContentManager.VerifyExternalContentName(name);
        if (ex != null)
        {
            throw ex;
        }

        if (Storage.GetExtension(name) != ".scskin")
        {
            name += ".scskin";
        }

        ValidateCharacterSkin(stream);
        stream.Position = 0L;
        GetFileName(name, out var fileName);
        using var destination = Storage.OpenFile(fileName, OpenFileMode.Create);
        stream.CopyTo(destination);

        return name;
    }

    public static void DeleteCharacterSkin(string name)
    {
        try
        {
            if (!GetFileName(name, out var fileName))
            {
                return;
            }

            Storage.DeleteFile(fileName);
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
        _characterSkinNames.Add("$Male1");
        _characterSkinNames.Add("$Male2");
        _characterSkinNames.Add("$Male3");
        _characterSkinNames.Add("$Male4");
        _characterSkinNames.Add("$Female1");
        _characterSkinNames.Add("$Female2");
        _characterSkinNames.Add("$Female3");
        _characterSkinNames.Add("$Female4");
        foreach (var item in Storage.ListFileNames(GamePaths.CharacterSkins))
        {
            if (Storage.GetExtension(item).ToLower() == ".scskin")
            {
                _characterSkinNames.Add(item);
            }
        }
    }

    public static Model GetPlayerModel(PlayerClass playerClass)
    {
        if (_playerModels.TryGetValue(playerClass, out var value))
        {
            return value;
        }

        var valuesDictionary = playerClass switch
        {
            PlayerClass.Male => DatabaseManager.FindEntityValuesDictionary("MalePlayer", true)!,
            PlayerClass.Female => DatabaseManager.FindEntityValuesDictionary("FemalePlayer", true)!,
            _ => throw new InvalidOperationException("Unknown player class.")
        };

        value = ContentManager.Get<Model>(valuesDictionary.GetValue<ValuesDictionary>("HumanModel")
            .GetValue<string>("ModelName"));
        _playerModels.Add(playerClass, value);

        return value;
    }

    public static Model GetOuterClothingModel(PlayerClass playerClass)
    {
        if (_outerClothingModels.TryGetValue(playerClass, out var value))
        {
            return value;
        }

        var valuesDictionary = playerClass switch
        {
            PlayerClass.Male => DatabaseManager.FindEntityValuesDictionary("MalePlayer", true)!,
            PlayerClass.Female => DatabaseManager.FindEntityValuesDictionary("FemalePlayer", true)!,
            _ => throw new InvalidOperationException("Unknown player class.")
        };

        value = ContentManager.Get<Model>(valuesDictionary.GetValue<ValuesDictionary>("OuterClothingModel")
            .GetValue<string>("ModelName"));
        _outerClothingModels.Add(playerClass, value);

        return value;
    }

    private static void ValidateCharacterSkin(Stream stream)
    {
        var image = Image.Load(stream);
        if (image.Width > 1024 || image.Height > 1024)
        {
            throw new InvalidOperationException(
                $"Character skin is larger than 1024x1024 pixels (size={image.Width}x{image.Height})");
        }

        if (!MathUtils.IsPowerOf2(image.Width) || !MathUtils.IsPowerOf2(image.Height))
        {
            throw new InvalidOperationException(
                $"Character skin does not have power-of-two size (size={image.Width}x{image.Height})");
        }
    }

    public class NetCharacterSkin
    {
        public Dictionary<string, Image> Cache = new();
    }
}
