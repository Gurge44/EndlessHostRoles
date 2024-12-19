#pragma warning disable CA1822
namespace EHR;

public class HashRandomWrapper : IRandom
{
    public int Next(int minValue, int maxValue)
    {
        return HashRandom.Next(minValue, maxValue);
    }

    public int Next(int maxValue)
    {
        return HashRandom.Next(maxValue);
    }

    public uint Next()
    {
        return HashRandom.Next();
    }

    public int FastNext(int maxValue)
    {
        return HashRandom.FastNext(maxValue);
    }
}