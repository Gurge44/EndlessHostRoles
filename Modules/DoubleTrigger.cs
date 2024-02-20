using System;
using System.Collections.Generic;
using UnityEngine;

namespace TOHE;

static class DoubleTrigger
{
    public static List<byte> PlayerIdList = [];

    public static Dictionary<byte, float> FirstTriggerTimer = [];
    public static Dictionary<byte, byte> FirstTriggerTarget = [];
    public static Dictionary<byte, Action> FirstTriggerAction = [];

    public static void Init()
    {
        PlayerIdList = [];
        FirstTriggerTimer = [];
        FirstTriggerAction = [];
    }

    /// <summary>
    /// Checks for whether the killer pressed their kill button twice on the same player
    /// </summary>
    /// <param name="killer">Who the killer is</param>
    /// <param name="target">Who the killer is targeting with the kill button</param>
    /// <param name="firstAction">The action that should be done if the killer only presses their kill button once</param>
    /// <param name="doAction">Whether the Action should be done or not</param>
    /// <returns>Returns true if the kill button is clicked twice within 1 second. Otherwise, does the Action specified in the parameter firstAction and returns false</returns>
    public static bool CheckDoubleTrigger(this PlayerControl killer, PlayerControl target, Action firstAction, bool doAction = true)
    {
        if (FirstTriggerTimer.ContainsKey(killer.PlayerId))
        {
            if (FirstTriggerTarget[killer.PlayerId] != target.PlayerId)
            {
                // If the second target is off target, single action on the first opponent.
                return false;
            }
            Logger.Info($"{killer.name} - Do Double Action", "DoubleTrigger");
            FirstTriggerTimer.Remove(killer.PlayerId);
            FirstTriggerTarget.Remove(killer.PlayerId);
            if (doAction) FirstTriggerAction.Remove(killer.PlayerId);
            return true;
        }
        // Ignore kill interval during single action
        CheckMurderPatch.TimeSinceLastKill.Remove(killer.PlayerId);
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
            Logger.Info($"{player.name} - Do Single Action", "DoubleTrigger");
            if (FirstTriggerAction.ContainsKey(playerId)) FirstTriggerAction[playerId]();

            FirstTriggerTimer.Remove(playerId);
            FirstTriggerTarget.Remove(playerId);
            FirstTriggerAction.Remove(playerId);
        }
    }
}
