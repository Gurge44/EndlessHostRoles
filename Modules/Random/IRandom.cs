using System;
using System.Collections.Generic;

namespace EHR;

public interface IRandom
{
    // A list of classes that implement IRandom
    public static readonly Dictionary<int, Type> RandomTypes = new()
    {
        { 0, typeof(NetRandomWrapper) }, // Default
        { 1, typeof(NetRandomWrapper) },
        { 2, typeof(HashRandomWrapper) },
        { 3, typeof(Xorshift) },
        { 4, typeof(MersenneTwister) }
    };

    public static IRandom Instance { get; private set; }

    /// <summary>Generates a random number greater than or equal to 0 and less than maxValue.</summary>
    public int Next(int maxValue);

    /// <summary>Generates a random number greater than or equal to minValue and less than maxValue.</summary>
    public int Next(int minValue, int maxValue);

    // == static ==

    /// <summary>
    /// Generates a sequence of random numbers.
    /// </summary>
    /// <param name="count">The number of random numbers to generate.</param>
    /// <param name="minValue">The inclusive lower bound of the random numbers to generate.</param>
    /// <param name="maxValue">The exclusive upper bound of the random numbers to generate.</param>
    /// <returns>A sequence of random numbers.</returns>
    public static IEnumerable<int> Sequence(int count, int minValue, int maxValue)
    {
        for (int i = 0; i < count; i++)
        {
            yield return Instance.Next(minValue, maxValue);
        }
    }

    /// <summary>
    /// Generates a sequence of unique random numbers.
    /// </summary>
    /// <param name="count">The number of random numbers to generate.</param>
    /// <param name="minValue">The inclusive lower bound of the random numbers to generate.</param>
    /// <param name="maxValue">The exclusive upper bound of the random numbers to generate.</param>
    /// <returns>A sequence of unique random numbers.</returns>
    public static IEnumerable<int> SequenceUnique(int count, int minValue, int maxValue)
    {
        var set = new HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            // If all possible values are used, the loop will be infinite. Break the loop if the set is full.
            if (set.Count == maxValue - minValue) break;
            int value;
            do value = Instance.Next(minValue, maxValue);
            while (!set.Add(value));
            yield return value;
        }
    }

    public static void SetInstance(IRandom instance)
    {
        if (instance != null)
            Instance = instance;
    }

    public static void SetInstanceById(int id)
    {
        if (RandomTypes.TryGetValue(id, out var type))
        {
            // The current instance is null or the type of the current instance does not match the specified type.
            if (Instance == null || Instance.GetType() != type)
            {
                Instance = Activator.CreateInstance(type) as IRandom ?? Instance;
            }
        }
        else Logger.Warn($"無効なID: {id}", "IRandom.SetInstanceById");
    }
}