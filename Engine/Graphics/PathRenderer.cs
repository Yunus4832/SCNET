using Engine.Core.Collections;
using Engine.Core.Mathematics;

namespace Engine.Graphics;

internal static class PathRenderer
{
    private static readonly DynamicArray<Point> _tmpPoints = [];

    private static readonly DynamicArray<Vector2> _tmpPositions = [];

    private static readonly DynamicArray<Vector2> _tmpNormals = [];

    public static int TrimPathStart(DynamicArray<Vector2> positions, BoundingRectangle rectangle)
    {
        var count = positions.Count;
        var firstOutsideIndex = -1;
        for (var i = 0; i < positions.Count; i++)
        {
            if (!rectangle.Contains(positions[i]))
            {
                firstOutsideIndex = i;
                break;
            }
        }

        if (firstOutsideIndex < 0)
        {
            positions.Count = 0;
        }
        else if (firstOutsideIndex > 0)
        {
            positions.RemoveRange(0, firstOutsideIndex - 1);
            var ray = new Ray2(positions[1], Vector2.Normalize(positions[0] - positions[1]));
            var valueOrDefault = ray.Intersection(rectangle).GetValueOrDefault();
            positions[0] = ray.Position + ray.Direction * valueOrDefault;
        }

        return count - positions.Count;
    }

    public static int TrimPathEnd(DynamicArray<Vector2> positions, BoundingRectangle rectangle)
    {
        positions.Reverse();
        var result = TrimPathStart(positions, rectangle);
        positions.Reverse();
        return result;
    }

    public static int TrimPathStart(DynamicArray<Vector2> positions, BoundingCircle circle)
    {
        var count = positions.Count;
        var firstOutsideIndex = -1;
        for (var i = 0; i < positions.Count; i++)
        {
            if (!circle.Contains(positions[i]))
            {
                firstOutsideIndex = i;
                break;
            }
        }

        if (firstOutsideIndex < 0)
        {
            positions.Count = 0;
        }
        else if (firstOutsideIndex > 0)
        {
            positions.RemoveRange(0, firstOutsideIndex - 1);
            var ray = new Ray2(positions[1], Vector2.Normalize(positions[0] - positions[1]));
            var valueOrDefault = ray.Intersection(circle).GetValueOrDefault();
            positions[0] = ray.Position + ray.Direction * valueOrDefault;
        }

        return count - positions.Count;
    }

    public static int TrimPathEnd(DynamicArray<Vector2> positions, BoundingCircle circle)
    {
        positions.Reverse();
        var result = TrimPathStart(positions, circle);
        positions.Reverse();
        return result;
    }

    public static void GeneratePathNormals(
        DynamicArray<Vector2> positions,
        DynamicArray<bool> visibility,
        bool loop,
        DynamicArray<Vector2> normals
    )
    {
        if (positions.Count == 0)
        {
            return;
        }

        var array = positions.Array;
        var array2 = visibility.Array;
        if (array2.Length > 0)
        {
            if (loop)
            {
                for (var i = 0; i < positions.Count; i++)
                {
                    var previousIndex = (i - 1 + positions.Count) % positions.Count;
                    var currentIndex = i;
                    var nextIndex = (i + 1) % positions.Count;
                    normals.Add(
                        array2[previousIndex] || array2[currentIndex]
                            ? Normal2(
                                array[currentIndex] - array[previousIndex],
                                array[nextIndex] - array[currentIndex]
                            )
                            : default
                    );
                }

                return;
            }

            normals.Add(array2[0] ? Normal1(array[1] - array[0]) : default);
            for (var j = 1; j < positions.Count - 1; j++)
            {
                normals.Add(
                    array2[j - 1] || array2[j]
                        ? Normal2(array[j] - array[j - 1], array[j + 1] - array[j])
                        : default
                );
            }

            if (positions.Count >= 2)
            {
                normals.Add(
                    array2[positions.Count - 2]
                        ? Normal1(array[positions.Count - 1] - array[positions.Count - 2])
                        : default
                );
            }
        }
        else if (loop)
        {
            for (var k = 0; k < positions.Count; k++)
            {
                var previousIndex = (k - 1 + positions.Count) % positions.Count;
                var currentIndex = k;
                var nextIndex = (k + 1) % positions.Count;
                normals.Add(
                    Normal2(array[currentIndex] - array[previousIndex], array[nextIndex] - array[currentIndex])
                );
            }
        }
        else
        {
            normals.Add(Normal1(array[1] - array[0]));
            for (var l = 1; l < positions.Count - 1; l++)
            {
                normals.Add(Normal2(array[l] - array[l - 1], array[l + 1] - array[l]));
            }

            if (positions.Count >= 2)
            {
                normals.Add(Normal1(array[positions.Count - 1] - array[positions.Count - 2]));
            }
        }
    }

