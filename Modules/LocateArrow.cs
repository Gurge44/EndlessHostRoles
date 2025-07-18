using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR;

internal static class LocateArrow
{
    private static readonly Dictionary<ArrowInfo, string> LocateArrows = [];

    private static readonly string[] Arrows =
    [
        "↑",
        "↗",
        "→",
        "↘",
        "↓",
        "↙",
        "←",
        "↖",
        "・"
    ];

    public static void Init()
    {
        LocateArrows.Clear();
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                Add(reader.ReadByte(), reader.ReadVector3());
                break;
            case 2:
                Remove(reader.ReadByte(), reader.ReadVector3());
                break;
            case 3:
                RemoveAllTarget(reader.ReadByte());
                break;
        }
    }

    /// <summary>
    ///     Register a new target arrow object
    /// </summary>
    /// <param name="seer"></param>
    /// <param name="locate"></param>
    public static void Add(byte seer, Vector3 locate)
    {
        if (Main.PlayerStates.TryGetValue(seer, out var state) && state.SubRoles.Contains(CustomRoles.Blind)) return;

        ArrowInfo arrowInfo = new(seer, locate);

        if (!LocateArrows.Any(a => a.Key.Equals(arrowInfo)))
        {
            LocateArrows[arrowInfo] = "・";
            Utils.SendRPC(CustomRPC.Arrow, false, 1, seer, locate);
            Logger.Info($"New locate arrow: {seer} ({seer.GetPlayer()?.GetRealName()}) => {locate}", "LocateArrow");
        }
    }

    /// <summary>
    ///     Delete target
    /// </summary>
    /// <param name="seer"></param>
    /// <param name="locate"></param>
    public static void Remove(byte seer, Vector3 locate)
    {
        ArrowInfo arrowInfo = new(seer, locate);
        List<ArrowInfo> removeList = new(LocateArrows.Keys.Where(k => k.Equals(arrowInfo)));
        removeList.ForEach(a => LocateArrows.Remove(a));

        Utils.SendRPC(CustomRPC.Arrow, false, 2, seer, locate);
        Logger.Info($"Removed locate arrow: {seer} ({seer.GetPlayer()?.GetRealName()}) => {locate}", "LocateArrow");
    }

    /// <summary>
    ///     Delete all targets for the specified seer
    /// </summary>
    /// <param name="seer"></param>
    public static void RemoveAllTarget(byte seer)
    {
        List<ArrowInfo> removeList = new(LocateArrows.Keys.Where(k => k.From == seer));
        removeList.ForEach(a => LocateArrows.Remove(a));

        Utils.SendRPC(CustomRPC.Arrow, false, 3, seer);
        Logger.Info($"Removed all locate arrows for: {seer} ({seer.GetPlayer()?.GetRealName()})", "LocateArrow");
    }

    /// <summary>
    ///     Get all visible target arrows
    /// </summary>
    /// <param name="seer"></param>
    /// <returns></returns>
    public static string GetArrows(PlayerControl seer)
    {
        return LocateArrows.Keys.Where(ai => ai.From == seer.PlayerId).Aggregate(string.Empty, (current, arrowInfo) => current + LocateArrows[arrowInfo]);
    }

    /// <summary>
    ///     Get a specific visible target arrow
    /// </summary>
    /// <param name="seer"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    public static string GetArrow(PlayerControl seer, Vector3 position)
    {
        ArrowInfo arrowInfo = new(seer.PlayerId, position);
        return LocateArrows.FirstOrDefault(a => a.Key.Equals(arrowInfo)).Value ?? string.Empty;
    }

    /// <summary>
    ///     Check target arrow every FixedUpdate
    ///     Issue NotifyRoles when there are updates
    /// </summary>
    /// <param name="seer"></param>
    public static void OnFixedUpdate(PlayerControl seer)
    {
        if (!GameStates.IsInTask) return;

        bool seerIsDead = !seer.IsAlive();

        List<ArrowInfo> arrowList = new(LocateArrows.Keys.Where(a => a.From == seer.PlayerId));
        if (arrowList.Count == 0) return;

        var update = false;

        foreach (ArrowInfo arrowInfo in arrowList.ToArray())
        {
            Vector3 loc = arrowInfo.To;

            if (seerIsDead)
            {
                LocateArrows.Remove(arrowInfo);
                update = true;
                continue;
            }

            // Take the direction vector of the target
            Vector3 dir = loc - seer.transform.position;
            int index;

            if (dir.magnitude < 2)
            {
                // Display a dot when close
                index = 8;
            }
            else
            {
                // Convert to index with -22.5 to 22.5 degrees as 0
                // Bottom is 0 degrees, left side is +180, right side is -180
                // Adding 180 degrees clockwise with top being 0 degrees
                // Add 45/2 to make index in 45 degree units
                double angle = Vector3.SignedAngle(Vector3.down, dir, Vector3.back) + 180 + 22.5;
                index = (int)(angle / 45) % 8;
            }

            string arrow = Arrows[index];

            if (LocateArrows[arrowInfo] != arrow)
            {
                LocateArrows[arrowInfo] = arrow;
                update = true;
            }
        }

        if (update) Utils.NotifyRoles(SpecifySeer: seer, ForceLoop: false, SpecifyTarget: seer);
    }

    private class ArrowInfo(byte from, Vector3 to)
    {
        public readonly byte From = from;
        public readonly Vector3 To = to;

        public bool Equals(ArrowInfo obj)
        {
            return From == obj.From && To == obj.To;
        }

        public override string ToString()
        {
            return $"(From:{From} To:{To})";
        }
    }
}