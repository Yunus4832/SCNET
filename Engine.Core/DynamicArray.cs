using System.Collections;

namespace Engine.Core;

public class DynamicArray<T> : IList<T>
{
    private static readonly T[] _emptyArray = [];

    private int _count;

    public DynamicArray()
    {
    }

    public DynamicArray(int capacity)
    {
        Capacity = capacity;
    }

    public DynamicArray(IEnumerable<T> items)
    {
        var itemArray = items as T[] ?? items.ToArray();
        Capacity = itemArray.Length;
        foreach (var item in itemArray)
        {
            Add(item);
        }
    }

    public int Capacity
    {
        get => Array.Length;
        set
        {
            if (value < _count)
            {
                throw new InvalidOperationException("Capacity cannot be made smaller than number of elements.");
            }

            if (value == Capacity)
            {
                return;
            }

            if (value > 0)
            {
                var array = new T[value];
                System.Array.Copy(Array, 0, array, 0, _count);
                Array = array;
            }
            else
            {
                Array = _emptyArray;
            }
        }
    }

    public T[] Array { get; private set; } = _emptyArray;

    public T this[int index]
    {
        get
        {
            if (index >= _count)
            {
                throw new IndexOutOfRangeException();
            }

            return Array[index];
        }
        set
        {
            if (index >= _count)
            {
                throw new IndexOutOfRangeException();
            }

            Array[index] = value;
        }
    }

    public int Count
    {
        get => _count;
        set
        {
            while (Capacity < value)
            {
                Capacity = MathUtils.Max(Capacity * 2, 4);
            }

            _count = value;
        }
    }

    public bool IsReadOnly => false;

    public void RemoveAt(int index)
    {
        if (index < _count)
        {
            _count--;
            if (index < _count)
            {
                System.Array.Copy(Array, index + 1, Array, index, _count - index);
            }

            return;
        }

        throw new IndexOutOfRangeException();
    }

    public void Clear()
    {
        System.Array.Clear(Array, 0, _count);
        _count = 0;
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(this);
    }

    public int IndexOf(T item)
    {
        var @default = EqualityComparer<T>.Default;
        for (var i = 0; i < _count; i++)
        {
            if (@default.Equals(item, Array[i]))
            {
                return i;
            }
        }

        return -1;
    }

    public void Add(T item)
    {
        if (_count >= Capacity)
        {
            Capacity = MathUtils.Max(Capacity * 2, 4);
        }

        Array[_count] = item;
        _count++;
    }

    public bool Remove(T item)
    {
        var num = IndexOf(item);
        if (num >= 0)
        {
            RemoveAt(num);
            return true;
        }

        return false;
    }

    public void Insert(int index, T item)
    {
        if (index <= _count)
        {
            if (_count >= Capacity)
            {
                Capacity = MathUtils.Max(Capacity * 2, 4);
            }

            if (index < _count)
            {
                System.Array.Copy(Array, index, Array, index + 1, _count - index);
            }

            Array[index] = item;
            _count++;
            return;
        }

        throw new IndexOutOfRangeException();
    }

    public bool Contains(T item)
    {
        var @default = EqualityComparer<T>.Default;
        for (var i = 0; i < _count; i++)
        {
            if (@default.Equals(item, Array[i]))
            {
                return true;
            }
        }

        return false;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        System.Array.Copy(Array, 0, array, arrayIndex, _count);
    }

    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items is ICollection collection)
        {
            Capacity = MathUtils.Max(Capacity, Count + collection.Count);
            foreach (var item in items)
            {
                Array[_count] = item;
                _count++;
            }
        }
        else
        {
            foreach (var item2 in items)
            {
                Add(item2);
            }
        }
    }

    public void AddRange(DynamicArray<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Capacity = MathUtils.Max(Capacity, Count + items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            Array[_count] = items.Array[i];
            _count++;
        }
    }

    public void RemoveAtEnd()
    {
        if (_count <= 0)
        {
            throw new IndexOutOfRangeException();
        }

        _count--;
    }

    public int RemoveAll(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        int i;
        for (i = 0; i < _count && !match(Array[i]); i++)
        {
        }

        if (i >= _count)
        {
            return 0;
        }

        var j = i + 1;
        while (j < _count)
        {
            for (; j < _count && match(Array[j]); j++)
            {
            }

            if (j < _count)
            {
                Array[i++] = Array[j++];
            }
        }

        var result = _count - i;
        _count = i;
        return result;
    }

    public void RemoveRange(int index, int count)
    {
        if (index < 0 || count < 0 || _count - index < count)
        {
            throw new IndexOutOfRangeException();
        }

        if (count > 0)
        {
            _count -= count;
            if (index < _count)
            {
                System.Array.Copy(Array, index + count, Array, index, _count - index);
            }

            System.Array.Clear(Array, _count, count);
        }
    }

    public void Reverse()
    {
        var num = 0;
        var num2 = _count - 1;
        while (num < num2)
        {
            (Array[num], Array[num2]) = (Array[num2], Array[num]);
            num++;
            num2--;
        }
    }

    public List<T> ToList()
    {
        var list = new List<T>(Count);
        for (var i = 0; i < Count; i++)
        {
            list.Add(Array[i]);
        }

        return list;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    public struct Enumerator : IEnumerator<T>
    {
        private readonly DynamicArray<T> _mArray = [];

        private int _mIndex;

        public T Current => _mArray.Array[_mIndex];

        object? IEnumerator.Current => _mArray.Array[_mIndex];

        public Enumerator(DynamicArray<T> array)
        {
            _mArray = array;
            _mIndex = -1;
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            _mIndex++;
            return _mIndex < _mArray.Count;
        }

        public void Reset()
        {
            _mIndex = -1;
        }
    }
}
