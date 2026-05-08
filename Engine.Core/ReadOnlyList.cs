using System.Collections;

namespace Engine.Core;

public readonly struct ReadOnlyList<T>(IList<T> list) : IList<T>
{
    private readonly List<T> _list = list.ToList();

    public struct Enumerator(IList<T> list) : IEnumerator<T>
    {
        private int _index = -1;

        public T Current => list[_index];

        object? IEnumerator.Current => list[_index];

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            return ++_index < list.Count;
        }

        public void Reset()
        {
            _index = -1;
        }
    }

    public static ReadOnlyList<T> Empty { get; } = new(Array.Empty<T>());

    public T this[int index]
    {
        get => _list[index];
        set => throw new NotSupportedException("List is readonly.");
    }

    public int Count => _list.Count;

    public bool IsReadOnly => true;

    public Enumerator GetEnumerator()
    {
        return new Enumerator(_list);
    }

    public int IndexOf(T item)
    {
        return _list.IndexOf(item);
    }

    public T? Find(Predicate<T> match)
    {
        if (match == null)
        {
            throw new Exception("Invalid Predicate");
        }

        for (var i = 0; i < Count; i++)
        {
            if (match(_list[i]))
            {
                return _list[i];
            }
        }

        return default;
    }

    public void Insert(int index, T item)
    {
        throw new NotSupportedException("List is readonly.");
    }

    public void RemoveAt(int index)
    {
        throw new NotSupportedException("List is readonly.");
    }

    public void Add(T item)
    {
        throw new NotSupportedException("List is readonly.");
    }

    public void Clear()
    {
        throw new NotSupportedException("List is readonly.");
    }

    public bool Contains(T item)
    {
        return _list.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _list.CopyTo(array, arrayIndex);
    }

    public bool Remove(T item)
    {
        throw new NotSupportedException("List is readonly.");
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return new Enumerator(_list);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(_list);
    }
}
