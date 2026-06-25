namespace Game.Managers;

public static class ClipboardManager
{
    private static Func<string>? _reader;
    private static Action<string>? _writer;

    public static string ClipboardString
    {
        get
        {
            try
            {
                if (_reader is not null)
                {
                    return _reader();
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
                if (_writer is null)
                {
                    Log.Warning("No clipboard writer registered.");
                    return;
                }

                _writer(value);
            }
            catch (Exception ex)
            {
                DialogsManager.Alert("复制文本失败：" + ex.Message);
            }
        }
    }

    public static void RegisterClipboard(Func<string> reader, Action<string> writer)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }
}
