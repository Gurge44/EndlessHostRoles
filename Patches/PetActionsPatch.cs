using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Impostor;
using EHR.Neutral;
using HarmonyLib;
using UnityEngine;

namespace EHR.Patches;

/*
 * HUGE THANKS TO
 * ImaMapleTree / 단풍잎 / Tealeaf
 * FOR THE CODE
 */

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TryPet))]
static class LocalPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = [];

    public static bool Prefix(PlayerControl __instance)
    {
        if (!Options.UsePets.GetBool()) return true;
        if (!(AmongUsClient.Instance.AmHost && AmongUsClient.Instance.AmClient)) return true;
        if (GameStates.IsLobby || !__instance.IsAlive()) return true;

        if (__instance.petting) return true;
        __instance.petting = true;

        if (!LastProcess.ContainsKey(__instance.PlayerId)) LastProcess.TryAdd(__instance.PlayerId, Utils.TimeStamp - 2);
        if (LastProcess[__instance.PlayerId] + 1 >= Utils.TimeStamp) return true;

        ExternalRpcPetPatch.Prefix(__instance.MyPhysics, (byte)RpcCalls.Pet);

        LastProcess[__instance.PlayerId] = Utils.TimeStamp;
        return !__instance.GetCustomRole().PetActivatedAbility();
    }

    public static void Postfix(PlayerControl __instance)
    {
        if (!Options.UsePets.GetBool()) return;
        if (!(AmongUsClient.Instance.AmHost && AmongUsClient.Instance.AmClient)) return;
        __instance.petting = false;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
static class ExternalRpcPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = [];

    public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callID)
    {
        if (GameStates.IsLobby || !Options.UsePets.GetBool() || !AmongUsClient.Instance.AmHost || (RpcCalls)callID != RpcCalls.Pet) return;

        var pc = __instance.myPlayer;
        var physics = __instance;

        if (pc == null || !pc.IsAlive()) return;

        if (!pc.inVent
            && !pc.inMovingPlat
            && !pc.walkingToVent
            && !pc.onLadder
            && !physics.Animations.IsPlayingEnterVentAnimation()
            && !physics.Animations.IsPlayingClimbAnimation()
            && !physics.Animations.IsPlayingAnyLadderAnimation()
            && !Pelican.IsEaten(pc.PlayerId)
            && GameStates.IsInTask
            && pc.GetCustomRole().PetActivatedAbility())
            physics.CancelPet();

        if (!LastProcess.ContainsKey(pc.PlayerId)) LastProcess.TryAdd(pc.PlayerId, Utils.TimeStamp - 2);
        if (LastProcess[pc.PlayerId] + 1 >= Utils.TimeStamp) return;
        LastProcess[pc.PlayerId] = Utils.TimeStamp;

        Logger.Info($"Player {pc.GetNameWithRole().RemoveHtmlTags()} petted their pet", "PetActionTrigger");

        LateTask.New(() => OnPetUse(pc), 0.2f, $"OnPetUse: {pc.GetNameWithRole().RemoveHtmlTags()}", false);
    }

    public static void OnPetUse(PlayerControl pc)
    {
        if (pc == null ||
            pc.inVent ||
            pc.inMovingPlat ||
            pc.onLadder ||
            pc.walkingToVent ||
            pc.MyPhysics.Animations.IsPlayingEnterVentAnimation() ||
            pc.MyPhysics.Animations.IsPlayingClimbAnimation() ||
            pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() ||
            Pelican.IsEaten(pc.PlayerId) ||
            Penguin.IsVictim(pc) ||
            !AmongUsClient.Instance.AmHost ||
            GameStates.IsLobby
           )
            return;

        if (Options.CurrentGameMode == CustomGameMode.CaptureTheFlag)
        {
            CTFManager.TryPickUpFlag(pc);
            return;
        }

        if (Mastermind.ManipulatedPlayers.ContainsKey(pc.PlayerId))
        {
            var killTarget = SelectKillButtonTarget(pc);
            if (killTarget != null) Mastermind.ForceKillForManipulatedPlayer(pc, killTarget);
            return;
        }

        if (pc.HasAbilityCD())
        {
            if (!pc.IsHost()) pc.Notify(Translator.GetString("AbilityOnCooldown"));
            else Main.Instance.StartCoroutine(FlashCooldownTimer());
            return;
        }

        bool hasKillTarget = false;
        PlayerControl target = SelectKillButtonTarget(pc);
        if (target != null) hasKillTarget = true;

        var role = pc.GetCustomRole();
        var alwaysPetRole = role is CustomRoles.Necromancer or CustomRoles.Deathknight or CustomRoles.Refugee or CustomRoles.Sidekick;

        if (!pc.CanUseKillButton() && !alwaysPetRole) hasKillTarget = false;

        if (role.UsesPetInsteadOfKill() && hasKillTarget && (pc.Data.RoleType != RoleTypes.Impostor || alwaysPetRole))
        {
            if (Options.CurrentGameMode != CustomGameMode.Speedrun)
                pc.AddKCDAsAbilityCD();

            if (Main.PlayerStates[pc.PlayerId].Role.OnCheckMurder(pc, target))
            {
                pc.RpcCheckAndMurder(target);
            }

            if (alwaysPetRole) pc.SetKillCooldown();
        }
        else
        {
            Main.PlayerStates[pc.PlayerId].Role.OnPet(pc);
        }

        if (pc.HasAbilityCD() || (Main.PlayerStates[pc.PlayerId].Role is Sniper { IsAim: true })) return;

        pc.AddAbilityCD();
    }

    public static PlayerControl SelectKillButtonTarget(PlayerControl pc)
    {
        var pos = pc.Pos();
        var players = Main.AllAlivePlayerControls.Without(pc).Select(x => (pc: x, distance: Vector2.Distance(pos, x.Pos()))).Where(x => x.distance < 2.5f).OrderBy(x => x.distance).ToList();
        var target = players.Count > 0 ? players[0].pc : null;

        if (target != null && target.Is(CustomRoles.Detour))
        {
            var tempTarget = target;
            target = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId && x.PlayerId != pc.PlayerId).MinBy(x => Vector2.Distance(x.Pos(), target.Pos()));
            Logger.Info($"Target was {tempTarget.GetNameWithRole()}, new target is {target.GetNameWithRole()}", "Detour");
        }

        return target;
    }

    private static IEnumerator FlashCooldownTimer()
    {
        var yellow = false;
        for (int i = 0; i < 8; i++)
        {
            HudManagerPatch.CooldownTimerFlashColor = yellow ? Color.red : Color.yellow;
            yellow = !yellow;
            yield return new WaitForSeconds(0.2f);
        }

        HudManagerPatch.CooldownTimerFlashColor = null;
    }
}