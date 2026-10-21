namespace Engine.Graphics;

public class VertexElement : IEquatable<VertexElement>
{
    public readonly int HashCode;

    public VertexElement(VertexElementFormat format, string semantic)
        : this(-1, format, semantic)
    {
    }

    public VertexElement(VertexElementFormat format, VertexElementSemantic semantic)
        : this(-1, format, semantic)
    {
    }

    public VertexElement(int offset, VertexElementFormat format, string semantic)
    {
        if (string.IsNullOrEmpty(semantic))
        {
            throw new ArgumentException("semantic cannot be empty or null.");
        }

        var num = semantic.Length;
        while (num > 0 && char.IsDigit(semantic[num - 1]))
        {
            num--;
        }

        if (num == 0)
        {
            throw new ArgumentException("semantic cannot start with a digit.");
        }

        Offset = offset;
        Format = format;
        Semantic = semantic;
        SemanticName = semantic.Substring(0, num);
        SemanticIndex = num < semantic.Length ? int.Parse(semantic.Substring(num)) : 0;
        HashCode = Offset.GetHashCode() + Format.GetHashCode() + Semantic.GetHashCode();
    }

    public VertexElement(int offset, VertexElementFormat format, VertexElementSemantic semantic)
        : this(offset, format, semantic.GetSemanticString())
    {
    }

    public int Offset { get; internal set; }

    public VertexElementFormat Format { get; }

    public string Semantic { get; }

    public string SemanticName { get; }

    public int SemanticIndex { get; }

    public bool Equals(VertexElement? other)
    {
        return other is not null &&
               other.Offset == Offset &&
               other.Format == Format &&
               other.Semantic == Semantic;
    }

    public override int GetHashCode()
    {
        return HashCode;
    }

    public override bool Equals(object? obj)
    {
        return obj is VertexElement vertexElement && Equals(vertexElement);
    }

    public static bool operator ==(VertexElement? ve1, VertexElement? ve2)
    {
        if (ve1 is null && ve2 is null)
        {
            return true;
        }

        if (ve1 is null || ve2 is null)
        {
            return false;
        }

        return ve1.Equals(ve2);
    }

    public static bool operator !=(VertexElement? ve1, VertexElement? ve2)
    {
        return !(ve1 == ve2);
    }
}
