#if !IL2CPP
using System.Collections.Generic;
using System.Linq;

namespace EHR;

public static class LinqHelpers
{
    public static IEnumerable<(TFirst First, TSecond Second)> Zip<TFirst, TSecond>(
        this IEnumerable<TFirst> first,
        IEnumerable<TSecond> second)
    {
        return first.Zip(second, (f, s) => (First: f, Second: s));
    }
}
#endif