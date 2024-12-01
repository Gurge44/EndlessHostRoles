using System;

namespace EHR;

public class NetRandomWrapper(Random instance) : IRandom
{
    public Random wrapping = instance;

    public NetRandomWrapper() : this(new Random()) { }

    public NetRandomWrapper(int seed) : this(new Random(seed)) { }

    public int Next(int minValue, int maxValue)
    {
        return wrapping.Next(minValue, maxValue);
    }

    public int Next(int maxValue)
    {
        return wrapping.Next(maxValue);
    }

    public int Next()
    {
        return wrapping.Next();
    }
}