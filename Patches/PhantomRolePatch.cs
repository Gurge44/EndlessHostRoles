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
                FastDestroyableSingleton<HudManager>.Instance.AbilityButton.SetFromSettings(phantom.Data.Role.Ability);
                phantom.Data.Role.SetCooldown();
                return false;
            }

            var sender = CustomRpcSender.Create($"Cancel vanish for {phantom.GetRealName()}");
            sender.StartMessage(phantom.GetClientId());

            sender.StartRpc(phantom.NetId, (byte)RpcCalls.SetRole)
                .Write((ushort)RoleTypes.Phantom)
                .Write(true)
                .EndRpc();

            sender.StartRpc(phantom.NetId, (byte)RpcCalls.ProtectPlayer)
                .WriteNetObject(phantom)
                .Write(0)
                .EndRpc();

            sender.EndMessage();
            sender.SendMessage();

            LateTask.New(() => phantom.SetKillCooldown(Math.Max(Main.KillTimers[phantom.PlayerId], 0.001f)), 0.2f);

            return false;
        }

        var writer = CustomRpcSender.Create("PhantomRolePatch.CheckVanish_Prefix", SendOption.Reliable);
        var hasValue = false;

        foreach (PlayerControl target in Main.AllPlayerControls)
        {
            if (!target.IsAlive() || phantom.PlayerId == target.PlayerId || target.AmOwner || !target.HasDesyncRole()) continue;

            int clientId = target.OwnerId;

            if (AmongUsClient.Instance.ClientId == clientId)
            {
                phantom.SetRole(RoleTypes.Phantom);
                phantom.CheckVanish();
            }
            else
            {
                writer.RpcSetRole(phantom, RoleTypes.Phantom, clientId);

                writer.AutoStartRpc(phantom.NetId, (byte)RpcCalls.CheckVanish, clientId);
                writer.Write(0); // not used, lol
                writer.EndRpc();

                hasValue = true;
            }
        }

        writer.SendMessage(dispose: !hasValue);

        LateTask.New(() =>
        {
            var sender = CustomRpcSender.Create("PhantomRolePatch.CheckVanish_Prefix - LateTask", SendOption.Reliable);
            var hasData = false;

            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                if (GameStates.IsMeeting || phantom == null || target.PlayerId == phantom.PlayerId) return;

                int clientId = target.OwnerId;
                string petId = phantom.Data.DefaultOutfit.PetId;

                if (petId != "")
                {
                    PetsList[phantom.PlayerId] = petId;

                    if (clientId != -1)
                    {
                        if (AmongUsClient.Instance.ClientId == clientId)
                            phantom.SetPet("");
                        else
                        {
                            phantom.Data.DefaultOutfit.PetSequenceId += 10;

                            sender.AutoStartRpc(phantom.NetId, (byte)RpcCalls.SetPetStr, clientId);
                            sender.Write("");
                            sender.Write(phantom.GetNextRpcSequenceId(RpcCalls.SetPetStr));
                            sender.EndRpc();

                            hasData = true;
                        }
                    }
                }

                if (AmongUsClient.Instance.ClientId == clientId)
                    phantom.Exiled();
                else
                {
                    sender.AutoStartRpc(phantom.NetId, (byte)RpcCalls.Exiled, clientId);
                    sender.EndRpc();

                    hasData = true;
                }
            }

            sender.SendMessage(dispose: !hasData);
        }, 1.2f, "Set Phantom Invisible");

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

        var sender = CustomRpcSender.Create("PhantomRolePatch.CheckAppear_Prefix", SendOption.Reliable);
        var hasValue = false;

        if (phantom.inVent)
        {
            int ventId = Main.LastEnteredVent[phantom.PlayerId].Id;
            if (AmongUsClient.Instance.AmClient) phantom.MyPhysics.BootFromVent(ventId);
            sender.AutoStartRpc(phantom.MyPhysics.NetId, 34);
            sender.WritePacked(ventId);
            sender.EndRpc();
            hasValue = true;
        }

        foreach (PlayerControl target in Main.AllPlayerControls)
        {
            if (!target.IsAlive() || phantom.PlayerId == target.PlayerId || target.AmOwner || !target.HasDesyncRole()) continue;
            sender.RpcSetRole(phantom, RoleTypes.Phantom, target.GetClientId());
            hasValue = true;
        }

        sender.SendMessage(dispose: !hasValue);

        LateTask.New(() =>
        {
            sender = CustomRpcSender.Create("PhantomRolePatch.CheckAppear_Prefix - LateTask 1", SendOption.Reliable);
            hasValue = false;

            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                if (target == null || target.PlayerId == phantom.PlayerId) continue;

                int clientId = target.OwnerId;

                if (AmongUsClient.Instance.ClientId == clientId)
                    phantom.CheckAppear(shouldAnimate);
                else
                {
                    sender.AutoStartRpc(phantom.NetId, (byte)RpcCalls.CheckAppear, clientId);
                    sender.Write(shouldAnimate);
                    sender.EndRpc();
                    hasValue = true;
                }
            }

            sender.SendMessage(dispose: !hasValue);
        }, 0.5f, "Check Appear when vanish is over");

        LateTask.New(() =>
        {
            sender = CustomRpcSender.Create("PhantomRolePatch.CheckAppear_Prefix - LateTask 2", SendOption.Reliable);
            hasValue = false;

            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                if (GameStates.IsMeeting || phantom == null || target.PlayerId == phantom.PlayerId) return;

                int clientId = target.OwnerId;

                InvisibilityList.Remove(phantom);
                sender.RpcSetRole(phantom, RoleTypes.Scientist, clientId);
                hasValue = true;

                if (PetsList.TryGetValue(phantom.PlayerId, out string petId))
                {
                    if (clientId != -1)
                    {
                        if (AmongUsClient.Instance.ClientId == clientId)
                            phantom.SetPet(petId);
                        else
                        {
                            phantom.Data.DefaultOutfit.PetSequenceId += 10;

                            sender.AutoStartRpc(phantom.NetId, (byte)RpcCalls.SetPetStr, clientId);
                            sender.Write(petId);
                            sender.Write(phantom.GetNextRpcSequenceId(RpcCalls.SetPetStr));
                            sender.EndRpc();

                            hasValue = true;
                        }
                    }
                }
            }

            sender.SendMessage(dispose: !hasValue);
        }, 1.8f, "Set Scientist when vanish is over");
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

    private static IEnumerator CoRevertInvisible(PlayerControl phantom, PlayerControl seer, bool force)
    {
        // Set Scientist for meeting
        if (!force) yield return new WaitForSeconds(0.0001f);

        if (seer.OwnerId == -1 || phantom == null) yield break;
        phantom.RpcSetRoleDesync(RoleTypes.Scientist, seer.GetClientId());

        // Return Phantom in meeting
        yield return new WaitForSeconds(1f);

        {
            if (seer.OwnerId == -1 || phantom == null) yield break;
            phantom.RpcSetRoleDesync(RoleTypes.Phantom, seer.GetClientId());
        }

        // Revert invis for phantom
        yield return new WaitForSeconds(1f);

        {
            if (seer.OwnerId == -1 || phantom == null) yield break;
            phantom.RpcStartAppearDesync(false, seer);
        }

        // Set Scientist back
        yield return new WaitForSeconds(4f);

        {
            if (seer.OwnerId == -1 || phantom == null) yield break;
            phantom.RpcSetRoleDesync(RoleTypes.Scientist, seer.GetClientId());

            if (PetsList.TryGetValue(phantom.PlayerId, out string petId))
                phantom.RpcSetPetDesync(petId, seer);
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
            bool RoleEffectAnimation(RoleEffectAnimation x) => x.effectType == global::RoleEffectAnimation.EffectType.Vanish_Charge;

            if (!__instance.Player.currentRoleAnimations.Find((Func<RoleEffectAnimation, bool>)RoleEffectAnimation) && !__instance.Player.walkingToVent && !__instance.Player.inMovingPlat)
            {
                if (__instance.isInvisible)
                {
                    __instance.MakePlayerVisible();
                    return false;
                }

                FastDestroyableSingleton<HudManager>.Instance.AbilityButton.SetSecondImage(__instance.Ability);
                FastDestroyableSingleton<HudManager>.Instance.AbilityButton.OverrideText(FastDestroyableSingleton<TranslationController>.Instance.GetString(StringNames.PhantomAbilityUndo, new Il2CppReferenceArray<Il2CppSystem.Object>(0)));
                __instance.Player.CmdCheckVanish(GameManager.Instance.LogicOptions.GetPhantomDuration());
                return false;
            }
        }

        return false;
    }
}