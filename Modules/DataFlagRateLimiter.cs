using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hazel;

namespace EHR.Modules;

public static class DataFlagRateLimiter
{
    private class QueuedAction
    {
        public Action Action;
        public int Cost;
        public bool IsDone;
    }

    // =========================
    // RELIABLE
    // =========================

    private static readonly Queue<QueuedAction> ReliableQueue = new();
    private static readonly Stopwatch ReliableTimer = Stopwatch.StartNew();
    private static int ReliableSent;

    private const int ReliableRateLimitPerSecond = 23;

    // =========================
    // UNRELIABLE (SendOption.None)
    // =========================

    private static readonly Queue<QueuedAction> UnreliableQueue = new();
    private static readonly Stopwatch UnreliableTimer = Stopwatch.StartNew();
    private static int UnreliableSent;

    private const int UnreliableRateLimitPerSecond = 23;

    // =========================
    // PUBLIC API
    // =========================

    public static void Enqueue(Action action, SendOption channel = SendOption.Reliable, int calls = 1)
    {
        var qa = new QueuedAction
        {
            Action = action,
            Cost = calls,
            IsDone = false
        };

        // Not needed on modded regions
        if (GameStates.CurrentServerType is not (GameStates.ServerType.Local or GameStates.ServerType.Vanilla))
        {
            Execute(qa);
            return;
        }

        switch (channel)
        {
            case SendOption.Reliable:
                EnqueueInternal(ReliableQueue, ref ReliableSent, ReliableTimer, ReliableRateLimitPerSecond, qa);
                break;

            case SendOption.None: // Unreliable
                EnqueueInternal(UnreliableQueue, ref UnreliableSent, UnreliableTimer, UnreliableRateLimitPerSecond, qa);
                break;
        }
    }

    public static System.Collections.IEnumerator EnqueueAndWait(Action action, SendOption channel = SendOption.Reliable, int calls = 1)
    {
        var qa = new QueuedAction
        {
            Action = action,
            Cost = calls,
            IsDone = false
        };

        // Not needed on modded regions
        if (GameStates.CurrentServerType is not (GameStates.ServerType.Local or GameStates.ServerType.Vanilla))
        {
            Execute(qa);
            yield break;
        }

        switch (channel)
        {
            case SendOption.Reliable:
                EnqueueInternal(ReliableQueue, ref ReliableSent, ReliableTimer, ReliableRateLimitPerSecond, qa);
                break;

            case SendOption.None: // Unreliable
                EnqueueInternal(UnreliableQueue, ref UnreliableSent, UnreliableTimer, UnreliableRateLimitPerSecond, qa);
                break;
        }

        // Wait until executed
        while (!qa.IsDone)
            yield return null;
    }

    // Called once per frame
    public static void OnFixedUpdate()
    {
        ProcessQueue(ReliableQueue, ref ReliableSent, ReliableTimer, ReliableRateLimitPerSecond);
        ProcessQueue(UnreliableQueue, ref UnreliableSent, UnreliableTimer, UnreliableRateLimitPerSecond);
    }

    // =========================
    // INTERNAL LOGIC
    // =========================

    private static void EnqueueInternal(
        Queue<QueuedAction> queue,
        ref int sent,
        Stopwatch timer,
        int limit,
        QueuedAction qa)
    {
        // Reset window every second
        if (timer.ElapsedMilliseconds >= 1000)
        {
            timer.Restart();
            sent = 0;
        }

        // Try immediate execution if no backlog
        if (queue.Count == 0 && sent + qa.Cost <= limit)
        {
            Execute(qa);
            sent += qa.Cost;
            return;
        }

        queue.Enqueue(qa);
    }

    private static void ProcessQueue(
        Queue<QueuedAction> queue,
        ref int sent,
        Stopwatch timer,
        int limit)
    {
        // Reset window every second
        if (timer.ElapsedMilliseconds >= 1000)
        {
            timer.Restart();
            sent = 0;
        }

        while (queue.Count > 0)
        {
            var next = queue.Peek();

            if (sent + next.Cost > limit)
                break;

            queue.Dequeue();

            Execute(next);
            sent += next.Cost;
        }
    }

    private static void Execute(QueuedAction qa)
    {
        try
        {
            qa.Action();
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
        }
        finally
        {
            qa.IsDone = true;
        }
    }
}