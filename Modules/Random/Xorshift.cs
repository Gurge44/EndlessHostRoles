using System;

namespace EHR;

public class Xorshift(uint seed) : IRandom
{
    // 参考元
    public const string REFERENCE = "https://ja.wikipedia.org/wiki/Xorshift";

    private uint num = seed;

    public Xorshift() : this((uint)DateTime.UtcNow.Ticks)
    { }

    public uint Next()
    {
        num ^= num << 13;
        num ^= num >> 17;
        num ^= num << 5;

        return num;
    }
    public int Next(int minValue, int maxValue)
    {
        if (minValue < 0 || maxValue < 0) throw new ArgumentOutOfRangeException("minValue and maxValue must be bigger than 0.");
        if (minValue > maxValue) throw new ArgumentException("maxValue must be bigger than minValue.");
        if (minValue == maxValue) return minValue;

        return (int)(minValue + (Next() % (maxValue - minValue)));
    }
    public int Next(int maxValue) => Next(0, maxValue);
}