    public static void QueuePath(
        FlatBatch2D batch,
        DynamicArray<Point> points,
        DynamicArray<bool> visibility,
        bool loop,
        bool flatShading,
        float depth = 0f
    )
    {
        _tmpPositions.Count = 0;
        foreach (var point in points)
        {
            _tmpPositions.Add(point.Position);
        }

        _tmpNormals.Count = 0;
        GeneratePathNormals(_tmpPositions, visibility, loop, _tmpNormals);
        QueuePathInternal(batch, points, _tmpNormals, visibility, loop, flatShading, depth);
    }

    public static void QueuePath(
        FlatBatch2D batch,
        DynamicArray<Point> points,
        DynamicArray<Vector2> normals,
        DynamicArray<bool> visibility,
        bool loop,
        bool flatShading,
        float depth = 0f
    )
    {
        QueuePathInternal(batch, points, normals, visibility, loop, flatShading, depth);
    }

    public static void QueuePath(
        FlatBatch2D batch,
        DynamicArray<Vector2> positions,
        DynamicArray<bool> visibility,
        bool loop,
        bool flatShading,
        float innerRadius,
        float outerRadius,
        Color innerColor,
        Color outerColor,
        float miterLimit,
        float depth = 0f
    )
    {
        QueuePath(
            batch,
            positions,
            visibility,
            loop,
            flatShading,
            innerRadius,
            innerRadius,
            outerRadius,
            outerRadius,
            innerColor,
            innerColor,
            outerColor,
            outerColor,
            miterLimit,
            depth
        );
    }

    public static void QueuePath(
        FlatBatch2D batch,
        DynamicArray<Vector2> positions,
        DynamicArray<bool> visibility,
        bool loop,
        bool flatShading,
        float innerRadiusL,
        float innerRadiusR,
        float outerRadiusL,
        float outerRadiusR,
        Color innerColorL,
        Color innerColorR,
        Color outerColorL,
        Color outerColorR,
        float miterLimit,
        float depth = 0f
    )
    {
        var point = default(Point);
        point.InnerRadiusL = innerRadiusL;
        point.InnerRadiusR = innerRadiusR;
        point.OuterRadiusL = outerRadiusL;
        point.OuterRadiusR = outerRadiusR;
        point.InnerColorL = innerColorL;
        point.InnerColorR = innerColorR;
        point.OuterColorL = outerColorL;
        point.OuterColorR = outerColorR;
        point.MiterLimit = miterLimit;
        var value = point;
        _tmpPoints.Count = positions.Count;
        for (var i = 0; i < positions.Count; i++)
        {
            value.Position = positions[i];
            _tmpPoints[i] = value;
        }

        _tmpNormals.Count = 0;
        GeneratePathNormals(positions, visibility, loop, _tmpNormals);
        QueuePathInternal(batch, _tmpPoints, _tmpNormals, visibility, loop, flatShading, depth);
    }

    public static void QueuePath(
        TexturedBatch2D batch,
        DynamicArray<Point> points,
        DynamicArray<bool> visibility,
        bool loop,
        bool flatShading,
        float lengthOffset,
        float depth = 0f
    )
    {
        _tmpPositions.Count = 0;
        foreach (var point in points)
        {
            _tmpPositions.Add(point.Position);
        }

        _tmpNormals.Count = 0;
        GeneratePathNormals(_tmpPositions, visibility, loop, _tmpNormals);
        QueuePathInternal(batch, points, _tmpNormals, visibility, loop, flatShading, lengthOffset, depth);
    }

    public static void QueuePath(
        TexturedBatch2D batch,
        DynamicArray<Point> points,
        DynamicArray<Vector2> normals,
        DynamicArray<bool> visibility,
        bool loop,
        bool flatShading,
        float lengthOffset,
        float depth = 0f
    )
    {
        QueuePathInternal(batch, points, normals, visibility, loop, flatShading, lengthOffset, depth);
    }

