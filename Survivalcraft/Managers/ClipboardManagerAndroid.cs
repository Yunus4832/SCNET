#if ANDROID

namespace Game.Managers;

public static class ClipboardManager
{
    public static string ClipboardString
    {
        get
        {
            if (Window.ActivityInstance.GetSystemService("clipboard") is Android.Content.ClipboardManager
                clipboardManager)
            {
                return clipboardManager.Text ?? string.Empty;
            }

            return string.Empty;
        }
        set
        {
            if (Window.ActivityInstance.GetSystemService("clipboard") is Android.Content.ClipboardManager
                clipboardManager)
            {
                clipboardManager.Text = value;
            }
        }
    }
}

#endif
