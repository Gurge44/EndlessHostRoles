using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.AddOns.Crewmate;
using EHR.AddOns.GhostRoles;
using EHR.AddOns.Impostor;
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

namespace EHR
{
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
            if (!AmongUsClient.Instance.AmHost) Logger.Error("Client is calling RpcMurderPlayer, are you hacking?", "RpcMurderPlayerPatch.Prefix");

            MurderResultFlags murderResultFlags = didSucceed ? MurderResultFlags.Succeeded : MurderResultFlags.FailedError;
            if (AmongUsClient.Instance.AmClient) __instance.MurderPlayer(target, murderResultFlags);

            MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable);
            messageWriter.WriteNetObject(target);
            messageWriter.Write((int)murderResultFlags);
            AmongUsClient.Instance.FinishRpcImmediately(messageWriter);

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
                MessageWriter writer = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.FFAKill);
                writer.WriteNetObject(__instance);
                writer.WriteNetObject(target);
                writer.EndMessage();
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))] // Vanilla
    internal static class CheckMurderPatch
    {
        public static readonly Dictionary<byte, float> TimeSinceLastKill = [];

        public static void Update()
        {
            int n = HudManager.InstanceExists ? Math.Max(Main.AllPlayerControls.Length, 15) : 15;

            for (byte i = 0; i < n; i++)
            {
                if (TimeSinceLastKill.ContainsKey(i))
                {
                    TimeSinceLastKill[i] += Time.deltaTime;
                    if (15f < TimeSinceLastKill[i]) TimeSinceLastKill.Remove(i);
                }
            }
        }

        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            if (!AmongUsClient.Instance.AmHost) return false;

            PlayerControl killer = __instance;

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

            if (target.Is(CustomRoles.Detour))
            {
                PlayerControl tempTarget = target;
                target = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId && x.PlayerId != killer.PlayerId).MinBy(x => Vector2.Distance(x.Pos(), target.Pos()));
                Logger.Info($"Target was {tempTarget.GetNameWithRole()}, new target is {target.GetNameWithRole()}", "Detour");

                if (PlayerControl.LocalPlayer.PlayerId == target.PlayerId)
                {
                    Detour.TotalRedirections++;
                    if (Detour.TotalRedirections >= 3) Achievements.Type.CantTouchThis.CompleteAfterGameEnd();
                }
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
                if (!Main.KilledDiseased.TryAdd(killer.PlayerId, 1))
                    Main.KilledDiseased[killer.PlayerId] += 1;

            if (target.Is(CustomRoles.Antidote))
                if (!Main.KilledAntidote.TryAdd(killer.PlayerId, 1))
                    Main.KilledAntidote[killer.PlayerId] += 1;

            killer.ResetKillCooldown();

            if (killer.PlayerId != target.PlayerId && !killer.CanUseKillButton())
            {
                Logger.Info(killer.GetNameWithRole().RemoveHtmlTags() + " cannot use their kill button, the kill was blocked", "CheckMurder");
                return false;
            }

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloKombat:
                    SoloKombatManager.OnPlayerAttack(killer, target);
                    return false;
                case CustomGameMode.FFA:
                    FFAManager.OnPlayerAttack(killer, target);
                    return false;
                case CustomGameMode.MoveAndStop:
                case CustomGameMode.HotPotato:
                case CustomGameMode.RoomRush:
                case CustomGameMode.NaturalDisasters:
                    return false;
                case CustomGameMode.Speedrun when !SpeedrunManager.OnCheckMurder(killer, target):
                    return false;
                case CustomGameMode.Speedrun:
                    killer.Kill(target);
                    return false;
                case CustomGameMode.HideAndSeek:
                    HnSManager.OnCheckMurder(killer, target);
                    return false;
                case CustomGameMode.CaptureTheFlag:
                    CTFManager.OnCheckMurder(killer, target);
                    return false;
            }

            Deadlined.SetDone(killer);

            if (ToiletMaster.OnAnyoneCheckMurderStart(killer, target)) return false;

            if (Dad.OnAnyoneCheckMurderStart(target)) return false;

            Simon.RemoveTarget(killer, Simon.Instruction.Kill);

            if (Mastermind.ManipulatedPlayers.ContainsKey(killer.PlayerId)) return Mastermind.ForceKillForManipulatedPlayer(killer, target);

            if (target.Is(CustomRoles.Spy) && !Spy.OnKillAttempt(killer, target)) return false;

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

                bool CheckMurder()
                {
                    return Main.PlayerStates[killer.PlayerId].Role.OnCheckMurder(killer, target);
                }
            }

            if (!killer.RpcCheckAndMurder(target, true)) return false;

            if (killer.Is(CustomRoles.Unlucky))
            {
                var Ue = IRandom.Instance;

                if (Ue.Next(0, 100) < Options.UnluckyKillSuicideChance.GetInt())
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

            if ((Romantic.PartnerId == target.PlayerId && Romantic.IsPartnerProtected) ||
                Medic.OnAnyoneCheckMurder(killer, target) ||
                Randomizer.IsShielded(target) ||
                Aid.ShieldedPlayers.ContainsKey(target.PlayerId) ||
                Gaslighter.IsShielded(target) ||
                !Grappler.OnAnyoneCheckMurder(target) ||
                !Adventurer.OnAnyoneCheckMurder(target) ||
                !Sentinel.OnAnyoneCheckMurder(killer) ||
                !ToiletMaster.OnAnyoneCheckMurder(killer, target))
            {
                Notify("SomeSortOfProtection");
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

            if (Jackal.On && Jackal.ResetKillCooldownWhenSbGetKilled.GetBool() && !killer.Is(CustomRoles.Sidekick) && !target.Is(CustomRoles.Sidekick) && !killer.Is(CustomRoles.Jackal) && !target.Is(CustomRoles.Jackal) && !GameStates.IsMeeting) Jackal.AfterPlayerDiedTask(killer);

            if (target.Is(CustomRoles.Lucky))
            {
                var rd = IRandom.Instance;

                if (rd.Next(0, 100) < Options.LuckyProbability.GetInt())
                {
                    killer.SetKillCooldown(15f);
                    return false;
                }
            }

            if (Crusader.ForCrusade.Contains(target.PlayerId))
            {
                foreach (PlayerControl player in Main.AllPlayerControls)
                {
                    if (player.Is(CustomRoles.Crusader) && player.IsAlive() && !killer.Is(CustomRoles.Pestilence) && !killer.Is(CustomRoles.Minimalism))
                    {
                        player.Kill(killer);
                        Crusader.ForCrusade.Remove(target.PlayerId);
                        killer.RpcGuardAndKill(target);
                        return false;
                    }

                    if (player.Is(CustomRoles.Crusader) && player.IsAlive() && killer.Is(CustomRoles.Pestilence))
                    {
                        killer.Kill(player);
                        Crusader.ForCrusade.Remove(target.PlayerId);
                        target.RpcGuardAndKill(killer);
                        Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.PissedOff;
                        return false;
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
                case CustomRoles.Spiritcaller:
                    if (Spiritcaller.InProtect(target))
                    {
                        killer.RpcGuardAndKill(target);
                        Notify("SomeSortOfProtection");
                        return false;
                    }

                    break;
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
                target.Notify(ColorString(GetRoleColor(CustomRoles.Madmate), GetString("BecomeMadmateCuzMadmateMode")));
                killer.SetKillCooldown();
                killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);
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

            void Notify(string message)
            {
                killer.Notify(ColorString(Color.yellow, GetString("CheckMurderFail") + GetString(message)), 15f);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    internal static class MurderPlayerPatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target /*, [HarmonyArgument(1)] MurderResultFlags resultFlags*/)
        {
            Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}{(target.IsProtected() ? " (Protected)" : string.Empty)}", "MurderPlayer");

            if (GameStates.IsLobby)
            {
                Logger.Info("Murder triggered in lobby, so murder canceled", "MurderPlayer Prefix");
                return false;
            }

            RandomSpawn.CustomNetworkTransformHandleRpcPatch.HasSpawned.Add(__instance.PlayerId);

            if (!target.IsProtected() && !Doppelganger.DoppelVictim.ContainsKey(target.PlayerId) && !Camouflage.ResetSkinAfterDeathPlayers.Contains(target.PlayerId))
            {
                Camouflage.ResetSkinAfterDeathPlayers.Add(target.PlayerId);
                Camouflage.RpcSetSkin(target, true);
            }

            return true;
        }

        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            if (__instance == null || target == null || __instance.PlayerId == 255 || target.PlayerId == 255) return;

            if (target.AmOwner) RemoveDisableDevicesPatch.UpdateDisableDevices();

            if (!target.Data.IsDead || !AmongUsClient.Instance.AmHost) return;

            if (OverKiller.OverDeadPlayerList.Contains(target.PlayerId)) return;

            PlayerControl killer = __instance; // Alternative variable
            if (target.PlayerId == Godfather.GodfatherTarget) killer.RpcSetCustomRole(CustomRoles.Refugee);

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
                case CustomRoles.BallLightning:
                    if (killer != target) BallLightning.MurderPlayer(killer, target);

                    break;
                case CustomRoles.Bane:
                    if (killer != target) Bane.OnKilled(killer);

                    break;
                case CustomRoles.Markseeker:
                    Markseeker.OnDeath(target);
                    break;
            }

            Main.PlayerStates[killer.PlayerId].Role.OnMurder(killer, target);

            Chef.SpitOutFood(killer);

            if (Options.CurrentGameMode == CustomGameMode.Speedrun) SpeedrunManager.ResetTimer(killer);

            if (killer.Is(CustomRoles.TicketsStealer) && killer.PlayerId != target.PlayerId) killer.Notify(string.Format(GetString("TicketsStealerGetTicket"), ((Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == killer.PlayerId) + 1) * Options.TicketsPerKill.GetFloat()).ToString("0.0#####")));

            if (killer.Is(CustomRoles.Pickpocket) && killer.PlayerId != target.PlayerId) killer.Notify(string.Format(GetString("PickpocketGetVote"), ((Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == killer.PlayerId) + 1) * Pickpocket.VotesPerKill.GetFloat()).ToString("0.0#####")));

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

                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} 击杀诱饵 => {target.GetNameWithRole().RemoveHtmlTags()}", "MurderPlayer");

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

            if (Options.LowLoadMode.GetBool())
            {
                __instance.MarkDirtySettings();
                target.MarkDirtySettings();
                NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
                NotifyRoles(SpecifySeer: target);
            }
            else
            {
                SyncAllSettings();
                NotifyRoles(ForceLoop: true);
            }

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
            if (!Main.ProcessShapeshifts) return true;

            if (shapeshifter == null || target == null) return true;

            Logger.Info($"{shapeshifter.GetNameWithRole()} => {target.GetNameWithRole()}", "Shapeshift");

            bool shapeshifting = shapeshifter.PlayerId != target.PlayerId;

            if (AmongUsClient.Instance.AmHost && shapeshifting)
            {
                if (!Rhapsode.CheckAbilityUse(shapeshifter)) return false;

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

            if (!Pelican.IsEaten(shapeshifter.PlayerId) && !GameStates.IsVoting) isSSneeded = Main.PlayerStates[shapeshifter.PlayerId].Role.OnShapeshift(shapeshifter, target, shapeshifting);

            if (shapeshifter.Is(CustomRoles.Hangman) && shapeshifter.GetAbilityUseLimit() < 1 && shapeshifting)
            {
                shapeshifter.SetKillCooldown(Hangman.ShapeshiftDuration.GetFloat() + 1f);
                isSSneeded = false;
            }

            bool ctf = Options.CurrentGameMode == CustomGameMode.CaptureTheFlag;
            if (ctf) CTFManager.TryPickUpFlag(shapeshifter);

            CustomRoles role = shapeshifter.GetCustomRole();

            bool forceCancel = role.ForceCancelShapeshift();
            bool unshiftTrigger = role.SimpleAbilityTrigger() && Options.UseUnshiftTrigger.GetBool() && (!role.IsNeutral() || Options.UseUnshiftTriggerForNKs.GetBool());
            unshiftTrigger |= role.AlwaysUsesUnshift();

            unshiftTrigger |= ctf;
            forceCancel |= unshiftTrigger;
            isSSneeded &= !ctf;

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

            // Forced rewriting in case the name cannot be corrected due to the timing of canceling the transformation being off.
            if (!shapeshifting && !shapeshifter.Is(CustomRoles.Glitch) && isSSneeded) LateTask.New(() => NotifyRoles(NoCache: true), 1.2f, "ShapeShiftNotify");

            if (!(shapeshifting && doSSwithoutAnim) && !isSSneeded && !Swapster.FirstSwapTarget.ContainsKey(shapeshifter.PlayerId)) LateTask.New(shapeshifter.RpcResetAbilityCooldown, 0.01f, log: false);

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

            if (unshiftTrigger)
            {
                shapeshifter.CheckAndSetUnshiftState(force: true);
                shapeshifter.AddAbilityCD();
            }


            bool animated = isSSneeded || (!shouldCancel && !forceCancel) || (!shapeshifting && !shouldAlwaysCancel && !unshiftTrigger);

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
                if (pc.Is(CustomRoles.Shiftguard)) pc.Notify(shapeshifting ? GetString("ShiftguardNotifySS") : GetString("ShiftguardNotifyUnshift"));

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

            if (Options.DisableReportWhenCC.GetBool() && (Camouflager.IsActive || (IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()))) return false;

            if (!CanReport[__instance.PlayerId])
            {
                WaitReport[__instance.PlayerId].Add(target);
                Logger.Warn($"{__instance.GetNameWithRole().RemoveHtmlTags()}: Reporting is currently prohibited, so we will wait until it becomes possible.", "ReportDeadBody");
                return false;
            }

            Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {target?.Object?.GetNameWithRole() ?? "null"}", "ReportDeadBody");

            foreach (KeyValuePair<byte, PlayerState> kvp in Main.PlayerStates) kvp.Value.LastRoom = GetPlayerById(kvp.Key).GetPlainShipRoom();

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

                if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && (Main.NormalOptions.MapId != 5 || !Options.CommsCamouflageDisableOnFungle.GetBool())) || Camouflager.IsActive) && Options.DisableReportWhenCC.GetBool()) return false;

                if (Main.PlayerStates.Values.Any(x => x.Role is Tremor { IsDoom: true })) return false;

                if (target == null)
                {
                    if (__instance.Is(CustomRoles.Jester) && !Jester.JesterCanUseButton.GetBool()) return false;

                    if (__instance.Is(CustomRoles.NiceSwapper) && !NiceSwapper.CanStartMeeting.GetBool()) return false;

                    if (__instance.Is(CustomRoles.Adrenaline) && !Adrenaline.CanCallMeeting(__instance)) return false;

                    if (SoulHunter.IsSoulHunterTarget(__instance.PlayerId))
                    {
                        __instance.Notify(GetString("SoulHunterTargetNotifyNoMeeting"));
                        return false;
                    }
                }

                if (target != null)
                {
                    if (Bloodhound.UnreportablePlayers.Contains(target.PlayerId)
                        || Vulture.UnreportablePlayers.Contains(target.PlayerId))
                        return false;

                    if (!Librarian.OnAnyoneReport(__instance)) return false;

                    if (!Hypnotist.OnAnyoneReport())
                    {
                        if (__instance.IsLocalPlayer())
                            Achievements.Type.Hypnosis.CompleteAfterGameEnd();

                        return false;
                    }

                    if (!Altruist.OnAnyoneCheckReportDeadBody(__instance, target)) return false;

                    if (!BoobyTrap.OnAnyoneCheckReportDeadBody(__instance, target)) return false;

                    if (!Main.PlayerStates[__instance.PlayerId].Role.CheckReportDeadBody(__instance, target, killer)) return false;

                    if (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.Gambled
                        || killerRole == CustomRoles.Scavenger
                        || Cleaner.CleanerBodies.Contains(target.PlayerId))
                        return false;

                    PlayerControl tpc = GetPlayerById(target.PlayerId);

                    if (__instance.Is(CustomRoles.Oblivious))
                    {
                        if (!tpc.Is(CustomRoles.Bait) || Options.ObliviousBaitImmune.GetBool()) /* && (target?.Object != null)*/
                            return false;
                    }

                    if (__instance.Is(CustomRoles.Unlucky) && (target.Object == null || !target.Object.Is(CustomRoles.Bait)))
                    {
                        var Ue = IRandom.Instance;

                        if (Ue.Next(0, 100) < Options.UnluckyReportSuicideChance.GetInt())
                        {
                            __instance.Suicide();
                            return false;
                        }
                    }

                    if (tpc.Is(CustomRoles.Unreportable)) return false;
                }

                if (Options.SyncButtonMode.GetBool() && target == null)
                {
                    Logger.Info("Max buttons:" + Options.SyncedButtonCount.GetInt() + ", used:" + Options.UsedButtonCount, "ReportDeadBody");

                    if (Options.SyncedButtonCount.GetFloat() <= Options.UsedButtonCount)
                    {
                        Logger.Info("The ship has no more emergency meetings left", "ReportDeadBody");
                        return false;
                    }

                    Options.UsedButtonCount++;
                    if (Math.Abs(Options.SyncedButtonCount.GetFloat() - Options.UsedButtonCount) < 0.5f) Logger.Info("This was the last allowed emergency meeting", "ReportDeadBody");
                }

                AfterReportTasks(__instance, target);
            }
            catch (Exception e)
            {
                ThrowException(e);
            }

            return true;
        }

        public static void AfterReportTasks(PlayerControl player, NetworkedPlayerInfo target, bool force = false)
        {
            //=============================================================================================
            //    Hereinafter, it is confirmed that the meeting is allowed, and the meeting will start.
            //=============================================================================================

            Asthmatic.RunChecks = false;

            Damocles.CountRepairSabotage = false;
            Stressed.CountRepairSabotage = false;

            Main.DiedThisRound = [];

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

                if (Virus.InfectedBodies.Contains(target.PlayerId)) Virus.OnKilledBodyReport(player);

                if (QuizMaster.On)
                {
                    QuizMaster.Data.LastReporterName = player.GetRealName();
                    QuizMaster.Data.LastReportedPlayer = (Palette.GetColorName(target.DefaultOutfit.ColorId), target.Object);
                    if (MeetingStates.FirstMeeting) QuizMaster.Data.FirstReportedBodyPlayerName = target.Object.GetRealName();
                }
            }

            if (QuizMaster.On)
            {
                if (MeetingStates.FirstMeeting) QuizMaster.Data.NumPlayersDeadFirstRound = Main.AllPlayerControls.Count(x => x.Data.IsDead && !x.Is(CustomRoles.GM));

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
            Main.GuesserGuessed.Clear();
            Veteran.VeteranInProtect.Clear();
            Grenadier.GrenadierBlinding.Clear();
            SecurityGuard.BlockSabo.Clear();
            Grenadier.MadGrenadierBlinding.Clear();
            Divinator.DidVote.Clear();
            Oracle.DidVote.Clear();
            Vulture.Clear();

            foreach (PlayerState state in Main.PlayerStates.Values)
                if (state.Role.IsEnable)
                    state.Role.OnReportDeadBody();

            Main.AbilityCD.Clear();
            SendRPC(CustomRPC.SyncAbilityCD, 2);

            if (player.Is(CustomRoles.Damocles)) Damocles.OnReport(player.PlayerId);

            Damocles.OnMeetingStart();

            if (player.Is(CustomRoles.Stressed)) Stressed.OnReport(player);

            Stressed.OnMeetingStart();

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if (Main.CheckShapeshift.ContainsKey(pc.PlayerId) && !Doppelganger.DoppelVictim.ContainsKey(pc.PlayerId)) Camouflage.RpcSetSkin(pc, RevertToDefault: true);

                if (Main.CurrentMap == MapNames.Fungle && (pc.IsMushroomMixupActive() || IsActive(SystemTypes.MushroomMixupSabotage))) pc.FixMixedUpOutfit();

                PhantomRolePatch.OnReportDeadBody(pc, force);
            }

            MeetingTimeManager.OnReportDeadBody();

            NameNotifyManager.Reset();
            NotifyRoles(true, NoCache: true, CamouflageIsForMeeting: true, GuesserIsForMeeting: true);

            LateTask.New(SyncAllSettings, 3f, "SyncAllSettings on meeting start");

            Main.ProcessShapeshifts = false;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                CustomRoles role = pc.GetCustomRole();
                if (role.AlwaysUsesUnshift() || (role.SimpleAbilityTrigger() && Options.UseUnshiftTrigger.GetBool() && (!pc.IsNeutralKiller() || Options.UseUnshiftTriggerForNKs.GetBool()))) pc.RpcShapeshift(pc, false);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    internal static class FixedUpdatePatch
    {
        private static readonly StringBuilder Mark = new(20);
        private static readonly StringBuilder Suffix = new(120);
        private static int LevelKickBufferTime = 10;
        private static readonly Dictionary<byte, int> BufferTime = [];
        private static readonly Dictionary<byte, int> DeadBufferTime = [];
        private static readonly Dictionary<byte, long> LastUpdate = [];
        private static readonly Dictionary<byte, long> LastAddAbilityTime = [];
        private static long LastErrorTS;

        public static void Postfix(PlayerControl __instance)
        {
            if (__instance == null || __instance.PlayerId == 255) return;

            if (AmongUsClient.Instance.AmHost && __instance.AmOwner) CustomNetObject.FixedUpdate();

            if (AmongUsClient.Instance.AmHost && __instance.IsAlive()) VentilationSystemDeterioratePatch.CheckVentInteraction(__instance);

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
                else if (!Main.HasJustStarted && GameStates.IsInTask && !ExileController.Instance && GhostRolesManager.ShouldHaveGhostRole(__instance)) GhostRolesManager.AssignGhostRole(__instance);
            }

            if (GameStates.InGame && Options.DontUpdateDeadPlayers.GetBool() && !__instance.IsAlive() && !__instance.GetCustomRole().NeedsUpdateAfterDeath() && Options.CurrentGameMode != CustomGameMode.RoomRush)
            {
                int buffer = Options.DeepLowLoad.GetBool() ? 30 : 10;
                DeadBufferTime.TryAdd(id, buffer);
                DeadBufferTime[id]--;
                if (DeadBufferTime[id] > 0) return;

                DeadBufferTime[id] = buffer;
            }

            if (Options.LowLoadMode.GetBool())
            {
                BufferTime.TryAdd(id, Options.DeepLowLoad.GetBool() ? 30 : 10);
                BufferTime[id]--;
            }

            try
            {
                DoPostfix(__instance);
            }
            catch (Exception ex)
            {
                long now = TimeStamp;

                if (LastErrorTS != now)
                {
                    Logger.Error($"Error for {__instance.GetNameWithRole()}:", "FixedUpdatePatch");
                    ThrowException(ex);
                    LastErrorTS = now;
                }

                Logger.Error($"Error for {__instance.GetNameWithRole()}: {ex}", "FixedUpdatePatch");
            }
        }

        private static void DoPostfix(PlayerControl __instance)
        {
            PlayerControl player = __instance;
            byte playerId = player.PlayerId;
            byte lpId = PlayerControl.LocalPlayer.PlayerId;
            bool localPlayer = playerId == lpId; // Updates that are independent of the player are only executed for the local player.

            var lowLoad = false;

            if (Options.LowLoadMode.GetBool())
            {
                if (BufferTime[playerId] > 0)
                    lowLoad = true;
                else
                    BufferTime[playerId] = Options.DeepLowLoad.GetBool() ? 30 : 10;
            }

            if (localPlayer)
            {
                Zoom.OnFixedUpdate();
                TextBoxTMPSetTextPatch.Update();
            }

            if (!lowLoad)
            {
                NameNotifyManager.OnFixedUpdate(player);
                TargetArrow.OnFixedUpdate(player);
                LocateArrow.OnFixedUpdate(player);

                if (AmongUsClient.Instance.AmHost)
                {
                    Camouflage.OnFixedUpdate(player);
                    AFKDetector.OnFixedUpdate(player);
                }

                if (RPCHandlerPatch.ReportDeadBodyRPCs.Remove(playerId)) Logger.Info($"Cleared ReportDeadBodyRPC Count for {player.GetRealName().RemoveHtmlTags()}", "FixedUpdatePatch");
            }

            bool inTask = GameStates.IsInTask;
            bool alive = player.IsAlive();

            if (AmongUsClient.Instance.AmHost)
            {
                if (GameStates.IsLobby && ((ModUpdater.HasUpdate && ModUpdater.ForceUpdate) || ModUpdater.IsBroken || !Main.AllowPublicRoom) && AmongUsClient.Instance.IsGamePublic) AmongUsClient.Instance.ChangeGamePublic(false);

                // Kick low level people
                if (!lowLoad && GameStates.IsLobby && !player.AmOwner && Options.KickLowLevelPlayer.GetInt() != 0 && (
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
                            Logger.SendInGame(msg);
                            AmongUsClient.Instance.KickPlayer(player.GetClientId(), true);
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
                        if (QuizMaster.On && inTask && !lowLoad && QuizMaster.AllSabotages.Any(IsActive))
                            QuizMaster.Data.LastSabotage = QuizMaster.AllSabotages.FirstOrDefault(IsActive);

                    if (!lowLoad && player.IsModClient() && player.Is(CustomRoles.Haste)) player.ForceKillTimerContinue = true;

                    if (DoubleTrigger.FirstTriggerTimer.Count > 0) DoubleTrigger.OnFixedUpdate(player);

                    if (Main.PlayerStates.TryGetValue(playerId, out PlayerState s) && s.Role.IsEnable) s.Role.OnFixedUpdate(player);

                    if (inTask && player.Is(CustomRoles.PlagueBearer) && PlagueBearer.IsPlaguedAll(player))
                    {
                        player.RpcSetCustomRole(CustomRoles.Pestilence);
                        player.Notify(GetString("PlagueBearerToPestilence"));
                        player.RpcGuardAndKill(player);
                        if (!PlagueBearer.PestilenceList.Contains(playerId)) PlagueBearer.PestilenceList.Add(playerId);

                        player.ResetKillCooldown();
                        PlagueBearer.PlayerIdList.Remove(playerId);
                    }

                    bool checkPos = inTask && player != null && alive && !Pelican.IsEaten(playerId);
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
                        if (subRoles.Contains(CustomRoles.Dynamo)) Dynamo.OnFixedUpdate(player);

                        if (subRoles.Contains(CustomRoles.Spurt)) Spurt.OnFixedUpdate(player);

                        if (!lowLoad)
                        {
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

                    long now = TimeStamp;

                    if (!lowLoad && Options.UsePets.GetBool() && inTask && (!LastUpdate.TryGetValue(playerId, out long lastPetNotify) || lastPetNotify < now))
                    {
                        if (Main.AbilityCD.TryGetValue(playerId, out (long StartTimeStamp, int TotalCooldown) timer))
                        {
                            if (timer.StartTimeStamp + timer.TotalCooldown < now || !alive) player.RemoveAbilityCD();

                            if (!player.IsModClient() && timer.TotalCooldown - (now - timer.StartTimeStamp) <= 60) NotifyRoles(SpecifySeer: player, SpecifyTarget: player);

                            LastUpdate[playerId] = now;
                        }
                    }

                    if (!lowLoad) Randomizer.OnFixedUpdateForPlayers(player);

                    RoleBlockManager.OnFixedUpdate(player);
                }
            }

            if (!lowLoad)
            {
                long now = TimeStamp;

                // Ability Use Gain every 5 seconds

                if (inTask && alive && Main.PlayerStates.TryGetValue(playerId, out PlayerState state) && state.TaskState.RemainingTasksCount <= 0 && (!LastAddAbilityTime.TryGetValue(playerId, out var ts) || ts + 5 < now))
                {
                    LastAddAbilityTime[playerId] = now;

                    AddExtraAbilityUsesOnFinishedTasks(player);
                }

                if (inTask && alive && Options.LadderDeath.GetBool()) FallFromLadder.FixedUpdate(player);

                if (localPlayer && GameStates.IsInGame) LoversSuicide();

                if (inTask && localPlayer && Options.DisableDevices.GetBool()) DisableDevice.FixedUpdate();

                if (localPlayer && GameStates.IsInGame && Main.RefixCooldownDelay <= 0)
                {
                    foreach (PlayerControl pc in Main.AllPlayerControls)
                        if (pc.Is(CustomRoles.Vampire) || pc.Is(CustomRoles.Warlock) || pc.Is(CustomRoles.Assassin) || pc.Is(CustomRoles.Undertaker) || pc.Is(CustomRoles.Poisoner))
                            Main.AllPlayerKillCooldown[pc.PlayerId] = Options.DefaultKillCooldown * 2;
                }

                if (!Main.DoBlockNameChange && AmongUsClient.Instance.AmHost) ApplySuffix(__instance);
            }

            if (__instance.AmOwner && inTask && ((Main.ChangedRole && localPlayer && AmongUsClient.Instance.AmHost) || (!__instance.Is(CustomRoleTypes.Impostor) && __instance.CanUseKillButton() && !__instance.Data.IsDead)))
            {
                List<PlayerControl> players = __instance.GetPlayersInAbilityRangeSorted();
                PlayerControl closest = players.Count == 0 ? null : players[0];
                HudManager.Instance.KillButton.SetTarget(closest);
            }

            Transform RoleTextTransform = __instance.cosmetics.nameText.transform.Find("RoleText");
            var RoleText = RoleTextTransform.GetComponent<TextMeshPro>();

            if (RoleText != null && __instance != null && !lowLoad)
            {
                if (GameStates.IsLobby)
                {
                    if (Main.PlayerVersion.TryGetValue(playerId, out PlayerVersion ver))
                    {
                        if (Main.ForkId != ver.forkId)
                            __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.2>{ver.forkId}</size>\n{__instance?.name}</color>";
                        else if (Main.Version.CompareTo(ver.version) == 0)
                            __instance.cosmetics.nameText.text = ver.tag == $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})" ? $"<color=#87cefa>{__instance.name}</color>" : $"<color=#ffff00><size=1.2>{ver.tag}</size>\n{__instance?.name}</color>";
                        else
                            __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.2>v{ver.version}</size>\n{__instance?.name}</color>";
                    }
                    else
                        __instance.cosmetics.nameText.text = Main.ShowPlayerInfoInLobby.Value && !__instance.AmOwner ? $"<#888888><size=1.2>{__instance.GetClient().PlatformData.Platform} | {__instance.FriendCode} | {__instance.GetClient().GetHashedPuid()}</size></color>\n{__instance?.Data?.PlayerName}" : __instance?.Data?.PlayerName;
                }

                if (GameStates.IsInGame)
                {
                    if (!AmongUsClient.Instance.AmHost && Options.CurrentGameMode != CustomGameMode.Standard)
                    {
                        RoleText.text = string.Empty;
                        RoleText.enabled = false;
                        return;
                    }

                    bool shouldSeeTargetAddons = playerId == lpId || new[] { PlayerControl.LocalPlayer, player }.All(x => x.Is(Team.Impostor));

                    (string, Color) RoleTextData = GetRoleText(lpId, playerId, seeTargetBetrayalAddons: shouldSeeTargetAddons);

                    RoleText.text = RoleTextData.Item1;
                    RoleText.color = RoleTextData.Item2;

                    if (Options.CurrentGameMode is not CustomGameMode.Standard and not CustomGameMode.HideAndSeek) RoleText.text = string.Empty;

                    RoleText.enabled = IsRoleTextEnabled(__instance);

                    if (!PlayerControl.LocalPlayer.Data.IsDead && PlayerControl.LocalPlayer.IsRevealedPlayer(__instance) && __instance.Is(CustomRoles.Trickster))
                    {
                        RoleText.text = Farseer.RandomRole[lpId];
                        RoleText.text += Farseer.GetTaskState();
                    }

                    if (!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay)
                    {
                        RoleText.enabled = false;
                        if (!__instance.AmOwner) __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
                    }

                    var isProgressTextLong = false;
                    string progressText = GetProgressText(__instance);

                    if (progressText.RemoveHtmlTags().Length > 25 && Main.VisibleTasksCount)
                    {
                        isProgressTextLong = true;
                        progressText = $"\n{progressText}";
                    }

                    if (Main.VisibleTasksCount) RoleText.text += progressText;

                    PlayerControl seer = PlayerControl.LocalPlayer;
                    PlayerControl target = __instance;

                    if (target.Is(CustomRoles.Car))
                    {
                        target.cosmetics.nameText.text = Car.Name;
                        RoleText.enabled = false;
                        return;
                    }

                    bool self = seer.PlayerId == target.PlayerId;

                    Mark.Clear();
                    Suffix.Clear();

                    string RealName = target.GetRealName();

                    if (target.AmOwner && inTask)
                    {
                        if (target.Is(CustomRoles.Arsonist) && target.IsDouseDone())
                            RealName = ColorString(GetRoleColor(CustomRoles.Arsonist), GetString("EnterVentToWin"));
                        else if (target.Is(CustomRoles.Revolutionist) && target.IsDrawDone()) RealName = ColorString(GetRoleColor(CustomRoles.Revolutionist), string.Format(GetString("EnterVentWinCountDown"), Revolutionist.RevolutionistCountdown.GetValueOrDefault(seer.PlayerId, 10)));

                        if (Pelican.IsEaten(seer.PlayerId)) RealName = ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"));

                        switch (Options.CurrentGameMode)
                        {
                            case CustomGameMode.SoloKombat:
                                SoloKombatManager.GetNameNotify(target, ref RealName);
                                break;
                        }

                        if (Deathpact.IsInActiveDeathpact(seer)) RealName = Deathpact.GetDeathpactString(seer);

                        if (NameNotifyManager.GetNameNotify(target, out string name) && name.Length > 0) RealName = name;
                    }

                    // Name Color Manager
                    RealName = RealName.ApplyNameColorData(seer, target, false);

                    Main.PlayerStates.Values.Do(x => Suffix.Append(x.Role.GetSuffix(seer, target, meeting: GameStates.IsMeeting)));

                    if (self) Suffix.Append(CustomTeamManager.GetSuffix(seer));

                    Suffix.Append(AFKDetector.GetSuffix(seer, target));

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
                        case CustomRoles.Lookout:
                            if (seer.IsAlive() && target.IsAlive()) Mark.Append(ColorString(GetRoleColor(CustomRoles.Lookout), " " + target.PlayerId) + " ");

                            break;
                        case CustomRoles.PlagueBearer when PlagueBearer.IsPlagued(seer.PlayerId, target.PlayerId):
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
                        case CustomRoles.Analyst:
                            if ((Main.PlayerStates[seer.PlayerId].Role as Analyst).CurrentTarget.ID == target.PlayerId) Mark.Append($"<color={GetRoleColorCode(CustomRoles.Analyst)}>○</color>");

                            break;
                        case CustomRoles.Samurai:
                            if ((Main.PlayerStates[seer.PlayerId].Role as Samurai).Target.Id == target.PlayerId) Mark.Append($"<color={GetRoleColorCode(CustomRoles.Samurai)}>○</color>");

                            break;
                        case CustomRoles.Puppeteer:
                            if (Puppeteer.PuppeteerList.ContainsValue(seer.PlayerId) && Puppeteer.PuppeteerList.ContainsKey(target.PlayerId)) Mark.Append($"<color={GetRoleColorCode(CustomRoles.Impostor)}>◆</color>");

                            break;
                        case CustomRoles.EvilTracker:
                            Mark.Append(EvilTracker.GetTargetMark(seer, target));
                            break;
                        case CustomRoles.Scout:
                            Mark.Append(Scout.GetTargetMark(seer, target));
                            break;
                        case CustomRoles.AntiAdminer when inTask:
                            (Main.PlayerStates[seer.PlayerId].Role as AntiAdminer).OnFixedUpdate(seer);

                            if (target.AmOwner)
                            {
                                if (AntiAdminer.IsAdminWatch) Suffix.Append(GetString("AntiAdminerAD"));

                                if (AntiAdminer.IsVitalWatch) Suffix.Append(GetString("AntiAdminerVI"));

                                if (AntiAdminer.IsDoorLogWatch) Suffix.Append(GetString("AntiAdminerDL"));

                                if (AntiAdminer.IsCameraWatch) Suffix.Append(GetString("AntiAdminerCA"));
                            }

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

                    if (Main.LoversPlayers.Exists(x => x.PlayerId == target.PlayerId))
                    {
                        if (Main.LoversPlayers.Exists(x => x.PlayerId == seer.PlayerId))
                            Mark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}> ♥</color>");
                        else if (!seer.IsAlive()) Mark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}> ♥</color>");
                    }

                    if (self)
                    {
                        Suffix.Append(Bloodmoon.GetSuffix(seer));
                        Suffix.Append(Haunter.GetSuffix(seer));
                        if (seer.Is(CustomRoles.Asthmatic)) Suffix.Append(Asthmatic.GetSuffixText(seer.PlayerId));

                        if (seer.Is(CustomRoles.Sonar)) Suffix.Append(Sonar.GetSuffix(seer, GameStates.IsMeeting));

                        if (seer.Is(CustomRoles.Deadlined)) Suffix.Append(Deadlined.GetSuffix(seer));

                        if (seer.Is(CustomRoles.Allergic)) Suffix.Append(Allergic.GetSelfSuffix(seer));
                    }

                    switch (Options.CurrentGameMode)
                    {
                        case CustomGameMode.FFA:
                            Suffix.Append(FFAManager.GetPlayerArrow(seer, target));
                            break;
                        case CustomGameMode.MoveAndStop when self:
                            Suffix.Append(MoveAndStopManager.GetSuffixText(seer));
                            break;
                        case CustomGameMode.Speedrun when self:
                            Suffix.Append(SpeedrunManager.GetSuffixText(seer));
                            break;
                        case CustomGameMode.HideAndSeek:
                            Suffix.Append(HnSManager.GetSuffixText(seer, target));
                            break;
                        case CustomGameMode.CaptureTheFlag:
                            Suffix.Append(CTFManager.GetSuffixText(seer, target));
                            break;
                        case CustomGameMode.RoomRush when self:
                            Suffix.Append(RoomRush.GetSuffix(seer));
                            break;
                    }

                    if (Options.CurrentGameMode == CustomGameMode.SoloKombat) Suffix.Append(SoloKombatManager.GetDisplayHealth(target));

                    if (MeetingStates.FirstMeeting && Main.ShieldPlayer == target.FriendCode && !string.IsNullOrEmpty(target.FriendCode) && !self && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.SoloKombat or CustomGameMode.FFA) Suffix.Append(GetString("DiedR1Warning"));

                    // Devourer
                    if (Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Devourer { IsEnable: true } dv && dv.PlayerSkinsCosumed.Contains(seer.PlayerId))) RealName = GetString("DevouredName");

                    // Camouflage
                    if ((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && (Main.NormalOptions.MapId != 5 || !Options.CommsCamouflageDisableOnFungle.GetBool())) || Camouflager.IsActive) RealName = $"<size=0>{RealName}</size> ";

                    string DeathReason = seer.Data.IsDead && seer.KnowDeathReason(target) ? $"\n<size=1.5>『{ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))}』</size>" : string.Empty;

                    string currentText = target.cosmetics.nameText.text;
                    var changeTo = $"{RealName}{DeathReason}{Mark}\r\n{Suffix}";
                    bool needUpdate = currentText != changeTo;

                    if (needUpdate)
                    {
                        target.cosmetics.nameText.text = changeTo;

                        var offset = 0.2f;

                        if (NameNotifyManager.GetNameNotify(seer, out string notify) && notify.Contains('\n'))
                        {
                            int count = notify.Count(x => x == '\n');
                            for (var i = 0; i < count; i++) offset += 0.15f;
                        }

                        if (Suffix.ToString() != string.Empty)
                        {
                            // If the name is on two lines, the job title text needs to be moved up.
                            //RoleText.transform.SetLocalY(0.35f);
                            offset += 0.15f;
                        }

                        if (!seer.IsAlive()) offset += 0.1f;

                        if (isProgressTextLong) offset += 0.3f;

                        if (Options.CurrentGameMode == CustomGameMode.MoveAndStop) offset += 0.2f;

                        RoleText.transform.SetLocalY(offset);
                    }
                }
                else
                {
                    // Restoring the position text coordinates to their initial values
                    RoleText.transform.SetLocalY(0.2f);
                }
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
            if (Lovers.LoverDieConsequence.GetValue() == 0 || Main.IsLoversDead || (!Main.LoversPlayers.Exists(player => player.Data.IsDead && player.PlayerId == deathId) && !force)) return;

            Main.IsLoversDead = true;
            PlayerControl partnerPlayer = Main.LoversPlayers.First(player => player.PlayerId != deathId && !player.Data.IsDead);

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

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
    internal static class PlayerStartPatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            TextMeshPro roleText = Object.Instantiate(__instance.cosmetics.nameText, __instance.cosmetics.nameText.transform, true);
            roleText.transform.localPosition = new(0f, 0.2f, 0f);
            roleText.fontSize -= 0.9f;
            roleText.text = "RoleText";
            roleText.gameObject.name = "RoleText";
            roleText.enabled = false;
        }
    }

