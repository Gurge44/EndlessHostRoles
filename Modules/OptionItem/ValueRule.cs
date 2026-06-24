using System;

namespace EHR;

public abstract class ValueRule<T>(T minValue, T maxValue, T step)
{
    protected ValueRule((T, T, T) tuple)
        : this(tuple.Item1, tuple.Item2, tuple.Item3) { }

    public T MinValue { get; } = minValue;
    public T MaxValue { get; } = maxValue;
    public T Step { get; } = step;
}

public class IntegerValueRule : ValueRule<int>
{
    public IntegerValueRule(int minValue, int maxValue, int step)
        : base(minValue, maxValue, step) { }

    public IntegerValueRule((int, int, int) tuple)
        : base(tuple) { }

    public static implicit operator IntegerValueRule((int, int, int) tuple)
    {
        return new(tuple);
    }

    public virtual int RepeatIndex(int value)
    {
        int maxIndex = (MaxValue - MinValue) / Step;
        value %= maxIndex + 1;
        if (value < 0) value = maxIndex;

        return value;
    }

    public virtual int GetValueByIndex(int index)
    {
        return (RepeatIndex(index) * Step) + MinValue;
    }

    public virtual int GetNearestIndex(int num)
    {
        return (int)Math.Round((num - MinValue) / (float)Step);
    }
}

public class FloatValueRule : ValueRule<float>
{
    public FloatValueRule(float minValue, float maxValue, float step)
        : base(minValue, maxValue, step) { }

    public FloatValueRule((float, float, float) tuple)
        : base(tuple) { }

    public static implicit operator FloatValueRule((float, float, float) tuple)
    {
        return new(tuple);
    }

    public virtual int RepeatIndex(int value)
    {
        var maxIndex = (int)((MaxValue - MinValue) / Step);
        value %= maxIndex + 1;
        if (value < 0) value = maxIndex;

        return value;
    }

    public virtual float GetValueByIndex(int index)
    {
        return (RepeatIndex(index) * Step) + MinValue;
    }

    public virtual int GetNearestIndex(float num)
    {
        return (int)Math.Round((num - MinValue) / Step);
    }
}