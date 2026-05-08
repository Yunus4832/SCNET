using Engine.Core;

namespace Engine.Graphics;

public sealed class VertexDeclaration : IEquatable<VertexDeclaration>
{
    public static readonly List<VertexElement[]> AllElements = [];

    internal readonly VertexElement[] elements = [];

    public VertexDeclaration(params VertexElement[] elements)
    {
        if (elements.Length == 0)
        {
            throw new ArgumentException("There must be at least one VertexElement.");
        }

        foreach (var vertexElement in elements)
        {
            if (vertexElement.Offset < 0)
            {
                vertexElement.Offset = VertexStride;
            }

            VertexStride = MathUtils.Max(VertexStride, vertexElement.Offset + vertexElement.Format.GetSize());
        }

        foreach (var element in AllElements)
        {
            if (!elements.SequenceEqual(element))
            {
                continue;
            }

            this.elements = element;
            break;
        }

        if (this.elements.Length != 0)
        {
            return;
        }

        this.elements = elements.ToArray();
        AllElements.Add(this.elements);
    }

    public ReadOnlyList<VertexElement> VertexElements => new(elements);

    public int VertexStride { get; set; }

    public bool Equals(VertexDeclaration? other)
    {
        if (other is not null)
        {
            return elements == other.elements;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return elements.GetHashCode();
    }

    public override bool Equals(object? other)
    {
        return other is VertexDeclaration declaration && Equals(declaration);
    }

    public static bool operator ==(VertexDeclaration? vd1, VertexDeclaration? vd2)
    {
        if (vd1 is null && vd2 is null)
        {
            return true;
        }

        if (vd1 is null || vd2 is null)
        {
            return false;
        }

        return vd1.Equals(vd2);
    }

    public static bool operator !=(VertexDeclaration? vd1, VertexDeclaration? vd2)
    {
        return !(vd1 == vd2);
    }
}
