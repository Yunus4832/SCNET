using Engine.Windowing;

namespace Engine.Test.Windowing;

public sealed class WindowClosePolicyTest
{
    [Fact]
    public void AClosingVetoCannotBeOverriddenByALaterApproval()
    {
        var calls = 0;

        bool Reject()
        {
            calls++;
            return false;
        }

        bool Approve()
        {
            calls++;
            return true;
        }

        Window.Closing += Reject;
        Window.Closing += Approve;
        try
        {
            Assert.False(Window.TryPrepareClose(true));
            Assert.Equal(2, calls);
        }
        finally
        {
            Window.Closing -= Reject;
            Window.Closing -= Approve;
        }
    }

    [Fact]
    public void AFaultedGuardPreventsCancellableClosure()
    {
        bool Fail() => throw new IOException("Simulated close failure.");
        Window.Closing += Fail;
        try { Assert.False(Window.TryPrepareClose(true)); }
        finally { Window.Closing -= Fail; }
    }

    [Fact]
    public void AnIrreversiblePlatformDestructionStillAttemptsEveryGuard()
    {
        var calls = 0;

        bool Save()
        {
            calls++;
            return false;
        }

        Window.Closing += Save;
        try
        {
            Assert.True(Window.TryPrepareClose(false));
            Assert.Equal(1, calls);
        }
        finally { Window.Closing -= Save; }
    }
}
