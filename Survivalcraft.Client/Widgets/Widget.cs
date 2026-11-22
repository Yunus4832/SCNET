using System.Xml.Linq;

using Engine.Core.Reflection;
using Engine.Core.Serialization;
using Engine.Graphics;
using Engine.Input;

using EntitySystem.XmlUtilities;

namespace Game.Widgets;

public class Widget : IDisposable
{
    public static Vector2? GlobalClickPoint;

    private static readonly Queue<DrawContext> _drawContextsCache = new();

    private const int _layersLimit = -1;

    public static bool DrawWidgetBounds = false;

    private Vector2 _actualSize;

    private float? _globalScale;

    private Matrix _globalTransform = Matrix.Identity;

    private Matrix? _invertedGlobalTransform;

    private bool _isLayoutTransformIdentity = true;

    private bool _isRenderTransformIdentity = true;

    private Matrix _layoutTransform = Matrix.Identity;

    private Vector2 _parentOffset;

    private Matrix _renderTransform = Matrix.Identity;

    public Action<Vector2>? MeasureOverrideReplace = null;

    public object Tag { get; set; } = new();

    public WidgetInput? WidgetsHierarchyInput
    {
        get;
        set
        {
            if (value != null)
            {
                if (value.Widget != null && value.Widget != this)
                {
                    throw new InvalidOperationException("WidgetInput already assigned to another widget.");
                }

                value.Widget = this;
                field = value;
            }
            else if (field != null)
            {
                field.Widget = null;
                field = null;
            }
        }
    }

    public WidgetInput Input
    {
        get
        {
            var widget = this;
            do
            {
                if (widget.WidgetsHierarchyInput != null)
                {
                    return widget.WidgetsHierarchyInput;
                }

                widget = widget.ParentWidget;
            } while (widget != null);

            return WidgetInput.EmptyInput;
        }
    }

    public Matrix LayoutTransform
    {
        get => _layoutTransform;
        set
        {
            _layoutTransform = value;
            _isLayoutTransformIdentity = value == Matrix.Identity;
        }
    }

    public Matrix RenderTransform
    {
        get => _renderTransform;
        set
        {
            _renderTransform = value;
            _isRenderTransformIdentity = value == Matrix.Identity;
        }
    }

    public Matrix GlobalTransform => _globalTransform;

    public float GlobalScale
    {
        get
        {
            _globalScale ??= _globalTransform.Right.Length();
            return _globalScale.Value;
        }
    }

    public Matrix InvertedGlobalTransform
    {
        get
        {
            _invertedGlobalTransform ??= Matrix.Invert(_globalTransform);
            return _invertedGlobalTransform.Value;
        }
    }

    public BoundingRectangle GlobalBounds { get; private set; }

    public Color ColorTransform { get; set; } = Color.White;

    public Color GlobalColorTransform { get; private set; }

    public virtual string Title { get; set; } = string.Empty;

    public virtual string Name { get; set; } = string.Empty;

