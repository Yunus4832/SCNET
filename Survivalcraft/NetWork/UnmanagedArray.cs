using System.Runtime.InteropServices;

namespace Game.NetWork;

public class UnmanagedArray<T> where T : struct
{
    private readonly int _elementSize;
    private readonly IntPtr _ptr;
    public int Length;

    public UnmanagedArray(int size)
    {
        Length = size;
        _elementSize = Marshal.SizeOf(typeof(T));
        _ptr = Marshal.AllocHGlobal(size * _elementSize);
    }

    public T this[int index]
    {
        get => Marshal.PtrToStructure<T>(_ptr);
        set
        {
            var addr = _ptr + index * _elementSize;
            Marshal.StructureToPtr(value, addr, true);
        }
    }

    public void Free()
    {
        Marshal.FreeHGlobal(_ptr);
    }
}
