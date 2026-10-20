namespace Game.Network;

internal static class ProtocolEnum
{
    public static byte Encode<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown game enum value.");
        }

        return Convert.ToByte(value);
    }

    public static TEnum Decode<TEnum>(byte code) where TEnum : struct, Enum =>
        TryDecode(code, out TEnum value)
            ? value
            : throw new InvalidDataException($"Unknown {typeof(TEnum).Name} value: {code}.");

    public static bool TryDecode<TEnum>(byte code, out TEnum value) where TEnum : struct, Enum
    {
        value = (TEnum)Enum.ToObject(typeof(TEnum), code);
        return Enum.IsDefined(value);
    }
}
