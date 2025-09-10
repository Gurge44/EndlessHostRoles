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

        if ((phantom.Is(CustomRoles.Trainee) && MeetingStates.FirstMeeting) || !Rhapsode.CheckAbilityUse(phantom) || Stasis.IsTimeFrozen || TimeMaster.Rewinding || !Main.PlayerStates[__instance.PlayerId].Role.OnVanish(__instance))
        {
            if (phantom.AmOwner)
            {
                FastDestroyableSingleton<HudManager>.Instance.AbilityButton.SetFromSettings(phantom.Data.Role.Ability);
                phantom.Data.Role.SetCooldown();
                return false;
            }

            var sender = CustomRpcSender.Create($"Cancel vanish for {phantom.GetRealName()}", SendOption.Reliable);
            sender.StartMessage(phantom.OwnerId);

            sender.StartRpc(phantom.NetId, RpcCalls.SetRole)
                .Write((ushort)RoleTypes.Phantom)
                .Write(true)
                .EndRpc();

            sender.StartRpc(phantom.NetId, RpcCalls.ProtectPlayer)
                .WriteNetObject(phantom)
                .Write(0)
                .EndRpc();

            sender.EndMessage();
            sender.SendMessage();

            LateTask.New(() => phantom.SetKillCooldown(Math.Max(Main.KillTimers[phantom.PlayerId], 0.001f)), 0.2f);

            return false;
        }

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
                var writer = CustomRpcSender.Create("PhantomRolePatch.CheckVanish_Prefix", SendOption.Reliable);
                
                writer.RpcSetRole(phantom, RoleTypes.Phantom, clientId);

                writer.AutoStartRpc(phantom.NetId, RpcCalls.CheckVanish, clientId);
                writer.Write(0); // not used, lol
                writer.EndRpc();

                writer.SendMessage();
            }
        }

        LateTask.New(() =>
        {
            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                if (GameStates.IsMeeting || phantom == null || target.PlayerId == phantom.PlayerId) return;

                int clientId = target.OwnerId;
                string petId = phantom.Data.DefaultOutfit.PetId;

                var sender = CustomRpcSender.Create("PhantomRolePatch.CheckVanish_Prefix - LateTask", SendOption.Reliable);
                var hasData = false;

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

                            sender.AutoStartRpc(phantom.NetId, RpcCalls.SetPetStr, clientId);
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
                    sender.AutoStartRpc(phantom.NetId, RpcCalls.Exiled, clientId);
                    sender.EndRpc();

                    hasData = true;
                }

                sender.SendMessage(!hasData);
            }
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

        if (phantom.inVent)
        {
            int ventId = Main.LastEnteredVent[phantom.PlayerId].Id;
            phantom.MyPhysics.RpcBootFromVent(ventId);
        }

        foreach (PlayerControl target in Main.AllPlayerControls)
        {
            if (!target.IsAlive() || phantom.PlayerId == target.PlayerId || target.AmOwner || !target.HasDesyncRole()) continue;
            phantom.RpcSetRoleDesync(RoleTypes.Phantom, target.OwnerId);
        }

        LateTask.New(() =>
        {
            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                if (target == null || target.PlayerId == phantom.PlayerId) continue;

                int clientId = target.OwnerId;

                if (AmongUsClient.Instance.ClientId == clientId)
                    phantom.CheckAppear(shouldAnimate);
                else
                {
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(phantom.NetId, 64, SendOption.Reliable, clientId);
                    writer.Write(shouldAnimate);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                }
            }
        }, 0.5f, "Check Appear when vanish is over");

        LateTask.New(() =>
        {
            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                if (GameStates.IsMeeting || phantom == null || target.PlayerId == phantom.PlayerId) return;

                int clientId = target.OwnerId;

                InvisibilityList.Remove(phantom);

                var sender = CustomRpcSender.Create("PhantomRolePatch.CheckAppear_Prefix - LateTask 2", SendOption.Reliable);
                sender.RpcSetRole(phantom, RoleTypes.Scientist, clientId);

                if (PetsList.TryGetValue(phantom.PlayerId, out string petId))
                {
                    if (clientId != -1)
                    {
                        if (AmongUsClient.Instance.ClientId == clientId)
                            phantom.SetPet(petId);
                        else
                        {
                            phantom.Data.DefaultOutfit.PetSequenceId += 10;

                            sender.AutoStartRpc(phantom.NetId, RpcCalls.SetPetStr, clientId);
                            sender.Write(petId);
                            sender.Write(phantom.GetNextRpcSequenceId(RpcCalls.SetPetStr));
                            sender.EndRpc();
                        }
                    }
                }

                sender.SendMessage();
            }
        }, 1.8f, "Set Scientist when vanish is over");
    }

    public static void OnReportDeadBody(PlayerControl seer)
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

                Main.Instance.StartCoroutine(CoRevertInvisible(phantom, seer));
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    private static IEnumerator CoRevertInvisible(PlayerControl phantom, PlayerControl seer)
    {
        if (seer.OwnerId == -1 || phantom == null) yield break;
        phantom.RpcSetRoleDesync(RoleTypes.Scientist, seer.OwnerId);

        // Return Phantom in meeting
        yield return new WaitForSeconds(1f);

        {
            if (seer.OwnerId == -1 || phantom == null) yield break;
            phantom.RpcSetRoleDesync(RoleTypes.Phantom, seer.OwnerId);
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
            phantom.RpcSetRoleDesync(RoleTypes.Scientist, seer.OwnerId);

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
                __instance.Player.CmdCheckVanish(GameManager.Instance.LogicOptions.GetRoleFloat(FloatOptionNames.PhantomDuration));
                return false;
            }
        }

        return false;
    }
}