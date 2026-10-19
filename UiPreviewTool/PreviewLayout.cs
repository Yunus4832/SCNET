using System.Globalization;
using System.Xml.Linq;

using Engine.Core;

using Game.Widgets;

namespace Scnet.UiPreviewTool;

public static class PreviewLayout
{
    public static void Arrange(Widget root, Vector2 viewport)
    {
        // Virtualized lists build rows after their viewport has been arranged.
        for (var pass = 0; pass < 6; pass++)
        {
            root.Measure(viewport);
            root.Arrange(Vector2.Zero, viewport);
        }
    }

    public static XElement Describe(Widget root, Vector2 viewport, out int errors, out int warnings)
    {
        var issues = new List<XElement>();
        var tree = DescribeWidget(root, "root", new BoundingRectangle(0, 0, viewport.X, viewport.Y), issues);
        errors = issues.Count(issue => (string?)issue.Attribute("Severity") == "Error");
        warnings = issues.Count - errors;
        return new XElement("Layout", new XAttribute("Errors", errors), new XAttribute("Warnings", warnings),
            tree, new XElement("Issues", issues));
    }

    private static XElement DescribeWidget(Widget widget, string path, BoundingRectangle clip, List<XElement> issues)
    {
        var bounds = widget.GlobalBounds;
        var visible = widget.IsVisibleGlobal;
        var finite = float.IsFinite(bounds.Min.X) && float.IsFinite(bounds.Min.Y) &&
            float.IsFinite(bounds.Max.X) && float.IsFinite(bounds.Max.Y);
        if (visible && !finite)
        {
            issues.Add(Issue("Error", "non-finite-bounds", path));
        }
        if (visible && finite && widget.ParentWidget?.Name == "Preview.Viewport" &&
            (bounds.Min.X < clip.Min.X - 1 || bounds.Min.Y < clip.Min.Y - 1 ||
             bounds.Max.X > clip.Max.X + 1 || bounds.Max.Y > clip.Max.Y + 1))
        {
            issues.Add(Issue("Warning", "content-outside-viewport", path));
        }
        if (visible && finite && widget.ParentWidget?.Name == "Preview.Viewport" &&
            (float.IsFinite(widget.ParentDesiredSize.X) && widget.ParentDesiredSize.X > widget.ParentWidget.ActualSize.X + 1 ||
             float.IsFinite(widget.ParentDesiredSize.Y) && widget.ParentDesiredSize.Y > widget.ParentWidget.ActualSize.Y + 1))
        {
            issues.Add(Issue("Warning", "content-compressed", path));
        }
        var clipped = finite && (bounds.Min.X < clip.Min.X || bounds.Min.Y < clip.Min.Y ||
            bounds.Max.X > clip.Max.X || bounds.Max.Y > clip.Max.Y);
        var result = new XElement("Widget", new XAttribute("Path", path),
            new XAttribute("Type", widget.GetType().FullName ?? widget.GetType().Name),
            new XAttribute("Name", widget.Name ?? string.Empty), new XAttribute("Visible", visible),
            new XAttribute("Enabled", widget.IsEnabledGlobal), new XAttribute("Clipped", clipped),
            new XAttribute("ClampToBounds", widget.ClampToBounds),
            new XAttribute("Bounds", Join(bounds.Min.X, bounds.Min.Y, bounds.Max.X, bounds.Max.Y)),
            new XAttribute("ActualSize", Join(widget.ActualSize.X, widget.ActualSize.Y)),
            new XAttribute("Clip", Join(clip.Min.X, clip.Min.Y, clip.Max.X, clip.Max.Y)));
        if (widget is LabelWidget label)
        {
            result.Add(new XAttribute("Text", label.Text), new XAttribute("Ellipsis", label.Ellipsis),
                new XAttribute("WordWrap", label.WordWrap), new XAttribute("FontScale", Join(label.FontScale)));
        }
        else if (widget is ButtonWidget button)
        {
            result.Add(new XAttribute("Text", button.Text ?? string.Empty));
        }
        else if (widget is CheckboxWidget checkbox)
        {
            result.Add(new XAttribute("Text", checkbox.Text ?? string.Empty), new XAttribute("Checked", checkbox.IsChecked));
        }
        if (widget is ContainerWidget container)
        {
            if (widget.ClampToBounds)
            {
                clip = new BoundingRectangle(Math.Max(clip.Min.X, bounds.Min.X), Math.Max(clip.Min.Y, bounds.Min.Y),
                    Math.Min(clip.Max.X, bounds.Max.X), Math.Min(clip.Max.Y, bounds.Max.Y));
            }
            for (var index = 0; index < container.Children.Count; index++)
            {
                var child = container.Children[index];
                result.Add(DescribeWidget(child, path + "/" + child.GetType().Name + "[" + index + "]", clip, issues));
            }
        }
        return result;
    }

    private static string Join(params float[] values) => string.Join(",", values.Select(value => value.ToString("G9", CultureInfo.InvariantCulture)));

    private static XElement Issue(string severity, string code, string path) => new("Issue",
        new XAttribute("Severity", severity), new XAttribute("Code", code), new XAttribute("Path", path));
}
