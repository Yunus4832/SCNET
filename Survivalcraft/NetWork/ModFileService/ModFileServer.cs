using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace Game.NetWork.ModFileService;

public static class ModFileServer
{
    public static HttpListener Listener = new();

    public static List<ModInfoData> ServerModInfoList = Utils.GetModInfoData();

    public static string ServerModInfoData = JsonConvert.SerializeObject(ServerModInfoList);

    public static void StartServer(string address)
    {
        Listener.Start();
        Listener.Prefixes.Add(address);
        foreach (var modInfo in ServerModInfoList)
        {
            Listener.Prefixes.Add(address + modInfo.ModMd5 + "/");
        }

        ThreadPool.QueueUserWorkItem(_ => { Listen(address); });
    }

    private static void Listen(string address)
    {
        while (Listener.IsListening)
        {
            ThreadPool.QueueUserWorkItem(c =>
                {
                    if (c is not HttpListenerContext context)
                    {
                        return;
                    }

                    try
                    {
                        var requestUrl = context.Request.Url?.AbsolutePath;
                        if (requestUrl is "" or "/")
                        {
                            var buffer = Encoding.UTF8.GetBytes(ServerModInfoData);
                            context.Response.ContentLength64 = buffer.Length;
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        }
                        else
                        {
                            if (requestUrl == null)
                            {
                                return;
                            }

                            var modMd5 = requestUrl.Split('/')[1];
                            var modInfo = ServerModInfoList.FirstOrDefault(m => m.ModMd5 == modMd5);
                            if (modInfo != null)
                            {
                                var filePath = Path.Combine(Utils.ModFileDirectory, modInfo.ModName);
                                if (File.Exists(filePath))
                                {
                                    try
                                    {
                                        context.Response.ContentType = "application/zip";
                                        var encodedFileName = Uri.EscapeDataString(modInfo.ModName);
                                        context.Response.AddHeader("Content-Disposition",
                                            $"attachment; filename=\"{encodedFileName}\"; filename*=UTF-8''{encodedFileName}");
                                        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                                        var buffer = new byte[1024 * 16];
                                        int read;
                                        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                                        {
                                            context.Response.OutputStream.Write(buffer, 0, read);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error($"发送文件时发生异常: {ex.Message}");
                                        context.Response.StatusCode = 500;
                                    }
                                }
                                else
                                {
                                    context.Response.StatusCode = 404;
                                    Log.Error("未找到文件");
                                }
                            }
                            else
                            {
                                context.Response.StatusCode = 404;
                                Log.Error("未找到文件");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                    }
                    finally
                    {
                        context.Response.OutputStream.Close();
                        context.Response.Close();
                    }
                },
                Listener.GetContext()
            );
        }
    }
}
