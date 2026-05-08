using System.Drawing;

namespace Engine.Serialization;

internal class SystemDrawingTypesConverters
{
    [HumanReadableConverter(typeof(Color))]
    internal class ColorStringConverter : IHumanReadableConverter
    {
        public string ConvertToString(object value)
        {
            var color = (Color)value;
            if (color.A != byte.MaxValue)
            {
                return HumanReadableConverter.ValuesListToString(',', color.A, color.R, color.G, color.B);
            }

            return HumanReadableConverter.ValuesListToString(',', color.R, color.G, color.B);
        }

        public object ConvertFromString(Type type, string data)
        {
            data = data.Trim();
            if (data.Length > 0)
            {
                if (!char.IsDigit(data[0]) && data[0] != '-' && data[0] != '+')
                {
                    return Color.FromName(data);
                }

                var array = HumanReadableConverter.ValuesListFromString<byte>(',', data);
                if (array.Length == 3)
                {
                    return Color.FromArgb(array[0], array[1], array[2]);
                }

                if (array.Length == 4)
                {
                    return Color.FromArgb(array[0], array[1], array[2], array[3]);
                }
            }

            throw new InvalidOperationException(
                $"Cannot convert string \"{data}\" to a value of type {type.FullName}.");
        }
    }

    [HumanReadableConverter(typeof(Point))]
    internal class PointStringConverter : IHumanReadableConverter
    {
        public string ConvertToString(object value)
        {
            var point = (Point)value;
            return HumanReadableConverter.ValuesListToString(',', point.X, point.Y);
        }

        public object ConvertFromString(Type type, string data)
        {
            var array = HumanReadableConverter.ValuesListFromString<int>(',', data);
            if (array.Length == 2)
            {
                return new Point(array[0], array[1]);
            }

            throw new InvalidOperationException(
                $"Cannot convert string \"{data}\" to a value of type {type.FullName}.");
        }
    }

    [HumanReadableConverter(typeof(PointF))]
    internal class PointFStringConverter : IHumanReadableConverter
    {
        public string ConvertToString(object value)
        {
            var pointF = (PointF)value;
            return HumanReadableConverter.ValuesListToString(',', pointF.X, pointF.Y);
        }

        public object ConvertFromString(Type type, string data)
        {
            var array = HumanReadableConverter.ValuesListFromString<float>(',', data);
            if (array.Length == 2)
            {
                return new PointF(array[0], array[1]);
            }

            throw new InvalidOperationException(
                $"Cannot convert string \"{data}\" to a value of type {type.FullName}.");
        }
    }

    [HumanReadableConverter(typeof(Size))]
    internal class SizeStringConverter : IHumanReadableConverter
    {
        public string ConvertToString(object value)
        {
            var size = (Size)value;
            return HumanReadableConverter.ValuesListToString(',', size.Width, size.Height);
        }

        public object ConvertFromString(Type type, string data)
        {
            var array = HumanReadableConverter.ValuesListFromString<int>(',', data);
            if (array.Length == 2)
            {
                return new Size(array[0], array[1]);
            }

            throw new InvalidOperationException(
                $"Cannot convert string \"{data}\" to a value of type {type.FullName}.");
        }
    }

    [HumanReadableConverter(typeof(SizeF))]
    internal class SizeFStringConverter : IHumanReadableConverter
    {
        public string ConvertToString(object value)
        {
            var sizeF = (SizeF)value;
            return HumanReadableConverter.ValuesListToString(',', sizeF.Width, sizeF.Height);
        }

        public object ConvertFromString(Type type, string data)
        {
            var array = HumanReadableConverter.ValuesListFromString<float>(',', data);
            if (array.Length == 2)
            {
                return new SizeF(array[0], array[1]);
            }

            throw new InvalidOperationException(
                $"Cannot convert string \"{data}\" to a value of type {type.FullName}.");
        }
    }

    [HumanReadableConverter(typeof(Rectangle))]
    internal class RectangleStringConverter : IHumanReadableConverter
    {
        public string ConvertToString(object value)
        {
            var rectangle = (Rectangle)value;
            return HumanReadableConverter.ValuesListToString(',', rectangle.X, rectangle.Y, rectangle.Width,
                rectangle.Height);
        }

        public object ConvertFromString(Type type, string data)
        {
            var array = HumanReadableConverter.ValuesListFromString<int>(',', data);
            if (array.Length == 4)
            {
                return new Rectangle(array[0], array[1], array[2], array[3]);
            }

            throw new InvalidOperationException(
                $"Cannot convert string \"{data}\" to a value of type {type.FullName}.");
        }
    }

    [HumanReadableConverter(typeof(RectangleF))]
    internal class RectangleFStringConverter : IHumanReadableConverter
    {
        public string ConvertToString(object value)
        {
            var rectangleF = (RectangleF)value;
            return HumanReadableConverter.ValuesListToString(',', rectangleF.X, rectangleF.Y, rectangleF.Width,
                rectangleF.Height);
        }

        public object ConvertFromString(Type type, string data)
        {
            var array = HumanReadableConverter.ValuesListFromString<float>(',', data);
            if (array.Length == 4)
            {
                return new RectangleF(array[0], array[1], array[2], array[3]);
            }

            throw new InvalidOperationException(
                $"Cannot convert string \"{data}\" to a value of type {type.FullName}.");
        }
    }
}
