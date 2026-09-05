namespace Engine.Windowing;

public interface IClipboardBackend
{
    string ReadText();

    void WriteText(string text);
}
