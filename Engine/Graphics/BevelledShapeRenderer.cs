using Engine.Core.Collections;
using Engine.Core.Mathematics;

namespace Engine.Graphics;

public static class BevelledShapeRenderer
{
    private static readonly FlatBatch2D _tmpBatch = new();

    private static readonly DynamicArray<Point> _tmpQuadPoints = [];

    private static readonly DynamicArray<Point> _tmpPoints = [];

    private static readonly DynamicArray<Vector2> _tmpPositions = [];

    private static readonly DynamicArray<Vector2> _tmpPositions2 = [];

    private static readonly DynamicArray<Vector2> _tmpNormals = [];

    private static readonly DynamicArray<ushort> _tmpIndices = [];

    private static readonly DynamicArray<ushort> _tmpIndicesTriangulation = [];

    private static readonly DynamicArray<PathRenderer.Point> _tmpPathPoints = [];

    public static void QueueShape(
        FlatBatch2D batch,
        IEnumerable<Point> points,
        float pixelsPerUnit,
        float antialiasSize,
        float bevelSize,
        bool flatShading,
        Color centerColor,
        Color bevelColor,
        float directional,
        float ambient
    )
    {
        if ((bevelColor == Color.Transparent || bevelSize == 0f) && centerColor == Color.Transparent)
        {
            return;
        }

        _tmpPoints.Count = 0;
        _tmpPoints.AddRange(points);
        _tmpPositions.Count = 0;
        RoundCorners(_tmpPoints, true, pixelsPerUnit, MathUtils.Abs(bevelSize), _tmpPositions);
        RemoveDuplicates(_tmpPositions);
        _tmpNormals.Count = 0;
        PathRenderer.GeneratePathNormals(_tmpPositions, [], true, _tmpNormals);
        if (bevelColor != Color.Transparent && bevelSize != 0f)
        {
            var bevelWidth = MathUtils.Abs(bevelSize);
            var directionToLight = Vector2.Normalize(bevelSize > 0f ? new Vector2(-1f, -2f) : -new Vector2(-1f, -2f));
            _tmpPathPoints.Count = 0;
            GeneratePathPoints(
                _tmpPositions,
                0f,
                0f,
                0f,
                bevelWidth,
                bevelColor,
                bevelColor,
                bevelColor,
                bevelColor,
                0f,
                float.PositiveInfinity,
                _tmpPathPoints
            );
            LightPoints(_tmpPathPoints, true, flatShading, directionToLight, directional, ambient);
            PathRenderer.QueuePath(batch, _tmpPathPoints, _tmpNormals, [], true, flatShading);
            _tmpPositions2.Count = 0;
            for (var i = 0; i < _tmpNormals.Count; i++)
            {
                _tmpPositions2.Add(_tmpPathPoints[i].Position + _tmpNormals[i] * bevelWidth);
            }

            _tmpIndices.Count = 0;
            Triangulate(_tmpPositions2, _tmpIndices);
            var count = batch.TriangleVertices.Count;
            foreach (var tempPosition in _tmpPositions2)
            {
                batch.TriangleVertices.Add(new VertexPositionColor(new Vector3(tempPosition, 0f), centerColor));
            }

            foreach (var tempIndex in _tmpIndices)
            {
                batch.TriangleIndices.Add(tempIndex + count);
            }

            if (!(antialiasSize > 0f))
            {
                return;
            }

            _tmpPathPoints.Count = 0;
            var outerRadiusR = bevelWidth + antialiasSize;
            GeneratePathPoints(
                _tmpPositions,
                0f,
                bevelWidth,
                0f,
                outerRadiusR,
                bevelColor,
                bevelColor,
                Color.Transparent,
                Color.Transparent,
                0f,
                float.PositiveInfinity,
                _tmpPathPoints
            );
            LightPoints(_tmpPathPoints, true, flatShading, directionToLight, directional, ambient);
            PathRenderer.QueuePath(batch, _tmpPathPoints, _tmpNormals, [], true, flatShading);
            _tmpPathPoints.Count = 0;
            GeneratePathPoints(
                _tmpPositions,
                0f,
                0f,
                antialiasSize,
                0f,
                bevelColor,
                bevelColor,
                Color.Transparent,
                Color.Transparent,
                0f,
                float.PositiveInfinity,
                _tmpPathPoints
            );
            LightPoints(_tmpPathPoints, true, flatShading, directionToLight, directional, ambient);
            PathRenderer.QueuePath(batch, _tmpPathPoints, _tmpNormals, [], true, flatShading);
        }
        else if (centerColor != Color.Transparent)
        {
            _tmpIndices.Count = 0;
            Triangulate(_tmpPositions, _tmpIndices);
            var count2 = batch.TriangleVertices.Count;
            foreach (var tempPosition in _tmpPositions)
            {
                batch.TriangleVertices.Add(new VertexPositionColor(new Vector3(tempPosition, 0f), centerColor));
            }

            foreach (var tempIndex in _tmpIndices)
            {
                batch.TriangleIndices.Add(tempIndex + count2);
            }

            if (!(antialiasSize > 0f))
            {
                return;
            }

            _tmpPathPoints.Count = 0;
            GeneratePathPoints(
                _tmpPositions,
                0f,
                0f,
                antialiasSize,
                0f,
                centerColor,
                centerColor,
                Color.Transparent,
                Color.Transparent,
                0f,
                float.PositiveInfinity,
                _tmpPathPoints
            );
            PathRenderer.QueuePath(batch, _tmpPathPoints, _tmpNormals, [], true, flatShading);
        }
    }