    public static void QueuePath(
        TexturedBatch2D batch,
        DynamicArray<Vector2> positions,
        DynamicArray<bool> visibility,
        bool loop,
        bool flatShading,
        float innerRadius,
        float outerRadius,
        Color innerColor,
        Color outerColor,
        float lengthScale,
        float lengthOffset,
        float miterLimit,
        float depth = 0f
    )
    {
        QueuePath(
            batch,
            positions,
            visibility,
            loop,
            flatShading,
            innerRadius,
            innerRadius,
            outerRadius,
            outerRadius,
            innerColor,
            innerColor,
            outerColor,
            outerColor,
            lengthScale,
            lengthOffset,
            miterLimit,
            depth
        );
    }

    public static void QueuePath(
        TexturedBatch2D batch,
        DynamicArray<Vector2> positions,
        DynamicArray<bool> visibility,
        bool loop,
        bool flatShading,
        float innerRadiusL,
        float innerRadiusR,
        float outerRadiusL,
        float outerRadiusR,
        Color innerColorL,
        Color innerColorR,
        Color outerColorL,
        Color outerColorR,
        float lengthScale,
        float lengthOffset,
        float miterLimit,
        float depth = 0f
    )
    {
        var point = default(Point);
        point.InnerRadiusL = innerRadiusL;
        point.InnerRadiusR = innerRadiusR;
        point.OuterRadiusL = outerRadiusL;
        point.OuterRadiusR = outerRadiusR;
        point.InnerColorL = innerColorL;
        point.InnerColorR = innerColorR;
        point.OuterColorL = outerColorL;
        point.OuterColorR = outerColorR;
        point.LengthScale = lengthScale;
        point.MiterLimit = miterLimit;
        var value = point;
        _tmpPoints.Count = positions.Count;
        for (var i = 0; i < positions.Count; i++)
        {
            value.Position = positions[i];
            _tmpPoints[i] = value;
        }

        _tmpNormals.Count = 0;
        GeneratePathNormals(positions, visibility, loop, _tmpNormals);
        QueuePathInternal(batch, _tmpPoints, _tmpNormals, visibility, loop, flatShading, lengthOffset, depth);
    }

    private static void QueuePathInternal(
        FlatBatch2D batch,
        DynamicArray<Point> points,
        DynamicArray<Vector2> normals,
        DynamicArray<bool> visibility,
        bool loop,
        bool flatShading,
        float depth
    )
    {
        var segmentCount = loop ? normals.Count : normals.Count - 1;
        for (var i = 0; i < segmentCount; i++)
        {
            if (visibility.Count > i && !visibility[i])
            {
                continue;
            }

            var index2 = (i + 1) % normals.Count;
            var point = points[i];
            var point2 = points[index2];
            var position = point.Position;
            var position2 = point2.Position;
            var startNormal = normals[i];
            var endNormal = normals[index2];
            if (startNormal.LengthSquared() > point.MiterLimit)
            {
                startNormal = Normal1(position2 - position);
            }

            if (endNormal.LengthSquared() > point2.MiterLimit)
            {
                endNormal = Normal1(position2 - position);
            }

            if (point.OuterRadiusL != 0f || point2.OuterRadiusL != 0f)
            {
                var startInnerLeftOffset = startNormal * (0f - point.InnerRadiusL);
                var startOuterLeftOffset = startNormal * (0f - point.OuterRadiusL);
                var endInnerLeftOffset = endNormal * (0f - point2.InnerRadiusL);
                var endOuterLeftOffset = endNormal * (0f - point2.OuterRadiusL);
                if (flatShading)
                {
                    batch.QueueQuad(
                        position + startOuterLeftOffset,
                        position + startInnerLeftOffset,
                        position2 + endInnerLeftOffset,
                        position2 + endOuterLeftOffset,
                        0f,
                        point.OuterColorR,
                        point.InnerColorR,
                        point.InnerColorR,
                        point.OuterColorR
                    );
                }
                else
                {
                    batch.QueueQuad(
                        position + startOuterLeftOffset,
                        position + startInnerLeftOffset,
                        position2 + endInnerLeftOffset,
                        position2 + endOuterLeftOffset,
                        0f,
                        point.OuterColorR,
                        point.InnerColorR,
                        point2.InnerColorR,
                        point2.OuterColorR
                    );
                }
            }

            if (point.OuterRadiusR == 0f && point2.OuterRadiusR == 0f)
            {
                continue;
            }

            var startInnerRightOffset = startNormal * point.InnerRadiusR;
            var startOuterRightOffset = startNormal * point.OuterRadiusR;
            var endInnerRightOffset = endNormal * point2.InnerRadiusR;
            var endOuterRightOffset = endNormal * point2.OuterRadiusR;
            if (flatShading)
            {
                batch.QueueQuad(
                    position + startOuterRightOffset,
                    position + startInnerRightOffset,
                    position2 + endInnerRightOffset,
                    position2 + endOuterRightOffset,
                    0f,
                    point.OuterColorR,
                    point.InnerColorR,
                    point.InnerColorR,
                    point.OuterColorR
                );
            }
            else
            {
                batch.QueueQuad(
                    position + startOuterRightOffset,
                    position + startInnerRightOffset,
                    position2 + endInnerRightOffset,
                    position2 + endOuterRightOffset,
                    0f,
                    point.OuterColorR,
                    point.InnerColorR,
                    point2.InnerColorR,
                    point2.OuterColorR
                );
            }
        }
    }

