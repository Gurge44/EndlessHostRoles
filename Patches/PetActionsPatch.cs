using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace EHR.Patches;
/*
 * HUGE THANKS TO
 * ImaMapleTree / 단풍잎 / Tealeaf
 * FOR THE CODE
 */

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TryPet))]
internal static class LocalPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = [];

    public static bool Prefix(PlayerControl __instance)
    {
        if (!Options.UsePets.GetBool()) return true;
        if (!(AmongUsClient.Instance.AmHost && AmongUsClient.Instance.AmClient)) return true;
        if (GameStates.IsLobby || !__instance.IsAlive()) return true;
        
        if (__instance.petting) return true;
        __instance.petting = true;

        AFKDetector.SetNotAFK(__instance.PlayerId);

        if (!LastProcess.ContainsKey(__instance.PlayerId)) LastProcess.TryAdd(__instance.PlayerId, Utils.TimeStamp - 2);
        if (LastProcess[__instance.PlayerId] + 1 >= Utils.TimeStamp) return true;

        ExternalRpcPetPatch.Prefix(__instance.MyPhysics, (byte)RpcCalls.Pet);

        LastProcess[__instance.PlayerId] = Utils.TimeStamp;
        return !Main.CancelPetAnimation.Value || !__instance.GetCustomRole().PetActivatedAbility();
    }

    public static void Postfix(PlayerControl __instance)
    {
        if (!Options.UsePets.GetBool()) return;
        if (!(AmongUsClient.Instance.AmHost && AmongUsClient.Instance.AmClient)) return;

        __instance.petting = false;
        
        if (!Main.CancelPetAnimation.Value) LateTask.New(() => __instance.MyPhysics?.CancelPet(), 0.4f, log: false);
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
internal static class ExternalRpcPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = [];

    public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callID)
    {
        if (GameStates.IsLobby || !Options.UsePets.GetBool() || !AmongUsClient.Instance.AmHost || (RpcCalls)callID != RpcCalls.Pet) return;

        PlayerControl pc = __instance.myPlayer;
        PlayerPhysics physics = __instance;

        if (pc == null || !pc.IsAlive()) return;

        AFKDetector.SetNotAFK(pc.PlayerId);

        if (!pc.inVent
            && !pc.inMovingPlat
            && !pc.walkingToVent
            && !pc.onLadder
            && !physics.Animations.IsPlayingEnterVentAnimation()
            && !physics.Animations.IsPlayingClimbAnimation()
            && !physics.Animations.IsPlayingAnyLadderAnimation()
            && !Pelican.IsEaten(pc.PlayerId)
            && GameStates.IsInTask)
        {
            CancelPet();
            LateTask.New(CancelPet, 0.4f, log: false);

            void CancelPet()
            {
                physics.CancelPet();
                MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(physics.NetId, (byte)RpcCalls.CancelPet, SendOption.None);
                AmongUsClient.Instance.FinishRpcImmediately(w);
            }
        }

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
            GameStates.IsLobby ||
            AntiBlackout.SkipTasks ||
            IntroCutsceneDestroyPatch.PreventKill
            )
            return;

        if (Options.CurrentGameMode == CustomGameMode.CaptureTheFlag)
        {
            CaptureTheFlag.TryPickUpFlag(pc);
            return;
        }

        if (Mastermind.ManipulatedPlayers.ContainsKey(pc.PlayerId))
        {
            PlayerControl killTarget = SelectKillButtonTarget(pc);
            if (killTarget != null) Mastermind.ForceKillForManipulatedPlayer(pc, killTarget);

            return;
        }

        if (pc.Is(CustomRoles.Trainee) && MeetingStates.FirstMeeting)
        {
            pc.Notify(Translator.GetString("TraineeNotify"));
            return;
        }

        if (pc.HasAbilityCD())
        {
            if (!pc.IsHost()) pc.Notify(Translator.GetString("AbilityOnCooldown"));
            else Main.Instance.StartCoroutine(FlashCooldownTimer());

            return;
        }

        var hasKillTarget = false;
        PlayerControl target = SelectKillButtonTarget(pc);
        if (target != null) hasKillTarget = true;

        CustomRoles role = pc.GetCustomRole();
        
        if (Options.CurrentGameMode == CustomGameMode.Standard && Options.UsePhantomBasis.GetBool() && (!role.IsNK() || Options.UsePhantomBasisForNKs.GetBool()) && role.SimpleAbilityTrigger()) return;
        
        bool alwaysPetRole = role is CustomRoles.Necromancer or CustomRoles.Deathknight or CustomRoles.Renegade or CustomRoles.Sidekick;

        if (!pc.CanUseKillButton() && !alwaysPetRole)
            hasKillTarget = false;

        RoleBase roleBase = Main.PlayerStates[pc.PlayerId].Role;

        if (role.UsesPetInsteadOfKill() && hasKillTarget && (pc.Data.RoleType != RoleTypes.Impostor || alwaysPetRole))
        {
            if (Options.CurrentGameMode != CustomGameMode.Speedrun)
                pc.AddKCDAsAbilityCD();

            if (target.Is(CustomRoles.Spy) && !Spy.OnKillAttempt(pc, target)) goto Skip;
            if (!Starspawn.CheckInteraction(pc, target)) goto Skip;

            Seamstress.OnAnyoneCheckMurder(pc, target);
            
            PlagueBearer.CheckAndSpreadInfection(pc, target);
            PlagueBearer.CheckAndSpreadInfection(target, pc);

            if (roleBase.OnCheckMurder(pc, target))
                pc.RpcCheckAndMurder(target);

            if (alwaysPetRole) pc.SetKillCooldown();
        }
        else roleBase.OnPet(pc);

        Skip:

        if (pc.HasAbilityCD() || Utils.ShouldNotApplyAbilityCooldown(roleBase)) return;

        pc.AddAbilityCD();
    }

    public static PlayerControl SelectKillButtonTarget(PlayerControl pc)
    {
        Vector2 pos = pc.Pos();
        List<(PlayerControl pc, float distance)> players = Main.AllAlivePlayerControls.Without(pc).Select(x => (pc: x, distance: Vector2.Distance(pos, x.Pos()))).Where(x => x.distance < 2.5f).OrderBy(x => x.distance).ToList();
        PlayerControl target = players.Count > 0 ? players[0].pc : null;

        if (target != null && target.Is(CustomRoles.Detour))
        {
            PlayerControl tempTarget = target;
            target = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId && x.PlayerId != pc.PlayerId).MinBy(x => Vector2.Distance(x.Pos(), target.Pos()));
            Logger.Info($"Target was {tempTarget.GetNameWithRole()}, new target is {target.GetNameWithRole()}", "Detour");

            if (tempTarget.AmOwner)
            {
                Detour.TotalRedirections++;
                if (Detour.TotalRedirections >= 3) Achievements.Type.CantTouchThis.CompleteAfterGameEnd();
            }
        }

        if (target != null && Spirit.TryGetSwapTarget(target, out PlayerControl newTarget))
        {
            Logger.Info($"Target was {target.GetNameWithRole()}, new target is {newTarget.GetNameWithRole()}", "Spirit");
            target = newTarget;
        }

        return target;
    }

    private static IEnumerator FlashCooldownTimer()
    {
        var yellow = false;

        for (var i = 0; i < 8; i++)
        {
            HudManagerPatch.CooldownTimerFlashColor = yellow ? Color.red : Color.yellow;
            yellow = !yellow;
            yield return new WaitForSeconds(0.2f);
        }

        HudManagerPatch.CooldownTimerFlashColor = null;
    }
}