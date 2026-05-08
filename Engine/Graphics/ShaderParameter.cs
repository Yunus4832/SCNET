using Engine.Core;

namespace Engine.Graphics;

public class ShaderParameter
{
    public readonly int Count;

    public readonly string Name;

    public readonly Shader? Shader;

    public readonly ShaderParameterType Type;

    internal bool isChanged = true;

    internal int location;

    internal object? resource;

    internal readonly float[] value = [];

    internal ShaderParameter(string name, ShaderParameterType type)
    {
        Name = name;
        Type = type;
    }

    internal ShaderParameter(Shader shader, string name, ShaderParameterType type, int count)
    {
        Shader = shader;
        Name = name;
        Type = type;
        Count = count;
        switch (type)
        {
            case ShaderParameterType.Texture2D:
            case ShaderParameterType.Sampler2D:
                break;
            case ShaderParameterType.Float:
                value = new float[count];
                break;
            case ShaderParameterType.Vector2:
                value = new float[2 * count];
                break;
            case ShaderParameterType.Vector3:
                value = new float[3 * count];
                break;
            case ShaderParameterType.Vector4:
                value = new float[4 * count];
                break;
            case ShaderParameterType.Matrix:
                value = new float[16 * count];
                break;
            default:
                throw new ArgumentException(null, nameof(type));
        }
    }

