using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.AddOns.Crewmate;
using EHR.AddOns.GhostRoles;
using EHR.AddOns.Impostor;
using EHR.Coven;
using EHR.Crewmate;
using EHR.GameMode.HideAndSeekRoles;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using EHR.Patches;
using HarmonyLib;
using Hazel;
using InnerNet;
using TMPro;
using UnityEngine;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckProtect))]
internal static class CheckProtectPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost || target.Data.IsDead) return false;

        Logger.Info($"CheckProtect: {__instance.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "CheckProtect");

        if (__instance.Is(CustomRoles.EvilSpirit))
        {
            if (target.Is(CustomRoles.Spiritcaller))
                Spiritcaller.ProtectSpiritcaller();
            else
                Spiritcaller.HauntPlayer(target);

            __instance.RpcResetAbilityCooldown();
            return true;
        }

        if (GhostRolesManager.AssignedGhostRoles.TryGetValue(__instance.PlayerId, out (CustomRoles Role, IGhostRole Instance) ghostRole))
        {
            ghostRole.Instance.OnProtect(__instance, target);
            __instance.RpcResetAbilityCooldown();
            return true;
        }

        if (__instance.Is(CustomRoles.Sheriff))
        {
            if (__instance.Data.IsDead)
            {
                Logger.Info("Blocked", "CheckProtect");
                return false;
            }
        }

        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcMurderPlayer))]
internal static class RpcMurderPlayerPatch
{
    public static bool Prefix(PlayerControl __instance, PlayerControl target, bool didSucceed)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            Logger.Error("Client is calling RpcMurderPlayer, are you hacking?", "RpcMurderPlayerPatch.Prefix");
            return false;
        }

        if (GameStates.IsLobby)
        {
            Logger.Info("Murder triggered in lobby, so murder canceled", "RpcMurderPlayer.Prefix");
            return false;
        }

        MurderResultFlags murderResultFlags = didSucceed ? MurderResultFlags.Succeeded : MurderResultFlags.FailedError | MurderResultFlags.DecisionByHost;

        if (AmongUsClient.Instance.AmClient)
            __instance.MurderPlayer(target, murderResultFlags);

        var sender = CustomRpcSender.Create("RpcMurderPlayer", SendOption.Reliable);
        sender.StartMessage();

        if (Main.Invisible.Contains(target.PlayerId) && murderResultFlags == MurderResultFlags.Succeeded)
        {
            sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(new Vector2(50f, 50f))
                .Write((ushort)(target.NetTransform.lastSequenceId + 16383))
                .EndRpc();
            sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(new Vector2(50f, 50f))
                .Write((ushort)(target.NetTransform.lastSequenceId + 32767))
                .EndRpc();
            sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(new Vector2(50f, 50f))
                .Write((ushort)(target.NetTransform.lastSequenceId + 32767 + 16383))
                .EndRpc();
            sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(target.transform.position)
                .Write(target.NetTransform.lastSequenceId)
                .EndRpc();

            NumSnapToCallsThisRound += 4;
        }

        sender.StartRpc(__instance.NetId, RpcCalls.MurderPlayer)
            .WriteNetObject(target)
            .Write((int)murderResultFlags)
            .EndRpc();

        sender.SendMessage();
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckMurder))] // Modded
internal static class CmdCheckMurderPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (AmongUsClient.Instance.AmHost)
            __instance.CheckMurder(target);
        else if (Options.CurrentGameMode != CustomGameMode.FFA)
        {
            MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.CheckMurder, SendOption.Reliable);
            messageWriter.WriteNetObject(target);
            AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.FFAKill, SendOption.Reliable, AmongUsClient.Instance.HostId);
            writer.WriteNetObject(__instance);
            writer.WriteNetObject(target);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))] // Vanilla
internal static class CheckMurderPatch
{
    public static readonly Dictionary<byte, float> TimeSinceLastKill = [];

    public static void Update(byte id)
    {
        if (TimeSinceLastKill.ContainsKey(id))
        {
            TimeSinceLastKill[id] += Time.deltaTime;
            if (15f < TimeSinceLastKill[id]) TimeSinceLastKill.Remove(id);
        }
    }

    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        try
        {
            if (!AmongUsClient.Instance.AmHost) return false;

            PlayerControl killer = __instance;

            AFKDetector.SetNotAFK(killer.PlayerId);

            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "CheckMurder");

            if (killer.Data.IsDead)
            {
                Logger.Info($"Killer {killer.GetNameWithRole().RemoveHtmlTags()} is dead, kill canceled", "CheckMurder");
                return false;
            }

            if (AntiBlackout.SkipTasks)
            {
                Logger.Info("CheckMurder while AntiBlackOut protection is in progress, kill canceled", "CheckMurder");
                return false;
            }

            if (!Main.IntroDestroyed)
            {
                Logger.Info("CheckMurder while intro is not destroyed, kill canceled", "CheckMurder");
                return false;
            }

            if (target.Is(CustomRoles.Detour))
            {
                PlayerControl tempTarget = target;
                target = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId && x.PlayerId != killer.PlayerId).MinBy(x => Vector2.Distance(x.Pos(), target.Pos()));
                Logger.Info($"Target was {tempTarget.GetNameWithRole()}, new target is {target.GetNameWithRole()}", "Detour");