    public virtual bool IsVisible
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            if (!field)
            {
                UpdateCeases();
            }
        }
    } = true;

    public virtual bool IsEnabled
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            if (!field)
            {
                UpdateCeases();
            }
        }
    } = true;

    public virtual bool IsHitTestVisible { get; set; } = true;

    public bool IsVisibleGlobal
    {
        get
        {
            if (!IsVisible)
            {
                return false;
            }

            return ParentWidget == null || ParentWidget.IsVisibleGlobal;
        }
    }

    public bool IsEnabledGlobal
    {
        get
        {
            if (!IsEnabled)
            {
                return false;
            }

            return ParentWidget == null || ParentWidget.IsEnabledGlobal;
        }
    }

    public bool ClampToBounds { get; set; }

    public virtual Vector2 Margin { get; set; }

    public virtual WidgetAlignment HorizontalAlignment { get; set; }

    public virtual WidgetAlignment VerticalAlignment { get; set; }

    public Vector2 ActualSize => _actualSize;

    public Vector2 DesiredSize { get; set; } = new(1f / 0f);

    public Vector2 ParentDesiredSize { get; private set; }

    public bool IsUpdateEnabled { get; set; } = true;

    public bool IsDrawEnabled { get; set; } = true;

    public bool IsDrawRequired { get; set; }

    public bool IsOverdrawRequired { get; set; }

    public XElement Style { set => LoadContents(null, value); }

    public ContainerWidget? ParentWidget { get; set; }

    public Widget RootWidget => ParentWidget == null ? this : ParentWidget.RootWidget;

    public virtual void Dispose()
    {
    }

    public static Widget LoadWidget(object? eventsTarget, XElement node, ContainerWidget? parentWidget)
    {
        if (node.Name.LocalName.Contains('.'))
        {
            throw new NotImplementedException("Node property specification not implemented.");
        }

        if (Activator.CreateInstance(FindTypeFromXmlName(node.Name.LocalName, node.Name.NamespaceName)) is not Widget
            widget)
        {
            throw new Exception($"Type \"{node.Name.LocalName}\" is not a Widget.");
        }

        parentWidget?.Children.Add(widget);
        widget.LoadContents(eventsTarget, node);
        return widget;
    }

    public void LoadContents(object? eventsTarget, XElement node)
    {
        LoadProperties(eventsTarget, node);
        LoadChildren(eventsTarget, node);
    }

    public void LoadProperties(object? eventsTarget, XElement node)
    {
        var runtimeProperties = GetType().GetRuntimeProperties();
        foreach (var attribute in node.Attributes())
        {
            if (!attribute.IsNamespaceDeclaration && !attribute.Name.LocalName.StartsWith("_"))
            {
                if (attribute.Name.LocalName.Contains('.'))
                {
                    var array = attribute.Name.LocalName.Split('.');
                    if (array.Length != 2)
                    {
                        throw new InvalidOperationException(
                            $"Attached property reference must have form \"TypeName.PropertyName\", property \"{attribute.Name.LocalName}\" in widget of type \"{GetType().FullName}\"."
                        );
                    }

                    var type = FindTypeFromXmlName(
                        array[0],
                        attribute.Name.NamespaceName != string.Empty
                            ? attribute.Name.NamespaceName
                            : node.Name.NamespaceName
                    );
                    var setterName = "Set" + array[1];
                    var methodInfo = type.GetRuntimeMethods()
                        .FirstOrDefault(mi => mi.Name == setterName && mi.IsPublic && mi.IsStatic);
                    if (!(methodInfo != null))
                    {
                        throw new InvalidOperationException(
                            $"Attached property public static setter method \"{setterName}\" not found, property \"{attribute.Name.LocalName}\" in widget of type \"{GetType().FullName}\"."
                        );
                    }

                    var parameters = methodInfo.GetParameters();
                    if (parameters.Length != 2 || !(parameters[0].ParameterType == typeof(Widget)))
                    {
                        throw new InvalidOperationException(
                            $"Attached property setter method must take 2 parameters and first one must be of type Widget, property \"{attribute.Name.LocalName}\" in widget of type \"{GetType().FullName}\"."
                        );
                    }

                    var obj = HumanReadableConverter.ConvertFromString(parameters[1].ParameterType, attribute.Value);
                    methodInfo.Invoke(null, [this, obj]);
                }
                else
                {
                    var propertyInfo = runtimeProperties.FirstOrDefault(pi => pi.Name == attribute.Name.LocalName);
                    if (!(propertyInfo != null))
                    {
                        throw new InvalidOperationException(
                            $"Property \"{attribute.Name.LocalName}\" not found in widget of type \"{GetType().FullName}\"."
                        );
                    }

                    if (attribute.Value.StartsWith('{') && attribute.Value.EndsWith('}'))
                    {
                        var name = attribute.Value.Substring(1, attribute.Value.Length - 2);
                        var value = ContentManager.Get(propertyInfo.PropertyType, name);
                        propertyInfo.SetValue(this, value, null);
                    }
                    else
                    {
                        var obj2 = HumanReadableConverter.ConvertFromString(propertyInfo.PropertyType, attribute.Value);
                        if (propertyInfo.PropertyType == typeof(string))
                        {
                            obj2 = ((string)obj2).Replace("\\n", "\n").Replace("\\t", "\t");
                        }

                        propertyInfo.SetValue(this, obj2, null);
                    }
                }
            }
        }
    }

    public void LoadChildren(object? eventsTarget, XElement node)
    {
        if (!node.HasElements)
        {
            return;
        }

        if (this is not ContainerWidget containerWidget)
        {
            throw new Exception(
                $"Type \"{node.Name.LocalName}\" is not a ContainerWidget, but it contains child widgets."
            );
        }

        foreach (var item in node.Elements())
        {
            if (!IsNodeIncludedOnCurrentPlatform(item))
            {
                continue;
            }

            Widget? widget = null;
            var attributeValue = XmlUtils.GetAttributeValue<string>(item, "Name", false);
            if (attributeValue != null)
            {
                widget = containerWidget.Children.Find(attributeValue, false);
            }

            if (widget != null)
            {
                widget.LoadContents(eventsTarget, item);
            }
            else
            {
                LoadWidget(eventsTarget, item, containerWidget);
            }
        }
    }

    public bool IsChildWidgetOf(ContainerWidget containerWidget)
    {
        if (containerWidget == ParentWidget)
        {
            return true;
        }

        return ParentWidget != null && ParentWidget.IsChildWidgetOf(containerWidget);
    }

    public virtual void ChangeParent(ContainerWidget? parentWidget)
    {
        if (parentWidget == ParentWidget)
        {
            return;
        }

        ParentWidget = parentWidget;
        if (parentWidget == null)
        {
            UpdateCeases();
        }
    }

    public void Measure(Vector2 parentAvailableSize)
    {
        if (MeasureOverrideReplace is null)
        {
            MeasureOverride(parentAvailableSize);
        }
        else
        {
            MeasureOverrideReplace(parentAvailableSize);
            return;
        }

        if (DesiredSize.X.UncloseTo(1f / 0f) && DesiredSize.Y.UncloseTo(1f / 0f))
        {
            var boundingRectangle = TransformBoundsToParent(DesiredSize);
            ParentDesiredSize = boundingRectangle.Size();
            _parentOffset = -boundingRectangle.Min;
        }
        else
        {
            ParentDesiredSize = DesiredSize;
            _parentOffset = Vector2.Zero;
        }
    }

    protected virtual void MeasureOverride(Vector2 parentAvailableSize)
    {
    }

    public void Arrange(Vector2 position, Vector2 parentActualSize)
    {
        var m11Squared = _layoutTransform.M11 * _layoutTransform.M11;
        var m12Squared = _layoutTransform.M12 * _layoutTransform.M12;
        var m21Squared = _layoutTransform.M21 * _layoutTransform.M21;
        var m22Squared = _layoutTransform.M22 * _layoutTransform.M22;
        _actualSize.X = (m11Squared * parentActualSize.X + m21Squared * parentActualSize.Y) / (m11Squared + m21Squared);
        _actualSize.Y = (m12Squared * parentActualSize.X + m22Squared * parentActualSize.Y) / (m12Squared + m22Squared);
        _parentOffset = -TransformBoundsToParent(_actualSize).Min;
        GlobalColorTransform = ParentWidget != null
            ? ParentWidget.GlobalColorTransform * ColorTransform
            : ColorTransform;
        if (_isRenderTransformIdentity)
        {
            _globalTransform = _layoutTransform;
        }
        else
        {
            _globalTransform = _isLayoutTransformIdentity ? _renderTransform : _renderTransform * _layoutTransform;
        }

        _globalTransform.M41 += position.X + _parentOffset.X;
        _globalTransform.M42 += position.Y + _parentOffset.Y;
        if (ParentWidget != null)
        {
            _globalTransform *= ParentWidget.GlobalTransform;
        }

        _invertedGlobalTransform = null;
        _globalScale = null;
        GlobalBounds = TransformBoundsToGlobal(_actualSize);
        ArrangeOverride();
    }

    public virtual void ArrangeOverride()
    {
    }

    public virtual void UpdateCeases()
    {
    }

    public virtual void Update()
    {
    }

    public virtual void Draw(DrawContext dc)
    {
    }

    public virtual void Overdraw(DrawContext dc)
    {
    }

    public virtual bool HitTest(Vector2 point)
    {
        var vector = ScreenToWidget(point);
        if (vector is { X: >= 0f, Y: >= 0f } && vector.X <= ActualSize.X)
        {
            return vector.Y <= ActualSize.Y;
        }

        return false;
    }

    public Widget? HitTestGlobal(Vector2 point, Func<Widget, bool>? predicate = null)
    {
        return HitTestGlobal(RootWidget, point, predicate);
    }

    public Vector2 ScreenToWidget(Vector2 p)
    {
        return Vector2.Transform(p, InvertedGlobalTransform);
    }

    public Vector2 WidgetToScreen(Vector2 p)
    {
        return Vector2.Transform(p, GlobalTransform);
    }

    public static bool TestOverlap(Widget w1, Widget w2)
    {
        if (w2.GlobalBounds.Min.X >= w1.GlobalBounds.Max.X - 0.001f)
        {
            return false;
        }

        if (w2.GlobalBounds.Min.Y >= w1.GlobalBounds.Max.Y - 0.001f)
        {
            return false;
        }

        if (w1.GlobalBounds.Min.X >= w2.GlobalBounds.Max.X - 0.001f)
        {
            return false;
        }

        if (w1.GlobalBounds.Min.Y >= w2.GlobalBounds.Max.Y - 0.001f)
        {
            return false;
        }

        return true;
    }

    public static bool IsNodeIncludedOnCurrentPlatform(XElement node)
    {
        var attributeValue = XmlUtils.GetAttributeValue<string>(node, "_IncludePlatforms", false);
        var attributeValue2 = XmlUtils.GetAttributeValue<string>(node, "_ExcludePlatforms", false);
        if (attributeValue != null && attributeValue2 == null)
        {
            if (attributeValue.Split(' ').Contains(Platforms.ClientPlatform.Kind.ToString()))
            {
                return true;
            }
        }
        else
        {
            if (attributeValue2 == null || attributeValue != null)
            {
                return true;
            }

            if (!attributeValue2.Split(' ').Contains(Platforms.ClientPlatform.Kind.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    public static void UpdateWidgetsHierarchy(Widget rootWidget)
    {
        if (rootWidget.IsUpdateEnabled)
        {
            var isMouseCursorVisible = false;
            UpdateWidgetsHierarchy(rootWidget, ref isMouseCursorVisible);
            Mouse.IsMouseVisible = isMouseCursorVisible;
        }
    }

    public static void LayoutWidgetsHierarchy(Widget rootWidget, Vector2 availableSize)
    {
        rootWidget.Measure(availableSize);
        rootWidget.Arrange(Vector2.Zero, availableSize);
    }

    public static void DrawWidgetsHierarchy(Widget rootWidget)
    {
        var drawContext = _drawContextsCache.Count > 0 ? _drawContextsCache.Dequeue() : new DrawContext();
        try
        {
            drawContext.DrawWidgetsHierarchy(rootWidget);
        }
        finally
        {
            _drawContextsCache.Enqueue(drawContext);
        }
    }

    public BoundingRectangle TransformBoundsToParent(Vector2 size)
    {
        var widthX = _layoutTransform.M11 * size.X;
        var heightX = _layoutTransform.M21 * size.Y;
        var x = widthX + heightX;
        var widthY = _layoutTransform.M12 * size.X;
        var heightY = _layoutTransform.M22 * size.Y;
        var x2 = widthY + heightY;
        var x3 = MathUtils.Min(0f, widthX, heightX, x);
        var x4 = MathUtils.Max(0f, widthX, heightX, x);
        var y = MathUtils.Min(0f, widthY, heightY, x2);
        var y2 = MathUtils.Max(0f, widthY, heightY, x2);
        return new BoundingRectangle(x3, y, x4, y2);
    }

    public BoundingRectangle TransformBoundsToGlobal(Vector2 size)
    {
        var widthX = _globalTransform.M11 * size.X;
        var heightX = _globalTransform.M21 * size.Y;
        var x = widthX + heightX;
        var widthY = _globalTransform.M12 * size.X;
        var heightY = _globalTransform.M22 * size.Y;
        var x2 = widthY + heightY;
        var minX = MathUtils.Min(0f, widthX, heightX, x);
        var maxX = MathUtils.Max(0f, widthX, heightX, x);
        var minY = MathUtils.Min(0f, widthY, heightY, x2);
        return new BoundingRectangle(
            y2: MathUtils.Max(0f, widthY, heightY, x2) + _globalTransform.M42,
            x1: minX + _globalTransform.M41,
            y1: minY + _globalTransform.M42,
            x2: maxX + _globalTransform.M41
        );
    }

    public static Type FindTypeFromXmlName(string name, string namespaceName)
    {
        if (!string.IsNullOrEmpty(namespaceName))
        {
            var uri = new Uri(namespaceName);
            if (uri.Scheme == "runtime-namespace")
            {
                return RuntimeTypeResolver.FindType(uri.AbsolutePath + "." + name)!;
            }

            throw new InvalidOperationException(
                "Unknown uri scheme when loading widget. Scheme must be runtime-namespace."
            );
        }

        throw new InvalidOperationException("Namespace must be specified when creating types in XML.");
    }

    public static Widget? HitTestGlobal(Widget? widget, Vector2 point, Func<Widget, bool>? predicate)
    {
        if (widget is not { IsVisible: true } || (widget.ClampToBounds && !widget.HitTest(point)))
        {
            return null;
        }

        if (widget is ContainerWidget containerWidget)
        {
            var children = containerWidget.Children;
            for (var childIndex = children.Count - 1; childIndex >= 0; childIndex--)
            {
                var widget2 = HitTestGlobal(children[childIndex], point, predicate);
                if (widget2 != null)
                {
                    return widget2;
                }
            }
        }

        if (widget.IsHitTestVisible && widget.HitTest(point) && (predicate == null || predicate(widget)))
        {
            return widget;
        }

        return null;
    }

    public static void UpdateWidgetsHierarchy(Widget widget, ref bool isMouseCursorVisible)
    {
        if (!widget.IsVisible || !widget.IsEnabled)
        {
            return;
        }

        if (widget.WidgetsHierarchyInput != null)
        {
            widget.WidgetsHierarchyInput.Update();
            isMouseCursorVisible |= widget.WidgetsHierarchyInput.IsMouseCursorVisible;
        }

        if (!widget.IsVisible || !widget.IsEnabled) { return; }

        if (widget is ContainerWidget containerWidget)
        {
            var children = containerWidget.Children;
            for (var childIndex = children.Count - 1; childIndex >= 0; childIndex--)
            {
                if (!widget.IsVisible || !widget.IsEnabled) { break; }

                if (childIndex < children.Count)
                {
                    UpdateWidgetsHierarchy(children[childIndex], ref isMouseCursorVisible);
                }
            }
        }

        if (!widget.IsVisible || !widget.IsEnabled) { return; }

        widget.Update();
    }

    public class DrawContext
    {
        private static readonly List<DrawItem> _drawItemsCache = [];

        public readonly PrimitivesRenderer2D CursorPrimitivesRenderer2D = new();

        public readonly PrimitivesRenderer2D PrimitivesRenderer2D = new();

        public readonly PrimitivesRenderer3D PrimitivesRenderer3D = new();

        private readonly List<DrawItem> _drawItems = [];

        public void DrawWidgetsHierarchy(Widget rootWidget)
        {
            _drawItems.Clear();
            CollateDrawItems(rootWidget, Display.ScissorRectangle);
            AssignDrawItemsLayers();
            RenderDrawItems();
            ReturnDrawItemsToCache();
        }

        public void CollateDrawItems(Widget widget, Rectangle scissorRectangle)
        {
            if (!widget.IsVisible || !widget.IsDrawEnabled)
            {
                return;
            }

            var intersectsScissor = widget.GlobalBounds.Intersection(
                new BoundingRectangle(
                    scissorRectangle.Left,
                    scissorRectangle.Top,
                    scissorRectangle.Right,
                    scissorRectangle.Bottom
                )
            );
            Rectangle? scissorRectangle2 = null;
            if (widget.ClampToBounds && intersectsScissor)
            {
                scissorRectangle2 = scissorRectangle;
                var clipLeft = (int)MathUtils.Floor(widget.GlobalBounds.Min.X - 0.5f);
                var clipTop = (int)MathUtils.Floor(widget.GlobalBounds.Min.Y - 0.5f);
                var clipRight = (int)MathUtils.Ceiling(widget.GlobalBounds.Max.X - 0.5f);
                var clipBottom = (int)MathUtils.Ceiling(widget.GlobalBounds.Max.Y - 0.5f);
                scissorRectangle = Rectangle.Intersection(
                    new Rectangle(clipLeft, clipTop, clipRight - clipLeft, clipBottom - clipTop),
                    scissorRectangle2.Value
                );
                var drawItemFromCache = GetDrawItemFromCache();
                drawItemFromCache.ScissorRectangle = scissorRectangle;
                _drawItems.Add(drawItemFromCache);
            }

            if (widget.IsDrawRequired && intersectsScissor)
            {
                var drawItemFromCache2 = GetDrawItemFromCache();
                drawItemFromCache2.Widget = widget;
                _drawItems.Add(drawItemFromCache2);
            }

            if (intersectsScissor || !widget.ClampToBounds)
            {
                if (widget is ContainerWidget containerWidget)
                {
                    foreach (var child in containerWidget.Children)
                    {
                        CollateDrawItems(child, scissorRectangle);
                    }
                }
            }

            if (widget.IsOverdrawRequired && intersectsScissor)
            {
                var drawItemFromCache3 = GetDrawItemFromCache();
                drawItemFromCache3.Widget = widget;
                drawItemFromCache3.IsOverdraw = true;
                _drawItems.Add(drawItemFromCache3);
            }

            if (scissorRectangle2.HasValue)
            {
                var drawItemFromCache4 = GetDrawItemFromCache();
                drawItemFromCache4.ScissorRectangle = scissorRectangle2;
                _drawItems.Add(drawItemFromCache4);
            }

            widget.WidgetsHierarchyInput?.Draw(this);
        }

        public void AssignDrawItemsLayers()
        {
            for (var i = 0; i < _drawItems.Count; i++)
            {
                var drawItem = _drawItems[i];
                for (var j = i + 1; j < _drawItems.Count; j++)
                {
                    var drawItem2 = _drawItems[j];
                    if (drawItem.ScissorRectangle.HasValue || drawItem2.ScissorRectangle.HasValue)
                    {
                        drawItem2.Layer = MathUtils.Max(drawItem2.Layer, drawItem.Layer + 1);
                    }
                    else if (TestOverlap(drawItem.Widget!, drawItem2.Widget!))
                    {
                        drawItem2.Layer = MathUtils.Max(drawItem2.Layer, drawItem.Layer + 1);
                    }
                }
            }

            _drawItems.Sort();
        }

        public void RenderDrawItems()
        {
            var scissorRectangle = Display.ScissorRectangle;
            var currentLayer = 0;
            foreach (var drawItem in _drawItems)
            {
                if (_layersLimit >= 0 && drawItem.Layer > _layersLimit)
                {
                    break;
                }

                if (drawItem.Layer != currentLayer)
                {
                    currentLayer = drawItem.Layer;
                    PrimitivesRenderer3D.Flush(Matrix.Identity);
                    PrimitivesRenderer2D.Flush();
                }

                if (drawItem.Widget != null)
                {
                    if (drawItem.IsOverdraw)
                    {
                        drawItem.Widget.Overdraw(this);
                    }
                    else
                    {
                        drawItem.Widget.Draw(this);
                    }
                }
                else
                {
                    Display.ScissorRectangle =
                        Rectangle.Intersection(scissorRectangle, drawItem.ScissorRectangle!.Value);
                }
            }

            PrimitivesRenderer3D.Flush(Matrix.Identity);
            PrimitivesRenderer2D.Flush();
            Display.ScissorRectangle = scissorRectangle;
            CursorPrimitivesRenderer2D.Flush();
        }

        private DrawItem GetDrawItemFromCache()
        {
            if (_drawItemsCache.Count <= 0)
            {
                return new DrawItem();
            }

            var result = _drawItemsCache[^1];
            _drawItemsCache.RemoveAt(_drawItemsCache.Count - 1);
            return result;
        }

        public void ReturnDrawItemsToCache()
        {
            foreach (var drawItem in _drawItems)
            {
                drawItem.Widget = null;
                drawItem.Layer = 0;
                drawItem.IsOverdraw = false;
                drawItem.ScissorRectangle = null;
                _drawItemsCache.Add(drawItem);
            }
        }
    }

    private class DrawItem : IComparable<DrawItem>
    {
        public bool IsOverdraw;

        public int Layer;

        public Rectangle? ScissorRectangle;

        public Widget? Widget;

        public int CompareTo(DrawItem? other)
        {
            if (other is null)
            {
                return Layer;
            }

            return Layer - other.Layer;
        }
    }
}
