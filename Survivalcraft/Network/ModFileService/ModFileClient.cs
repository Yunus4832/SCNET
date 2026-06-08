using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

using Newtonsoft.Json;

namespace Game.Network.ModFileService;

public static class ModFileClient
{
    public static BusyDialog BusyDialog = new(string.Empty, string.Empty);

    public static void DownloadModAndJoinServer(string address, IPEndPoint ep, string pwd)
    {
        BusyDialog.LargeMessage = "正在连接至模组服务器";
        BusyDialog.SmallMessage = string.Empty;
        DialogsManager.ShowDialog(null, BusyDialog);
        _ = StartClient(address, ep, pwd);
    }

    public static async Task StartClient(string address, IPEndPoint ep, string pwd)
    {
        using var client = new HttpClient();
        try
        {
            var response = await client.GetAsync(address);
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var modInfoDataList = JsonConvert.DeserializeObject<List<ModInfoData>>(jsonString);
            BusyDialog.LargeMessage = "已获取服务器模组数据，正在下载";
            BusyDialog.SmallMessage = string.Empty;
            if (modInfoDataList != null && Utils.ModInfoListsHaveSameMd5(Utils.GetModInfoData(), modInfoDataList))
            {
                DialogsManager.HideAllDialogs();
                ScreensManager.SwitchScreen("GameLoading", string.Empty, string.Empty, ep, pwd);
            }
            else
            {
                Utils.CacheAllModFile();
                if (modInfoDataList != null && modInfoDataList.Count != 0)
                {
                    for (var i = 0; i < modInfoDataList.Count; i++)
                    {
                        if (Utils.CopyCachedMod(modInfoDataList[i]))
                        {
                            continue;
                        }

                        if (modInfoDataList[i].DownloadThread != 0)
                        {
                            await MultithreadDownloadMod(client, modInfoDataList, i, address,
                                modInfoDataList[i].DownloadThread);
                            continue;
                        }

                        await DownloadMod(client, modInfoDataList, i, address);
                    }
                }

                GameRestarter.RestartGame();
            }
        }
        catch (Exception ex)
        {
            DialogsManager.HideAllDialogs();
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    LanguageControl.Error,
                    ex.Message, LanguageControl.Ok
                )
            );
        }
    }

    public static async Task DownloadMod(
        HttpClient client,
        List<ModInfoData> modInfoDataList,
        int index,
        string serverAddress
    )
    {
        var modInfoData = modInfoDataList[index];
        var address = !string.IsNullOrEmpty(modInfoDataList[index].ModUrl)
            ? modInfoDataList[index].ModUrl
            : serverAddress + modInfoData.ModMd5 + "/";

        await using var responseStream = await client.GetStreamAsync(address);
        await using var fileStream = new FileStream(Path.Combine(Utils.ModFileDirectory, modInfoData.ModName),
            FileMode.Create, FileAccess.Write, FileShare.None, 8192);
        var buffer = new byte[8192];
        long totalRead = 0;
        int read;
        while ((read = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read);
            totalRead += read;
            UpdateDownloadProcess(modInfoDataList, index, totalRead);
        }
    }


    public static async Task MultithreadDownloadMod(
        HttpClient client,
        List<ModInfoData> modInfoDataList,
        int index,
        string serverAddress,
        int maxThread
    )
    {
        var modInfoData = modInfoDataList[index];
        var address = !string.IsNullOrEmpty(modInfoDataList[index].ModUrl)
            ? modInfoDataList[index].ModUrl
            : serverAddress + modInfoData.ModMd5 + "/";

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(address);
        var fileSize = response.Content.Headers.ContentLength ?? 0;

        var partSize = fileSize / maxThread;

        var downloadTasks = new Task[maxThread];
        var downloadByte = new ConcurrentDictionary<int, byte[]>();

        long totalRead = 0;

        for (var i = 0; i < maxThread; i++)
        {
            var partNumber = i;
            downloadTasks[i] = Task.Run(async () =>
            {
                var startByte = partNumber * partSize;
                var endByte = partNumber == maxThread - 1 ? fileSize - 1 : startByte + partSize - 1;
                var request1 = new HttpRequestMessage(HttpMethod.Get, address);
                request1.Headers.Range = new RangeHeaderValue(startByte, endByte);
                var partResponse = await client.SendAsync(request1, HttpCompletionOption.ResponseHeadersRead);
                await using var partStream = await partResponse.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                var buffer = new byte[8192];
                int read;
                while ((read = await partStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await memoryStream.WriteAsync(buffer, 0, read);
                    Interlocked.Add(ref totalRead, read);
                    UpdateDownloadProcess(modInfoDataList, index, totalRead);
                }

                downloadByte[partNumber] = memoryStream.ToArray();
            });
        }

        await Task.WhenAll(downloadTasks);
        await using var fileStream = new FileStream(Path.Combine(Utils.ModFileDirectory, modInfoData.ModName),
            FileMode.Create, FileAccess.Write, FileShare.None);
        for (var i = 0; i < downloadByte.Count; i++)
        {
            fileStream.Write(downloadByte[i], 0, downloadByte[i].Length);
        }
    }

    public static void UpdateDownloadProcess(
        List<ModInfoData> modInfoDataList,
        int index,
        long downloadedByteCount
    )
    {
        var downloadedM = downloadedByteCount / (1024.0 * 1024.0);
        var fileSizeM = modInfoDataList[index].ModSize / (1024.0 * 1024.0);

        BusyDialog.LargeMessage =
            $"正在下载模组{modInfoDataList[index].ModName}" +
            $"({index}/{modInfoDataList.Count})";
        BusyDialog.SmallMessage =
            $"{downloadedM / fileSizeM * 100:0.00}% - " +
            $"({downloadedM:0.00}MB/{fileSizeM:0.00}MB)";
    }
}