    public static void QueueShapeShadow(
        FlatBatch2D batch,
        IEnumerable<Point> points,
        float pixelsPerUnit,
        float shadowSize,
        Color shadowColor
    )
    {
        _tmpPoints.Count = 0;
        _tmpPoints.AddRange(points);
        _tmpPositions.Count = 0;
        RoundCorners(_tmpPoints, true, pixelsPerUnit, shadowSize, _tmpPositions);
        RemoveDuplicates(_tmpPositions);
        _tmpNormals.Count = 0;
        PathRenderer.GeneratePathNormals(_tmpPositions, [], true, _tmpNormals);
        _tmpPathPoints.Count = 0;
        GeneratePathPoints(
            _tmpPositions,
            0f,
            0f,
            shadowSize,
            0f,
            shadowColor,
            Color.Transparent,
            Color.Transparent,
            Color.Transparent,
            0f,
            float.PositiveInfinity,
            _tmpPathPoints
        );
        PathRenderer.QueuePath(batch, _tmpPathPoints, _tmpNormals, [], true, true);
        _tmpIndices.Count = 0;
        Triangulate(_tmpPositions, _tmpIndices);
        var count = batch.TriangleVertices.Count;
        foreach (var tempPosition in _tmpPositions)
        {
            batch.TriangleVertices.Add(new VertexPositionColor(new Vector3(tempPosition, 0f), shadowColor));
        }

        foreach (var tempIndex in _tmpIndices)
        {
            batch.TriangleIndices.Add(tempIndex + count);
        }
    }

    public static void QueueShape(
        TexturedBatch2D batch,
        IEnumerable<Point> points,
        Vector2 textureScale,
        Vector2 textureOffset,
        float pixelsPerUnit,
        float antialiasSize,
        float bevelSize,
        bool flatShading,
        Color centerColor,
        Color bevelColor,
        float directional,
        float ambient
    )
    {
        _tmpBatch.Clear();
        QueueShape(
            _tmpBatch,
            points,
            pixelsPerUnit,
            antialiasSize,
            bevelSize,
            flatShading,
            centerColor,
            bevelColor,
            directional,
            ambient
        );
        var vector = new Vector2 { X = 1f, Y = 1f } /
                     (textureScale * new Vector2(batch.Texture.Width, batch.Texture.Height));
        var count = batch.TriangleVertices.Count;
        foreach (var triangleVertex in _tmpBatch.TriangleVertices)
        {
            var triangleVertices = batch.TriangleVertices;
            var item = new VertexPositionColorTexture
            {
                Position = triangleVertex.Position,
                Color = triangleVertex.Color
            };
            var position = triangleVertex.Position;
            item.TexCoord = position.XY * vector + textureOffset;
            triangleVertices.Add(item);
        }

        foreach (var triangleIndex in _tmpBatch.TriangleIndices)
        {
            batch.TriangleIndices.Add(triangleIndex + count);
        }
    }

