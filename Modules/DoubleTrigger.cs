using System;
using System.Collections.Generic;
using UnityEngine;

namespace TOHE;

static class DoubleTrigger
{
    public static List<byte> PlayerIdList = new();

    public static Dictionary<byte, float> FirstTriggerTimer = new();
    public static Dictionary<byte, byte> FirstTriggerTarget = new();
    public static Dictionary<byte, Action> FirstTriggerAction = new();

    public static void Init()
    {
        PlayerIdList = new();
        FirstTriggerTimer = new();
        FirstTriggerAction = new();
    }
    public static void AddDoubleTrigger(this PlayerControl killer)
    {
        PlayerIdList.Add(killer.PlayerId);
    }
    public static bool CanDoubleTrigger(this PlayerControl killer)
    {
        return PlayerIdList.Contains(killer.PlayerId);
    }

    ///     一回目アクション時 false、2回目アクション時true
    public static bool CheckDoubleTrigger(this PlayerControl killer, PlayerControl target, Action firstAction, bool doAction = true)
    {
        if (FirstTriggerTimer.ContainsKey(killer.PlayerId))
        {
            if (FirstTriggerTarget[killer.PlayerId] != target.PlayerId)
            {
                //2回目がターゲットずれてたら最初の相手にシングルアクション
                return false;
            }
            Logger.Info($"{killer.name} DoDoubleAction", "DoubleTrigger");
            _ = FirstTriggerTimer.Remove(killer.PlayerId);
            _ = FirstTriggerTarget.Remove(killer.PlayerId);
            if (doAction) _ = FirstTriggerAction.Remove(killer.PlayerId);
            return true;
        }
        //シングルアクション時はキル間隔を無視
        _ = CheckMurderPatch.TimeSinceLastKill.Remove(killer.PlayerId);
        FirstTriggerTimer.Add(killer.PlayerId, 1f);
        FirstTriggerTarget.Add(killer.PlayerId, target.PlayerId);
        if (doAction) FirstTriggerAction.Add(killer.PlayerId, firstAction);
        return false;
    }
    public static void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask)
        {
            FirstTriggerTimer.Clear();
            FirstTriggerTarget.Clear();
            FirstTriggerAction.Clear();
            return;
        }

        var playerId = player.PlayerId;
        if (!FirstTriggerTimer.ContainsKey(playerId)) return;

        FirstTriggerTimer[playerId] -= Time.fixedDeltaTime;
        if (FirstTriggerTimer[playerId] <= 0)
        {
            Logger.Info($"{player.name} DoSingleAction", "DoubleTrigger");
            FirstTriggerAction[playerId]();

            _ = FirstTriggerTimer.Remove(playerId);
            _ = FirstTriggerTarget.Remove(playerId);
            _ = FirstTriggerAction.Remove(playerId);
        }
    }
}
