using System.Text;

using Game.Managers;

namespace Survivalcraft.Test.Content;

public sealed class ContentInfoCursorTest
{
    [Fact]
    public async Task ConcurrentReadCursorsAreIndependentAndCannotMutateTheAsset()
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 65536) + "end");
        using var content = new ContentInfo("test/data.txt");
        content.SetContentStream(new MemoryStream(input));
        input[0] = (byte)'z';
        var factory = content.CreateStreamFactory();
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
                {
                    using var stream = factory();
                    Assert.False(stream.CanWrite);
                    Assert.Throws<UnauthorizedAccessException>(() => ((MemoryStream)stream).GetBuffer());
                    using var text = new StreamReader(stream);
                    return text.ReadToEnd();
                }
            )
        );
        var values = await Task.WhenAll(readers);
        Assert.All(values, value => Assert.Equal(new string('a', 65536) + "end", value));
    }

    [Fact]
    public void RetirementPreventsNewBorrowsButDoesNotDestroyAnOwnedPreparationInput()
    {
        var content = new ContentInfo("test/data.bin");
        content.SetContentStream(new MemoryStream([1, 2, 3]));
        var factory = content.CreateStreamFactory();
        content.Dispose();
        Assert.Throws<ObjectDisposedException>(() => content.Duplicate());
        using var borrowed = factory();
        Assert.Equal(1, borrowed.ReadByte());
        Assert.True(content.IsDisposed);
    }
}