    public static void QueueQuad(
        FlatBatch2D batch,
        Vector2 p1,
        Vector2 p2,
        float pixelsPerUnit,
        float antialiasSize,
        float bevelSize,
        float roundingRadius,
        int roundingCount,
        bool flatShading,
        Color centerColor,
        Color bevelColor,
        float directional,
        float ambient
    )
    {
        _tmpQuadPoints.Count = 0;
        _tmpQuadPoints.Add(
            new Point
            {
                Position = new Vector2(p1.X, p1.Y),
                RoundingRadius = roundingRadius,
                RoundingCount = roundingCount
            }
        );
        _tmpQuadPoints.Add(
            new Point
            {
                Position = new Vector2(p2.X, p1.Y),
                RoundingRadius = roundingRadius,
                RoundingCount = roundingCount
            }
        );
        _tmpQuadPoints.Add(
            new Point
            {
                Position = new Vector2(p2.X, p2.Y),
                RoundingRadius = roundingRadius,
                RoundingCount = roundingCount
            }
        );
        _tmpQuadPoints.Add(
            new Point
            {
                Position = new Vector2(p1.X, p2.Y),
                RoundingRadius = roundingRadius,
                RoundingCount = roundingCount
            }
        );
        QueueShape(
            batch,
            _tmpQuadPoints,
            pixelsPerUnit,
            antialiasSize,
            bevelSize,
            flatShading,
            centerColor,
            bevelColor,
            directional,
            ambient
        );
    }

    public static void QueueQuad(
        TexturedBatch2D batch,
        Vector2 p1,
        Vector2 p2,
        Vector2 textureScale,
        Vector2 textureOffset,
        float pixelsPerUnit,
        float antialiasSize,
        float bevelSize,
        float roundingRadius,
        int roundingCount,
        bool flatShading,
        Color centerColor,
        Color bevelColor,
        float directional,
        float ambient
    )
    {
        _tmpQuadPoints.Count = 0;
        _tmpQuadPoints.Add(
            new Point
            {
                Position = new Vector2(p1.X, p1.Y),
                RoundingRadius = roundingRadius,
                RoundingCount = roundingCount
            }
        );
        _tmpQuadPoints.Add(
            new Point
            {
                Position = new Vector2(p2.X, p1.Y),
                RoundingRadius = roundingRadius,
                RoundingCount = roundingCount
            }
        );
        _tmpQuadPoints.Add(
            new Point
            {
                Position = new Vector2(p2.X, p2.Y),
                RoundingRadius = roundingRadius,
                RoundingCount = roundingCount
            }
        );
        _tmpQuadPoints.Add(
            new Point
            {
                Position = new Vector2(p1.X, p2.Y),
                RoundingRadius = roundingRadius,
                RoundingCount = roundingCount
            }
        );
        QueueShape(
            batch,
            _tmpQuadPoints,
            textureScale,
            textureOffset,
            pixelsPerUnit,
            antialiasSize,
            bevelSize,
            flatShading,
            centerColor,
            bevelColor,
            directional,
            ambient
        );
    }

    public static void RemoveDuplicates(DynamicArray<Vector2> positions)
    {
        var count = 0;
        Vector2? vector = null;
        for (var i = 0; i < positions.Count; i++)
        {
            var value = positions[i];
            if (value == vector)
            {
                continue;
            }

            positions[count++] = value;
            vector = value;
        }

        positions.Count = count;
    }

