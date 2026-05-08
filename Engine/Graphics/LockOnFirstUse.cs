namespace Engine.Graphics;

public class LockOnFirstUse
{
    internal bool isLocked;

    public void ThrowIfLocked()
    {
        if (isLocked)
        {
            throw new InvalidOperationException("Object was attached to a device and can no longer be modified.");
        }
    }
}