                if (tempTarget.IsLocalPlayer())
                {
                    Detour.TotalRedirections++;
                    if (Detour.TotalRedirections >= 3) Achievements.Type.CantTouchThis.CompleteAfterGameEnd();
                }
            }

            if (Spirit.TryGetSwapTarget(target, out PlayerControl newTarget))
            {
                Logger.Info($"Target was {target.GetNameWithRole()}, new target is {newTarget.GetNameWithRole()}", "Spirit");
                target = newTarget;
            }

            if (target.Data == null
                || target.inVent
                || target.inMovingPlat
                || target.MyPhysics.Animations.IsPlayingEnterVentAnimation()
                || target.MyPhysics.Animations.IsPlayingAnyLadderAnimation()
                || target.onLadder)
            {
                Logger.Info("The target is in a state where they cannot be killed, kill canceled.", "CheckMurder");
                return false;
            }

            if (target.Data.IsDead)
            {
                Logger.Info("Target is already dead, kill canceled", "CheckMurder");
                return false;
            }

            if (MeetingHud.Instance != null)
            {
                Logger.Info("Kill during meeting, canceled", "CheckMurder");
                return false;
            }

            float minTime = CalculatePingDelay();

            // No value is stored in TimeSinceLastKill || Stored time is greater than or equal to minTime => Allow kill
            // ↓ If not allowed
            if (TimeSinceLastKill.TryGetValue(killer.PlayerId, out float time) && time < minTime)
            {
                Logger.Info($"Last kill was too shortly before, canceled - time: {time}, minTime: {minTime}", "CheckMurder");
                return false;
            }

            TimeSinceLastKill[killer.PlayerId] = 0f;

            if (target.Is(CustomRoles.Diseased))
            {
                if (!Main.KilledDiseased.TryAdd(killer.PlayerId, 1))
                    Main.KilledDiseased[killer.PlayerId] += 1;
            }

            if (target.Is(CustomRoles.Antidote))
            {
                if (!Main.KilledAntidote.TryAdd(killer.PlayerId, 1))
                    Main.KilledAntidote[killer.PlayerId] += 1;
            }

            killer.ResetKillCooldown(false);

            if (killer.PlayerId != target.PlayerId && !killer.CanUseKillButton())
            {
                Logger.Info(killer.GetNameWithRole().RemoveHtmlTags() + " cannot use their kill button, the kill was blocked", "CheckMurder");
                return false;
            }

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloKombat:
                    SoloPVP.OnPlayerAttack(killer, target);
                    return false;
                case CustomGameMode.FFA:
                    FreeForAll.OnPlayerAttack(killer, target);
                    return false;
                case CustomGameMode.HotPotato:
                    (byte holderID, byte lastHolderID) = HotPotato.GetState();
                    if (HotPotato.CanPassViaKillButton && holderID == killer.PlayerId && (lastHolderID != target.PlayerId || Main.AllAlivePlayerControls.Length <= 2))
                        HotPotato.FixedUpdatePatch.PassHotPotato(target, false);
                    return false;
                case CustomGameMode.TheMindGame:
                case CustomGameMode.MoveAndStop:
                case CustomGameMode.RoomRush:
                case CustomGameMode.NaturalDisasters:
                case CustomGameMode.Speedrun when !Speedrun.OnCheckMurder(killer, target):
                    return false;
                case CustomGameMode.Quiz:
                    if (Quiz.AllowKills) killer.Kill(target);
                    return false;
                case CustomGameMode.Speedrun:
                    killer.Kill(target);
                    return false;
                case CustomGameMode.HideAndSeek:
                    CustomHnS.OnCheckMurder(killer, target);
                    return false;
                case CustomGameMode.CaptureTheFlag:
                    CaptureTheFlag.OnCheckMurder(killer, target);
                    return false;
                case CustomGameMode.KingOfTheZones:
                    KingOfTheZones.OnCheckMurder(killer, target);
                    return false;
                case CustomGameMode.BedWars:
                    BedWars.OnCheckMurder(killer, target);
                    return false;
            }

            Deadlined.SetDone(killer);

            if (ToiletMaster.OnAnyoneCheckMurderStart(killer, target)) return false;
            if (Dad.OnAnyoneCheckMurderStart(target)) return false;

            Simon.RemoveTarget(killer, Simon.Instruction.Kill);

            if (Mastermind.ManipulatedPlayers.ContainsKey(killer.PlayerId))
                return Mastermind.ForceKillForManipulatedPlayer(killer, target);

            if (target.Is(CustomRoles.Spy) && !Spy.OnKillAttempt(killer, target)) return false;
            if (!Starspawn.CheckInteraction(killer, target)) return false;

            if (Penguin.IsVictim(killer)) return false;

            Sniper.TryGetSniper(target.PlayerId, ref killer);
            if (killer != __instance) Logger.Info($"Real Killer: {killer.GetNameWithRole().RemoveHtmlTags()}", "CheckMurder");

            if (Pelican.IsEaten(target.PlayerId)) return false;

            if (killer.IsRoleBlocked())
            {
                killer.Notify(BlockedAction.Kill.GetBlockNotify());
                return false;
            }

            if (Pursuer.OnClientMurder(killer)) return false;

            Seamstress.OnAnyoneCheckMurder(killer, target);

            if (killer.PlayerId != target.PlayerId)
            {
                switch (killer.Is(CustomRoles.Bloodlust))
                {
                    case false when !CheckMurder():
                        return false;
                    case true when killer.GetCustomRole().GetDYRole() == RoleTypes.Impostor:
                        if (killer.CheckDoubleTrigger(target, () =>
                        {
                            if (CheckMurder()) killer.RpcCheckAndMurder(target);
                        }))
                            killer.RpcCheckAndMurder(target);

                        return false;
                }

                bool CheckMurder() => Main.PlayerStates[killer.PlayerId].Role.OnCheckMurder(killer, target) || target.Is(CustomRoles.Fragile);
            }

            if (!killer.RpcCheckAndMurder(target, true)) return false;

            if (killer.Is(CustomRoles.Unlucky))
            {
                if (IRandom.Instance.Next(0, 100) < Options.UnluckyKillSuicideChance.GetInt())
                {
                    killer.Suicide();
                    return false;
                }
            }

            if (killer.Is(CustomRoles.Mare)) killer.ResetKillCooldown();

            if (!DoubleTrigger.FirstTriggerTimer.ContainsKey(killer.PlayerId) && killer.Is(CustomRoles.Swift) && !target.Is(CustomRoles.Pestilence))
            {
                if (killer.RpcCheckAndMurder(target, true))
                {
                    target.Suicide(PlayerState.DeathReason.Kill, killer);
                    killer.SetKillCooldown();
                }

                RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                return false;
            }

            if (killer.Is(CustomRoles.Magnet) && !target.Is(CustomRoles.Pestilence))
            {
                target.TP(killer);
                LateTask.New(() => killer.RpcCheckAndMurder(target), 0.1f, log: false);
                return false;
            }

            //==Kill processing==
            __instance.Kill(target);
            //===================
        }
        catch (Exception e) { ThrowException(e); }

        return false;
    }

    public static bool RpcCheckAndMurder(PlayerControl killer, PlayerControl target, bool check = false)
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        if (target == null) target = killer;

        if (CustomTeamManager.AreInSameCustomTeam(killer.PlayerId, target.PlayerId) && !CustomTeamManager.IsSettingEnabledForPlayerTeam(killer.PlayerId, CTAOption.KillEachOther))
        {
            Notify("SameCTATeam");
            return false;
        }

        if (AFKDetector.ShieldedPlayers.Contains(target.PlayerId))
        {
            Notify("AFKShielded");
            return false;
        }

        if ((killer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick) && !Options.JackalCanKillSidekick.GetBool()) ||
            (killer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Jackal) && !Options.SidekickCanKillJackal.GetBool()) ||
            (killer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Recruit) && !Options.JackalCanKillSidekick.GetBool()) ||
            (killer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Jackal) && !Options.SidekickCanKillJackal.GetBool()) ||
            (killer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick) && !Options.SidekickCanKillSidekick.GetBool()) ||
            (killer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Recruit) && !Options.SidekickCanKillSidekick.GetBool()) ||
            (killer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Sidekick) && !Options.SidekickCanKillSidekick.GetBool()) ||
            (killer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Recruit) && !Options.SidekickCanKillSidekick.GetBool()))
        {
            Notify("JackalSidekick");
            return false;
        }

        if (!Virus.ContagiousPlayersCanKillEachOther.GetBool() && target.Is(CustomRoles.Contagious) && killer.Is(CustomRoles.Contagious))
        {
            Notify("ContagiousPlayers");
            return false;
        }

        if (killer.IsMadmate() && target.Is(CustomRoleTypes.Impostor) && !Options.MadmateCanKillImp.GetBool())
        {
            Notify("MadmateKillImpostor");
            return false;
        }

        if (killer.Is(CustomRoleTypes.Impostor) && target.IsMadmate() && !Options.ImpCanKillMadmate.GetBool())
        {
            Notify("ImpostorKillMadmate");
            return false;
        }

        if (killer.Is(CustomRoleTypes.Coven) && target.Is(CustomRoleTypes.Coven))
        {
            Notify("CovenKillEachOther");
            return false;
        }

        if (killer.Is(CustomRoleTypes.Coven) && target.Is(CustomRoles.Entranced) && !Siren.CovenCanKillEntranced.GetBool())
        {
            Notify("CovenKillEntranced");
            return false;
        }

        if (killer.Is(CustomRoles.Entranced) && target.Is(CustomRoleTypes.Coven) && !Siren.EntrancedCanKillCoven.GetBool())
        {
            Notify("EntrancedKillCoven");
            return false;
        }

        if ((Romantic.PartnerId == target.PlayerId && Romantic.IsPartnerProtected) ||
            Medic.OnAnyoneCheckMurder(killer, target) ||
            Randomizer.IsShielded(target) ||
            Aid.ShieldedPlayers.ContainsKey(target.PlayerId) ||
            Gaslighter.IsShielded(target) ||
            !PotionMaster.OnAnyoneCheckMurder(target) ||
            !Grappler.OnAnyoneCheckMurder(target) ||
            !Adventurer.OnAnyoneCheckMurder(target) ||
            !Sentinel.OnAnyoneCheckMurder(killer) ||
            !ToiletMaster.OnAnyoneCheckMurder(killer, target))
        {
            Notify("SomeSortOfProtection");
            return false;
        }

        if (!Gardener.OnAnyoneCheckMurder(killer, target))
        {
            Notify("GardenerPlantNearby");
            return false;
        }

        if (!Socialite.OnAnyoneCheckMurder(killer, target))
        {
            Notify("SocialiteTarget");
            return false;
        }

        if (Penguin.IsVictim(killer))
        {
            Notify("PenguinVictimKill");
            return false;
        }

        if (Mathematician.State.ProtectedPlayerId == target.PlayerId)
        {
            Notify("MathematicianProtected");
            return false;
        }

        if (killer.Is(CustomRoles.Refugee) && target.Is(CustomRoleTypes.Impostor))
        {
            Notify("RefugeeKillImpostor");
            return false;
        }

        if (!Bodyguard.OnAnyoneCheckMurder(killer, target))
        {
            Notify("BodyguardProtected");
            return false;
        }

        if (Echo.On)
        {
            foreach (Echo echo in Echo.Instances)
            {
                if (echo.EchoPC.shapeshiftTargetPlayerId == target.PlayerId)
                {
                    echo.OnTargetCheckMurder(killer, target);
                    return false;
                }
            }
        }

        if (GhostRolesManager.AssignedGhostRoles.Values.Any(x => x.Instance is GA ga && ga.ProtectionList.Contains(target.PlayerId)))
        {
            Notify("GAGuarded");

            if (killer.IsLocalPlayer())
                Achievements.Type.IForgotThisRoleExists.CompleteAfterGameEnd();

            return false;
        }

        if (SoulHunter.IsSoulHunterTarget(killer.PlayerId) && target.Is(CustomRoles.SoulHunter))
        {
            Notify("SoulHunterTargetNotifyNoKill");
            return false;
        }

        if (killer.IsRoleBlocked())
        {
            killer.Notify(BlockedAction.Kill.GetBlockNotify());
            return false;
        }

        if (killer.Is(CustomRoles.Traitor) && target.Is(CustomRoleTypes.Impostor))
        {
            Notify("TraitorKillImpostor");
            return false;
        }

        if (Jackal.On && Jackal.ResetKillCooldownWhenSbGetKilled.GetBool() && !killer.Is(CustomRoles.Sidekick) && !target.Is(CustomRoles.Sidekick) && !killer.Is(CustomRoles.Jackal) && !target.Is(CustomRoles.Jackal) && !GameStates.IsMeeting)
            Jackal.AfterPlayerDiedTask(killer);

        if (target.Is(CustomRoles.Lucky))
        {
            if (IRandom.Instance.Next(0, 100) < Options.LuckyProbability.GetInt())
            {
                killer.SetKillCooldown(15f);
                return false;
            }
        }

        if (Crusader.ForCrusade.Contains(target.PlayerId))
        {
            foreach (PlayerControl player in Main.AllPlayerControls)
            {
                if (player.Is(CustomRoles.Crusader) && player.IsAlive())
                {
                    switch (killer.Is(CustomRoles.Pestilence))
                    {
                        case false when !killer.Is(CustomRoles.Minimalism):
                            player.Kill(killer);
                            Crusader.ForCrusade.Remove(target.PlayerId);
                            killer.RpcGuardAndKill(target);
                            return false;
                        case true:
                            killer.Kill(player);
                            Crusader.ForCrusade.Remove(target.PlayerId);
                            target.RpcGuardAndKill(killer);
                            Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.PissedOff;
                            return false;
                    }
                }
            }
        }

        switch (target.GetCustomRole())
        {
            case CustomRoles.Medic:
                Medic.IsDead(target);
                break;
            case CustomRoles.Gambler when Gambler.IsShielded.ContainsKey(target.PlayerId):
                Notify("SomeSortOfProtection");
                killer.SetKillCooldown(5f);
                return false;
            case CustomRoles.Spiritcaller when Spiritcaller.InProtect(target):
                killer.RpcGuardAndKill(target);
                Notify("SomeSortOfProtection");
                return false;
        }

        if (MeetingStates.FirstMeeting && Main.ShieldPlayer == target.FriendCode && !string.IsNullOrEmpty(target.FriendCode))
        {
            Main.ShieldPlayer = string.Empty;
            killer.SetKillCooldown(15f);
            killer.Notify(GetString("TriedToKillLastGameFirstKill"), 10f);
            return false;
        }

        if (Options.MadmateSpawnMode.GetInt() == 1 && Main.MadmateNum < CustomRoles.Madmate.GetCount() && target.CanBeMadmate())
        {
            Main.MadmateNum++;
            target.RpcSetCustomRole(CustomRoles.Madmate);
            ExtendedPlayerControl.RpcSetCustomRole(target.PlayerId, CustomRoles.Madmate);
            var sender = CustomRpcSender.Create("RpcCheckAndMurder - Madmate", SendOption.Reliable);
            sender.Notify(target, ColorString(GetRoleColor(CustomRoles.Madmate), GetString("BecomeMadmateCuzMadmateMode")));
            sender.RpcGuardAndKill(target, killer);
            sender.RpcGuardAndKill(target, target);
            sender.RpcGuardAndKill(killer, target);
            sender.SetKillCooldown(killer);
            sender.SendMessage();
            Logger.Info($"Add-on assigned: {target?.Data?.PlayerName} = {target.GetCustomRole()} + {CustomRoles.Madmate}", $"Assign {CustomRoles.Madmate}");
            return false;
        }

        if (!Main.PlayerStates[target.PlayerId].Role.OnCheckMurderAsTarget(killer, target))
        {
            Notify("SomeSortOfProtection");
            return false;
        }

        if (killer.Is(CustomRoles.Rookie) && MeetingStates.FirstMeeting)
        {
            Notify("RookieKillRoundOne");
            return false;
        }

        if (!check) killer.Kill(target);

        if (killer.Is(CustomRoles.Doppelganger)) Doppelganger.OnCheckMurderEnd(killer, target);

        return true;

        void Notify(string message) => killer.Notify(ColorString(Color.yellow, GetString("CheckMurderFail") + GetString(message)), 12f);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
