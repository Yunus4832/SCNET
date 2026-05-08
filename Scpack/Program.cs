using System.Text;

namespace Scpack;

internal abstract class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var path = Directory.GetCurrentDirectory();
        path = Path.Combine(path, "Content.zip");
        if (File.Exists(path))
        {
            StrengtheningFile(path);
        }
        else
        {
            Console.WriteLine("Content.zip文件不存在：" + path);
        }
    }

    private static void StrengtheningFile(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine(path + "文件找不到");
            return;
        }

        Console.WriteLine("正在混淆文件：" + path);
        const string headingCode = "修改联机请获得联机开发组授权，否则小心出名！";
        const string headingCode2 = "再乱改就跑路，谁也别想玩！";
        Stream stream = File.Open(path, FileMode.Open);
        var buff = new byte[stream.Length];
        stream.ReadExactly(buff, 0, buff.Length);
        var hc = Encoding.UTF8.GetBytes(headingCode);
        var decipher = !hc.Where((t, i) => t != buff[i]).Any();

        var hc2 = Encoding.UTF8.GetBytes(headingCode2);
        var decipher2 = !hc2.Where((t, i) => t != buff[i]).Any();

        if (decipher || decipher2)
        {
            Console.WriteLine(path + "文件已经混淆过了");
            return;
        }

        var buff2 = new byte[buff.Length + hc2.Length];
        var k = 0;
        var l = hc2.Length;
        for (var i = 0; i < hc2.Length; i++)
        {
            buff2[i] = hc2[i];
        }

        for (var i = 0; i < buff.Length; i++)
        {
            if (i % 2 == 0)
            {
                buff2[k + l] = buff[i];
                k++;
            }
        }

        k = 0;
        l = hc2.Length + (buff.Length + 1) / 2;
        for (var i = 0; i < buff.Length; i++)
        {
            if (i % 2 != 0)
            {
                buff2[k + l] = buff[i];
                k++;
            }
        }

        var newPath = path.Substring(0, path.LastIndexOf('.')) + ".scpak";
        var fileStream = new FileStream(newPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        fileStream.Write(buff2, 0, buff2.Length);
        fileStream.Flush();
        stream.Dispose();
        fileStream.Dispose();
        Console.WriteLine("文件混淆成功：" + newPath);
        File.Delete(path);
        Console.WriteLine("文件删除成功：" + path);
    }
}
