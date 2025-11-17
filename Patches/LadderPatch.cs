using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace EHR;

public class FallFromLadder
{
    public static Dictionary<byte, Vector3> TargetLadderData;
    private static int Chance => (Options.LadderDeathChance as StringOptionItem)?.GetChance() ?? 0;

    public static void Reset()
    {
        TargetLadderData = [];
    }

    public static void OnClimbLadder(PlayerPhysics player, Ladder source)
    {
        PlayerControl pc = player.myPlayer;

        if (pc.shouldAppearInvisible || pc.invisibilityAlpha < 1f)
            pc.RpcResetInvisibility();
        
        if (!Options.LadderDeath.GetBool()) return;

        Vector3 sourcePos = source.transform.position;
        Vector3 targetPos = source.Destination.transform.position;

        if (sourcePos.y > targetPos.y)
        {
            int chance = IRandom.Instance.Next(1, 101);
            if (chance <= Chance) TargetLadderData[pc.PlayerId] = targetPos;
        }
    }

    public static void FixedUpdate(PlayerControl player)
    {
        if (player.Data.Disconnected) return;

        if (TargetLadderData.TryGetValue(player.PlayerId, out Vector3 targetLadderData) && Vector2.Distance(targetLadderData, player.Pos()) < 0.5f)
        {
            if (player.Data.IsDead) return;

            // To insert LateTask, first enter the death judgment.
            player.Data.IsDead = true;

            LateTask.New(() =>
            {
                Vector2 targetPos = (Vector2)targetLadderData + new Vector2(0.1f, 0f);
                var num = (ushort)(NetHelpers.XRange.ReverseLerp(targetPos.x) * 65535f);
                var num2 = (ushort)(NetHelpers.YRange.ReverseLerp(targetPos.y) * 65535f);
                var sender = CustomRpcSender.Create("LadderFallRpc", SendOption.Reliable);

                sender.AutoStartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                    .Write(num)
                    .Write(num2)
                    .EndRpc();

                sender.AutoStartRpc(player.NetId, RpcCalls.MurderPlayer)
                    .WriteNetObject(player)
                    .Write((int)MurderResultFlags.Succeeded)
                    .EndRpc();

                sender.SendMessage();
                player.NetTransform.SnapTo(targetPos);
                player.MurderPlayer(player, MurderResultFlags.Succeeded);
                Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Fall;
                Main.PlayerStates[player.PlayerId].SetDead();
            }, 0.05f, "LadderFallTask");
        }
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.ClimbLadder))]
internal static class LadderPatch
{
    public static void Postfix(PlayerPhysics __instance, Ladder source /*, byte climbLadderSid*/)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        FallFromLadder.OnClimbLadder(__instance, source);
    }
}