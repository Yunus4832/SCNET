using System.Runtime.InteropServices;

namespace Engine.Core;

public static class Utilities
{
    public static void Swap<T>(ref T a, ref T b)
    {
        (a, b) = (b, a);
    }

    public static int SizeOf<T>()
    {
        return Marshal.SizeOf<T>();
    }

    public static T? PtrToStructure<T>(IntPtr ptr)
    {
        return Marshal.PtrToStructure<T>(ptr);
    }

    public static T? ArrayToStructure<T>(Array array)
    {
        var gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
        try
        {
            return PtrToStructure<T>(gCHandle.AddrOfPinnedObject());
        }
        finally
        {
            gCHandle.Free();
        }
    }

    public static byte[] StructureToArray<T>(T structure)
    {
        var array = new byte[SizeOf<T>()];
        var gCHandle = GCHandle.Alloc(structure, GCHandleType.Pinned);
        try
        {
            Marshal.Copy(gCHandle.AddrOfPinnedObject(), array, 0, array.Length);
            return array;
        }
        finally
        {
            gCHandle.Free();
        }
    }

    public static void Dispose<T>(ref T? disposable) where T : IDisposable
    {
        if (disposable is null)
        {
            return;
        }

        disposable.Dispose();
        disposable = default;
    }

    public static void DisposeCollection<T>(ICollection<T>? disposableCollection) where T : IDisposable
    {
        if (disposableCollection is null)
        {
            return;
        }

        foreach (var item in disposableCollection)
        {
            item?.Dispose();
        }

        if (!disposableCollection.IsReadOnly)
        {
            disposableCollection.Clear();
        }
    }

    public static int GetTotalAvailableMemory()
    {
        return 0;
    }
}