internal static class MurderPlayerPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, [HarmonyArgument(1)] MurderResultFlags resultFlags, ref bool __state)
    {
        if (GameStates.IsLobby || AntiBlackout.SkipTasks) return false;

        bool protectedByClient = resultFlags.HasFlag(MurderResultFlags.DecisionByHost) && target.IsProtected();
        bool protectedByHost = resultFlags.HasFlag(MurderResultFlags.FailedProtected);
        bool failed = resultFlags.HasFlag(MurderResultFlags.FailedError);
        __state = !protectedByClient && !protectedByHost && !failed;

        Logger.Info($"{__instance.GetNameWithRole()} => {target.GetNameWithRole()} - {nameof(protectedByClient)}: {protectedByClient}, {nameof(protectedByHost)}: {protectedByHost}, {nameof(failed)}: {failed}", "MurderPlayer");

        RandomSpawn.CustomNetworkTransformHandleRpcPatch.HasSpawned.Add(__instance.PlayerId);

        if (!target.IsProtected() && !Doppelganger.DoppelVictim.ContainsKey(target.PlayerId) && !Camouflage.ResetSkinAfterDeathPlayers.Contains(target.PlayerId))
        {
            Camouflage.ResetSkinAfterDeathPlayers.Add(target.PlayerId);
            Camouflage.RpcSetSkin(target, true);
        }

        return true;
    }

    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, bool __state)
    {
        if (__instance == null || target == null || __instance.PlayerId >= 254 || target.PlayerId >= 254 || !__state) return;

        if (target.AmOwner) RemoveDisableDevicesPatch.UpdateDisableDevices();

        if (!target.Data.IsDead || !AmongUsClient.Instance.AmHost) return;
        if (OverKiller.OverDeadPlayerList.Contains(target.PlayerId)) return;

        PlayerControl killer = __instance; // Alternative variable

        PlagueDoctor.OnAnyMurder();

        // Replacement process when the actual killer and killer are different
        if (Sniper.TryGetSniper(target.PlayerId, ref killer)) Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Sniped;

        if (killer.Is(CustomRoles.Sniper))
        {
            if (!Options.UsePets.GetBool())
                killer.RpcResetAbilityCooldown();
            else
            {
                int cd = Options.DefaultShapeshiftCooldown.GetInt();
                Main.AbilityCD[killer.PlayerId] = (TimeStamp, cd);
                SendRPC(CustomRPC.SyncAbilityCD, 1, killer.PlayerId, cd);
            }
        }

        if (killer != __instance) Logger.Info($"Real Killer = {killer.GetNameWithRole().RemoveHtmlTags()}", "MurderPlayer");

        if (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.etc)
        {
            // If the cause of death is not specified, it is determined as a normal kill.
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Kill;
        }

        // Let’s see if Youtuber was stabbed first
        if (Main.FirstDied == string.Empty && target.Is(CustomRoles.Youtuber))
        {
            CustomSoundsManager.RPCPlayCustomSoundAll("Congrats");
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Youtuber);
            CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
        }

        Postman.CheckAndResetTargets(target, true);

        if (target.Is(CustomRoles.Trapper) && killer != target) killer.TrapperKilled(target);

        if (target.Is(CustomRoles.Stained)) Stained.OnDeath(target, killer);

        Witness.AllKillers[killer.PlayerId] = TimeStamp;

        killer.AddKillTimerToDict();

        switch (target.GetCustomRole())
        {
            case CustomRoles.BallLightning when killer != target:
                BallLightning.MurderPlayer(killer, target);
                break;
            case CustomRoles.Bane when killer != target:
                Bane.OnKilled(killer);
                break;
            case CustomRoles.Markseeker:
                Markseeker.OnDeath(target);
                break;
        }

        Main.PlayerStates[killer.PlayerId].Role.OnMurder(killer, target);

        Chef.SpitOutFood(killer);

        if (Options.CurrentGameMode == CustomGameMode.Speedrun)
            Speedrun.ResetTimer(killer);

        if (killer.Is(CustomRoles.TicketsStealer) && killer.PlayerId != target.PlayerId)
            killer.Notify(string.Format(GetString("TicketsStealerGetTicket"), ((Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == killer.PlayerId) + 1) * Options.TicketsPerKill.GetFloat()).ToString("0.0#####")));

        if (killer.Is(CustomRoles.Pickpocket) && killer.PlayerId != target.PlayerId)
            killer.Notify(string.Format(GetString("PickpocketGetVote"), ((Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == killer.PlayerId) + 1) * Pickpocket.VotesPerKill.GetFloat()).ToString("0.0#####")));

        if (killer.Is(CustomRoles.Deadlined)) Deadlined.SetDone(killer);

        if (target.Is(CustomRoles.Avanger))
        {
            PlayerControl[] pcList = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId).ToArray();
            PlayerControl rp = pcList.RandomElement();
            if (!rp.Is(CustomRoles.Pestilence)) rp.Suicide(PlayerState.DeathReason.Revenge, target);
        }

        if (target.Is(CustomRoles.Bait) && !killer.Is(CustomRoles.Minimalism) && (killer.PlayerId != target.PlayerId || target.GetRealKiller()?.GetCustomRole() is CustomRoles.Swooper or CustomRoles.Wraith || !killer.Is(CustomRoles.Oblivious) || !Options.ObliviousBaitImmune.GetBool()))
        {
            killer.RPCPlayCustomSound("Congrats");
            target.RPCPlayCustomSound("Congrats");
            float delay;

            if (Options.BaitDelayMax.GetFloat() < Options.BaitDelayMin.GetFloat())
                delay = 0f;
            else
                delay = IRandom.Instance.Next((int)Options.BaitDelayMin.GetFloat(), (int)Options.BaitDelayMax.GetFloat() + 1);

            delay = Math.Max(delay, 0.15f);
            if (delay > 0.15f && Options.BaitDelayNotify.GetBool()) killer.Notify(ColorString(GetRoleColor(CustomRoles.Bait), string.Format(GetString("KillBaitNotify"), (int)delay)), delay);

            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} killed Bait => {target.GetNameWithRole().RemoveHtmlTags()}", "MurderPlayer");

            LateTask.New(() =>
            {
                if (GameStates.IsInTask)
                {
                    if (!Options.ReportBaitAtAllCost.GetBool())
                        killer.CmdReportDeadBody(target.Data);
                    else
                        killer.NoCheckStartMeeting(target.Data, true);
                }
            }, delay, "Bait Self Report");
        }

        AfterPlayerDeathTasks(target);

        Main.PlayerStates[target.PlayerId].SetDead();
        target.SetRealKiller(killer, true);
        CountAlivePlayers(true);

        __instance.MarkDirtySettings();
        target.MarkDirtySettings();
        Main.Instance.StartCoroutine(NotifyEveryoneAsync(4, false));

        Statistics.OnMurder(killer, target);
    }
}

// Triggered when the shapeshifter selects a target
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckShapeshift))]
internal static class CheckShapeshiftPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target /*, [HarmonyArgument(1)] bool shouldAnimate*/)
    {
        return ShapeshiftPatch.ProcessShapeshift(__instance, target); // return false to cancel the shapeshift
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckShapeshift))]
internal static class CmdCheckShapeshiftPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target /*, [HarmonyArgument(1)] bool shouldAnimate*/)
    {
        return CheckShapeshiftPatch.Prefix(__instance, target /*, shouldAnimate*/);
    }
}

// Triggered when the egg animation starts playing
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
internal static class ShapeshiftPatch
{
    public static bool ProcessShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        if (!Main.ProcessShapeshifts || shapeshifter.PlayerId >= 254) return true;

        if (AntiBlackout.SkipTasks)
        {
            Logger.Info("Shapeshift while AntiBlackOut protection is in progress, shapeshift canceled", "Shapeshift");
            return false;
        }

        if (shapeshifter == null || target == null) return true;

        Logger.Info($"{shapeshifter.GetNameWithRole()} => {target.GetNameWithRole()}", "Shapeshift");

        bool shapeshifting = shapeshifter.PlayerId != target.PlayerId;

        CustomRoles role = shapeshifter.GetCustomRole();

        if (AmongUsClient.Instance.AmHost && shapeshifting)
        {
            if (!Rhapsode.CheckAbilityUse(shapeshifter) || Stasis.IsTimeFrozen || TimeMaster.Rewinding) return false;

            if (shapeshifter.Is(CustomRoles.Trainee) && MeetingStates.FirstMeeting)
            {
                shapeshifter.Notify(GetString("TraineeNotify"));
                return false;
            }
        }

        Main.CheckShapeshift[shapeshifter.PlayerId] = shapeshifting;
        Main.ShapeshiftTarget[shapeshifter.PlayerId] = target.PlayerId;

        if (!AmongUsClient.Instance.AmHost) return true;

        if (!shapeshifting) Camouflage.RpcSetSkin(shapeshifter);

        var isSSneeded = true;

        if (!Pelican.IsEaten(shapeshifter.PlayerId) && !GameStates.IsVoting)
            isSSneeded = Main.PlayerStates[shapeshifter.PlayerId].Role.OnShapeshift(shapeshifter, target, shapeshifting);

        if (shapeshifter.Is(CustomRoles.Hangman) && shapeshifter.GetAbilityUseLimit() < 1 && shapeshifting)
        {
            shapeshifter.SetKillCooldown(Hangman.ShapeshiftDuration.GetFloat() + 1f);
            isSSneeded = false;
        }

        bool forceCancel = role.ForceCancelShapeshift();

        if (Changeling.ChangedRole.TryGetValue(shapeshifter.PlayerId, out bool changed) && changed && shapeshifter.GetRoleTypes() != RoleTypes.Shapeshifter)
        {
            forceCancel = true;
            isSSneeded = false;
        }

        bool shouldCancel = Options.DisableShapeshiftAnimations.GetBool();
        bool shouldAlwaysCancel = shouldCancel && Options.DisableAllShapeshiftAnimations.GetBool();
        bool doSSwithoutAnim = isSSneeded && shouldAlwaysCancel;

        doSSwithoutAnim |= isSSneeded && role.IsNoAnimationShifter();
        isSSneeded &= !shouldAlwaysCancel;
        forceCancel |= shouldAlwaysCancel;
        isSSneeded &= !doSSwithoutAnim;

        // Forced rewriting in case the name cannot be corrected due to the timing of unshifting being off.
        if (!shapeshifting && !shapeshifter.Is(CustomRoles.Glitch) && isSSneeded)
            LateTask.New(() => Main.Instance.StartCoroutine(NotifyEveryoneAsync(3)), 1.2f, "ShapeShiftNotify");

        if (!(shapeshifting && doSSwithoutAnim) && !isSSneeded && !Swapster.FirstSwapTarget.ContainsKey(shapeshifter.PlayerId) && !Transporter.FirstSwapTarget.ContainsKey(shapeshifter.PlayerId))
            LateTask.New(shapeshifter.RpcResetAbilityCooldown, 0.01f, log: false);

