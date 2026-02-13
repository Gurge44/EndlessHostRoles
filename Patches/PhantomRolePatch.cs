using System;
using AmongUs.GameOptions;
using EHR.Roles;
using HarmonyLib;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace EHR.Patches;
// By TommyXL & NikoCat233

[HarmonyPatch(typeof(PlayerControl))]
public static class PhantomRolePatch
{
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

    [HarmonyPatch(nameof(PlayerControl.CheckVanish))] // This doesn't always get called for non-hosts, so we invoke CheckTrigger directly when the CheckVanish RPC is received
    [HarmonyPrefix]
    private static bool CheckVanish_Prefix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        Logger.Info($" {__instance.GetNameWithRole()}", "CheckVanish");
        return __instance.AmOwner && CheckTrigger(__instance); // This is assuming that all non-host vanish requests are for ability triggers and should be cancelled
    }

    public static bool CheckTrigger(PlayerControl phantom)
    {
        if (!HudManager.InstanceExists) return true;
        
        RoleBase roleBase = Main.PlayerStates[phantom.PlayerId].Role;

        if ((phantom.Is(CustomRoles.Trainee) && MeetingStates.FirstMeeting) || !Rhapsode.CheckAbilityUse(phantom) || Stasis.IsTimeFrozen || TimeMaster.Rewinding || IntroCutsceneDestroyPatch.PreventKill || !roleBase.OnVanish(phantom))
        {
            if (phantom.AmOwner)
            {
                try
                {
                    HudManager.Instance.AbilityButton.SetFromSettings(phantom.Data.Role.Ability);
                }
                catch { }
                if (Utils.ShouldNotApplyAbilityCooldown(roleBase)) return false;
                phantom.RpcResetAbilityCooldown();
                return false;
            }

            var sender = CustomRpcSender.Create($"Cancel vanish for {phantom.GetRealName()}", SendOption.Reliable);
            sender.StartMessage(phantom.OwnerId);

            sender.StartRpc(phantom.NetId, RpcCalls.SetRole)
                .Write((ushort)RoleTypes.Phantom)
                .Write(true)
                .EndRpc();

            if (!Utils.ShouldNotApplyAbilityCooldown(roleBase))
            {
                sender.StartRpc(phantom.NetId, RpcCalls.ProtectPlayer)
                    .WriteNetObject(phantom)
                    .Write(0)
                    .EndRpc();
            }

            sender.EndMessage();
            sender.SendMessage();

            LateTask.New(() => phantom.SetKillCooldown(Math.Max(Main.KillTimers[phantom.PlayerId], 0.001f)), 0.2f);

            return false;
        }

        return true;
    }
}

// Fixed vanilla bug for host (from TOH-Y)
[HarmonyPatch(typeof(PhantomRole), nameof(PhantomRole.UseAbility))]
public static class PhantomRoleUseAbilityPatch
{
    public static bool Prefix(PhantomRole __instance)
    {
        if (!AmongUsClient.Instance.AmHost || !HudManager.InstanceExists) return true;

        if (__instance.Player.AmOwner && !__instance.Player.Data.IsDead && __instance.Player.IsAlive() && __instance.Player.moveable && !Minigame.Instance && !__instance.IsCoolingDown && !__instance.fading)
        {
            bool RoleEffectAnimation(RoleEffectAnimation x) => x.effectType == global::RoleEffectAnimation.EffectType.Vanish_Charge;

            if (!__instance.Player.currentRoleAnimations.Find((Func<RoleEffectAnimation, bool>)RoleEffectAnimation) && !__instance.Player.walkingToVent && !__instance.Player.inMovingPlat)
            {
                if (__instance.isInvisible)
                {
                    __instance.MakePlayerVisible();
                    return false;
                }

                HudManager.Instance.AbilityButton.SetSecondImage(__instance.Ability);
                HudManager.Instance.AbilityButton.OverrideText(TranslationController.Instance.GetString(StringNames.PhantomAbilityUndo, new Il2CppReferenceArray<Il2CppSystem.Object>(0)));
                __instance.Player.CmdCheckVanish(GameManager.Instance.LogicOptions.GetRoleFloat(FloatOptionNames.PhantomDuration));
                return false;
            }
        }

        return false;
    }
}