    public static void Triangulate(DynamicArray<Vector2> source, DynamicArray<ushort> destination)
    {
        _tmpIndicesTriangulation.Count = source.Count;
        for (var i = 0; i < source.Count; i++)
        {
            _tmpIndicesTriangulation.Array[i] = (ushort)i;
        }

        while (true)
        {
            var candidateIndex = _tmpIndicesTriangulation.Count - 1;
            ushort previousVertex;
            ushort currentVertex;
            ushort nextVertex;
            while (true)
            {
                if (candidateIndex >= 3)
                {
                    previousVertex = _tmpIndicesTriangulation[
                        (candidateIndex - 1 + _tmpIndicesTriangulation.Count) % _tmpIndicesTriangulation.Count];
                    currentVertex = _tmpIndicesTriangulation[candidateIndex];
                    nextVertex = _tmpIndicesTriangulation[(candidateIndex + 1) % _tmpIndicesTriangulation.Count];
                    var previousPosition = source[previousVertex];
                    var currentPosition = source[currentVertex];
                    var nextPosition = source[nextVertex];
                    if (Vector2.Cross(currentPosition - previousPosition, nextPosition - currentPosition) >= 0f)
                    {
                        break;
                    }

                    candidateIndex--;
                    continue;
                }

                if (_tmpIndicesTriangulation.Count == 3)
                {
                    destination.AddRange(_tmpIndicesTriangulation);
                }

                return;
            }

            destination.Add(previousVertex);
            destination.Add(currentVertex);
            destination.Add(nextVertex);
            _tmpIndicesTriangulation.RemoveAt(candidateIndex);
        }
    }

    private static void LightPoints(
        DynamicArray<PathRenderer.Point> points,
        bool loop,
        bool flatShading,
        Vector2 directionToLight,
        float directional,
        float ambient
    )
    {
        if (flatShading)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var index = i;
                var index2 = loop ? (i + 1) % points.Count : MathUtils.Min(i + 1, points.Count - 1);
                var value = points[index];
                var v = -Vector2.Perpendicular(Vector2.Normalize(points[index2].Position - value.Position));
                var directionalBrightness = directional * Vector2.Dot(v, directionToLight);
                var color = new Color(new Vector3(directionalBrightness + ambient));
                value.InnerColorL *= color;
                value.InnerColorR *= color;
                value.OuterColorL *= color;
                value.OuterColorR *= color;
                points[index] = value;
            }