        if (!isSSneeded)
        {
            Main.CheckShapeshift[shapeshifter.PlayerId] = false;
            shapeshifter.RpcRejectShapeshift();
            NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: shapeshifter);
        }

        if (doSSwithoutAnim)
        {
            shapeshifter.RpcShapeshift(target, false);
            return false;
        }


        bool animated = isSSneeded || (!shouldCancel && !forceCancel) || (!shapeshifting && !shouldAlwaysCancel);
        Statistics.OnShapeshift(shapeshifter, shapeshifting, animated);
        return animated;
    }

    // Tasks that should run when someone performs a shapeshift (with the egg animation) should be here.
    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!Main.ProcessShapeshifts || !GameStates.IsInTask || __instance == null || target == null) return;

        bool shapeshifting = __instance.PlayerId != target.PlayerId;

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            PlainShipRoom plainShipRoom = pc.GetPlainShipRoom();
            string room = GetString(plainShipRoom != null ? plainShipRoom.RoomId.ToString() : "Outside");
            if (pc.Is(CustomRoles.Shiftguard)) pc.Notify($"[<#00ffa5>{room}</color>] {GetString(shapeshifting ? "ShiftguardNotifySS" : "ShiftguardNotifyUnshift")}");

            switch (Main.PlayerStates[pc.PlayerId].Role)
            {
                case Adventurer av:
                    Adventurer.OnAnyoneShapeshiftLoop(av, __instance);
                    break;
                case Crewmate.Sentry st:
                    st.OnAnyoneShapeshiftLoop(__instance, target);
                    break;
            }
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcShapeshift))]
internal static class RpcShapeshiftPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        Main.CheckShapeshift[__instance.PlayerId] = __instance.PlayerId != target.PlayerId;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
internal static class ReportDeadBodyPatch
{
    public static Dictionary<byte, bool> CanReport;
    public static readonly Dictionary<byte, List<NetworkedPlayerInfo>> WaitReport = [];

    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] NetworkedPlayerInfo target)
    {
        if (GameStates.IsMeeting) return false;
        if (Options.DisableMeeting.GetBool()) return false;
        if (Options.CurrentGameMode != CustomGameMode.Standard) return false;
        if (Options.DisableReportWhenCC.GetBool() && Camouflage.IsCamouflage) return false;

        if (!CanReport[__instance.PlayerId])
        {
            WaitReport[__instance.PlayerId].Add(target);
            Logger.Warn($"{__instance.GetNameWithRole().RemoveHtmlTags()}: Reporting is currently prohibited, so we will wait until it becomes possible.", "ReportDeadBody");
            return false;
        }

        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {target?.Object?.GetNameWithRole() ?? "null"}", "ReportDeadBody");

        foreach (KeyValuePair<byte, PlayerState> kvp in Main.PlayerStates)
            kvp.Value.LastRoom = GetPlayerById(kvp.Key).GetPlainShipRoom();

        if (!AmongUsClient.Instance.AmHost) return true;

        try
        {
            // If the caller is dead, this process will cancel the meeting, so stop here.
            if (__instance.Data.IsDead) return false;

            //=============================================
            // Next, check whether this meeting is allowed
            //=============================================

            PlayerControl killer = target?.Object?.GetRealKiller();
            CustomRoles? killerRole = killer?.GetCustomRole();

            if (Main.PlayerStates.Values.Any(x => x.Role is Tremor { IsDoom: true })) return false;

            if (target == null)
            {
                if (CustomSabotage.Instances.Count > 0)
                {
                    Notify("CannotCallEmergencyMeetingWhileSabotage");
                    return false;
                }
                
                if (__instance.Is(CustomRoles.Jester) && !Jester.JesterCanUseButton.GetBool())
                {
                    Notify("JesterCannotCallEmergencyMeeting");
                    return false;
                }

                if (__instance.Is(CustomRoles.NiceSwapper) && !NiceSwapper.CanStartMeeting.GetBool())
                {
                    Notify("NiceSwapperCannotCallEmergencyMeeting");
                    return false;
                }

                if (__instance.Is(CustomRoles.Adrenaline) && !Adrenaline.CanCallMeeting(__instance))
                {
                    Notify("AdrenalineCannotCallEmergencyMeeting");
                    return false;
                }

                if (SoulHunter.IsSoulHunterTarget(__instance.PlayerId))
                {
                    Notify("SoulHunterTargetNotifyNoMeeting");
                    return false;
                }

                if (!Wyrd.CheckPlayerAction(__instance, Wyrd.Action.Button)) return false; // Player dies, no notify needed
            }

            if (target != null)
            {
                if (Bloodhound.UnreportablePlayers.Contains(target.PlayerId)
                    || Vulture.UnreportablePlayers.Contains(target.PlayerId)
                    || (killer != null && killer.Is(CustomRoles.Goddess))
                    || Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.Gambled
                    || killerRole == CustomRoles.Scavenger
                    || Cleaner.CleanerBodies.Contains(target.PlayerId))
                {
                    Notify("UnreportableBody");
                    return false;
                }


                if (!Occultist.OnAnyoneReportDeadBody(target) ||
                    !Altruist.OnAnyoneCheckReportDeadBody(__instance, target))
                {
                    Notify("PlayerWasRevived");
                    return false;
                }

                if (!Librarian.OnAnyoneReport(__instance)) return false; // Player dies, no notify needed
                if (!BoobyTrap.OnAnyoneCheckReportDeadBody(__instance, target)) return false; // Player dies, no notify needed

                if (!Hypnotist.OnAnyoneReport())
                {
                    if (__instance.IsLocalPlayer())
                        Achievements.Type.Hypnosis.CompleteAfterGameEnd();

                    Notify("HypnosisNoMeeting");
                    return false;
                }

                if (!Wyrd.CheckPlayerAction(__instance, Wyrd.Action.Report)) return false; // Player dies, no notify needed

                if (!Main.PlayerStates[__instance.PlayerId].Role.CheckReportDeadBody(__instance, target, killer)) return false;

                PlayerControl tpc = target.Object;

                if (__instance.Is(CustomRoles.Unlucky) && (tpc == null || !tpc.Is(CustomRoles.Bait)))
                {
                    if (IRandom.Instance.Next(0, 100) < Options.UnluckyReportSuicideChance.GetInt())
                    {
                        __instance.Suicide();
                        return false;
                    }
                }

                if (tpc.Is(CustomRoles.Unreportable))
                {
                    Notify("TargetDisregarded");
                    return false;
                }

                if (__instance.Is(CustomRoles.Oblivious))
                {
                    Notify("AmOblivious");
                    return false;
                }
            }

            if (Options.SyncButtonMode.GetBool() && target == null)
            {
                Logger.Info($"Max buttons: {Options.SyncedButtonCount.GetInt()}, used: {Options.UsedButtonCount}", "ReportDeadBody");

                if (Options.SyncedButtonCount.GetFloat() <= Options.UsedButtonCount)
                {
                    Logger.Info("The ship has no more emergency meetings left", "ReportDeadBody");
                    Notify("ShipHasNoMoreMeetingsLeft");
                    return false;
                }

                Options.UsedButtonCount++;

                if (Math.Abs(Options.SyncedButtonCount.GetFloat() - Options.UsedButtonCount) < 0.5f)
                    Logger.Info("This was the last allowed emergency meeting", "ReportDeadBody");
            }
        }
        catch (Exception e) { ThrowException(e); }

        AfterReportTasks(__instance, target);

        return true;

        void Notify(string str) => __instance.Notify(ColorString(Color.yellow, GetString("CheckReportFail") + GetString(str)), 15f);
    }

    public static void AfterReportTasks(PlayerControl player, NetworkedPlayerInfo target)
    {
        //=============================================================================================
        //    Hereinafter, it is confirmed that the meeting is allowed, and the meeting will start.
        //=============================================================================================

        CustomNetObject.OnMeeting();

        Asthmatic.RunChecks = false;

        Damocles.CountRepairSabotage = false;
        Stressed.CountRepairSabotage = false;

        HudSpritePatch.ResetButtonIcons = true;

        if (Options.CurrentGameMode == CustomGameMode.Standard)
        {
            foreach (byte id in Main.DiedThisRound)
            {
                PlayerControl receiver = id.GetPlayer();
                if (receiver == null) continue;

                PlayerControl killer = receiver.GetRealKiller();
                if (killer == null) continue;

                SendMessage("\n", receiver.PlayerId, string.Format(GetString("DeathCommand"), killer.PlayerId.ColoredPlayerName(), (killer.Is(CustomRoles.Bloodlust) ? $"{CustomRoles.Bloodlust.ToColoredString()} " : string.Empty) + killer.GetCustomRole().ToColoredString()), sendOption: SendOption.None);
            }
        }

        Main.DiedThisRound = [];

        Main.AllAlivePlayerControls.DoIf(x => x.Is(CustomRoles.Lazy), x => Lazy.BeforeMeetingPositions[x.PlayerId] = x.Pos());

        if (Lovers.PrivateChat.GetBool()) ChatManager.ClearChat(Main.AllAlivePlayerControls.ExceptBy(Main.LoversPlayers.ConvertAll(x => x.PlayerId), x => x.PlayerId).ToArray());

        if (target == null)
        {
            switch (Main.PlayerStates[player.PlayerId].Role)
            {
                case Mayor:
                    Mayor.MayorUsedButtonCount[player.PlayerId]++;
                    break;
                case Rogue rg:
                    rg.OnButtonPressed();
                    break;
            }

            if (QuizMaster.On)
            {
                QuizMaster.Data.LastPlayerPressedButtonName = player.GetRealName();
                QuizMaster.Data.NumEmergencyMeetings++;
            }
        }
        else
        {
            PlayerControl tpc = target.Object;

            if (tpc != null && !tpc.IsAlive())
            {
                if (player.Is(CustomRoles.Detective) && player.PlayerId != target.PlayerId)
                    Detective.OnReportDeadBody(player, target.Object);
                else if (player.Is(CustomRoles.Sleuth) && player.PlayerId != target.PlayerId)
                {
                    string msg = string.Format(GetString("SleuthMsg"), tpc.GetRealName(), tpc.GetDisplayRoleName());
                    Main.SleuthMsgs[player.PlayerId] = msg;
                }
            }

            if (Virus.InfectedBodies.Contains(target.PlayerId))
                Virus.OnKilledBodyReport(player);

            if (QuizMaster.On)
            {
                QuizMaster.Data.LastReporterName = player.GetRealName();
                QuizMaster.Data.LastReportedPlayer = (Palette.GetColorName(target.DefaultOutfit.ColorId), target.Object);
                if (MeetingStates.FirstMeeting) QuizMaster.Data.FirstReportedBodyPlayerName = target.Object.GetRealName();
            }
        }

        if (QuizMaster.On)
        {
            if (MeetingStates.FirstMeeting) QuizMaster.Data.NumPlayersDeadFirstRound = Main.AllPlayerControls.Count(x => !x.IsAlive() && !x.Is(CustomRoles.GM));
            QuizMaster.Data.NumMeetings++;
        }

        if (Main.LoversPlayers.Exists(x => x.IsAlive()) && Main.IsLoversDead && Lovers.LoverDieConsequence.GetValue() == 1)
        {
            PlayerControl aliveLover = Main.LoversPlayers.First(x => x.IsAlive());

            switch (Lovers.LoverSuicideTime.GetValue())
            {
                case 1:
                    aliveLover.Suicide(PlayerState.DeathReason.FollowingSuicide);
                    break;
                case 2:
                    CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.FollowingSuicide, aliveLover.PlayerId);
                    break;
            }
        }

        EAC.TimeSinceLastTaskCompletion.Clear();

        Enigma.OnReportDeadBody(player, target);
        Mediumshiper.OnReportDeadBody(target);
        Mortician.OnReportDeadBody(player, target);
        Spiritualist.OnReportDeadBody(target);

        Bloodmoon.OnMeetingStart();
        Deadlined.OnMeetingStart();

        Main.LastVotedPlayerInfo = null;
        Arsonist.ArsonistTimer.Clear();
        Farseer.FarseerTimer.Clear();
        Puppeteer.PuppeteerList.Clear();
        Puppeteer.PuppeteerDelayList.Clear();
        Veteran.VeteranInProtect.Clear();
        Grenadier.GrenadierBlinding.Clear();
        SecurityGuard.BlockSabo.Clear();
        Grenadier.MadGrenadierBlinding.Clear();
        Divinator.DidVote.Clear();
        Oracle.DidVote.Clear();

        Imitator.ImitatingRole.SetAllValues(CustomRoles.Imitator);

        foreach (PlayerState state in Main.PlayerStates.Values)
        {
            if (state.Role.IsEnable)
                state.Role.OnReportDeadBody();
        }

        Main.AbilityCD.Clear();
        SendRPC(CustomRPC.SyncAbilityCD, 2);

        if (player.Is(CustomRoles.Damocles)) Damocles.OnReport(player.PlayerId);

        Damocles.OnMeetingStart();

        if (player.Is(CustomRoles.Stressed)) Stressed.OnReport(player);

        Stressed.OnMeetingStart();

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            if (Main.CheckShapeshift.ContainsKey(pc.PlayerId) && !Doppelganger.DoppelVictim.ContainsKey(pc.PlayerId))
                Camouflage.RpcSetSkin(pc, revertToDefault: true);

            if (Main.CurrentMap == MapNames.Fungle && (pc.IsMushroomMixupActive() || IsActive(SystemTypes.MushroomMixupSabotage)))
                pc.FixMixedUpOutfit();

            PhantomRolePatch.OnReportDeadBody(pc);
        }

        MeetingTimeManager.OnReportDeadBody();

        NameNotifyManager.Reset();
        NotifyRoles(true, ForceLoop: true, CamouflageIsForMeeting: true, GuesserIsForMeeting: true);

        Main.ProcessShapeshifts = false;

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            if (pc.IsAlive())
            {
                if (Camouflage.IsCamouflage && !Magistrate.CallCourtNextMeeting)
                    Camouflage.RpcSetSkin(pc, revertToDefault: true, forceRevert: true);

                if (Magistrate.CallCourtNextMeeting)
                {
                    string name = pc.GetRealName();
                    RpcChangeSkin(pc, new NetworkedPlayerInfo.PlayerOutfit().Set(name, 15, "", "", "", "", ""));
                }
            }
            else if (!pc.Data.IsDead)
                pc.RpcExileV2();
        }

        RPCHandlerPatch.RemoveExpiredWhiteList();

        Camouflage.CamoTimesThisRound = 0;

        NumSnapToCallsThisRound = 0;

        if (HudManagerPatch.AchievementUnlockedText == string.Empty)
            HudManagerPatch.ClearLowerInfoText();
    }
}

//[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
internal static class FixedUpdatePatch
{
    private static readonly StringBuilder Mark = new(20);
    private static readonly StringBuilder Suffix = new();
    private static int LevelKickBufferTime = 10;
    private static readonly Dictionary<byte, int> DeadBufferTime = [];
    private static readonly Dictionary<byte, long> LastUpdate = [];
    private static readonly Dictionary<byte, long> LastAddAbilityTime = [];
    private static long LastErrorTS;
    private static long LastSelfNameUpdateTS;
    private static readonly Dictionary<byte, TextMeshPro> RoleTextTMPs = [];

