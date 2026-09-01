using System.Collections;

namespace Game;

public class SortedMultiCollection<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TValue : class
{
    public const int MinCapacity = 4;

    private KeyValuePair<TKey, TValue>[] _array;

    private readonly IComparer<TKey> _comparer;

    private int _count;

    private int _version;

    public SortedMultiCollection()
    {
        _array = new KeyValuePair<TKey, TValue>[4];
        _comparer = Comparer<TKey>.Default;
    }

    public SortedMultiCollection(IComparer<TKey> comparer)
    {
        _array = new KeyValuePair<TKey, TValue>[4];
        _comparer = comparer;
    }

    public SortedMultiCollection(int capacity)
        : this(capacity, Comparer<TKey>.Default)
    {
        _array = new KeyValuePair<TKey, TValue>[capacity];
    }

    public SortedMultiCollection(int capacity, IComparer<TKey> comparer)
    {
        capacity = Math.Max(capacity, 4);
        _array = new KeyValuePair<TKey, TValue>[capacity];
        _comparer = comparer;
    }

    public int Count => _count;

    public int Capacity
    {
        get => _array.Length;
        set
        {
            value = Math.Max(Math.Max(4, _count), value);
            if (value != _array.Length)
            {
                var array = new KeyValuePair<TKey, TValue>[value];
                Array.Copy(_array, array, _count);
                _array = array;
            }
        }
    }

    public KeyValuePair<TKey, TValue> this[int i]
    {
        get
        {
            if (i < _count)
            {
                return _array[i];
            }

            throw new ArgumentOutOfRangeException();
        }
    }

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(this);
    }

    public void Add(TKey key, TValue value)
    {
        var num = Find(key);
        if (num < 0)
        {
            num = ~num;
        }

        EnsureCapacity(_count + 1);
        Array.Copy(_array, num, _array, num + 1, _count - num);
        _array[num] = new KeyValuePair<TKey, TValue>(key, value);
        _count++;
        _version++;
    }

    public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        foreach (var item in items)
        {
            Add(item.Key, item.Value);
        }
    }

    public bool Remove(TKey key)
    {
        var num = Find(key);
        if (num < 0)
        {
            return false;
        }

        Array.Copy(_array, num + 1, _array, num, _count - num - 1);
        _array[_count - 1] = default;
        _count--;
        _version++;
        return true;
    }

    public void Clear()
    {
        for (var i = 0; i < _count; i++)
        {
            _array[i] = default;
        }

        _count = 0;
        _version++;
    }

    public bool TryGetValue(TKey key, out TValue? value)
    {
        var num = Find(key);
        if (num >= 0)
        {
            value = _array[num].Value;
            return true;
        }

        value = null;
        return false;
    }

    public bool ContainsKey(TKey key)
    {
        return Find(key) >= 0;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity > Capacity)
        {
            Capacity = Math.Max(capacity, 2 * Capacity);
        }
    }

    public int Find(TKey key)
    {
        if (_count > 0)
        {
            var num = 0;
            var num2 = _count - 1;
            while (num <= num2)
            {
                var num3 = (num + num2) >> 1;
                var num4 = _comparer.Compare(_array[num3].Key, key);
                if (num4 == 0)
                {
                    return num3;
                }

                if (num4 < 0)
                {
                    num = num3 + 1;
                }
                else
                {
                    num2 = num3 - 1;
                }
            }

            return ~num;
        }

        return -1;
    }

    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly SortedMultiCollection<TKey, TValue> _collection;

        private int _index;

        private readonly int _version;

        public KeyValuePair<TKey, TValue> Current { get; private set; }

        object IEnumerator.Current => Current;

        internal Enumerator(SortedMultiCollection<TKey, TValue> collection)
        {
            _collection = collection;
            Current = default;
            _index = 0;
            _version = collection._version;
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (_collection._version != _version)
            {
                throw new InvalidOperationException("SortedMultiCollection was modified, enumeration cannot continue.");
            }

            if (_index < _collection._count)
            {
                Current = _collection._array[_index];
                _index++;
                return true;
            }

            Current = default;
            return false;
        }

        public void Reset()
        {
            if (_collection._version != _version)
            {
                throw new InvalidOperationException("SortedMultiCollection was modified, enumeration cannot continue.");
            }

            _index = 0;
            Current = default;
        }
    }
}
