namespace Game.Network.ModFileService;

public class ModInfoData
{
    public int DownloadThread = 0;

    public string ModMd5 = string.Empty;

    public string ModName = string.Empty;

    public long ModSize;

    public string ModUrl = string.Empty;
}

public static class Utils
{
    public static string ModFileDirectory = Storage.GetSystemPath(GamePaths.Mods);

    public static string CacheModDirectory = Storage.GetSystemPath(GamePaths.ModCache);

    public static List<ModInfoData> GetModInfoData()
    {
        if (!Directory.Exists(ModFileDirectory))
        {
            Directory.CreateDirectory(ModFileDirectory);
        }

        return Directory.EnumerateFiles(ModFileDirectory, ModPackage.SearchPattern, SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var fileInfo = new FileInfo(path);
                return new ModInfoData
                {
                    ModName = fileInfo.Name,
                    ModMd5 = GetModMd5(fileInfo),
                    ModSize = fileInfo.Length
                };
            })
            .ToList();
    }

    public static bool ModInfoListsHaveSameMd5(List<ModInfoData> list1, List<ModInfoData> list2)
    {
        if (list1.Count != list2.Count)
        {
            return false;
        }

        var sortedList1 = list1.OrderBy(mod => mod.ModMd5).ToList();
        var sortedList2 = list2.OrderBy(mod => mod.ModMd5).ToList();
        return !sortedList1.Where((t, i) => t.ModMd5 != sortedList2[i].ModMd5).Any();
    }

    public static string GetModMd5(FileInfo fileInfo)
    {
        using var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var data = new byte[fileStream.Length];
        fileStream.ReadExactly(data, 0, data.Length);
        return HashUtils.ComputeMd5(data);
    }

    public static void RemoveAllModFile()
    {
        if (Directory.Exists(ModFileDirectory))
        {
            Directory.Delete(ModFileDirectory, true);
        }

        Directory.CreateDirectory(ModFileDirectory);
    }

    public static void CacheAllModFile()
    {
        if (!Directory.Exists(CacheModDirectory))
        {
            Directory.CreateDirectory(CacheModDirectory);
        }

        if (!Directory.Exists(ModFileDirectory))
        {
            Directory.CreateDirectory(ModFileDirectory);
        }

        var files = Directory.GetFiles(ModFileDirectory, ModPackage.SearchPattern, SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var destinationPath = Path.Combine(CacheModDirectory, fileName);
            File.Copy(file, destinationPath, true);
        }

        RemoveAllModFile();
    }

    public static bool CopyCachedMod(ModInfoData modInfoData)
    {
        foreach (var file in Directory.GetFiles(CacheModDirectory, ModPackage.SearchPattern, SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) != modInfoData.ModName)
            {
                continue;
            }

            if (GetModMd5(new FileInfo(file)) != modInfoData.ModMd5)
            {
                continue;
            }

            var fileName = Path.GetFileName(file);
            var destinationPath = Path.Combine(ModFileDirectory, fileName);
            File.Copy(file, destinationPath, true);
            return true;
        }

        return false;
    }

    public static void HandleModDataValidationMessage(string message)
    {
        if (message.Contains("资源包") && message.Contains("校验不通过"))
        {
            var serverStartIndex = message.IndexOf("[服务端]", StringComparison.Ordinal) + "[服务端]".Length;
            var serverEndIndex = message.IndexOf("[客户端]", StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(message.Substring(serverStartIndex, serverEndIndex - serverStartIndex)))
            {
                var clientStartIndex = message.IndexOf("[客户端]", StringComparison.Ordinal) + "[客户端]".Length;
                var clientEndIndex = message.Length;
                if (!string.IsNullOrWhiteSpace(message.Substring(clientStartIndex, clientEndIndex - clientStartIndex)))
                {
                    RestartGameDueToInvalidModData();
                }
            }
        }

        if (message.Contains("The resource package") && message.Contains("verification failed"))
        {
            var serverStartIndex = message.IndexOf("[Server]", StringComparison.Ordinal) + "[Server]".Length;
            var serverEndIndex = message.IndexOf("[Client]", StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(message.Substring(serverStartIndex, serverEndIndex - serverStartIndex)))
            {
                return;
            }

            var clientStartIndex = message.IndexOf("[Client]", StringComparison.Ordinal) + "[Client]".Length;
            var clientEndIndex = message.Length;
            if (!string.IsNullOrWhiteSpace(message.Substring(clientStartIndex, clientEndIndex - clientStartIndex)))
            {
                RestartGameDueToInvalidModData();
            }
        }
        else if (message.Contains("Mod验证不通过。请去掉多余的mod或添加服务器所需要的mod"))
        {
            if (Directory.GetFiles(ModFileDirectory).Length > 0)
            {
                RestartGameDueToInvalidModData();
            }
        }
    }

    public static void RestartGameDueToInvalidModData()
    {
        CacheAllModFile();
        GameExitManager.RequestRestart();
    }
}