    public static void Postfix(PlayerControl __instance, bool lowLoad)
    {
        if (__instance == null || __instance.PlayerId >= 254) return;

        CheckMurderPatch.Update(__instance.PlayerId);

        if (AmongUsClient.Instance.AmHost && __instance.AmOwner)
            CustomNetObject.FixedUpdate();

        byte id = __instance.PlayerId;

        if (AmongUsClient.Instance.AmHost && GameStates.IsInTask && ReportDeadBodyPatch.CanReport[id] && ReportDeadBodyPatch.WaitReport[id].Count > 0)
        {
            if (id.IsPlayerRoleBlocked())
            {
                __instance.Notify(BlockedAction.Report.GetBlockNotify());
                Logger.Info("Dead Body Report Blocked (player role blocked)", "FixedUpdate.ReportDeadBody");
                ReportDeadBodyPatch.WaitReport[id].Clear();
            }
            else
            {
                NetworkedPlayerInfo info = ReportDeadBodyPatch.WaitReport[id][0];
                ReportDeadBodyPatch.WaitReport[id].Clear();
                Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()}: Now that it is possible to report, we will process the report.", "ReportDeadBody");
                __instance.ReportDeadBody(info);
            }
        }

        if (AmongUsClient.Instance.AmHost)
        {
            if (GhostRolesManager.AssignedGhostRoles.TryGetValue(id, out (CustomRoles Role, IGhostRole Instance) ghostRole))
            {
                switch (ghostRole.Instance)
                {
                    case Warden warden:
                        warden.Update(__instance);
                        break;
                    case Haunter haunter:
                        haunter.Update(__instance);
                        break;
                    case Bloodmoon bloodmoon:
                        Bloodmoon.Update(__instance, bloodmoon);
                        break;
                }
            }
            else if (!Main.HasJustStarted && GameStates.IsInTask && !ExileController.Instance && GhostRolesManager.ShouldHaveGhostRole(__instance))
                GhostRolesManager.AssignGhostRole(__instance);
        }

        if (GameStates.InGame && Options.DontUpdateDeadPlayers.GetBool() && !(__instance.IsHost() && __instance.IsLocalPlayer()) && !__instance.IsAlive() && !__instance.GetCustomRole().NeedsUpdateAfterDeath() && Options.CurrentGameMode is not CustomGameMode.RoomRush and not CustomGameMode.Quiz)
        {
            int buffer = Options.DeepLowLoad.GetBool() ? 150 : 60;
            DeadBufferTime.TryAdd(id, buffer);
            DeadBufferTime[id]--;
            if (DeadBufferTime[id] > 0) return;
            DeadBufferTime[id] = buffer;
        }

