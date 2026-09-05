using Silk.NET.SDL;

namespace Engine.Windowing;

public sealed class SdlClipboardBackend : IClipboardBackend
{
    public string ReadText()
    {
        using var sdl = Sdl.GetApi();
        return sdl.GetClipboardTextS();
    }

    public void WriteText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var sdl = Sdl.GetApi();
        if (sdl.SetClipboardText(text) < 0)
        {
            throw new InvalidOperationException($"Could not set clipboard text: {sdl.GetErrorS()}");
        }
    }
}
