using Engine.Core;

using Game.Protocol.Blocks;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteSignBlockPackage(SignBlockPackage message, ProtocolWriter writer)
    {
        if (message.Colors.Count > byte.MaxValue || message.Lines.Count > byte.MaxValue) { throw new ArgumentException("Too many sign lines or colors."); }
        writer.Write(message.Point); writer.Write(message.Url); writer.Write((byte)message.Colors.Count);
        foreach (var color in message.Colors) { writer.Write(color); }
        writer.Write((byte)message.Lines.Count);
        foreach (var line in message.Lines) { writer.Write(line); }
    }
    private static SignBlockPackage ReadSignBlockPackage(ProtocolReader reader)
    {
        var point = reader.ReadPoint3(); var url = reader.ReadString();
        var colors = new Color[reader.ReadByte()];
        for (var i = 0; i < colors.Length; i++) { colors[i] = reader.ReadColor(); }
        var lines = new string[reader.ReadByte()];
        for (var i = 0; i < lines.Length; i++) { lines[i] = reader.ReadString(); }
        return new SignBlockPackage { Point = point, Url = url, Colors = colors, Lines = lines };
    }
}