        try { DoPostfix(__instance, lowLoad); }
        catch (Exception ex)
        {
            if (OnGameJoinedPatch.JoiningGame && ex is NullReferenceException) return;
            
            long now = TimeStamp;
            string nameWithRole = __instance.GetNameWithRole();
            if (string.IsNullOrEmpty(nameWithRole.Trim())) return;

            if (LastErrorTS != now)
            {
                Logger.Error($"Error for {nameWithRole}:", "FixedUpdatePatch");
                ThrowException(ex);
                LastErrorTS = now;
                return;
            }

            Logger.Error($"Error for {nameWithRole}: {ex}", "FixedUpdatePatch");
        }
    }

    private static void DoPostfix(PlayerControl __instance, bool lowLoad)
    {
        PlayerControl player = __instance;
        byte playerId = player.PlayerId;
        byte lpId = PlayerControl.LocalPlayer.PlayerId;
        bool localPlayer = playerId == lpId; // Updates that are independent of the player are only executed for the local player.

        bool inTask = GameStates.IsInTask;
        bool alive = player.IsAlive();

        if (localPlayer)
        {
            CustomSabotage.UpdateAll();
            Zoom.OnFixedUpdate();
            TextBoxPatch.CheckChatOpen();

            if (!lowLoad) NameNotifyManager.OnFixedUpdate();
        }

        if (!lowLoad)
        {
            TargetArrow.OnFixedUpdate(player);
            LocateArrow.OnFixedUpdate(player);

            if (AmongUsClient.Instance.AmHost)
            {
                Camouflage.OnFixedUpdate(player);

                if (localPlayer && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.FFA or CustomGameMode.CaptureTheFlag or CustomGameMode.NaturalDisasters or CustomGameMode.BedWars && GameStartTimeStamp + 44 == TimeStamp)
                    NotifyRoles();
            }
        }

        long now = TimeStamp;

        if (AmongUsClient.Instance.AmHost)
        {
            AFKDetector.OnFixedUpdate(player);

            if (GameStates.IsLobby && ((ModUpdater.HasUpdate && ModUpdater.ForceUpdate) || ModUpdater.IsBroken || !Main.AllowPublicRoom) && AmongUsClient.Instance.IsGamePublic)
                AmongUsClient.Instance.ChangeGamePublic(false);

            // Kick low-level people
            if (!lowLoad && GameSettingMenuPatch.LastPresetChange + 5 < TimeStamp && GameStates.IsLobby && !player.AmOwner && Options.KickLowLevelPlayer.GetInt() != 0 && (
                (player.Data.PlayerLevel != 0 && player.Data.PlayerLevel < Options.KickLowLevelPlayer.GetInt()) ||
                player.Data.FriendCode == string.Empty
            ))
            {
                LevelKickBufferTime--;

                if (LevelKickBufferTime <= 0)
                {
                    LevelKickBufferTime = 20;

                    if (player.GetClient().ProductUserId != "")
                    {
                        string hashedPuid = player.GetClient().GetHashedPuid();
                        if (!BanManager.TempBanWhiteList.Contains(hashedPuid)) BanManager.TempBanWhiteList.Add(hashedPuid);
                    }

                    if (!Main.AllPlayerControls.All(x => x.Data.PlayerLevel <= 1) && !LobbyPatch.IsGlitchedRoomCode())
                    {
                        string msg = string.Format(GetString("KickBecauseLowLevel"), player.GetRealName().RemoveHtmlTags());
                        Logger.SendInGame(msg, Color.yellow);
                        AmongUsClient.Instance.KickPlayer(player.OwnerId, true);
                        Logger.Info(msg, "Low Level Temp Ban");
                    }
                }
            }

            if (!GameStates.IsLobby)
            {
                if (player.Is(CustomRoles.Spurt) && !Mathf.Approximately(Main.AllPlayerSpeed[playerId], Spurt.StartingSpeed[playerId]) && !inTask && !GameStates.IsMeeting) // fix ludicrous bug
                {
                    Main.AllPlayerSpeed[playerId] = Spurt.StartingSpeed[playerId];
                    player.MarkDirtySettings();
                }

                if (!Main.KillTimers.TryAdd(playerId, 10f) && ((!player.inVent && !player.MyPhysics.Animations.IsPlayingEnterVentAnimation()) || player.Is(CustomRoles.Haste)) && Main.KillTimers[playerId] > 0) Main.KillTimers[playerId] -= Time.fixedDeltaTime;

                if (localPlayer)
                {
                    if (QuizMaster.On && inTask && !lowLoad && QuizMaster.AllSabotages.Any(IsActive))
                        QuizMaster.Data.LastSabotage = QuizMaster.AllSabotages.FirstOrDefault(IsActive);
                }

                if (!lowLoad && player.IsModdedClient() && player.Is(CustomRoles.Haste))
                    player.ForceKillTimerContinue = true;

                if (DoubleTrigger.FirstTriggerTimer.Count > 0)
                    DoubleTrigger.OnFixedUpdate(player);

                SabotageSystemTypeRepairDamagePatch.SabotageDoubleTrigger.Update(Time.deltaTime);

                if (Main.PlayerStates.TryGetValue(playerId, out PlayerState s) && s.Role.IsEnable)
                    s.Role.OnFixedUpdate(player);

                if (inTask && player.Is(CustomRoles.PlagueBearer) && PlagueBearer.IsPlaguedAll(player))
                {
                    player.RpcSetCustomRole(CustomRoles.Pestilence);
                    player.Notify(GetString("PlagueBearerToPestilence"));
                    player.RpcGuardAndKill(player);

                    if (!PlagueBearer.PestilenceList.Contains(playerId))
                        PlagueBearer.PestilenceList.Add(playerId);

                    player.ResetKillCooldown();
                    PlagueBearer.PlayerIdList.Remove(playerId);
                }

                bool checkPos = inTask && !ExileController.Instance && !AntiBlackout.SkipTasks && player != null && alive && !Pelican.IsEaten(playerId) && Main.IntroDestroyed;
                if (checkPos) Asthmatic.OnCheckPlayerPosition(player);

                foreach (PlayerState state in Main.PlayerStates.Values)
                {
                    if (state.Role.IsEnable)
                    {
                        if (checkPos) state.Role.OnCheckPlayerPosition(player);
                        state.Role.OnGlobalFixedUpdate(player, lowLoad);
                    }
                }

                if (Main.PlayerStates.TryGetValue(playerId, out PlayerState playerState) && inTask && alive)
                {
                    List<CustomRoles> subRoles = playerState.SubRoles;

                    if (!lowLoad)
                    {
                        if (subRoles.Contains(CustomRoles.Dynamo)) Dynamo.OnFixedUpdate(player);
                        if (subRoles.Contains(CustomRoles.Spurt)) Spurt.OnFixedUpdate(player);
                        if (subRoles.Contains(CustomRoles.Damocles)) Damocles.Update(player);
                        if (subRoles.Contains(CustomRoles.Stressed)) Stressed.Update(player);
                        if (subRoles.Contains(CustomRoles.Asthmatic)) Asthmatic.OnFixedUpdate();
                        if (subRoles.Contains(CustomRoles.Disco)) Disco.OnFixedUpdate(player);
                        if (subRoles.Contains(CustomRoles.Clumsy)) Clumsy.OnFixedUpdate(player);
                        if (subRoles.Contains(CustomRoles.Sonar)) Sonar.OnFixedUpdate(player);
                        if (subRoles.Contains(CustomRoles.Sleep)) Sleep.CheckGlowNearby(player);
                        if (subRoles.Contains(CustomRoles.Introvert)) Introvert.OnFixedUpdate(player);
                        if (subRoles.Contains(CustomRoles.Allergic)) Allergic.OnFixedUpdate(player);
                    }
                }

                if (!lowLoad && Options.UsePets.GetBool() && inTask && (!LastUpdate.TryGetValue(playerId, out long lastPetNotify) || lastPetNotify < now))
                {
                    if (Main.AbilityCD.TryGetValue(playerId, out (long StartTimeStamp, int TotalCooldown) timer))
                    {
                        if (timer.StartTimeStamp + timer.TotalCooldown < now || !alive)
                            player.RemoveAbilityCD();

                        if (!player.IsModdedClient() && timer.TotalCooldown - (now - timer.StartTimeStamp) <= 60)
                            NotifyRoles(SpecifySeer: player, SpecifyTarget: player);

                        LastUpdate[playerId] = now;
                    }
                }

                if (!lowLoad) Randomizer.OnFixedUpdateForPlayers(player);

                RoleBlockManager.OnFixedUpdate(player);
            }
        }

        if (!lowLoad)
        {
            // Ability Use Gain every 5 seconds
            if (inTask && alive && Main.PlayerStates.TryGetValue(playerId, out PlayerState state) && state.TaskState.IsTaskFinished && (!LastAddAbilityTime.TryGetValue(playerId, out long ts) || ts + 5 < now))
            {
                LastAddAbilityTime[playerId] = now;
                AddExtraAbilityUsesOnFinishedTasks(player);
            }

            if (AmongUsClient.Instance.AmHost && inTask && alive && Options.LadderDeath.GetBool())
                FallFromLadder.FixedUpdate(player);

            if (localPlayer && GameStates.IsInGame)
                LoversSuicide();

            if (inTask && localPlayer && Options.DisableDevices.GetBool())
                DisableDevice.FixedUpdate();

            if (localPlayer && GameStates.IsInGame && Main.RefixCooldownDelay <= 0)
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    if (pc.Is(CustomRoles.Vampire) || pc.Is(CustomRoles.Warlock) || pc.Is(CustomRoles.Assassin) || pc.Is(CustomRoles.Undertaker) || pc.Is(CustomRoles.Poisoner))
                        Main.AllPlayerKillCooldown[pc.PlayerId] = Options.AdjustedDefaultKillCooldown * 2;
                }
            }

            if (!Main.DoBlockNameChange && ApplySuffix(__instance, out var name))
                player.RpcSetName(name);
        }

        if (GameStates.IsEnded || !Main.IntroDestroyed || GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks)
        {
            RoleTextTMPs.Clear();
            return;
        }

        if (!RoleTextTMPs.TryGetValue(__instance.PlayerId, out TextMeshPro roleText) || roleText == null)
        {
            Transform roleTextTransform = __instance.cosmetics.nameText.transform.Find("RoleText");
            RoleTextTMPs[__instance.PlayerId] = roleText = roleTextTransform.GetComponent<TextMeshPro>();
        }

        bool self = lpId == __instance.PlayerId;

        bool shouldUpdateRegardlessOfLowLoad = self && GameStates.InGame && PlayerControl.LocalPlayer.IsAlive() && ((PlayerControl.AllPlayerControls.Count > 30 && LastSelfNameUpdateTS != now && Options.CurrentGameMode is CustomGameMode.MoveAndStop or CustomGameMode.HotPotato or CustomGameMode.Speedrun or CustomGameMode.RoomRush or CustomGameMode.KingOfTheZones or CustomGameMode.Quiz) || DirtyName.Remove(lpId));

        if (roleText == null || __instance == null || (lowLoad && !shouldUpdateRegardlessOfLowLoad)) return;

        if (self) LastSelfNameUpdateTS = now;

        if (GameStates.IsLobby)
        {
            if (!__instance.IsHost())
            {
                if (Main.PlayerVersion.TryGetValue(playerId, out PlayerVersion ver))
                {
                    if (Main.ForkId != ver.forkId)
                        __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.4>{ver.forkId}</size>\n{__instance.name}</color>";
                    else if (Main.Version.CompareTo(ver.version) == 0)
                        __instance.cosmetics.nameText.text = ver.tag == $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})" ? Main.ShowModdedClientText.Value ? $"<color=#00a5ff><size=1.4>{GetString("ModdedClient")}</size>\n{__instance.name}</color>" : $"<color=#00a5ff>{__instance.name}</color>" : $"<color=#ffff00><size=1.4>{ver.tag}</size>\n{__instance.name}</color>";
                    else
                        __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.4>v{ver.version}</size>\n{__instance.name}</color>";
                }
                else
                    __instance.cosmetics.nameText.text = Main.ShowPlayerInfoInLobby.Value && !__instance.AmOwner ? $"<#888888><size=1.2>{__instance.GetClient().PlatformData.Platform} | {__instance.FriendCode} | {__instance.GetClient().GetHashedPuid()}</size></color>\n{__instance.Data?.PlayerName}" : __instance.Data?.PlayerName;
            }
        }

        if (GameStates.IsInGame)
        {
            if (!AmongUsClient.Instance.AmHost && Options.CurrentGameMode != CustomGameMode.Standard)
            {
                roleText.text = string.Empty;
                roleText.enabled = false;
                return;
            }

            bool shouldSeeTargetAddons = playerId == lpId || new[] { PlayerControl.LocalPlayer, player }.All(x => x.Is(Team.Impostor));

            (string, Color) roleTextData = GetRoleText(lpId, playerId, seeTargetBetrayalAddons: shouldSeeTargetAddons);

            roleText.text = roleTextData.Item1;
            roleText.color = roleTextData.Item2;

            if (Options.CurrentGameMode is not CustomGameMode.Standard and not CustomGameMode.HideAndSeek) roleText.text = string.Empty;

            roleText.enabled = IsRoleTextEnabled(__instance);

            if (PlayerControl.LocalPlayer.IsAlive() && PlayerControl.LocalPlayer.IsRevealedPlayer(__instance) && __instance.Is(CustomRoles.Trickster))
            {
                roleText.text = Farseer.RandomRole[lpId];
                roleText.text += Farseer.GetTaskState();
            }

            if (!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay)
            {
                roleText.enabled = false;
                if (!__instance.AmOwner) __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
            }

            var isProgressTextLong = false;
            string progressText = GetProgressText(__instance);

            if (progressText.RemoveHtmlTags().Length > 25 && Main.VisibleTasksCount)
            {
                isProgressTextLong = true;
                progressText = $"\n{progressText}";
            }

            bool moveandstop = Options.CurrentGameMode == CustomGameMode.MoveAndStop;

            if (Main.VisibleTasksCount)
            {
                if (moveandstop) roleText.text = roleText.text.Insert(0, progressText);
                else roleText.text += progressText;
            }

            PlayerControl seer = PlayerControl.LocalPlayer;
            PlayerControl target = __instance;

            if (target.Is(CustomRoles.Car))
            {
                target.cosmetics.nameText.text = Car.Name;
                roleText.enabled = false;
                return;
            }

            Mark.Clear();
            Suffix.Clear();

            string realName = target.GetRealName();

            if (target.Is(CustomRoles.BananaMan))
                realName = realName.Insert(0, $"{GetString("Prefix.BananaMan")} ");

            if (target.AmOwner && inTask)
            {
                if (target.Is(CustomRoles.Arsonist) && target.IsDouseDone())
                    realName = ColorString(GetRoleColor(CustomRoles.Arsonist), GetString("EnterVentToWin"));
                else if (target.Is(CustomRoles.Revolutionist) && target.IsDrawDone()) realName = ColorString(GetRoleColor(CustomRoles.Revolutionist), string.Format(GetString("EnterVentWinCountDown"), Revolutionist.RevolutionistCountdown.GetValueOrDefault(lpId, 10)));

                if (Pelican.IsEaten(lpId)) realName = ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"));

                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.SoloKombat:
                        SoloPVP.GetNameNotify(target, ref realName);
                        break;
                    case CustomGameMode.BedWars when self:
                    case CustomGameMode.Quiz when self:
                        realName = string.Empty;
                        break;
                }

                if (Deathpact.IsInActiveDeathpact(seer)) realName = Deathpact.GetDeathpactString(seer);

                if (Options.CurrentGameMode != CustomGameMode.BedWars && NameNotifyManager.GetNameNotify(target, out string name) && name.Length > 0)
                    realName = name;
            }

            // Name Color Manager
            realName = realName.ApplyNameColorData(seer, target, false);

            Suffix.Append(BuildSuffix(seer, target, meeting: GameStates.IsMeeting));

            List<string> additionalSuffixes = [];

            if (self) additionalSuffixes.Add(CustomTeamManager.GetSuffix(seer));

            additionalSuffixes.Add(AFKDetector.GetSuffix(seer, target));

            switch (target.GetCustomRole())
            {
                case CustomRoles.Snitch when seer.IsImpostor() && target.Is(CustomRoles.Madmate) && target.GetTaskState().IsTaskFinished:
                    Mark.Append(ColorString(GetRoleColor(CustomRoles.Impostor), "★"));
                    break;
                case CustomRoles.Marshall when Marshall.CanSeeMarshall(seer) && target.GetTaskState().IsTaskFinished:
                    Mark.Append(ColorString(GetRoleColor(CustomRoles.Marshall), "★"));
                    break;
                case CustomRoles.SuperStar when Options.EveryOneKnowSuperStar.GetBool():
                    Mark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));
                    break;
                case CustomRoles.PlagueDoctor:
                    Mark.Append(PlagueDoctor.GetMarkOthers(seer, target));
                    break;
            }

            switch (seer.GetCustomRole())
            {
                case CustomRoles.Lookout when seer.IsAlive() && target.IsAlive():
                    Mark.Append(ColorString(GetRoleColor(CustomRoles.Lookout), " " + target.PlayerId) + " ");
                    break;
                case CustomRoles.PlagueBearer when PlagueBearer.IsPlagued(lpId, target.PlayerId):
                    Mark.Append($"<color={GetRoleColorCode(CustomRoles.PlagueBearer)}>●</color>");
                    break;
                case CustomRoles.Arsonist:
                    if (seer.IsDousedPlayer(target))
                        Mark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>▲</color>");
                    else if (
                            Arsonist.CurrentDousingTarget != byte.MaxValue &&
                            Arsonist.CurrentDousingTarget == target.PlayerId
                        )
                        Mark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>△</color>");

                    break;
                case CustomRoles.Revolutionist:
                    if (seer.IsDrawPlayer(target))
                        Mark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>●</color>");
                    else if (
                            Revolutionist.CurrentDrawTarget != byte.MaxValue &&
                            Revolutionist.CurrentDrawTarget == target.PlayerId
                        )
                        Mark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>○</color>");

                    break;
                case CustomRoles.Farseer:
                    if (Revolutionist.CurrentDrawTarget != byte.MaxValue &&
                        Revolutionist.CurrentDrawTarget == target.PlayerId)
                        Mark.Append($"<color={GetRoleColorCode(CustomRoles.Farseer)}>○</color>");

                    break;
                case CustomRoles.Analyst when (Main.PlayerStates[lpId].Role as Analyst).CurrentTarget.ID == target.PlayerId:
                    Mark.Append($"<color={GetRoleColorCode(CustomRoles.Analyst)}>○</color>");
                    break;
                case CustomRoles.Samurai when (Main.PlayerStates[lpId].Role as Samurai).Target.Id == target.PlayerId:
                    Mark.Append($"<color={GetRoleColorCode(CustomRoles.Samurai)}>○</color>");
                    break;
                case CustomRoles.Puppeteer when Puppeteer.PuppeteerList.ContainsValue(lpId) && Puppeteer.PuppeteerList.ContainsKey(target.PlayerId):
                    Mark.Append($"<color={GetRoleColorCode(CustomRoles.Impostor)}>◆</color>");
                    break;
                case CustomRoles.EvilTracker:
                    Mark.Append(EvilTracker.GetTargetMark(seer, target));
                    break;
                case CustomRoles.Scout:
                    Mark.Append(Scout.GetTargetMark(seer, target));
                    break;
                case CustomRoles.Monitor when inTask && self:
                    if (AntiAdminer.IsAdminWatch) additionalSuffixes.Add($"{GetString("AntiAdminerAD")} <size=70%>({AntiAdminer.PlayersNearDevices.Where(x => x.Value.Contains(AntiAdminer.Device.Admin)).Select(x => x.Key.ColoredPlayerName()).Join()})</size>");
                    if (AntiAdminer.IsVitalWatch) additionalSuffixes.Add($"{GetString("AntiAdminerVI")} <size=70%>({AntiAdminer.PlayersNearDevices.Where(x => x.Value.Contains(AntiAdminer.Device.Vitals)).Select(x => x.Key.ColoredPlayerName()).Join()})</size>");
                    if (AntiAdminer.IsDoorLogWatch) additionalSuffixes.Add($"{GetString("AntiAdminerDL")} <size=70%>({AntiAdminer.PlayersNearDevices.Where(x => x.Value.Contains(AntiAdminer.Device.DoorLog)).Select(x => x.Key.ColoredPlayerName()).Join()})</size>");
                    if (AntiAdminer.IsCameraWatch) additionalSuffixes.Add($"{GetString("AntiAdminerCA")} <size=70%>({AntiAdminer.PlayersNearDevices.Where(x => x.Value.Contains(AntiAdminer.Device.Camera)).Select(x => x.Key.ColoredPlayerName()).Join()})</size>");
                    break;
                case CustomRoles.AntiAdminer when inTask && self:
                    if (AntiAdminer.IsAdminWatch) additionalSuffixes.Add(GetString("AntiAdminerAD"));
                    if (AntiAdminer.IsVitalWatch) additionalSuffixes.Add(GetString("AntiAdminerVI"));
                    if (AntiAdminer.IsDoorLogWatch) additionalSuffixes.Add(GetString("AntiAdminerDL"));
                    if (AntiAdminer.IsCameraWatch) additionalSuffixes.Add(GetString("AntiAdminerCA"));
                    break;
                case CustomRoles.Executioner:
                    Mark.Append(Executioner.TargetMark(seer, target));
                    break;
                case CustomRoles.Gamer:
                    Mark.Append(Gamer.TargetMark(seer, target));
                    break;
            }

            Mark.Append(Totocalcio.TargetMark(seer, target));
            Mark.Append(Romantic.TargetMark(seer, target));
            Mark.Append(Lawyer.LawyerMark(seer, target));
            Mark.Append(Marshall.GetWarningMark(seer, target));

            if (Randomizer.IsShielded(target)) Mark.Append(ColorString(GetRoleColor(CustomRoles.Randomizer), "✚"));

            if (target.AmOwner) Mark.Append(Sniper.GetShotNotify(target.PlayerId));

            if (BallLightning.IsGhost(target)) Mark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));

            Mark.Append(Medic.GetMark(seer, target));
            Mark.Append(Gaslighter.GetMark(seer, target));
            Mark.Append(Snitch.GetWarningArrow(seer, target));
            Mark.Append(Snitch.GetWarningMark(seer, target));
            Mark.Append(Deathpact.GetDeathpactMark(seer, target));

            Main.LoversPlayers.ToArray().DoIf(x => x == null, x => Main.LoversPlayers.Remove(x));
            if (!Main.HasJustStarted) Main.LoversPlayers.DoIf(x => !x.Is(CustomRoles.Lovers), x => x.RpcSetCustomRole(CustomRoles.Lovers));

            if (Main.LoversPlayers.Exists(x => x.PlayerId == target.PlayerId) && (Main.LoversPlayers.Exists(x => x.PlayerId == lpId) || !seer.IsAlive()))
                Mark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}> ♥</color>");

            if (self)
            {
                additionalSuffixes.Add(Bloodmoon.GetSuffix(seer));
                additionalSuffixes.Add(Haunter.GetSuffix(seer));
                if (seer.Is(CustomRoles.Asthmatic)) additionalSuffixes.Add(Asthmatic.GetSuffixText(lpId));
                if (seer.Is(CustomRoles.Sonar)) additionalSuffixes.Add(Sonar.GetSuffix(seer, GameStates.IsMeeting));
                if (seer.Is(CustomRoles.Deadlined)) additionalSuffixes.Add(Deadlined.GetSuffix(seer));
                if (seer.Is(CustomRoles.Allergic)) additionalSuffixes.Add(Allergic.GetSelfSuffix(seer));
            }

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloKombat:
                    additionalSuffixes.Add(SoloPVP.GetDisplayHealth(target, self));
                    break;
                case CustomGameMode.FFA:
                    additionalSuffixes.Add(FreeForAll.GetPlayerArrow(seer, target));
                    break;
                case CustomGameMode.MoveAndStop when self:
                    additionalSuffixes.Add(MoveAndStop.GetSuffixText(seer));
                    break;
                case CustomGameMode.Speedrun when self:
                    additionalSuffixes.Add(Speedrun.GetSuffixText(seer));
                    break;
                case CustomGameMode.HideAndSeek:
                    additionalSuffixes.Add(CustomHnS.GetSuffixText(seer, target));
                    break;
                case CustomGameMode.CaptureTheFlag:
                    additionalSuffixes.Add(CaptureTheFlag.GetSuffixText(seer, target));
                    break;
                case CustomGameMode.RoomRush when self:
                    additionalSuffixes.Add(RoomRush.GetSuffix(seer));
                    break;
                case CustomGameMode.KingOfTheZones when self:
                    additionalSuffixes.Add(KingOfTheZones.GetSuffix(seer));
                    break;
                case CustomGameMode.Quiz when self:
                    additionalSuffixes.Add(Quiz.GetSuffix(seer));
                    break;
                case CustomGameMode.TheMindGame:
                    additionalSuffixes.Add(TheMindGame.GetSuffix(seer, target));
                    break;
                case CustomGameMode.BedWars:
                    additionalSuffixes.Add(BedWars.GetSuffix(seer, target));
                    break;
            }

            if (MeetingStates.FirstMeeting && Main.ShieldPlayer == target.FriendCode && !string.IsNullOrEmpty(target.FriendCode) && !self && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.SoloKombat or CustomGameMode.FFA)
                additionalSuffixes.Add(GetString("DiedR1Warning"));

            Suffix.Append(string.Join('\n', additionalSuffixes.ConvertAll(x => x.Trim()).FindAll(x => !string.IsNullOrEmpty(x))));

            if (self && GameStartTimeStamp + 44 > TimeStamp && Main.HasPlayedGM.TryGetValue(Options.CurrentGameMode, out HashSet<string> playedFCs) && !playedFCs.Contains(seer.FriendCode))
                Suffix.Append($"\n\n<#ffffff>{GetString($"GameModeTutorial.{Options.CurrentGameMode}")}</color>\n");

            // Devourer
            if (Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Devourer { IsEnable: true } dv && dv.PlayerSkinsCosumed.Contains(target.PlayerId)))
                realName = GetString("DevouredName");

            // Camouflage
            if (Camouflage.IsCamouflage) realName = $"<size=0>{realName}</size> ";

            if (!self && Options.CurrentGameMode == CustomGameMode.KingOfTheZones && Main.IntroDestroyed && !KingOfTheZones.GameGoing)
                realName = EmptyMessage;

            string newLineBeforeSuffix = !(Options.CurrentGameMode == CustomGameMode.BedWars && !self && GameStates.InGame) ? "\r\n" : " - ";
            string deathReason = !seer.IsAlive() && seer.KnowDeathReason(target) ? $"{newLineBeforeSuffix}<size=1.5>『{ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))}』</size>" : string.Empty;
            
            string currentText = target.cosmetics.nameText.text;
            var changeTo = $"{realName}{deathReason}{Mark}{newLineBeforeSuffix}{Suffix}";
            bool needUpdate = currentText != changeTo;

            if (needUpdate)
            {
                target.cosmetics.nameText.text = changeTo;

                var offset = 0.2f;

                if (self && NameNotifyManager.GetNameNotify(seer, out string notify) && notify.Contains('\n'))
                {
                    int count = notify.Count(x => x == '\n');
                    for (var i = 0; i < count; i++) offset += 0.15f;
                }

                if (Suffix.ToString() != string.Empty)
                {
                    offset += moveandstop ? 0.15f : 0.1f;
                    offset += moveandstop ? 0f : Suffix.ToString().Count(x => x == '\n') * 0.15f;
                }

                if (!seer.IsAlive())
                    offset += 0.1f;

                if (isProgressTextLong)
                    offset += 0.3f;

                if (moveandstop)
                    offset += 0.3f;

                if (Suffix.ToString().Contains(GetString("MoveAndStop_Tutorial")))
                    offset += 0.8f;

                if (Options.LargerRoleTextSize.GetBool())
                    offset += 0.15f;

                roleText.transform.SetLocalY(offset);
                target.cosmetics.colorBlindText.transform.SetLocalY(-(offset + 0.2f));
            }
        }
        else
        {
            // Restoring the position text coordinates to their initial values
            roleText.transform.SetLocalY(0.2f);
        }
    }

    public static void AddExtraAbilityUsesOnFinishedTasks(PlayerControl player)
    {
        if (Main.HasJustStarted || !player.IsAlive()) return;

        if (Main.PlayerStates[player.PlayerId].Role is SabotageMaster sm)
        {
            sm.UsedSkillCount -= SabotageMaster.AbilityChargesWhenFinishedTasks.GetFloat();
            sm.SendRPC();
        }
        else
        {
            float add = GetSettingNameAndValueForRole(player.GetCustomRole(), "AbilityChargesWhenFinishedTasks");

            if (Math.Abs(add - float.MaxValue) > 0.5f && add > 0)
            {
                if (player.Is(CustomRoles.Bloodlust)) add *= 5;

                player.RpcIncreaseAbilityUseLimitBy(add);
            }
        }
    }

    public static void LoversSuicide(byte deathId = 0x7f, bool exile = false, bool force = false, bool guess = false)
    {
        if (Options.CurrentGameMode != CustomGameMode.Standard) return;
        if (Lovers.LoverDieConsequence.GetValue() == 0 || Main.IsLoversDead || (Main.LoversPlayers.FindAll(x => x.IsAlive()).Count != 1 && !force)) return;

        PlayerControl partnerPlayer = Main.LoversPlayers.FirstOrDefault(player => player.PlayerId != deathId && player.IsAlive());
        if (partnerPlayer == null) return;

        Main.IsLoversDead = true;

        if (Lovers.LoverDieConsequence.GetValue() == 2)
        {
            partnerPlayer.MarkDirtySettings();
            PlayerGameOptionsSender.SetDirty(deathId);
            return;
        }

        if (Lovers.LoverSuicideTime.GetValue() != 0 && !exile && !guess) return;

        if (exile)
            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.FollowingSuicide, partnerPlayer.PlayerId);
        else
            partnerPlayer.Suicide(PlayerState.DeathReason.FollowingSuicide);
    }
}

