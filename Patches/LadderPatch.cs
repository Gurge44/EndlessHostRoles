﻿using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace EHR;

public class FallFromLadder
{
    public static Dictionary<byte, Vector3> TargetLadderData;
    private static int Chance => (Options.LadderDeathChance as StringOptionItem).GetChance();

    public static void Reset()
    {
        TargetLadderData = [];
    }

    public static void OnClimbLadder(PlayerPhysics player, Ladder source)
    {
        if (!Options.LadderDeath.GetBool()) return;
        var sourcePos = source.transform.position;
        var targetPos = source.Destination.transform.position;

        if (sourcePos.y > targetPos.y)
        {
            int chance = IRandom.Instance.Next(1, 101);
            if (chance <= Chance)
            {
                TargetLadderData[player.myPlayer.PlayerId] = targetPos;
            }
        }
    }

    public static void FixedUpdate(PlayerControl player)
    {
        if (player.Data.Disconnected) return;
        if (TargetLadderData.ContainsKey(player.PlayerId) && Vector2.Distance(TargetLadderData[player.PlayerId], player.transform.position) < 0.5f)
        {
            if (player.Data.IsDead) return;
            // In order to insert LateTask, first enter the death judgment.
            player.Data.IsDead = true;
            LateTask.New(() =>
            {
                Vector2 targetPos = (Vector2)TargetLadderData[player.PlayerId] + new Vector2(0.1f, 0f);
                ushort num = (ushort)(NetHelpers.XRange.ReverseLerp(targetPos.x) * 65535f);
                ushort num2 = (ushort)(NetHelpers.YRange.ReverseLerp(targetPos.y) * 65535f);
                CustomRpcSender sender = CustomRpcSender.Create("LadderFallRpc", sendOption: SendOption.None);
                sender.AutoStartRpc(player.NetTransform.NetId, (byte)RpcCalls.SnapTo)
                    .Write(num)
                    .Write(num2)
                    .EndRpc();
                sender.AutoStartRpc(player.NetId, (byte)RpcCalls.MurderPlayer)
                    .WriteNetObject(player)
                    .Write((byte)ExtendedPlayerControl.ResultFlags)
                    .EndRpc();
                sender.SendMessage();
                player.NetTransform.SnapTo(targetPos);
                player.MurderPlayer(player, ExtendedPlayerControl.ResultFlags);
                Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Fall;
                Main.PlayerStates[player.PlayerId].SetDead();
            }, 0.05f, "LadderFallTask");
        }
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.ClimbLadder))]
class LadderPatch
{
    public static void Postfix(PlayerPhysics __instance, Ladder source /*, byte climbLadderSid*/)
    {
        FallFromLadder.OnClimbLadder(__instance, source);
    }
}