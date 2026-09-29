using System;

namespace EHR;

public static class EnumHelper
{
    public static T[] GetValues<T>() => (T[])Enum.GetValues(typeof(T));
    public static string[] GetNames<T>() => Enum.GetNames(typeof(T));
}