[HarmonyPatch(typeof(PlayerControl._Start_d__82), nameof(PlayerControl._Start_d__82.MoveNext))]
internal static class PlayerStartPatch
{
    public static void Postfix(PlayerControl._Start_d__82 __instance, ref bool __result)
    {
        try
        {
            if (__result || __instance == null || __instance.__4__this == null || __instance.__4__this.PlayerId >= 254 || __instance.__4__this.cosmetics == null) return;
            TextMeshPro nameText = __instance.__4__this.cosmetics.nameText;
            TextMeshPro roleText = Object.Instantiate(nameText, nameText.transform, true);
            bool largerFontSize = Options.LargerRoleTextSize.GetBool();
            roleText.transform.localPosition = new(0f, largerFontSize ? 0.4f : 0.2f, 0f);
            if (!largerFontSize) roleText.fontSize -= 0.9f;
            roleText.text = "RoleText";
            roleText.gameObject.name = "RoleText";
            roleText.enabled = false;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.ExitVent))]
internal static class ExitVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {
        Logger.Info($" {pc.GetNameWithRole()}, Vent ID: {__instance.Id} ({__instance.name})", "ExitVent");

        if (pc.IsLocalPlayer()) LateTask.New(() => HudManager.Instance.SetHudActive(pc, pc.Data.Role, true), 0.1f, log: false);

        if (!AmongUsClient.Instance.AmHost) return;

        if (Main.KillTimers.ContainsKey(pc.PlayerId))
            Main.KillTimers[pc.PlayerId] += 0.5f;

        Drainer.OnAnyoneExitVent(pc);

        Main.PlayerStates[pc.PlayerId].Role.OnExitVent(pc, __instance);

        if (Options.WhackAMole.GetBool()) LateTask.New(() => pc.TPToRandomVent(), 0.5f, "Whack-A-Mole TP");

        if (!pc.IsModdedClient() && pc.Is(CustomRoles.Haste)) pc.SetKillCooldown(Math.Max(Main.KillTimers[pc.PlayerId], 0.1f));
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
internal static class EnterVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {
        Logger.Info($" {pc.GetNameWithRole()}, Vent ID: {__instance.Id} ({__instance.name})", "EnterVent");

        if (pc.IsLocalPlayer()) LateTask.New(() => HudManager.Instance.SetHudActive(pc, pc.Data.Role, true), 0.1f, log: false);

        if (AmongUsClient.Instance.AmHost && !pc.CanUseVent(__instance.Id) && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.HideAndSeek && !pc.Is(CustomRoles.Nimble) && !pc.Is(CustomRoles.Bloodlust))
        {
            pc.MyPhysics?.RpcBootFromVent(__instance.Id);
            return;
        }

        Drainer.OnAnyoneEnterVent(pc, __instance);
        Analyst.OnAnyoneEnterVent(pc);
        Crewmate.Sentry.OnAnyoneEnterVent(pc);

        switch (pc.GetCustomRole())
        {
            case CustomRoles.Mayor when !Options.UsePets.GetBool() && Mayor.MayorUsedButtonCount.TryGetValue(pc.PlayerId, out int count2) && count2 < Mayor.MayorNumOfUseButton.GetInt():
                if (AmongUsClient.Instance.AmHost) pc.MyPhysics?.RpcBootFromVent(__instance.Id);
                pc.ReportDeadBody(null);
                break;
        }

        if (!AmongUsClient.Instance.AmHost) return;

        if (Main.KillTimers.TryGetValue(pc.PlayerId, out var killTimer) && killTimer > 0f)
            Main.KillTimers[pc.PlayerId] += 0.5f;

        CheckInvalidMovementPatch.ExemptedPlayers.Add(pc.PlayerId);