    public void SetValue(float inputValue)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != 0 || Count != 1)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (inputValue.CloseTo(this.value[0]))
        {
            return;
        }

        this.value[0] = inputValue;
        isChanged = true;
    }

    public void SetValue(float[] inputValue, int count)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != 0)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (count < 0 || count > inputValue.Length || count > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (!isChanged)
        {
            for (var i = 0; i < count; i++)
            {
                if (value[i].CloseTo(inputValue[i]))
                {
                    continue;
                }

                isChanged = true;
                break;
            }
        }

        for (var j = 0; j < count; j++)
        {
            this.value[j] = inputValue[j];
        }

        isChanged = true;
    }

    public void SetValue(Vector2 inputValue)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != ShaderParameterType.Vector2 || Count != 1)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (!isChanged && inputValue.X.CloseTo(this.value[0]) && inputValue.Y.CloseTo(this.value[1]))
        {
            return;
        }

        value[0] = inputValue.X;
        value[1] = inputValue.Y;
        isChanged = true;
    }

    public void SetValue(Vector2[] inputValue, int count)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != ShaderParameterType.Vector2)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (count < 0 || count > inputValue.Length || count > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (!isChanged)
        {
            var i = 0;
            var num = 0;
            for (; i < count; i++)
            {
                if (this.value[num++].CloseTo(inputValue[i].X) && this.value[num++].CloseTo(inputValue[i].Y))
                {
                    continue;
                }

                isChanged = true;
                break;
            }
        }

        var j = 0;
        var num2 = 0;
        for (; j < count; j++)
        {
            this.value[num2++] = inputValue[j].X;
            this.value[num2++] = inputValue[j].Y;
        }
    }

    public void SetValue(Vector3 inputValue)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != ShaderParameterType.Vector3 || Count != 1)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (!isChanged && inputValue.X.CloseTo(value[0]) && inputValue.Y.CloseTo(value[1]) &&
            inputValue.Z.CloseTo(value[2]))
        {
            return;
        }

        this.value[0] = inputValue.X;
        this.value[1] = inputValue.Y;
        this.value[2] = inputValue.Z;
        isChanged = true;
    }

    public void SetValue(Vector3[] inputValue, int count)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != ShaderParameterType.Vector3)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (count < 0 || count > inputValue.Length || count > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (!isChanged)
        {
            var i = 0;
            var num = 0;
            for (; i < count; i++)
            {
                if (this.value[num++].CloseTo(inputValue[i].X) &&
                    this.value[num++].CloseTo(inputValue[i].Y) &&
                    this.value[num++].CloseTo(inputValue[i].Z))
                {
                    continue;
                }

                isChanged = true;
                break;
            }
        }

        var j = 0;
        var num2 = 0;
        for (; j < count; j++)
        {
            value[num2++] = inputValue[j].X;
            value[num2++] = inputValue[j].Y;
            value[num2++] = inputValue[j].Z;
        }
    }

    public void SetValue(Vector4 inputValue)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != ShaderParameterType.Vector4 || Count != 1)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (!isChanged &&
            inputValue.X.CloseTo(value[0]) &&
            inputValue.Y.CloseTo(value[1]) &&
            inputValue.Z.CloseTo(value[2]) &&
            inputValue.W.CloseTo(value[3]))
        {
            return;
        }

        value[0] = inputValue.X;
        value[1] = inputValue.Y;
        value[2] = inputValue.Z;
        value[3] = inputValue.W;
        isChanged = true;
    }

    public void SetValue(Vector4[] inputValue, int count)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != ShaderParameterType.Vector4)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (count < 0 || count > inputValue.Length || count > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (!isChanged)
        {
            var i = 0;
            var num = 0;
            for (; i < count; i++)
            {
                if (value[num++].CloseTo(inputValue[i].X) &&
                    value[num++].CloseTo(inputValue[i].Y) &&
                    value[num++].CloseTo(inputValue[i].Z) &&
                    value[num++].CloseTo(inputValue[i].W))
                {
                    continue;
                }

                isChanged = true;
                break;
            }
        }

        var j = 0;
        var num2 = 0;
        for (; j < count; j++)
        {
            this.value[num2++] = inputValue[j].X;
            this.value[num2++] = inputValue[j].Y;
            this.value[num2++] = inputValue[j].Z;
            this.value[num2++] = inputValue[j].W;
        }
    }

    public void SetValue(Matrix inputValue)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != ShaderParameterType.Matrix || Count != 1)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (!isChanged && inputValue.M11.CloseTo(value[0]) && inputValue.M12.CloseTo(value[1]) &&
            inputValue.M13.CloseTo(value[2]) &&
            inputValue.M14.CloseTo(value[3]) && inputValue.M21.CloseTo(value[4]) && inputValue.M22.CloseTo(value[5]) &&
            inputValue.M23.CloseTo(value[6]) && inputValue.M24.CloseTo(value[7]) && inputValue.M31.CloseTo(value[8]) &&
            inputValue.M32.CloseTo(value[9]) && inputValue.M33.CloseTo(value[10]) &&
            inputValue.M34.CloseTo(value[11]) &&
            inputValue.M41.CloseTo(value[12]) && inputValue.M42.CloseTo(value[13]) &&
            inputValue.M43.CloseTo(value[14]) &&
            inputValue.M44.CloseTo(value[15]))
        {
            return;
        }

        value[0] = inputValue.M11;
        value[1] = inputValue.M12;
        value[2] = inputValue.M13;
        value[3] = inputValue.M14;
        value[4] = inputValue.M21;
        value[5] = inputValue.M22;
        value[6] = inputValue.M23;
        value[7] = inputValue.M24;
        value[8] = inputValue.M31;
        value[9] = inputValue.M32;
        value[10] = inputValue.M33;
        value[11] = inputValue.M34;
        value[12] = inputValue.M41;
        value[13] = inputValue.M42;
        value[14] = inputValue.M43;
        value[15] = inputValue.M44;
        isChanged = true;
    }

    public void SetValue(Matrix[] inputValue, int count)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != ShaderParameterType.Matrix)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (count < 0 || count > inputValue.Length || count > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (!isChanged)
        {
            var i = 0;
            var num = 0;
            for (; i < count; i++)
            {
                if (this.value[num++].CloseTo(inputValue[i].M11) && this.value[num++].CloseTo(inputValue[i].M12) &&
                    this.value[num++].CloseTo(inputValue[i].M13) && this.value[num++].CloseTo(inputValue[i].M14) &&
                    this.value[num++].CloseTo(inputValue[i].M21) && this.value[num++].CloseTo(inputValue[i].M22) &&
                    this.value[num++].CloseTo(inputValue[i].M23) && this.value[num++].CloseTo(inputValue[i].M24) &&
                    this.value[num++].CloseTo(inputValue[i].M31) && this.value[num++].CloseTo(inputValue[i].M32) &&
                    this.value[num++].CloseTo(inputValue[i].M33) && this.value[num++].CloseTo(inputValue[i].M34) &&
                    this.value[num++].CloseTo(inputValue[i].M41) && this.value[num++].CloseTo(inputValue[i].M42) &&
                    this.value[num++].CloseTo(inputValue[i].M43) && this.value[num++].CloseTo(inputValue[i].M44))
                {
                    continue;
                }

                isChanged = true;
                break;
            }
        }

        var j = 0;
        var num2 = 0;
        for (; j < count; j++)
        {
            value[num2++] = inputValue[j].M11;
            value[num2++] = inputValue[j].M12;
            value[num2++] = inputValue[j].M13;
            value[num2++] = inputValue[j].M14;
            value[num2++] = inputValue[j].M21;
            value[num2++] = inputValue[j].M22;
            value[num2++] = inputValue[j].M23;
            value[num2++] = inputValue[j].M24;
            value[num2++] = inputValue[j].M31;
            value[num2++] = inputValue[j].M32;
            value[num2++] = inputValue[j].M33;
            value[num2++] = inputValue[j].M34;
            value[num2++] = inputValue[j].M41;
            value[num2++] = inputValue[j].M42;
            value[num2++] = inputValue[j].M43;
            value[num2++] = inputValue[j].M44;
        }
    }

    public void SetValue(Texture2D inputValue)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != ShaderParameterType.Texture2D || Count != 1)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (inputValue == resource)
        {
            return;
        }

        resource = inputValue;
        isChanged = true;
    }

    public void SetValue(SamplerState inputValue)
    {
        if (Type == ShaderParameterType.Null)
        {
            return;
        }

        if (Type != ShaderParameterType.Sampler2D || Count != 1)
        {
            throw new InvalidOperationException("Shader parameter type mismatch.");
        }

        if (inputValue == resource)
        {
            return;
        }

        resource = inputValue;
        isChanged = true;
    }
}