//[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetColor))]
//class SetColorPatch
//{
//    public static bool IsAntiGlitchDisabled;

//    public static bool Prefix(PlayerControl __instance, int bodyColor)
//    {
//        return true;
//    }
//}

    [HarmonyPatch(typeof(Vent), nameof(Vent.ExitVent))]
    internal static class ExitVentPatch
    {
        public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
        {
            Logger.Info($" {pc.GetNameWithRole()}, Vent ID: {__instance.Id} ({__instance.name})", "ExitVent");

            if (pc.IsLocalPlayer()) LateTask.New(() => HudManager.Instance.SetHudActive(pc, pc.Data.Role, true), 0.6f, log: false);

            if (!AmongUsClient.Instance.AmHost) return;

            if (Main.KillTimers.ContainsKey(pc.PlayerId)) Main.KillTimers[pc.PlayerId] += 0.5f;

            Drainer.OnAnyoneExitVent(pc);

            Main.PlayerStates[pc.PlayerId].Role.OnExitVent(pc, __instance);

            if (Options.WhackAMole.GetBool()) LateTask.New(() => pc.TPToRandomVent(), 0.5f, "Whack-A-Mole TP");

            if (!pc.IsModClient() && pc.Is(CustomRoles.Haste)) pc.SetKillCooldown(Math.Max(Main.KillTimers[pc.PlayerId], 0.1f));
        }
    }

    [HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
    internal static class EnterVentPatch
    {
        public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
        {
            Logger.Info($" {pc.GetNameWithRole()}, Vent ID: {__instance.Id} ({__instance.name})", "EnterVent");

            if (AmongUsClient.Instance.AmHost && !pc.CanUseVent() && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.HideAndSeek && !pc.Is(CustomRoles.Nimble) && !pc.Is(CustomRoles.Bloodlust))
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

            Main.LastEnteredVent[pc.PlayerId] = __instance;
            Main.LastEnteredVentLocation[pc.PlayerId] = pc.Pos();

            if (pc.Is(CustomRoles.Unlucky))
            {
                var Ue = IRandom.Instance;
                if (Ue.Next(0, 100) < Options.UnluckyVentSuicideChance.GetInt()) pc.Suicide();
            }

            if (pc.Is(CustomRoles.Damocles)) Damocles.OnEnterVent(pc.PlayerId, __instance.Id);

            Chef.SpitOutFood(pc);

            Main.PlayerStates[pc.PlayerId].Role.OnEnterVent(pc, __instance);
        }
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoEnterVent))]
    internal static class CoEnterVentPatch
    {
        public static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] int id)
        {
            if (!AmongUsClient.Instance.AmHost) return true;

            Logger.Info($" {__instance.myPlayer.GetNameWithRole()}, Vent ID: {id}", "CoEnterVent");

            if (Main.KillTimers.ContainsKey(__instance.myPlayer.PlayerId)) Main.KillTimers[__instance.myPlayer.PlayerId] += 0.5f;

            CheckInvalidMovementPatch.ExemptedPlayers.Add(__instance.myPlayer.PlayerId);

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.FFA when FFAManager.FFADisableVentingWhenTwoPlayersAlive.GetBool() && Main.AllAlivePlayerControls.Length <= 2:
                    LateTask.New(() =>
                    {
                        __instance.myPlayer?.Notify(GetString("FFA-NoVentingBecauseTwoPlayers"), 7f);
                        __instance.RpcBootFromVent(id);
                    }, 0.5f, "FFA-NoVentingWhenTwoPlayersAlive");

                    return true;
                case CustomGameMode.FFA when FFAManager.FFADisableVentingWhenKcdIsUp.GetBool() && Main.KillTimers[__instance.myPlayer.PlayerId] <= 0:
                    LateTask.New(() =>
                    {
                        __instance.myPlayer?.Notify(GetString("FFA-NoVentingBecauseKCDIsUP"), 7f);
                        __instance.RpcBootFromVent(id);
                    }, 0.5f, "FFA-NoVentingWhenKCDIsUP");

                    return true;
                case CustomGameMode.MoveAndStop:
                case CustomGameMode.HotPotato:
                case CustomGameMode.Speedrun:
                case CustomGameMode.CaptureTheFlag:
                case CustomGameMode.NaturalDisasters:
                    LateTask.New(() => __instance.RpcBootFromVent(id), 0.5f, log: false);
                    return true;
                case CustomGameMode.HideAndSeek:
                    HnSManager.OnCoEnterVent(__instance, id);
                    break;
                case CustomGameMode.RoomRush:
                    return true;
            }

            if (__instance.myPlayer.IsRoleBlocked())
            {
                LateTask.New(() =>
                {
                    __instance.myPlayer?.Notify(BlockedAction.Vent.GetBlockNotify());
                    __instance.RpcBootFromVent(id);
                }, 0.5f, "RoleBlockedBootFromVent");

                return true;
            }

            if (!Rhapsode.CheckAbilityUse(__instance.myPlayer))
            {
                LateTask.New(() => __instance.RpcBootFromVent(id), 0.5f, log: false);
                return true;
            }

            if (Penguin.IsVictim(__instance.myPlayer))
            {
                LateTask.New(() => __instance.RpcBootFromVent(id), 0.5f, "PenguinVictimBootFromVent");
                return true;
            }

            if (SoulHunter.IsSoulHunterTarget(__instance.myPlayer.PlayerId))
            {
                LateTask.New(() =>
                {
                    __instance.myPlayer?.Notify(GetString("SoulHunterTargetNotifyNoVent"));
                    __instance.RpcBootFromVent(id);
                }, 0.5f, "SoulHunterTargetBootFromVent");

                return true;
            }

            if (Ventguard.BlockedVents.Contains(id))
            {
                PlayerControl pc = __instance.myPlayer;

                if (!Ventguard.VentguardBlockDoesNotAffectCrew.GetBool() || !pc.IsCrewmate())
                {
                    LateTask.New(() =>
                    {
                        pc?.Notify(GetString("EnteredBlockedVent"));
                        __instance.RpcBootFromVent(id);
                    }, 0.5f, "VentguardBlockedVentBootFromVent");

                    if (Ventguard.VentguardNotifyOnBlockedVentUse.GetBool())
                        foreach (PlayerControl ventguard in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Ventguard)).ToArray())
                            ventguard.Notify(GetString("VentguardNotify"));

                    return true;
                }
            }

            if (__instance.myPlayer.Is(CustomRoles.Circumvent)) Circumvent.OnCoEnterVent(__instance, id);

            if (!__instance.myPlayer.CanUseVent() && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.HideAndSeek && !__instance.myPlayer.Is(CustomRoles.Nimble) && !__instance.myPlayer.Is(CustomRoles.Bloodlust)) LateTask.New(() => __instance.RpcBootFromVent(id), 0.5f, "CannotUseVentBootFromVent");

            if (((__instance.myPlayer.Data.Role.Role != RoleTypes.Engineer && !__instance.myPlayer.CanUseImpostorVentButton()) ||
                 (__instance.myPlayer.Is(CustomRoles.Mayor) && Mayor.MayorUsedButtonCount.TryGetValue(__instance.myPlayer.PlayerId, out int count) && count >= Mayor.MayorNumOfUseButton.GetInt()) ||
                 (__instance.myPlayer.Is(CustomRoles.Paranoia) && Paranoia.ParaUsedButtonCount.TryGetValue(__instance.myPlayer.PlayerId, out int count2) && count2 >= Options.ParanoiaNumOfUseButton.GetInt()))
                && !__instance.myPlayer.Is(CustomRoles.Nimble) && !__instance.myPlayer.Is(CustomRoles.Bloodlust) && Options.CurrentGameMode == CustomGameMode.Standard)
            {
                try
                {
                    LateTask.New(() => __instance.RpcBootFromVent(id), 0.5f, "CannotUseVentBootFromVent2");
                    return true;
                }
                catch { }

                return true;
            }

            Main.PlayerStates[__instance.myPlayer.PlayerId].Role.OnCoEnterVent(__instance, id);

            if (__instance.myPlayer.IsLocalPlayer())
                Statistics.VentTimes++;

            return true;
        }
    }

    [HarmonyPatch(typeof(GameData), nameof(GameData.CompleteTask))]
    internal static class GameDataCompleteTaskPatch
    {
        public static void Postfix(PlayerControl pc /*, uint taskId*/)
        {
            if (GameStates.IsMeeting) return;

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
            if (GameStates.IsMeeting) return;

            var task = __instance.myTasks[(Index)Convert.ToInt32(idx)] as PlayerTask;
            if (__instance != null && __instance.IsAlive()) Benefactor.OnTaskComplete(__instance, task);

            if (__instance == null) return;

            Snitch.OnCompleteTask(__instance);

            bool isTaskFinish = __instance.GetTaskState().IsTaskFinished;

            if (isTaskFinish && __instance.Is(CustomRoles.Snitch) && __instance.Is(CustomRoles.Madmate))
            {
                foreach (PlayerControl impostor in Main.AllAlivePlayerControls.Where(pc => pc.Is(CustomRoleTypes.Impostor)).ToArray()) NameColorManager.Add(impostor.PlayerId, __instance.PlayerId, "#ff1919");

                NotifyRoles(SpecifySeer: __instance, ForceLoop: true);
            }

            if (Options.CurrentGameMode == CustomGameMode.HideAndSeek && HnSManager.PlayerRoles[__instance.PlayerId].Interface.Team == Team.Crewmate && __instance.IsAlive()) Hider.OnSpecificTaskComplete(__instance, task);
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
    public static class PlayerControlDiePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (AmongUsClient.Instance.AmHost) PetsPatch.RpcRemovePet(__instance);
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
                bool targetIsKiller = __instance.Is(CustomRoleTypes.Impostor) || Main.ResetCamPlayerList.Contains(__instance.PlayerId);
                Dictionary<PlayerControl, RoleTypes> ghostRoles = new();

                foreach (PlayerControl seer in Main.AllPlayerControls)
                {
                    bool self = seer.PlayerId == __instance.PlayerId;
                    bool seerIsKiller = seer.Is(CustomRoleTypes.Impostor) || Main.ResetCamPlayerList.Contains(seer.PlayerId);

                    if (__instance.HasGhostRole())
                        ghostRoles[seer] = RoleTypes.GuardianAngel;
                    else if ((self && targetIsKiller) || (!seerIsKiller && __instance.Is(CustomRoleTypes.Impostor)))
                        ghostRoles[seer] = RoleTypes.ImpostorGhost;
                    else
                        ghostRoles[seer] = RoleTypes.CrewmateGhost;
                }

                if (__instance.HasGhostRole())
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
                        __instance.RpcSetRoleDesync(role, seer.GetClientId());
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

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckVanish))]
    internal static class CmdCheckVanishPatch
    {
        public static bool Prefix(PlayerControl __instance, float maxDuration)
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
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckAppear))]
    internal static class CmdCheckAppearPatch
    {
        public static bool Prefix(PlayerControl __instance, bool shouldAnimate)
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
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckVanish))]
    internal static class CheckVanishPatch
    {
        public static bool Prefix(PlayerControl __instance)
        {
            Logger.Info($" {__instance.GetNameWithRole()}", "CheckVanish");
            bool allow = Main.PlayerStates[__instance.PlayerId].Role.OnVanish(__instance);
            if (!allow) LateTask.New(__instance.RpcResetAbilityCooldown, 0.2f, log: false);

            return true;
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
        public static void Postfix(PlayerControl __instance)
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
        public static bool Prefix(PlayerControl __instance, RpcCalls rpc, byte sequenceId, ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}