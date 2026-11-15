using System.Buffers;

namespace Engine.Audio;

internal readonly record struct AudioPcmBlock(byte[] Data, int Count)
{
    public void Release() => ArrayPool<byte>.Shared.Return(Data);
}
