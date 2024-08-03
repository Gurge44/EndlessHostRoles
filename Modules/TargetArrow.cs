using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR;

static class TargetArrow
{
    static readonly Dictionary<ArrowInfo, string> TargetArrows = [];

    static readonly string[] Arrows =
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
        TargetArrows.Clear();
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                Add(reader.ReadByte(), reader.ReadByte());
                break;
            case 2:
                Remove(reader.ReadByte(), reader.ReadByte());
                break;
            case 3:
                RemoveAllTarget(reader.ReadByte());
                break;
        }
    }

    /// <summary>
    /// Register a new target arrow object
    /// </summary>
    /// <param name="seer"></param>
    /// <param name="target"></param>
    public static void Add(byte seer, byte target)
    {
        var arrowInfo = new ArrowInfo(seer, target);
        if (!TargetArrows.Any(a => a.Key.Equals(arrowInfo)))
        {
            TargetArrows[arrowInfo] = "・";
            Utils.SendRPC(CustomRPC.Arrow, true, 1, seer, target);
            Logger.Info($"New target arrow: {seer} ({seer.GetPlayer()?.GetRealName()}) => {target} ({target.GetPlayer()?.GetRealName()})", "TargetArrow");
        }
    }

    /// <summary>
    /// Delete target
    /// </summary>
    /// <param name="seer"></param>
    /// <param name="target"></param>
    public static void Remove(byte seer, byte target)
    {
        var arrowInfo = new ArrowInfo(seer, target);
        var removeList = new List<ArrowInfo>(TargetArrows.Keys.Where(k => k.Equals(arrowInfo)));
        foreach (ArrowInfo a in removeList.ToArray())
        {
            TargetArrows.Remove(a);
        }

        Utils.SendRPC(CustomRPC.Arrow, true, 2, seer, target);
        Logger.Info($"Removed target arrow: {seer} ({seer.GetPlayer()?.GetRealName()}) => {target} ({target.GetPlayer()?.GetRealName()})", "TargetArrow");
    }

    /// <summary>
    /// Delete all targets for the specified seer
    /// </summary>
    /// <param name="seer"></param>
    public static void RemoveAllTarget(byte seer)
    {
        var removeList = new List<ArrowInfo>(TargetArrows.Keys.Where(k => k.From == seer));
        foreach (ArrowInfo arrowInfo in removeList.ToArray())
        {
            TargetArrows.Remove(arrowInfo);
        }

        Utils.SendRPC(CustomRPC.Arrow, true, 3, seer);
        Logger.Info($"Removed all target arrows for {seer} ({seer.GetPlayer()?.GetRealName()})", "TargetArrow");
    }

    /// <summary>
    /// Get all visible target arrows
    /// </summary>
    /// <param name="seer"></param>
    /// <param name="targets"></param>
    /// <returns></returns>
    public static string GetArrows(PlayerControl seer, params byte[] targets)
    {
        return TargetArrows.Keys.Where(ai => ai.From == seer.PlayerId && targets.Contains(ai.To)).Aggregate(string.Empty, (current, arrowInfo) => current + TargetArrows[arrowInfo]);
    }

    /// <summary>
    /// Check target arrow every FixedUpdate
    /// Issue NotifyRoles when there are updates
    /// </summary>
    /// <param name="seer"></param>
    public static void OnFixedUpdate(PlayerControl seer)
    {
        if (!GameStates.IsInTask) return;

        var seerIsDead = !seer.IsAlive();

        var arrowList = new List<ArrowInfo>(TargetArrows.Keys.Where(a => a.From == seer.PlayerId));
        if (arrowList.Count == 0) return;

        var update = false;
        foreach (ArrowInfo arrowInfo in arrowList.ToArray())
        {
            var targetId = arrowInfo.To;
            var target = Utils.GetPlayerById(targetId);
            if (seerIsDead || (!target.IsAlive() && !seer.Is(CustomRoles.Spiritualist)))
            {
                TargetArrows.Remove(arrowInfo);
                update = true;
                continue;
            }

            // Take the direction vector of the target
            var dir = target.transform.position - seer.transform.position;
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
                var angle = Vector3.SignedAngle(Vector3.down, dir, Vector3.back) + 180 + 22.5;
                index = ((int)(angle / 45)) % 8;
            }

            var arrow = Arrows[index];
            if (TargetArrows[arrowInfo] != arrow)
            {
                TargetArrows[arrowInfo] = arrow;
                update = true;
            }
        }

        if (update)
        {
            Utils.NotifyRoles(SpecifySeer: seer, ForceLoop: false, SpecifyTarget: seer);
        }
    }

    class ArrowInfo(byte from, byte to)
    {
        public readonly byte From = from;
        public readonly byte To = to;

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