    private static void QueuePathInternal(
        TexturedBatch2D batch,
        DynamicArray<Point> points,
        DynamicArray<Vector2> normals,
        DynamicArray<bool> visibility,
        bool loop,
        bool flatShading,
        float lengthOffset,
        float depth
    )
    {
        var textureU = lengthOffset;
        var segmentCount = loop ? normals.Count : normals.Count - 1;
        for (var i = 0; i < segmentCount; i++)
        {
            if (!visibility[i])
            {
                continue;
            }

            var index = i;
            var index2 = (i + 1) % normals.Count;
            var point = points[index];
            var point2 = points[index2];
            var position = point.Position;
            var position2 = point2.Position;
            var inverseLengthScale = 1f / point.LengthScale;
            var segmentDelta = position2 - position;
            var segmentLength = segmentDelta.Length();
            var textureGradient = segmentDelta / segmentLength * inverseLengthScale;
            var startNormal = normals[index];
            var endNormal = normals[index2];
            if (startNormal.LengthSquared() > point.MiterLimit)
            {
                startNormal = Normal1(position2 - position);
            }

            if (endNormal.LengthSquared() > point2.MiterLimit)
            {
                endNormal = Normal1(position2 - position);
            }

            var startUv = new Vector2(textureU, 0.5f);
            textureU += segmentLength * inverseLengthScale;
            var endUv = new Vector2(textureU, 0.5f);
            if (point.OuterRadiusL > 0f || point2.OuterRadiusL > 0f)
            {
                var startInnerLeftOffset = startNormal * (0f - point.InnerRadiusL);
                var startOuterLeftOffset = startNormal * (0f - point.OuterRadiusL);
                var endInnerLeftOffset = endNormal * (0f - point2.InnerRadiusL);
                var endOuterLeftOffset = endNormal * (0f - point2.OuterRadiusL);
                var startInnerLeftUvOffset = new Vector2(
                    Vector2.Dot(startInnerLeftOffset, textureGradient),
                    -0.5f * point.InnerRadiusL / point.OuterRadiusL
                );
                var startOuterLeftUvOffset = new Vector2(Vector2.Dot(startOuterLeftOffset, textureGradient), -0.5f);
                var endInnerLeftUvOffset = new Vector2(
                    Vector2.Dot(endInnerLeftOffset, textureGradient),
                    -0.5f * point2.InnerRadiusL / point2.OuterRadiusL
                );
                var endOuterLeftUvOffset = new Vector2(Vector2.Dot(endOuterLeftOffset, textureGradient), -0.5f);
                if (flatShading)
                {
                    batch.QueueQuad(
                        position + startOuterLeftOffset,
                        position + startInnerLeftOffset,
                        position2 + endInnerLeftOffset,
                        position2 + endOuterLeftOffset,
                        0f,
                        startUv + startOuterLeftUvOffset,
                        startUv + startInnerLeftUvOffset,
                        endUv + endInnerLeftUvOffset,
                        endUv + endOuterLeftUvOffset,
                        point.OuterColorR,
                        point.InnerColorR,
                        point.InnerColorR,
                        point.OuterColorR
                    );
                }
                else
                {
                    batch.QueueQuad(
                        position + startOuterLeftOffset,
                        position + startInnerLeftOffset,
                        position2 + endInnerLeftOffset,
                        position2 + endOuterLeftOffset,
                        0f,
                        startUv + startOuterLeftUvOffset,
                        startUv + startInnerLeftUvOffset,
                        endUv + endInnerLeftUvOffset,
                        endUv + endOuterLeftUvOffset,
                        point.OuterColorR,
                        point.InnerColorR,
                        point2.InnerColorR,
                        point2.OuterColorR
                    );
                }
            }

            if (point.OuterRadiusR == 0f && point2.OuterRadiusR == 0f)
            {
                continue;
            }

            var startInnerRightOffset = startNormal * point.InnerRadiusR;
            var startOuterRightOffset = startNormal * point.OuterRadiusR;
            var endInnerRightOffset = endNormal * point2.InnerRadiusR;
            var endOuterRightOffset = endNormal * point2.OuterRadiusR;
            var startInnerRightUvOffset = new Vector2(
                Vector2.Dot(startInnerRightOffset, textureGradient),
                0.5f * point.InnerRadiusR / point.OuterRadiusR
            );
            var startOuterRightUvOffset = new Vector2(Vector2.Dot(startOuterRightOffset, textureGradient), 0.5f);
            var endInnerRightUvOffset = new Vector2(
                Vector2.Dot(endInnerRightOffset, textureGradient),
                0.5f * point2.InnerRadiusR / point2.OuterRadiusR
            );
            var endOuterRightUvOffset = new Vector2(Vector2.Dot(endOuterRightOffset, textureGradient), 0.5f);
            if (flatShading)
            {
                batch.QueueQuad(
                    position + startOuterRightOffset,
                    position + startInnerRightOffset,
                    position2 + endInnerRightOffset,
                    position2 + endOuterRightOffset,
                    0f,
                    startUv + startOuterRightUvOffset,
                    startUv + startInnerRightUvOffset,
                    endUv + endInnerRightUvOffset,
                    endUv + endOuterRightUvOffset,
                    point.OuterColorR,
                    point.InnerColorR,
                    point.InnerColorR,
                    point.OuterColorR
                );
            }
            else
            {
                batch.QueueQuad(
                    position + startOuterRightOffset,
                    position + startInnerRightOffset,
                    position2 + endInnerRightOffset,
                    position2 + endOuterRightOffset,
                    0f,
                    startUv + startOuterRightUvOffset,
                    startUv + startInnerRightUvOffset,
                    endUv + endInnerRightUvOffset,
                    endUv + endOuterRightUvOffset,
                    point.OuterColorR,
                    point.InnerColorR,
                    point2.InnerColorR,
                    point2.OuterColorR
                );
            }
        }
    }

