#if DESKTOP
using Clipboard;

namespace Game.Managers;

public static class ClipboardManager
{
    public static string ClipboardString
    {
        get => SystemClipboard.Instance.Read();
        set
        {
            try
            {
                SystemClipboard.Instance.Write(value);
            }
            catch (Exception e)
            {
                DialogsManager.Alert("复制文本失败：" + e.Message);
            }
        }
    }
}

#endif
