using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace EHR.Modules.Extensions;

public static class FastVector2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains2D(this Bounds b, Vector2 p)
    {
        return
            p.x >= b.min.x && p.x <= b.max.x &&
            p.y >= b.min.y && p.y <= b.max.y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool DistanceWithinRange(in Vector2 a, in Vector2 b, float range)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return dx * dx + dy * dy <= range * range;
    }
    
    /// <summary>
    /// Finds all living players from origin in a given range.
    /// </summary>
    public static IEnumerable<PlayerControl> GetPlayersInRange(Vector2 origin, float range, Predicate<PlayerControl> predicate = null)
    {
        predicate ??= _ => true;
        float rangeSq = range * range;

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
        {
            if (pc.inVent || !predicate(pc)) continue;
            
            Vector2 p = pc.Pos();
            float dx = p.x - origin.x;
            float dy = p.y - origin.y;
            float sq = dx * dx + dy * dy;

            if (sq > rangeSq) continue;

            yield return pc;
        }
    }
    
    /// <summary>
    /// Finds the closest position to origin within the given range.
    /// Returns true if one was found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetClosestInRange(
        in Vector2 origin,
        IEnumerable<Vector2> positions,
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
    /// Finds the closest position to origin.
    /// Returns true if one was found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetClosest(
        in Vector2 origin,
        IEnumerable<Vector2> positions,
        out Vector2 closest)
    {
        float minSq = float.MaxValue;
        bool found = false;
        closest = default(Vector2);

        foreach (Vector2 p in positions)
        {
            float dx = p.x - origin.x;
            float dy = p.y - origin.y;
            float sq = dx * dx + dy * dy;

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
    /// Finds the closest living player to origin.
    /// Returns true if one was found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetClosestPlayer(
        in Vector2 origin,
        out PlayerControl closest,
        [Annotations.InstantHandle] Predicate<PlayerControl> predicate = null)
    {
        predicate ??= _ => true;
        float minSq = float.MaxValue;
        bool found = false;
        closest = null;

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
        {
            if (pc.inVent || !predicate(pc)) continue;
            
            Vector2 p = pc.Pos();
            float dx = p.x - origin.x;
            float dy = p.y - origin.y;
            float sq = dx * dx + dy * dy;

            if (sq < minSq)
            {
                minSq = sq;
                closest = pc;
                found = true;
            }
        }

        return found;
    }
    
    /// <summary>
    /// Finds the closest living player to the source player, exempting themselves.
    /// Returns true if one was found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetClosestPlayerTo(
        in PlayerControl source,
        out PlayerControl closest,
        [Annotations.InstantHandle] Predicate<PlayerControl> predicate = null)
    {
        predicate ??= _ => true;
        Vector2 origin = source.Pos();
        float minSq = float.MaxValue;
        bool found = false;
        closest = null;

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
        {
            if (pc.inVent || pc.PlayerId == source.PlayerId || !predicate(pc)) continue;
            
            Vector2 p = pc.Pos();
            float dx = p.x - origin.x;
            float dy = p.y - origin.y;
            float sq = dx * dx + dy * dy;

            if (sq < minSq)
            {
                minSq = sq;
                closest = pc;
                found = true;
            }
        }

        return found;
    }
    
    /// <summary>
    /// Finds the closest living player to the source player within the given range, exempting themselves.
    /// Returns true if one was found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetClosestPlayerInRangeTo(
        in PlayerControl source,
        float range,
        out PlayerControl closest,
        [Annotations.InstantHandle] Predicate<PlayerControl> predicate = null)
    {
        predicate ??= _ => true;
        Vector2 origin = source.Pos();
        float rangeSq = range * range;
        float minSq = float.MaxValue;
        bool found = false;
        closest = null;

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
        {
            if (pc.inVent || pc.PlayerId == source.PlayerId || !predicate(pc)) continue;
            
            Vector2 p = pc.Pos();
            float dx = p.x - origin.x;
            float dy = p.y - origin.y;
            float sq = dx * dx + dy * dy;

            if (sq > rangeSq) continue;

            if (sq < minSq)
            {
                minSq = sq;
                closest = pc;
                found = true;
            }
        }

        return found;
    }
    
    /// <summary>
    /// Finds the closest living player to origin within the given range.
    /// Returns true if one was found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetClosestPlayerInRange(
        in Vector2 origin,
        float range,
        out PlayerControl closest,
        Predicate<PlayerControl> predicate = null)
    {
        predicate ??= _ => true;
        float rangeSq = range * range;
        float minSq = float.MaxValue;
        bool found = false;
        closest = null;

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
        {
            if (pc.inVent || !predicate(pc)) continue;
            
            Vector2 p = pc.Pos();
            float dx = p.x - origin.x;
            float dy = p.y - origin.y;
            float sq = dx * dx + dy * dy;

            if (sq > rangeSq) continue;

            if (sq < minSq)
            {
                minSq = sq;
                closest = pc;
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