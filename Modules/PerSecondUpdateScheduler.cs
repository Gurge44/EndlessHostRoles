using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace EHR.Modules;

public static class PerSecondUpdateScheduler
{
    private const int Slots = 30;

    private static long FixedTick;

    private static int CurrentSlot => (int)(FixedTick % Slots);

    private struct Entry
    {
        public long LastSecond;
    }

    private static readonly Dictionary<int, Entry> State = new();

    public static void OnFixedUpdate()
    {
        FixedTick++;
    }

    public static bool ShouldRunUpdate(object identifier = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        long now = Utils.TimeStamp;
        int key = HashCode.Combine(file, line, identifier);

        if (!State.TryGetValue(key, out var entry))
            entry.LastSecond = -1;

        if (entry.LastSecond == now)
            return false;

        int slot = (key & int.MaxValue) % Slots;

        if (slot != CurrentSlot)
            return false;

        entry.LastSecond = now;
        State[key] = entry;

        return true;
    }

    public static void Reset()
    {
        State.Clear();
    }
}