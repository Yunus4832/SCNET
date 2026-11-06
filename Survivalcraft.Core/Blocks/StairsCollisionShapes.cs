namespace Game.Blocks;

public static class StairsCollisionShapes
{
    public static BoundingBox[][] Create()
    {
        var boxes = new BoundingBox[24][];
        boxes[0] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.5f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.5f, 1f))
        ];
        boxes[1] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 1f, 1f)),
            new(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.5f, 1f))
        ];
        boxes[2] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 0.5f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 1f, 1f))
        ];
        boxes[3] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.5f, 1f)),
            new(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 1f, 1f))
        ];
        boxes[4] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.5f)),
            new(new Vector3(0f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f))
        ];
        boxes[5] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 1f, 1f)),
            new(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f))
        ];
        boxes[6] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.5f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 1f, 1f))
        ];
        boxes[7] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1f, 1f)),
            new(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 1f, 1f))
        ];
        boxes[8] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0.5f))
        ];
        boxes[9] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1f, 0.5f))
        ];
        boxes[10] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0.5f), new Vector3(0.5f, 1f, 1f))
        ];
        boxes[11] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f))
        ];
        boxes[12] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.5f, 0.5f))
        ];
        boxes[13] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f))
        ];
        boxes[14] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(0.5f, 0.5f, 1f))
        ];
        boxes[15] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0.5f, 0f, 0.5f), new Vector3(1f, 0.5f, 1f))
        ];
        boxes[16] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.5f)),
            new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f))
        ];
        boxes[17] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.5f)),
            new(new Vector3(0f, 0.5f, 0.5f), new Vector3(0.5f, 1f, 1f))
        ];
        boxes[18] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1f, 0.5f))
        ];
        boxes[19] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0.5f))
        ];
        boxes[20] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 0.5f)),
            new(new Vector3(0.5f, 0f, 0.5f), new Vector3(1f, 0.5f, 1f))
        ];
        boxes[21] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 0.5f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(0.5f, 0.5f, 1f))
        ];
        boxes[22] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f))
        ];
        boxes[23] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.5f, 0.5f))
        ];
        return boxes;
    }
}