    private static Vector2 Normal1(Vector2 d)
    {
        return Vector2.Perpendicular(Vector2.Normalize(d));
    }

    private static Vector2 Normal2(Vector2 d1, Vector2 d2)
    {
        d1 = Vector2.Normalize(d1);
        var halfInteriorAngleTangent = MathUtils.Tan(((float)Math.PI - Vector2.Angle(d1, d2)) / 2f);
        return Vector2.Perpendicular(d1) - d1 / halfInteriorAngleTangent;
    }

    public struct Point : IEquatable<Point>
    {
        public Vector2 Position;

        public float InnerRadiusL;

        public float InnerRadiusR;

        public float OuterRadiusL;

        public float OuterRadiusR;

        public Color InnerColorL;

        public Color InnerColorR;

        public Color OuterColorL;

        public Color OuterColorR;

        public float LengthScale;

        public float MiterLimit;

        public float InnerRadius { set => InnerRadiusL = InnerRadiusR = value; }

        public float OuterRadius { set => OuterRadiusL = OuterRadiusR = value; }

        public Color InnerColor { set => InnerColorL = InnerColorR = value; }

        public Color OuterColor { set => OuterColorL = OuterColorR = value; }

        public Color Color
        {
            set
            {
                var innerColor = OuterColor = value;
                InnerColor = innerColor;
            }
        }

        bool IEquatable<Point>.Equals(Point other)
        {
            return Position == other.Position &&
                   InnerRadiusL.CloseTo(other.InnerRadiusL) &&
                   InnerRadiusR.CloseTo(other.InnerRadiusR) &&
                   OuterRadiusL.CloseTo(other.OuterRadiusL) &&
                   OuterRadiusR.CloseTo(other.OuterRadiusR) &&
                   InnerColorL == other.InnerColorL &&
                   InnerColorR == other.InnerColorR &&
                   OuterColorL == other.OuterColorL &&
                   OuterColorR == other.OuterColorR &&
                   LengthScale.CloseTo(other.LengthScale) &&
                   MiterLimit.CloseTo(other.MiterLimit);
        }
    }
}
