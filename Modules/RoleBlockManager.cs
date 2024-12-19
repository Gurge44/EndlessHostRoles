using System.Collections.Generic;
using UnityEngine;

namespace EHR.Modules;

public static class RoleBlockManager
{
    public static Dictionary<byte, (long StartTimeStamp, float Duration)> RoleBlockedPlayers = [];

    public static void Reset()
    {
        RoleBlockedPlayers = [];
    }

    public static void AddRoleBlock(PlayerControl pc, float duration)
    {
        long now = Utils.TimeStamp;

        if (RoleBlockedPlayers.TryGetValue(pc.PlayerId, out (long StartTimeStamp, float Duration) data) && data.Duration - (now - data.StartTimeStamp) + 1 > duration)
        {
            Logger.Info($"{pc.GetNameWithRole()} got role blocked, but the duration is less than the previous one", "RoleBlockManager");
            return;
        }

        RoleBlockedPlayers[pc.PlayerId] = (now, duration);
        Logger.Info($"{pc.GetNameWithRole()} is now role blocked for {duration} seconds", "RoleBlockManager");
    }

    public static void OnFixedUpdate(PlayerControl pc)
    {
        if (RoleBlockedPlayers.TryGetValue(pc.PlayerId, out (long StartTimeStamp, float Duration) data))
        {
            long now = Utils.TimeStamp;

            if (now - data.StartTimeStamp >= data.Duration)
            {
                RoleBlockedPlayers.Remove(pc.PlayerId);
                Logger.Info($"{pc.GetNameWithRole()} is no longer role blocked", "RoleBlockManager");
            }
        }
    }

    public static string GetBlockNotify(this BlockedAction blockedAction)
    {
        string blockedActionString = Translator.GetString($"{blockedAction}");
        string notify = string.Format(Translator.GetString("RoleBlockNotify"), blockedActionString);
        return Utils.ColorString(Color.yellow, notify);
    }
}

public enum BlockedAction
{
    Kill,
    Report,
    Sabotage,
    Vent
}