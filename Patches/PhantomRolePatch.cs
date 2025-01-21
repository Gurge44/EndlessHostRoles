using System;
using System.Collections;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Impostor;
using HarmonyLib;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace EHR.Patches;
// By TommyXL & NikoCat233

[HarmonyPatch(typeof(PlayerControl))]
public static class PhantomRolePatch
{
    private static readonly List<PlayerControl> InvisibilityList = new();
    private static readonly System.Collections.Generic.Dictionary<byte, string> PetsList = [];

    [HarmonyPatch(nameof(PlayerControl.CmdCheckVanish))]
    [HarmonyPrefix]
    private static bool CmdCheckVanish_Prefix(PlayerControl __instance, float maxDuration)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            __instance.CheckVanish();
            return false;
        }

        __instance.SetRoleInvisibility(true);
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.CheckVanish, SendOption.Reliable, AmongUsClient.Instance.HostId);
        messageWriter.Write(maxDuration);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        return false;
    }

    [HarmonyPatch(nameof(PlayerControl.CmdCheckAppear))]
    [HarmonyPrefix]
    private static bool CmdCheckAppear_Prefix(PlayerControl __instance, bool shouldAnimate)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            __instance.CheckAppear(shouldAnimate);
            return false;
        }

        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.CheckAppear, SendOption.Reliable, AmongUsClient.Instance.HostId);
        messageWriter.Write(shouldAnimate);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        return false;
    }

    // Called when Phantom press vanish button when visible
    [HarmonyPatch(nameof(PlayerControl.CheckVanish))]
    [HarmonyPrefix]
    private static bool CheckVanish_Prefix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        PlayerControl phantom = __instance;
        Logger.Info($"Player: {phantom.GetNameWithRole()}", "CheckVanish");

        if (!Rhapsode.CheckAbilityUse(phantom) || Stasis.IsTimeFrozen || TimeMaster.Rewinding || !Main.PlayerStates[__instance.PlayerId].Role.OnVanish(__instance))
        {
            if (phantom.AmOwner)
            {
                DestroyableSingleton<HudManager>.Instance.AbilityButton.SetFromSettings(phantom.Data.Role.Ability);
                phantom.Data.Role.SetCooldown();
                return false;
            }

            CustomRpcSender sender = CustomRpcSender.Create($"Cancel vanish for {phantom.GetRealName()}");
            sender.StartMessage(phantom.GetClientId());

            sender.StartRpc(phantom.NetId, (byte)RpcCalls.SetRole)
                .Write((ushort)RoleTypes.Phantom)
                .Write(true)
                .EndRpc();

            sender.EndMessage();
            sender.SendMessage();

            phantom.RpcResetAbilityCooldown();

            LateTask.New(() => phantom.SetKillCooldown(Math.Max(Main.KillTimers[phantom.PlayerId], 0.001f)), 0.2f);

            return false;
        }

        foreach (PlayerControl target in Main.AllPlayerControls)
        {
            if (!target.IsAlive() || phantom == target || target.AmOwner || !target.HasDesyncRole()) continue;

            phantom.RpcSetRoleDesync(RoleTypes.Phantom, target.GetClientId());
            phantom.RpcCheckVanishDesync(target);

            LateTask.New(() =>
                {
                    if (GameStates.IsMeeting || phantom == null) return;

                    string petId = phantom.Data.DefaultOutfit.PetId;

                    if (petId != "")
                    {
                        PetsList[phantom.PlayerId] = petId;
                        phantom.RpcSetPetDesync("", target);
                    }

                    phantom.RpcExileDesync(target);
                }, 1.2f, $"Set Phantom invisible {target.PlayerId}");
        }

        InvisibilityList.Add(phantom);

        return true;
    }

    [HarmonyPatch(nameof(PlayerControl.CheckAppear))]
    [HarmonyPrefix]
    private static void CheckAppear_Prefix(PlayerControl __instance, bool shouldAnimate)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        PlayerControl phantom = __instance;
        Logger.Info($"Player: {phantom.GetRealName()} => shouldAnimate {shouldAnimate}", "CheckAppear");

        if (phantom.inVent) phantom.MyPhysics.RpcBootFromVent(Main.LastEnteredVent[phantom.PlayerId].Id);

        foreach (PlayerControl target in Main.AllPlayerControls)
        {
            if (!target.IsAlive() || phantom == target || target.AmOwner || !target.HasDesyncRole()) continue;

            int clientId = target.GetClientId();

            phantom.RpcSetRoleDesync(RoleTypes.Phantom, clientId);

            LateTask.New(() =>
                {
                    if (target != null) phantom.RpcCheckAppearDesync(shouldAnimate, target);
                }, 0.5f, $"Check Appear when vanish is over {target.PlayerId}");

            LateTask.New(() =>
                {
                    if (GameStates.IsMeeting || phantom == null) return;

                    InvisibilityList.Remove(phantom);
                    phantom.RpcSetRoleDesync(RoleTypes.Scientist, clientId);

                    if (PetsList.TryGetValue(phantom.PlayerId, out string petId)) phantom.RpcSetPetDesync(petId, target);
                }, 1.8f, $"Set Scientist when vanish is over {target.PlayerId}");
        }
    }

    [HarmonyPatch(nameof(PlayerControl.SetRoleInvisibility))]
    [HarmonyPrefix]
    private static void SetRoleInvisibility_Prefix(PlayerControl __instance, bool isActive, bool shouldAnimate, bool playFullAnimation)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        Logger.Info($"Player: {__instance.GetRealName()} => Is Active {isActive}, Animate:{shouldAnimate}, Full Animation:{playFullAnimation}", "SetRoleInvisibility");
    }

    public static void OnReportDeadBody(PlayerControl seer, bool force)
    {
        try
        {
            if (InvisibilityList.Count == 0 || !seer.IsAlive() || seer.Data.Role.Role is RoleTypes.Phantom || seer.AmOwner || !seer.HasDesyncRole()) return;

            foreach (PlayerControl phantom in InvisibilityList)
            {
                if (!phantom.IsAlive())
                {
                    InvisibilityList.Remove(phantom);
                    continue;
                }

                Main.Instance.StartCoroutine(CoRevertInvisible(phantom, seer, force));
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    private static bool InValid(PlayerControl phantom, PlayerControl seer)
    {
        return seer.GetClientId() == -1 || phantom == null;
    }

    private static IEnumerator CoRevertInvisible(PlayerControl phantom, PlayerControl seer, bool force)
    {
        // Set Scientist for meeting
        if (!force) yield return new WaitForSeconds(0.0001f);

        if (InValid(phantom, seer)) yield break;

        phantom?.RpcSetRoleDesync(RoleTypes.Scientist, seer.GetClientId());

        // Return Phantom in meeting
        yield return new WaitForSeconds(1f);

        {
            if (InValid(phantom, seer)) yield break;

            phantom?.RpcSetRoleDesync(RoleTypes.Phantom, seer.GetClientId());
        }

        // Revert invis for phantom
        yield return new WaitForSeconds(1f);

        {
            if (InValid(phantom, seer)) yield break;

            phantom?.RpcStartAppearDesync(false, seer);
        }

        // Set Scientist back
        yield return new WaitForSeconds(4f);

        {
            if (InValid(phantom, seer)) yield break;

            phantom?.RpcSetRoleDesync(RoleTypes.Scientist, seer.GetClientId());

            if (phantom != null && PetsList.TryGetValue(phantom.PlayerId, out string petId)) phantom.RpcSetPetDesync(petId, seer);
        }
    }

    public static void AfterMeeting()
    {
        InvisibilityList.Clear();
        PetsList.Clear();
    }
}

// Fixed vanilla bug for host (from TOH-Y)
[HarmonyPatch(typeof(PhantomRole), nameof(PhantomRole.UseAbility))]
public static class PhantomRoleUseAbilityPatch
{
    public static bool Prefix(PhantomRole __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (__instance.Player.AmOwner && !__instance.Player.Data.IsDead && __instance.Player.moveable && !Minigame.Instance && !__instance.IsCoolingDown && !__instance.fading)
        {
            bool RoleEffectAnimation(RoleEffectAnimation x)
            {
                return x.effectType == global::RoleEffectAnimation.EffectType.Vanish_Charge;
            }

            if (!__instance.Player.currentRoleAnimations.Find((Func<RoleEffectAnimation, bool>)RoleEffectAnimation) && !__instance.Player.walkingToVent && !__instance.Player.inMovingPlat)
            {
                if (__instance.isInvisible)
                {
                    __instance.MakePlayerVisible();
                    return false;
                }

                DestroyableSingleton<HudManager>.Instance.AbilityButton.SetSecondImage(__instance.Ability);
                DestroyableSingleton<HudManager>.Instance.AbilityButton.OverrideText(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.PhantomAbilityUndo, new Il2CppReferenceArray<Il2CppSystem.Object>(0)));
                __instance.Player.CmdCheckVanish(GameManager.Instance.LogicOptions.GetPhantomDuration());
                return false;
            }
        }

        return false;
    }
}