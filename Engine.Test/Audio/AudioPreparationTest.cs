using Engine.Audio;

namespace Engine.Test.Audio;

public sealed class AudioPreparationTest
{
    [Fact]
    public async Task CapacityIsBoundedAndShutdownDrainsRejectedWork()
    {
        var preparation = new AudioPreparation();
        using var gate = new ManualResetEventSlim();
        var accepted = new List<Task<int>>();
        try
        {
            for (var i = 0; i < 64; i++)
            {
                Assert.True(
                    preparation.TrySubmit(
                        () =>
                        {
                            gate.Wait();
                            return 7;
                        },
                        default,
                        out var task
                    )
                );
                accepted.Add(task!);
            }

            Assert.False(preparation.TrySubmit(() => 8, default, out _));
            preparation.Stop();
            gate.Set();
            await preparation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            foreach (var task in accepted)
            {
                try { await task; }
                catch (InvalidOperationException) { }

                Assert.True(task.IsCompleted);
            }

            Assert.False(preparation.TrySubmit(() => 9, default, out _));
        }
        finally
        {
            gate.Set();
            preparation.Stop();
            await preparation.Completion;
        }
    }
}