            return;
        }

        for (var j = 0; j < points.Count; j++)
        {
            var index3 = loop ? (j - 1 + points.Count) % points.Count : MathUtils.Max(j - 1, 0);
            var index4 = j;
            var index5 = loop ? (j + 1) % points.Count : MathUtils.Min(j + 1, points.Count - 1);
            var point = points[index3];
            var value2 = points[index4];
            var point2 = points[index5];
            var obj = value2.Position != point.Position
                ? Vector2.Normalize(value2.Position - point.Position)
                : Vector2.Zero;
            var vector = point2.Position != value2.Position
                ? Vector2.Normalize(point2.Position - value2.Position)
                : Vector2.Zero;
            var v2 = -Vector2.Perpendicular(Vector2.Normalize(obj + vector));
            var color2 = new Color(new Vector3(directional * Vector2.Dot(v2, directionToLight) + ambient));
            value2.InnerColorL *= color2;
            value2.InnerColorR *= color2;
            value2.OuterColorL *= color2;
            value2.OuterColorR *= color2;
            points[index4] = value2;
        }
    }

    private static void GeneratePathPoints(
        DynamicArray<Vector2> positions,
        float innerRadiusL,
        float innerRadiusR,
        float outerRadiusL,
        float outerRadiusR,
        Color innerColorL,
        Color innerColorR,
        Color outerColorL,
        Color outerColorR,
        float lengthScale,
        float miterLimit,
        DynamicArray<PathRenderer.Point> result
    )
    {
        foreach (var position in positions)
        {
            result.Add(
                new PathRenderer.Point
                {
                    Position = position,
                    InnerRadiusL = innerRadiusL,
                    InnerRadiusR = innerRadiusR,
                    OuterRadiusL = outerRadiusL,
                    OuterRadiusR = outerRadiusR,
                    InnerColorL = innerColorL,
                    InnerColorR = innerColorR,
                    OuterColorL = outerColorL,
                    OuterColorR = outerColorR,
                    LengthScale = lengthScale,
                    MiterLimit = miterLimit
                }
            );
        }
    }

    public static void RoundCorners(
        DynamicArray<Point> points,
        bool loop,
        float pixelsPerUnit,
        float bevelSize,
        DynamicArray<Vector2> result
    )
    {
        var cornerCount = loop ? points.Count : points.Count - 2;
        for (var i = 0; i < cornerCount; i++)
        {
            var point = points[i];
            var point2 = points[(i + 1) % points.Count];
            var point3 = points[(i + 2) % points.Count];
            RoundCorner(
                point.Position,
                point2.Position,
                point3.Position,
                point2.RoundingRadius,
                point2.RoundingCount,
                pixelsPerUnit,
                bevelSize,
                result
            );
        }
    }

    public static void RoundCorner(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        float radius,
        int count,
        float pixelsPerUnit,
        float bevelSize,
        DynamicArray<Vector2> result
    )
    {
        var previousEdge = p0 - p1;
        var nextEdge = p2 - p1;
        var previousEdgeLength = previousEdge.Length();
        var nextEdgeLength = nextEdge.Length();
        var tangentDistance = MathUtils.Min(previousEdgeLength * 0.49f, nextEdgeLength * 0.49f, radius);
        if (tangentDistance > 0f)
        {
            var towardPrevious = previousEdge / previousEdgeLength;
            var towardNext = nextEdge / nextEdgeLength;
            var arcStart = p1 + towardPrevious * tangentDistance;
            var arcEnd = p1 + towardNext * tangentDistance;
            var v = p1 - arcStart;
            var v2 = p1 - arcEnd;
            var l = new Line2(arcStart, arcStart + Vector2.Perpendicular(v));
            var l2 = new Line2(arcEnd, arcEnd + Vector2.Perpendicular(v2));
            var arcCenter = Line2.Intersection(l, l2);
            if (arcCenter.HasValue)
            {
                var arcRadius = Vector2.Distance(arcCenter.Value, arcStart);
                if (count < 0)
                {
                    var maxPixelError = 0.25f;
                    var radiusInPixels = (arcRadius + bevelSize) * pixelsPerUnit;
                    var halfStepAngle = MathUtils.Acos(1f - maxPixelError / radiusInPixels);
                    count = !float.IsNaN(halfStepAngle)
                        ? (int)MathUtils.Clamp(MathUtils.Ceiling(((float)Math.PI / halfStepAngle - 4f) / 4f), 0f, 50f)
                        : 0;
                }

                var startAngle = Vector2.Angle(arcStart - arcCenter.Value, Vector2.UnitY);
                var sweepAngle =
                    MathUtils.NormalizeAngle(Vector2.Angle(arcEnd - arcCenter.Value, Vector2.UnitY) - startAngle);
                result.Add(arcStart);
                for (var i = 1; i <= count; i++)
                {
                    var x = startAngle + sweepAngle * i / (count + 1);
                    var item = arcCenter.Value +
                               new Vector2(arcRadius * MathUtils.Sin(x), arcRadius * MathUtils.Cos(x));
                    result.Add(item);
                }

                result.Add(arcEnd);
                return;
            }
        }

        result.Add(p1);
    }

    public struct Point : IEquatable<Point>
    {
        public Vector2 Position;

        public float RoundingRadius;

        public int RoundingCount;

        public bool Equals(Point other)
        {
            return Position == other.Position &&
                   RoundingRadius.CloseTo(other.RoundingRadius) &&
                   RoundingCount == other.RoundingCount;
        }
    }
}
