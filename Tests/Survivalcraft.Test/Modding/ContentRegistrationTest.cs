using Game.Modding.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentRegistrationTest
{
    [Fact]
    public void SharedRegistrationIsImmutableAndOpensIndependentReadCursors()
    {
        var source = new byte[] { 1, 2, 3 };
        var content = new ContentRegistration("shared.bin", source);
        source[0] = 9;
        using var first = content.OpenRead();
        using var second = content.OpenRead();
        Assert.False(first.CanWrite);
        Assert.Throws<NotSupportedException>(() => first.WriteByte(8));
        Assert.Throws<UnauthorizedAccessException>(() => first.GetBuffer());
        Assert.Equal(1, first.ReadByte());
        Assert.Equal(2, first.ReadByte());
        Assert.Equal(1, second.ReadByte());
        var exported = content.CopyBytes();
        exported[0] = 7;
        using var third = content.OpenRead();
        Assert.Equal(1, third.ReadByte());
    }
}
