using System.Collections;

namespace Game.Widgets;

public class WidgetsList(ContainerWidget containerWidget) : IEnumerable<Widget>
{
    private int _version;

    private readonly List<Widget> _widgets = [];

    public int Count => _widgets.Count;

    public Widget this[int index] => _widgets[index];

    IEnumerator<Widget> IEnumerable<Widget>.GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(this);
    }

    public void Add(Widget widget)
    {
        Insert(Count, widget);
    }

    public void Add(params Widget[] widgets)
    {
        AddRange(widgets);
    }

    public void AddRange(IEnumerable<Widget> widgets)
    {
        foreach (var widget in widgets)
        {
            Add(widget);
        }
    }

    public void Insert(int index, Widget widget)
    {
        if (_widgets.Contains(widget))
        {
            throw new InvalidOperationException("Child widget already present in container.");
        }

        if (index < 0 || index > _widgets.Count)
        {
            throw new InvalidOperationException("Widget index out of range.");
        }

        widget.ChangeParent(containerWidget);
        _widgets.Insert(index, widget);
        containerWidget.WidgetAdded(widget);
        _version++;
    }

    public void InsertBefore(Widget beforeWidget, Widget widget)
    {
        var num = _widgets.IndexOf(beforeWidget);
        if (num < 0)
        {
            throw new InvalidOperationException("Widget not present in container.");
        }

        Insert(num, widget);
    }

    public void InsertAfter(Widget afterWidget, Widget widget)
    {
        var num = _widgets.IndexOf(afterWidget);
        if (num < 0)
        {
            throw new InvalidOperationException("Widget not present in container.");
        }

        Insert(num + 1, widget);
    }

    public void Remove(Widget widget)
    {
        var num = IndexOf(widget);
        if (num < 0)
        {
            throw new InvalidOperationException("Child widget not present in container.");
        }

        RemoveAt(num);
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= _widgets.Count)
        {
            throw new InvalidOperationException("Widget index out of range.");
        }

        var widget = _widgets[index];
        widget.ChangeParent(null);
        _widgets.RemoveAt(index);
        containerWidget.WidgetRemoved(widget);
        _version--;
    }

    public void Clear()
    {
        while (Count > 0)
        {
            RemoveAt(Count - 1);
        }
    }

    public int IndexOf(Widget widget)
    {
        return _widgets.IndexOf(widget);
    }

    public bool Contains(Widget widget)
    {
        return _widgets.Contains(widget);
    }

    public Widget? Find(string? name, Type? type, bool throwIfNotFound = true)
    {
        foreach (var widget2 in _widgets)
        {
            if ((name is null || (!string.IsNullOrEmpty(widget2.Name) && widget2.Name == name)) &&
                (type is null || type == widget2.GetType() || widget2.GetType().GetTypeInfo().IsSubclassOf(type)))
            {
                return widget2;
            }

            var widget3 = widget2 as ContainerWidget;
            var widget = widget3?.Children.Find(name, type, false);
            if (widget != null)
            {
                return widget;
            }
        }

        return throwIfNotFound
            ? throw new Exception($"Required widget \"{name}\" of type \"{type}\" not found.")
            : null;
    }

    public Widget? Find(string name, bool throwIfNotFound = true)
    {
        return Find(name, null, throwIfNotFound);
    }

    public T? Find<T>(string? name, bool throwIfNotFound = true) where T : class
    {
        return Find(name, typeof(T), throwIfNotFound) as T;
    }

    public T? Find<T>(bool throwIfNotFound = true) where T : class
    {
        return Find(null, typeof(T), throwIfNotFound) as T;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    public struct Enumerator(WidgetsList collection) : IEnumerator<Widget>
    {
        private int _index = 0;

        private readonly int _version = collection._version;

        public Widget Current { get; private set; } = null!;

        object IEnumerator.Current => Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (collection._version != _version)
            {
                throw new InvalidOperationException("WidgetsList was modified, enumeration cannot continue.");
            }

            if (_index < collection._widgets.Count)
            {
                Current = collection._widgets[_index];
                _index++;
                return true;
            }

            Current = null!;
            return false;
        }

        public void Reset()
        {
            if (collection._version != _version)
            {
                throw new InvalidOperationException("SortedMultiCollection was modified, enumeration cannot continue.");
            }

            _index = 0;
            Current = null!;
        }
    }
}
