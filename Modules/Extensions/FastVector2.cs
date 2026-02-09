using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace EHR.Modules.Extensions;

public static class FastVector2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool DistanceWithinRange(in Vector2 a, in Vector2 b, float range)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return dx * dx + dy * dy <= range * range;
    }
    
    /// <summary>
    /// Finds the closest position to origin within the given range.
    /// Returns true if one was found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetClosestInRange(
        in Vector2 origin,
        IReadOnlyList<Vector2> positions,
        float range,
        out Vector2 closest)
    {
        float rangeSq = range * range;
        float minSq = float.MaxValue;
        bool found = false;
        closest = default(Vector2);

        foreach (Vector2 p in positions)
        {
            float dx = p.x - origin.x;
            float dy = p.y - origin.y;
            float sq = dx * dx + dy * dy;

            if (sq > rangeSq) continue;

            if (sq < minSq)
            {
                minSq = sq;
                closest = p;
                found = true;
            }
        }

        return found;
    }
    
    /// <summary>
    /// Finds the closest position to origin within the given range from the values of the given dictionary,
    /// and returns the key assiciated with it in the dictionary.
    /// Returns true if one was found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetClosestInRange<T>(
        in Vector2 origin,
        Dictionary<T, Vector2> positions,
        float range,
        out T closest)
    {
        float rangeSq = range * range;
        float minSq = float.MaxValue;
        bool found = false;
        closest = default(T);

        foreach ((T t, Vector2 p) in positions)
        {
            float dx = p.x - origin.x;
            float dy = p.y - origin.y;
            float sq = dx * dx + dy * dy;

            if (sq > rangeSq) continue;

            if (sq < minSq)
            {
                minSq = sq;
                closest = t;
                found = true;
            }
        }

        return found;
    }
    
    /// <summary>
    /// Finds the closest position to origin within the given range from the keys of the given dictionary,
    /// and returns the value assiciated with it in the dictionary.
    /// Returns true if one was found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetClosestInRange<T>(
        in Vector2 origin,
        Dictionary<Vector2, T> positions,
        float range,
        out T closest)
    {
        float rangeSq = range * range;
        float minSq = float.MaxValue;
        bool found = false;
        closest = default(T);

        foreach ((Vector2 p, T t) in positions)
        {
            float dx = p.x - origin.x;
            float dy = p.y - origin.y;
            float sq = dx * dx + dy * dy;

            if (sq > rangeSq) continue;

            if (sq < minSq)
            {
                minSq = sq;
                closest = t;
                found = true;
            }
        }

        return found;
    }
}