using System.Reflection;

using Engine.Windowing;

using Game;

namespace Survivalcraft.Windows;

public class Starter
{

    public static void Main(string[] args)
    {
        Window.IconStream = LoadWindowIcon();
        Program.Main(args);
    }

    /// <summary>
    /// 加载窗口图标
    /// </summary>
    private static Stream LoadWindowIcon()
    {
        var iconStream = typeof(Starter).GetTypeInfo().Assembly.GetManifestResourceStream("Starter.Resources.icon.png");
        return iconStream ?? throw new InvalidOperationException("Survivalcraft icon not found");
    }
}
