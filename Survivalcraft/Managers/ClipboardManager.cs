namespace Game.Managers;

public static class ClipboardManager
{
    private static IClipboardBackend? _backend;

    public static string ClipboardString
    {
        get
        {
            try
            {
                if (_backend is not null)
                {
                    return _backend.ReadText();
                }

                Log.Warning("No clipboard reader registered.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning(ExceptionManager.MakeFullErrorMessage("Could not read clipboard.", ex));
                return string.Empty;
            }
        }
        set
        {
            try
            {
                if (_backend is null)
                {
                    Log.Warning("No clipboard writer registered.");
                    return;
                }

                _backend.WriteText(value);
            }
            catch (Exception ex)
            {
                DialogsManager.Alert("复制文本失败：" + ex.Message);
            }
        }
    }

    public static void RegisterBackend(IClipboardBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }
}
