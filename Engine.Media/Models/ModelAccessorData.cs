using System.Buffers.Binary;

using Engine.Core.Mathematics;

namespace Engine.Media.Models;

/// <summary>Dense, tightly packed little-endian components; sparse overrides and matrix padding are resolved on import.</summary>
public sealed class ModelAccessorData : ModelElementData
{
    public ModelComponentType ComponentType;

    public string Dimensions = "SCALAR";

    public int Count;

    public bool Normalized;

    public byte[] Data = [];

    public double[] Minimum = [];

    public double[] Maximum = [];

    public int ComponentCount => Dimensions switch
    {
        "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" or "MAT2" => 4,
        "MAT3" => 9, "MAT4" => 16, _ => throw new InvalidDataException("Unsupported accessor dimensions.")
    };

    public int ComponentSize => ComponentType switch
    {
        ModelComponentType.SignedByte or ModelComponentType.UnsignedByte => 1,
        ModelComponentType.SignedShort or ModelComponentType.UnsignedShort => 2,
        ModelComponentType.UnsignedInt or ModelComponentType.Float => 4,
        _ => throw new InvalidDataException("Unsupported accessor component type.")
    };

    public double GetComponent(int index, int component)
    {
        if ((uint)index >= Count || (uint)component >= ComponentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var bytes = Data.AsSpan(checked((index * ComponentCount + component) * ComponentSize), ComponentSize);
        return ComponentType switch
        {
            ModelComponentType.Float => BinaryPrimitives.ReadSingleLittleEndian(bytes),
            ModelComponentType.UnsignedInt => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            ModelComponentType.UnsignedByte => Normalized ? bytes[0] / 255.0 : bytes[0],
            ModelComponentType.SignedByte => Normalized ? Math.Max((sbyte)bytes[0] / 127.0, -1) : (sbyte)bytes[0],
            ModelComponentType.UnsignedShort => Normalized
                ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) / 65535.0
                : BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            ModelComponentType.SignedShort => Normalized
                ? Math.Max(BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32767.0, -1)
                : BinaryPrimitives.ReadInt16LittleEndian(bytes),
            _ => throw new InvalidDataException("Unsupported accessor component type.")
        };
    }

    public Vector3 GetVector3(int index)
    {
        return new Vector3((float)GetComponent(index, 0), (float)GetComponent(index, 1), (float)GetComponent(index, 2));
    }
}