        Main.LastEnteredVent[pc.PlayerId] = __instance;
        Main.LastEnteredVentLocation[pc.PlayerId] = pc.Pos();

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.FFA when FreeForAll.FFADisableVentingWhenTwoPlayersAlive.GetBool() && Main.AllAlivePlayerControls.Length <= 2:
                pc.Notify(GetString("FFA-NoVentingBecauseTwoPlayers"), 7f);
                pc.MyPhysics?.RpcBootFromVent(__instance.Id);
                break;
            case CustomGameMode.FFA when FreeForAll.FFADisableVentingWhenKcdIsUp.GetBool() && Main.KillTimers[pc.PlayerId] <= 0:
                pc.Notify(GetString("FFA-NoVentingBecauseKCDIsUP"), 7f);
                pc.MyPhysics?.RpcExitVent(__instance.Id);
                break;
            case CustomGameMode.HotPotato:
            case CustomGameMode.Speedrun:
            case CustomGameMode.CaptureTheFlag:
            case CustomGameMode.NaturalDisasters:
            case CustomGameMode.KingOfTheZones:
            case CustomGameMode.TheMindGame:
            case CustomGameMode.Quiz:
            case CustomGameMode.SoloKombat when !SoloPVP.CanVent:
                pc.MyPhysics?.RpcBootFromVent(__instance.Id);
                break;
        }

        if (pc.IsRoleBlocked())
        {
            pc.Notify(BlockedAction.Vent.GetBlockNotify());
            pc.MyPhysics?.RpcExitVent(__instance.Id);
            return;
        }

        if (!Rhapsode.CheckAbilityUse(pc) || Stasis.IsTimeFrozen || TimeMaster.Rewinding)
        {
            pc.MyPhysics?.RpcExitVent(__instance.Id);
            return;
        }

        if (Penguin.IsVictim(pc))
        {
            pc.MyPhysics?.RpcBootFromVent(__instance.Id);
            return;
        }

        if (SoulHunter.IsSoulHunterTarget(pc.PlayerId))
        {
            pc.Notify(GetString("SoulHunterTargetNotifyNoVent"));
            pc.MyPhysics?.RpcBootFromVent(__instance.Id);
            return;
        }

        if (Ventguard.BlockedVents.Contains(__instance.Id) && (!Ventguard.VentguardBlockDoesNotAffectCrew.GetBool() || !pc.IsCrewmate()))
        {
            pc.Notify(GetString("EnteredBlockedVent"));
            pc.MyPhysics?.RpcExitVent(__instance.Id);

            if (Ventguard.VentguardNotifyOnBlockedVentUse.GetBool())
            {
                foreach (PlayerControl ventguard in Main.AllAlivePlayerControls)
                {
                    if (ventguard.Is(CustomRoles.Ventguard))
                        ventguard.Notify(GetString("VentguardNotify"));
                }
            }

            return;
        }

        if (pc.Is(CustomRoles.Circumvent))
            Circumvent.OnEnterVent(pc, __instance);

        if (!pc.CanUseVent(__instance.Id) && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.HideAndSeek && !pc.Is(CustomRoles.Nimble) && !pc.Is(CustomRoles.Bloodlust))
            pc.MyPhysics?.RpcExitVent(__instance.Id);

        if (pc.Is(CustomRoles.Unlucky))
        {
            if (IRandom.Instance.Next(0, 100) < Options.UnluckyVentSuicideChance.GetInt())
                pc.Suicide();
        }

        if (pc.Is(CustomRoles.Damocles)) Damocles.OnEnterVent(pc.PlayerId, __instance.Id);

        Chef.SpitOutFood(pc);

        Main.PlayerStates[pc.PlayerId].Role.OnEnterVent(pc, __instance);

        if (pc.IsLocalPlayer())
            Statistics.VentTimes++;
    }
}

[HarmonyPatch(typeof(GameData), nameof(GameData.CompleteTask))]
internal static class GameDataCompleteTaskPatch
{
    public static void Postfix(PlayerControl pc, uint taskId)
    {
        if (GameStates.IsMeeting) return;

        if (Options.CurrentGameMode == CustomGameMode.HideAndSeek && CustomHnS.PlayerRoles[pc.PlayerId].Interface.Team == Team.Crewmate && pc.IsAlive())
        {
            var task = pc.myTasks.Find((Il2CppSystem.Predicate<PlayerTask>)(x => taskId == x.Id));
            Hider.OnSpecificTaskComplete(pc, task);
        }

        Logger.Info($"TaskComplete: {pc.GetNameWithRole().RemoveHtmlTags()}", "CompleteTask");
        Main.PlayerStates[pc.PlayerId].UpdateTask(pc);
        NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
internal static class PlayerControlCompleteTaskPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        if (GameStates.IsMeeting) return false;

        return !Workhorse.OnCompleteTask(__instance) && Capitalism.AddTaskForPlayer(__instance); // Cancel task win
    }

    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] uint idx)
    {
        if (GameStates.IsMeeting || __instance == null || !__instance.IsAlive()) return;

        Snitch.OnCompleteTask(__instance);

        try
        {
            var task = __instance.myTasks.Find((Il2CppSystem.Predicate<PlayerTask>)(x => idx == x.Id));
            Benefactor.OnTaskComplete(__instance, task);
        }
        catch (Exception e) { ThrowException(e); }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
public static class PlayerControlDiePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (AmongUsClient.Instance.AmHost) PetsHelper.RpcRemovePet(__instance);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckSporeTrigger))]
public static class PlayerControlCheckSporeTriggerPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] Mushroom mushroom)
    {
        Logger.Info($"{__instance.GetNameWithRole()}, mushroom: {mushroom.name} / {mushroom.Id}, at {mushroom.origPosition}", "Spore Trigger");
        return !AmongUsClient.Instance.AmHost || !Options.DisableSporeTriggerOnFungle.GetBool();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckUseZipline))]
public static class PlayerControlCheckUseZiplinePatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, [HarmonyArgument(1)] ZiplineBehaviour ziplineBehaviour, [HarmonyArgument(2)] bool fromTop)
    {
        ziplineBehaviour.downTravelTime = Options.ZiplineTravelTimeFromTop.GetFloat();
        ziplineBehaviour.upTravelTime = Options.ZiplineTravelTimeFromBottom.GetFloat();

        Logger.Info($"{__instance.GetNameWithRole()}, target: {target.GetNameWithRole()}, {(fromTop ? $"from Top, travel time: {ziplineBehaviour.downTravelTime}s" : $"from Bottom, travel time: {ziplineBehaviour.upTravelTime}s")}", "Zipline Use");

        if (AmongUsClient.Instance.AmHost)
        {
            if (Options.DisableZiplineFromTop.GetBool() && fromTop) return false;
            if (Options.DisableZiplineFromUnder.GetBool() && !fromTop) return false;

            if (__instance.Is(CustomRoleTypes.Coven) && Options.DisableZiplineForCoven.GetBool()) return false;
            if (__instance.IsImpostor() && Options.DisableZiplineForImps.GetBool()) return false;
            if (__instance.GetCustomRole().IsNeutral() && Options.DisableZiplineForNeutrals.GetBool()) return false;
            if (__instance.IsCrewmate() && Options.DisableZiplineForCrew.GetBool()) return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
internal static class PlayerControlProtectPlayerPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "ProtectPlayer");
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RemoveProtection))]
internal static class PlayerControlRemoveProtectionPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()}", "RemoveProtection");
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
internal static class PlayerControlSetRolePatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] ref RoleTypes roleType, [HarmonyArgument(1)] ref bool canOverrideRole)
    {
        canOverrideRole = true;

        // Skip after first assign
        if (StartGameHostPatch.RpcSetRoleReplacer.BlockSetRole) return true;

        string targetName = __instance.GetNameWithRole();
        Logger.Info($"{targetName} => {roleType}", "PlayerControl.RpcSetRole");
        if (!ShipStatus.Instance.enabled) return true;

        if (roleType is RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost)
        {
            bool targetIsKiller = __instance.Is(CustomRoleTypes.Impostor) || __instance.HasDesyncRole();
            Dictionary<PlayerControl, RoleTypes> ghostRoles = new();

            foreach (PlayerControl seer in Main.AllPlayerControls)
            {
                bool self = seer.PlayerId == __instance.PlayerId;
                bool seerIsKiller = seer.Is(CustomRoleTypes.Impostor) || seer.HasDesyncRole();

                if (__instance.HasGhostRole() || GhostRolesManager.ShouldHaveGhostRole(__instance))
                    ghostRoles[seer] = RoleTypes.GuardianAngel;
                else if ((self && targetIsKiller) || (!seerIsKiller && __instance.Is(CustomRoleTypes.Impostor)))
                    ghostRoles[seer] = RoleTypes.ImpostorGhost;
                else
                    ghostRoles[seer] = RoleTypes.CrewmateGhost;
            }

            if (__instance.HasGhostRole() || GhostRolesManager.ShouldHaveGhostRole(__instance))
                roleType = RoleTypes.GuardianAngel;
            else if (ghostRoles.All(kvp => kvp.Value == RoleTypes.CrewmateGhost))
                roleType = RoleTypes.CrewmateGhost;
            else if (ghostRoles.All(kvp => kvp.Value == RoleTypes.ImpostorGhost))
                roleType = RoleTypes.ImpostorGhost;
            else
            {
                foreach ((PlayerControl seer, RoleTypes role) in ghostRoles)
                {
                    Logger.Info($"Desync {targetName} => {role} for {seer.GetNameWithRole().RemoveHtmlTags()}", "PlayerControl.RpcSetRole");
                    __instance.RpcSetRoleDesync(role, seer.OwnerId);
                }

                return false;
            }
        }

        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CoSetRole))]
internal static class PlayerControlLocalSetRolePatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] RoleTypes role)
    {
        if (!AmongUsClient.Instance.AmHost && !GameStates.IsModHost)
        {
            CustomRoles moddedRole = role switch
            {
                RoleTypes.Impostor => CustomRoles.ImpostorEHR,
                RoleTypes.Phantom => CustomRoles.PhantomEHR,
                RoleTypes.Shapeshifter => CustomRoles.ShapeshifterEHR,
                RoleTypes.Crewmate => CustomRoles.CrewmateEHR,
                RoleTypes.Engineer => CustomRoles.EngineerEHR,
                RoleTypes.Noisemaker => CustomRoles.NoisemakerEHR,
                RoleTypes.Scientist => CustomRoles.ScientistEHR,
                RoleTypes.Tracker => CustomRoles.TrackerEHR,
                _ => CustomRoles.NotAssigned
            };

            if (moddedRole != CustomRoles.NotAssigned) Main.PlayerStates[__instance.PlayerId].SetMainRole(moddedRole);
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.AssertWithTimeout))]
internal static class AssertWithTimeoutPatch
{
    public static bool Prefix()
    {
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckName))]
internal static class CmdCheckNameVersionCheckPatch
{
    public static void Postfix()
    {
        RPC.RpcVersionCheck();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MixUpOutfit))]
public static class PlayerControlMixupOutfitPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!__instance.IsAlive()) return;

        if (PlayerControl.LocalPlayer.Data.Role.IsImpostor && // Has Impostor role behavior
            !PlayerControl.LocalPlayer.Is(Team.Impostor) && // Not an actual Impostor
            PlayerControl.LocalPlayer.GetCustomRole().GetDYRole() is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom) // Has Desynced Impostor role
            __instance.cosmetics.ToggleNameVisible(false);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixMixedUpOutfit))]
public static class PlayerControlFixMixedUpOutfitPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!__instance.IsAlive()) return;

        __instance.cosmetics.ToggleNameVisible(true);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ShouldProcessRpc))]
internal static class ShouldProcessRpcPatch
{
    // Since the stupid AU code added a check for RPC processing for outfit players, we need to patch this
    // Always return true because the check is absolutely pointless
    public static bool Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.RpcBootFromVent))]
internal static class BootFromVentPatch
{
    public static bool Prefix(PlayerPhysics __instance)
    {
        return !GameStates.IsInTask || ExileController.Instance || __instance == null || __instance.myPlayer == null || !__instance.myPlayer.IsAlive() || !__instance.myPlayer.Is(CustomRoles.Nimble);
    }
}