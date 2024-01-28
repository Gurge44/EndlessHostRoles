using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE;

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
class GameEndChecker
{
    private static GameEndPredicate predicate;
    public static bool Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (predicate == null) return false;

        if (Options.NoGameEnd.GetBool() && CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.Error) return false;

        var reason = GameOverReason.ImpostorByKill;

        predicate.CheckForEndGame(out reason);

        if (Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop)
        {
            if (CustomWinnerHolder.WinnerIds.Count > 0 || CustomWinnerHolder.WinnerTeam != CustomWinner.Default)
            {
                ShipStatus.Instance.enabled = false;
                StartEndGame(reason);
                predicate = null;
            }
            return false;
        }

        //ゲーム終了時
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default)
        {
            NameNotifyManager.Reset();
            NotifyRoles(ForceLoop: true);

            Main.AllPlayerControls.Do(pc => Camouflage.RpcSetSkin(pc, ForceRevert: true, RevertToDefault: true));

            if (reason == GameOverReason.ImpostorBySabotage && (CustomRoles.Jackal.RoleExist() || CustomRoles.Sidekick.RoleExist()) && Jackal.CanWinBySabotageWhenNoImpAlive.GetBool() && !Main.AllAlivePlayerControls.Any(x => x.GetCustomRole().IsImpostorTeam()))
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.WinnerIds.Clear();
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jackal);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Jackal);
            }

            switch (CustomWinnerHolder.WinnerTeam)
            {
                case CustomWinner.Crewmate:
                    Main.AllPlayerControls
                        .Where(pc => (pc.Is(CustomRoleTypes.Crewmate) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Madmate) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Infected) && !pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.EvilSpirit) && !pc.Is(CustomRoles.Recruit)) || pc.Is(CustomRoles.Admired))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Impostor:
                    Main.AllPlayerControls
                        .Where(pc => ((pc.Is(CustomRoleTypes.Impostor) && (!pc.Is(CustomRoles.DeadlyQuota) || Main.PlayerStates.Count(x => x.Value.GetRealKiller() == pc.PlayerId) >= Options.DQNumOfKillsNeeded.GetInt())) || pc.Is(CustomRoles.Madmate)) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Infected) && !pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.EvilSpirit) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Admired))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Succubus:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Succubus) || (pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Admired)))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                //case CustomWinner.CursedSoul:
                //    Main.AllPlayerControls
                //        .Where(pc => pc.Is(CustomRoles.CursedSoul) || (pc.Is(CustomRoles.Soulless) && CursedSoul.SoullessWinsWithCS.GetBool() && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Admired)))
                //        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                //    break;
                case CustomWinner.Infectious:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Infectious) || (pc.Is(CustomRoles.Infected) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Admired)))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Virus:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Virus) || (pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Admired)))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Jackal:
                    Main.AllPlayerControls
                        .Where(pc => (pc.Is(CustomRoles.Jackal) || pc.Is(CustomRoles.Sidekick) || pc.Is(CustomRoles.Recruit)) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Infected) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Admired))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Spiritcaller:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Spiritcaller) || pc.Is(CustomRoles.EvilSpirit))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.RuthlessRomantic:
                    foreach (var pc in Main.AllPlayerControls.Where(pc => pc.Is(CustomRoles.RuthlessRomantic)).ToArray())
                    {
                        CustomWinnerHolder.WinnerIds.Add(Romantic.BetPlayer[pc.PlayerId]);
                    }
                    //Main.AllPlayerControls
                    //    .Where(pc => (pc.Is(CustomRoles.RuthlessRomantic) || (Romantic.BetPlayer.TryGetValue(pc.PlayerId, out var RomanticPartner)) && pc.PlayerId == RomanticPartner))
                    //    .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
            }
            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.None and not CustomWinner.Error)
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    switch (pc.GetCustomRole())
                    {
                        case CustomRoles.DarkHide when !pc.Data.IsDead && ((CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor && !reason.Equals(GameOverReason.ImpostorBySabotage)) || CustomWinnerHolder.WinnerTeam == CustomWinner.DarkHide || (CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate && !reason.Equals(GameOverReason.HumansByTask) && DarkHide.IsWinKill[pc.PlayerId] == true && DarkHide.SnatchesWin.GetBool())):
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.DarkHide);
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Phantom when pc.GetTaskState().IsTaskFinished && pc.Data.IsDead && (CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor || CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate || CustomWinnerHolder.WinnerTeam == CustomWinner.Jackal || CustomWinnerHolder.WinnerTeam == CustomWinner.BloodKnight || CustomWinnerHolder.WinnerTeam == CustomWinner.SerialKiller || CustomWinnerHolder.WinnerTeam == CustomWinner.Juggernaut || CustomWinnerHolder.WinnerTeam == CustomWinner.Ritualist || CustomWinnerHolder.WinnerTeam == CustomWinner.Poisoner || CustomWinnerHolder.WinnerTeam == CustomWinner.Succubus || CustomWinnerHolder.WinnerTeam == CustomWinner.Infectious || CustomWinnerHolder.WinnerTeam == CustomWinner.Jinx || CustomWinnerHolder.WinnerTeam == CustomWinner.Virus || CustomWinnerHolder.WinnerTeam == CustomWinner.Arsonist || CustomWinnerHolder.WinnerTeam == CustomWinner.Pelican || CustomWinnerHolder.WinnerTeam == CustomWinner.HexMaster || CustomWinnerHolder.WinnerTeam == CustomWinner.Wraith || CustomWinnerHolder.WinnerTeam == CustomWinner.Pestilence || CustomWinnerHolder.WinnerTeam == CustomWinner.Rogue || CustomWinnerHolder.WinnerTeam == CustomWinner.Doppelganger || CustomWinnerHolder.WinnerTeam == CustomWinner.Spiritcaller) && Options.PhantomSnatchesWin.GetBool():
                            reason = GameOverReason.ImpostorByKill;
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Phantom);
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Phantom when !Options.PhantomSnatchesWin.GetBool() && !pc.IsAlive() && pc.GetTaskState().IsTaskFinished:
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Phantom);
                            break;
                        //case CustomRoles.CursedSoul when !pc.Data.IsDead && (CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor || CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate || CustomWinnerHolder.WinnerTeam == CustomWinner.Jackal || CustomWinnerHolder.WinnerTeam == CustomWinner.BloodKnight || CustomWinnerHolder.WinnerTeam == CustomWinner.SerialKiller || CustomWinnerHolder.WinnerTeam == CustomWinner.Juggernaut || CustomWinnerHolder.WinnerTeam == CustomWinner.Ritualist || CustomWinnerHolder.WinnerTeam == CustomWinner.Poisoner || CustomWinnerHolder.WinnerTeam == CustomWinner.Succubus || CustomWinnerHolder.WinnerTeam == CustomWinner.Infectious || CustomWinnerHolder.WinnerTeam == CustomWinner.Jinx || CustomWinnerHolder.WinnerTeam == CustomWinner.Virus || CustomWinnerHolder.WinnerTeam == CustomWinner.Arsonist || CustomWinnerHolder.WinnerTeam == CustomWinner.Pelican || CustomWinnerHolder.WinnerTeam == CustomWinner.HexMaster || CustomWinnerHolder.WinnerTeam == CustomWinner.Wraith || CustomWinnerHolder.WinnerTeam == CustomWinner.Pestilence || CustomWinnerHolder.WinnerTeam == CustomWinner.Rogue || CustomWinnerHolder.WinnerTeam == CustomWinner.Jester || CustomWinnerHolder.WinnerTeam == CustomWinner.Doppelganger || CustomWinnerHolder.WinnerTeam == CustomWinner.Executioner):
                        //    reason = GameOverReason.ImpostorByKill;
                        //    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.CursedSoul);
                        //    CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Soulless);
                        //    CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        //    break;
                        case CustomRoles.Opportunist when pc.IsAlive():
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Opportunist);
                            break;
                        case CustomRoles.Pursuer when pc.IsAlive() && CustomWinnerHolder.WinnerTeam != CustomWinner.Jester && CustomWinnerHolder.WinnerTeam != CustomWinner.Lovers && CustomWinnerHolder.WinnerTeam != CustomWinner.Terrorist && CustomWinnerHolder.WinnerTeam != CustomWinner.Executioner && CustomWinnerHolder.WinnerTeam != CustomWinner.Collector && CustomWinnerHolder.WinnerTeam != CustomWinner.Innocent && CustomWinnerHolder.WinnerTeam != CustomWinner.Youtuber:
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Pursuer);
                            break;
                        case CustomRoles.Sunnyboy when !pc.IsAlive():
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Sunnyboy);
                            break;
                        case CustomRoles.Maverick when pc.IsAlive() && Maverick.NumOfKills >= Maverick.MinKillsToWin.GetInt():
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Maverick);
                            break;
                        case CustomRoles.Provocateur when Main.Provoked.TryGetValue(pc.PlayerId, out var tar) && !CustomWinnerHolder.WinnerIds.Contains(tar):
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Provocateur);
                            break;
                        case CustomRoles.FFF when FFF.isWon:
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.FFF);
                            break;
                        case CustomRoles.Totocalcio when Totocalcio.BetPlayer.TryGetValue(pc.PlayerId, out var betTarget) && (CustomWinnerHolder.WinnerIds.Contains(betTarget) || (Main.PlayerStates.TryGetValue(betTarget, out var ps) && CustomWinnerHolder.WinnerRoles.Contains(ps.MainRole))):
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Totocalcio);
                            break;
                        case CustomRoles.Romantic when Romantic.BetPlayer.TryGetValue(pc.PlayerId, out var betTarget) && (CustomWinnerHolder.WinnerIds.Contains(betTarget) || (Main.PlayerStates.TryGetValue(betTarget, out var ps) && CustomWinnerHolder.WinnerRoles.Contains(ps.MainRole))):
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Romantic);
                            break;
                        case CustomRoles.VengefulRomantic when VengefulRomantic.hasKilledKiller:
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.WinnerIds.Add(Romantic.BetPlayer[pc.PlayerId]);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.VengefulRomantic);
                            break;
                        case CustomRoles.Lawyer when Lawyer.Target.TryGetValue(pc.PlayerId, out var lawyertarget) && (CustomWinnerHolder.WinnerIds.Contains(lawyertarget) || (Main.PlayerStates.TryGetValue(lawyertarget, out var ps) && CustomWinnerHolder.WinnerRoles.Contains(ps.MainRole))):
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Lawyer);
                            break;
                        case CustomRoles.Postman when Postman.IsFinished:
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Postman);
                            break;
                        case CustomRoles.SoulHunter when SoulHunter.Souls >= SoulHunter.NumOfSoulsToWin.GetInt():
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.SoulHunter);
                            break;
                    }
                }

                if (CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor)
                {
                    var aliveImps = Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoleTypes.Impostor));
                    var aliveImpCount = aliveImps.Count();

                    // If there's an Egoist, and there is at least 1 non-Egoist impostor alive, Egoist loses
                    if (aliveImpCount > 1 && CustomWinnerHolder.WinnerIds.Any(x => GetPlayerById(x).Is(CustomRoles.Egoist)))
                    {
                        CustomWinnerHolder.WinnerIds.RemoveWhere(x => GetPlayerById(x).Is(CustomRoles.Egoist));
                    }
                    // If there's only 1 impostor alive, and all alive impostors are Egoists, the Egoist wins alone
                    else if (aliveImpCount == 1 && aliveImps.All(x => x.Is(CustomRoles.Egoist)))
                    {
                        var pc = aliveImps.FirstOrDefault();
                        reason = GameOverReason.ImpostorByKill;
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Egoist);
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                    }
                }

                if (CustomRoles.God.RoleExist())
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.God);
                    Main.AllPlayerControls
                        .Where(p => p.Is(CustomRoles.God) && p.IsAlive())
                        .Do(p => CustomWinnerHolder.WinnerIds.Add(p.PlayerId));
                }

                if (CustomRoles.Lovers.RoleExist() && !reason.Equals(GameOverReason.HumansByTask) && Main.AllPlayerControls.Length >= 2 && !(!Main.LoversPlayers.All(p => p.IsAlive()) && Options.LoverSuicide.GetBool()) && CustomWinnerHolder.WinnerTeam is CustomWinner.Crewmate or CustomWinner.Impostor or CustomWinner.Jackal or CustomWinner.Pelican)
                {
                    CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Lovers);
                    Main.AllPlayerControls
                        .Where(p => p.Is(CustomRoles.Lovers))
                        .Do(p => CustomWinnerHolder.WinnerIds.Add(p.PlayerId));
                }

                //Lovers follow winner
                if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Lovers and not CustomWinner.Crewmate and not CustomWinner.Impostor)
                {
                    foreach (var pc in Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Lovers)).ToArray())
                    {
                        if (CustomWinnerHolder.WinnerIds.Any(x => GetPlayerById(x).Is(CustomRoles.Lovers)))
                        {
                            if (!CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId)) CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            if (!CustomWinnerHolder.AdditionalWinnerTeams.Contains(AdditionalWinners.Lovers)) CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Lovers);
                        }
                    }
                }

                //Ruthless Romantic partner win condition
                if (RuthlessRomantic.IsEnable && (CustomWinnerHolder.WinnerIds.Contains(RuthlessRomantic.playerIdList[0]) || (Main.PlayerStates.TryGetValue(RuthlessRomantic.playerIdList[0], out var pz) && CustomWinnerHolder.WinnerRoles.Contains(pz.MainRole))))
                {
                    foreach (PlayerControl pc in Main.AllPlayerControls)
                    {
                        if (Romantic.BetPlayer.TryGetValue(pc.PlayerId, out var betTarget))
                        {
                            CustomWinnerHolder.WinnerIds.Add(betTarget);
                        }
                    }
                }

                if (CustomWinnerHolder.WinnerTeam == CustomWinner.Lovers || CustomWinnerHolder.AdditionalWinnerTeams.Contains(AdditionalWinners.Lovers))
                {
                    Main.AllPlayerControls.Where(p => p.Is(CustomRoles.Lovers) && !CustomWinnerHolder.WinnerIds.Contains(p.PlayerId))
                                          .Do(p => CustomWinnerHolder.WinnerIds.Add(p.PlayerId));
                }

                if (Options.NeutralWinTogether.GetBool() && CustomWinnerHolder.WinnerIds.Any(x => GetPlayerById(x) != null && GetPlayerById(x).GetCustomRole().IsNeutral()))
                {
                    foreach (PlayerControl pc in Main.AllPlayerControls)
                    {
                        if (pc.GetCustomRole().IsNeutral() && !CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId))
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                    }
                }
                else if (Options.NeutralRoleWinTogether.GetBool())
                {
                    foreach (var id in CustomWinnerHolder.WinnerIds)
                    {
                        var pc = GetPlayerById(id);
                        if (pc == null || !pc.GetCustomRole().IsNeutral()) continue;
                        foreach (PlayerControl tar in Main.AllPlayerControls)
                        {
                            if (!CustomWinnerHolder.WinnerIds.Contains(tar.PlayerId) && tar.GetCustomRole() == pc.GetCustomRole())
                                CustomWinnerHolder.WinnerIds.Add(tar.PlayerId);
                        }
                    }
                }
            }
            ShipStatus.Instance.enabled = false;
            StartEndGame(reason);
            predicate = null;
        }
        return false;
    }
    public static void StartEndGame(GameOverReason reason)
    {
        AmongUsClient.Instance.StartCoroutine(CoEndGame(AmongUsClient.Instance, reason).WrapToIl2Cpp());
    }
    private static IEnumerator CoEndGame(AmongUsClient self, GameOverReason reason)
    {
        if (Blackmailer.IsEnable) Blackmailer.ForBlackmailer.Clear();

        // Set ghost role
        List<byte> ReviveRequiredPlayerIds = [];
        var winner = CustomWinnerHolder.WinnerTeam;
        foreach (var pc in Main.AllPlayerControls)
        {
            if (winner == CustomWinner.Draw)
            {
                SetGhostRole(ToGhostImpostor: true);
                continue;
            }
            bool canWin = CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId) || CustomWinnerHolder.WinnerRoles.Contains(pc.GetCustomRole());
            bool isCrewmateWin = reason.Equals(GameOverReason.HumansByVote) || reason.Equals(GameOverReason.HumansByTask);
            SetGhostRole(ToGhostImpostor: canWin ^ isCrewmateWin); // XOR

            void SetGhostRole(bool ToGhostImpostor)
            {
                if (!pc.Data.IsDead) ReviveRequiredPlayerIds.Add(pc.PlayerId);
                if (ToGhostImpostor)
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()}: changed to ImpostorGhost", "ResetRoleAndEndGame");
                    pc.RpcSetRole(RoleTypes.ImpostorGhost);
                }
                else
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()}: changed to CrewmateGhost", "ResetRoleAndEndGame");
                    pc.RpcSetRole(RoleTypes.CrewmateGhost);
                }
            }
            SetEverythingUpPatch.LastWinsReason = winner is CustomWinner.Crewmate or CustomWinner.Impostor ? GetString($"GameOverReason.{reason}") : string.Empty;
        }

        // Sync of CustomWinnerHolder info
        var winnerWriter = self.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.EndGame, SendOption.Reliable);
        CustomWinnerHolder.WriteTo(winnerWriter);
        self.FinishRpcImmediately(winnerWriter);

        // Delay to ensure that resuscitation is delivered after the ghost roll setting
        yield return new WaitForSeconds(EndGameDelay);

        if (ReviveRequiredPlayerIds.Count > 0)
        {
            // Resuscitation Resuscitate one person per transmission to prevent the packet from swelling up and dying
            for (int i = 0; i < ReviveRequiredPlayerIds.Count; i++)
            {
                var playerId = ReviveRequiredPlayerIds[i];
                var playerInfo = GameData.Instance.GetPlayerById(playerId);
                // resuscitation
                playerInfo.IsDead = false;
                // transmission
                GameData.Instance.SetDirtyBit(0b_1u << playerId);
                AmongUsClient.Instance.SendAllStreamedObjects();
            }
            // Delay to ensure that the end of the game is delivered at the end of the game
            yield return new WaitForSeconds(EndGameDelay);
        }

        // Start End Game
        GameManager.Instance.RpcEndGame(reason, false);
    }
    private const float EndGameDelay = 0.2f;

    public static void SetPredicateToNormal() => predicate = new NormalGameEndPredicate();
    public static void SetPredicateToSoloKombat() => predicate = new SoloKombatGameEndPredicate();
    public static void SetPredicateToFFA() => predicate = new FFAGameEndPredicate();
    public static void SetPredicateToMoveAndStop() => predicate = new MoveAndStopGameEndPredicate();

    class NormalGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;
            if (CheckGameEndBySabotage(out reason) || CheckGameEndByTask(out reason) || CheckGameEndByLivingPlayers(out reason)) return true;
            return false;
        }

        public static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (CustomRoles.Sunnyboy.RoleExist() && Main.AllAlivePlayerControls.Length > 1) return false;

            int Imp = AlivePlayersCount(CountTypes.Impostor);
            int Crew = AlivePlayersCount(CountTypes.Crew);

            Dictionary<(CustomRoles? ROLE, CustomWinner WINNER), int> roleCounts = new()  // Self Note: If you're adding a new NK, you just have to add it into this dictionary and that's it
            {
                { (null,                         CustomWinner.Jackal),             AlivePlayersCount(CountTypes.Jackal) },
                { (CustomRoles.Pelican,          CustomWinner.Pelican),            AlivePlayersCount(CountTypes.Pelican) },
                { (CustomRoles.Gamer,            CustomWinner.Gamer),              AlivePlayersCount(CountTypes.Gamer) },
                { (CustomRoles.Poisoner,         CustomWinner.Poisoner),           AlivePlayersCount(CountTypes.Poisoner) },
                { (CustomRoles.BloodKnight,      CustomWinner.BloodKnight),        AlivePlayersCount(CountTypes.BloodKnight) },
                { (null,                         CustomWinner.Succubus),           AlivePlayersCount(CountTypes.Succubus) },
                { (CustomRoles.HexMaster,        CustomWinner.HexMaster),          AlivePlayersCount(CountTypes.HexMaster) },
                { (CustomRoles.Wraith,           CustomWinner.Wraith),             AlivePlayersCount(CountTypes.Wraith) },
                { (CustomRoles.Pestilence,       CustomWinner.Pestilence),         AlivePlayersCount(CountTypes.Pestilence) },
                { (CustomRoles.PlagueBearer,     CustomWinner.Plaguebearer),       AlivePlayersCount(CountTypes.PlagueBearer) },
                { (CustomRoles.NSerialKiller,    CustomWinner.SerialKiller),       AlivePlayersCount(CountTypes.NSerialKiller) },
                { (CustomRoles.Enderman,         CustomWinner.Enderman),           AlivePlayersCount(CountTypes.Enderman) },
                { (CustomRoles.Mycologist,       CustomWinner.Mycologist),         AlivePlayersCount(CountTypes.Mycologist) },
                { (CustomRoles.Bubble,           CustomWinner.Bubble),             AlivePlayersCount(CountTypes.Bubble) },
                { (CustomRoles.Hookshot,         CustomWinner.Hookshot),           AlivePlayersCount(CountTypes.Hookshot) },
                { (CustomRoles.Sprayer,          CustomWinner.Sprayer),            AlivePlayersCount(CountTypes.Sprayer) },
                { (CustomRoles.PlagueDoctor,     CustomWinner.PlagueDoctor),       AlivePlayersCount(CountTypes.PlagueDoctor) },
                { (CustomRoles.WeaponMaster,     CustomWinner.WeaponMaster),       AlivePlayersCount(CountTypes.WeaponMaster) },
                { (CustomRoles.Magician,         CustomWinner.Magician),           AlivePlayersCount(CountTypes.Magician) },
                { (CustomRoles.Reckless,         CustomWinner.Reckless),           AlivePlayersCount(CountTypes.Reckless) },
                { (CustomRoles.Eclipse,          CustomWinner.Eclipse),            AlivePlayersCount(CountTypes.Eclipse) },
                { (CustomRoles.Pyromaniac,       CustomWinner.Pyromaniac),         AlivePlayersCount(CountTypes.Pyromaniac) },
                { (CustomRoles.HeadHunter,       CustomWinner.HeadHunter),         AlivePlayersCount(CountTypes.HeadHunter) },
                { (CustomRoles.Vengeance,        CustomWinner.Vengeance),          AlivePlayersCount(CountTypes.Vengeance) },
                { (CustomRoles.Imitator,         CustomWinner.Imitator),           AlivePlayersCount(CountTypes.Imitator) },
                { (CustomRoles.Werewolf,         CustomWinner.Werewolf),           AlivePlayersCount(CountTypes.Werewolf) },
                { (CustomRoles.RuthlessRomantic, CustomWinner.RuthlessRomantic),   AlivePlayersCount(CountTypes.RuthlessRomantic) },
                { (CustomRoles.Juggernaut,       CustomWinner.Juggernaut),         AlivePlayersCount(CountTypes.Juggernaut) },
                { (null,                         CustomWinner.Infectious),         AlivePlayersCount(CountTypes.Infectious) },
                { (null,                         CustomWinner.Virus),              AlivePlayersCount(CountTypes.Virus) },
                { (null,                         CustomWinner.Rogue),              AlivePlayersCount(CountTypes.Rogue) },
                { (CustomRoles.DarkHide,         CustomWinner.DarkHide),           AlivePlayersCount(CountTypes.DarkHide) },
                { (CustomRoles.Jinx,             CustomWinner.Jinx),               AlivePlayersCount(CountTypes.Jinx) },
                { (CustomRoles.Ritualist,        CustomWinner.Ritualist),          AlivePlayersCount(CountTypes.Ritualist) },
                { (CustomRoles.Pickpocket,       CustomWinner.Pickpocket),         AlivePlayersCount(CountTypes.Pickpocket) },
                { (CustomRoles.Traitor,          CustomWinner.Traitor),            AlivePlayersCount(CountTypes.Traitor) },
                { (CustomRoles.Medusa,           CustomWinner.Medusa),             AlivePlayersCount(CountTypes.Medusa) },
                { (null,                         CustomWinner.Spiritcaller),       AlivePlayersCount(CountTypes.Spiritcaller) },
                { (CustomRoles.Glitch,           CustomWinner.Glitch),             AlivePlayersCount(CountTypes.Glitch) },
                { (CustomRoles.Arsonist,         CustomWinner.Arsonist),           AlivePlayersCount(CountTypes.Arsonist) },
                { (CustomRoles.Bandit,           CustomWinner.Bandit),             AlivePlayersCount(CountTypes.Bandit) },
                { (CustomRoles.Doppelganger,     CustomWinner.Doppelganger),       AlivePlayersCount(CountTypes.Doppelganger) },
                { (CustomRoles.Agitater,         CustomWinner.Agitater),           AlivePlayersCount(CountTypes.Agitater) },
            };

            foreach (PlayerControl x in Main.AllAlivePlayerControls)
            {
                CustomRoles role = x.GetCustomRole();
                if ((x.Is(CustomRoles.Madmate) && x.Is(CustomRoles.DualPersonality)) ||
                    (role.IsImpostor() && x.Is(CustomRoles.DualPersonality))) Imp++;
                if (role.IsCrewmate() && x.Is(CustomRoles.DualPersonality)) Crew++;
                if (role.IsImpostor() && x.Is(CustomRoles.Admired) && x.Is(CustomRoles.DualPersonality)) Crew++;
                if (role.IsNeutral() && x.Is(CustomRoles.Admired) && x.Is(CustomRoles.DualPersonality)) Crew++;
                if (role.IsCrewmate() && x.Is(CustomRoles.Admired) && x.Is(CustomRoles.DualPersonality)) Crew++;
                if (x.Is(CustomRoles.Charmed) && x.Is(CustomRoles.DualPersonality)) roleCounts[(null, CustomWinner.Succubus)]++;
                if (x.Is(CustomRoles.Sidekick) && x.Is(CustomRoles.DualPersonality)) roleCounts[(null, CustomWinner.Jackal)]++;
                if (x.Is(CustomRoles.Recruit) && x.Is(CustomRoles.DualPersonality)) roleCounts[(null, CustomWinner.Jackal)]++;
                if (x.Is(CustomRoles.Infected) && x.Is(CustomRoles.DualPersonality)) roleCounts[(null, CustomWinner.Infectious)]++;
                if (x.Is(CustomRoles.Contagious) && x.Is(CustomRoles.DualPersonality)) roleCounts[(null, CustomWinner.Virus)]++;
            }

            int totalNKAlive = roleCounts.Values.Sum();

            CustomWinner? winner = null;
            CustomRoles? rl = null;

            if (Main.AllAlivePlayerControls.All(p => p.Is(CustomRoles.Lovers)))
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Lovers);
                return true;
            }

            else if (totalNKAlive == 0)
            {
                if (Crew == 0 && Imp == 0)
                {
                    reason = GameOverReason.ImpostorByKill;
                    winner = CustomWinner.None;
                }
                else if (Crew <= Imp)
                {
                    reason = GameOverReason.ImpostorByKill;
                    winner = CustomWinner.Impostor;
                }
                else if (Imp == 0)
                {
                    reason = GameOverReason.HumansByVote;
                    winner = CustomWinner.Crewmate;
                }
                else return false;
                if (winner != null) CustomWinnerHolder.ResetAndSetWinner((CustomWinner)winner);
                return true;
            }

            else
            {
                if (Imp >= 1) return false; // both imps and NKs are alive, game must continue
                else if (Crew > totalNKAlive) return false; // Imps are dead, but crew still outnumbers NKs, game must continue
                else // Imps dead, Crew <= NK, Checking if all NKs alive are in 1 team
                {
                    var aliveCounts = roleCounts.Values.Where(x => x > 0).ToList();
                    if (aliveCounts.Count > 1) return false; // There are multiple types of NKs alive, game must continue
                    else if (aliveCounts.Count == 1) // There is only one type of NK alive, they've won
                    {
                        if (aliveCounts[0] != roleCounts.Values.Max()) Logger.Warn("There is something wrong here.", "CheckGameEndPatch");
                        foreach (var keyValuePair in roleCounts.Where(keyValuePair => keyValuePair.Value == aliveCounts[0]))
                        {
                            reason = GameOverReason.ImpostorByKill;
                            winner = keyValuePair.Key.WINNER;
                            rl = keyValuePair.Key.ROLE;
                            break;
                        }
                    }
                    else
                    {
                        Logger.Fatal("Error while selecting NK winner", "CheckGameEndPatch.CheckGameEndByLivingPlayers");
                        Logger.SendInGame("There was an error while selecting the winner. Please report this bug to the developer! (Do /dump to get logs)");
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                        return true;
                    }

                    if (winner != null) CustomWinnerHolder.ResetAndSetWinner((CustomWinner)winner);
                    if (rl != null) CustomWinnerHolder.WinnerRoles.Add((CustomRoles)rl);
                    return true;
                }
            }
        }
    }

    // 个人竞技模式用
    class SoloKombatGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (CustomWinnerHolder.WinnerIds.Count > 0) return false;
            if (CheckGameEndByLivingPlayers(out reason)) return true;
            return false;
        }

        public static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (SoloKombatManager.RoundTime > 0) return false;

            var winner = Main.AllPlayerControls.FirstOrDefault(x => !x.Is(CustomRoles.GM) && SoloKombatManager.GetRankOfScore(x.PlayerId) == 1);

            CustomWinnerHolder.WinnerIds =
            [
                winner.PlayerId
            ];

            Main.DoBlockNameChange = true;

            return true;
        }
    }
    class FFAGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (CustomWinnerHolder.WinnerIds.Count > 0) return false;
            if (CheckGameEndByLivingPlayers(out reason)) return true;
            return false;
        }

        public static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (FFAManager.RoundTime <= 0)
            {
                var winner = Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && x != null).OrderBy(x => FFAManager.GetRankOfScore(x.PlayerId)).First();

                byte winnerId;
                if (winner == null) winnerId = 0;
                else winnerId = winner.PlayerId;

                Logger.Warn($"Winner: {GetPlayerById(winnerId).GetRealName().RemoveHtmlTags()}", "FFA");

                CustomWinnerHolder.WinnerIds =
                [
                    winnerId
                ];

                Main.DoBlockNameChange = true;

                return true;
            }
            else if (Main.AllAlivePlayerControls.Length == 1)
            {
                var winner = Main.AllAlivePlayerControls.FirstOrDefault();

                Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "FFA");

                CustomWinnerHolder.WinnerIds =
                [
                    winner.PlayerId
                ];

                Main.DoBlockNameChange = true;

                return true;
            }
            else if (Main.AllAlivePlayerControls.Length == 0)
            {
                FFAManager.RoundTime = 0;
                Logger.Warn("No players alive. Force ending the game", "FFA");
                return false;
            }
            else return false;
        }
    }
    class MoveAndStopGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (CustomWinnerHolder.WinnerIds.Count > 0) return false;
            if (CheckGameEndByLivingPlayers(out reason)) return true;
            return false;
        }

        public static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (MoveAndStopManager.RoundTime <= 0)
            {
                var winner = Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && x != null).OrderBy(x => MoveAndStopManager.GetRankOfScore(x.PlayerId)).First();

                byte winnerId;
                if (winner == null) winnerId = 0;
                else winnerId = winner.PlayerId;

                Logger.Warn($"Winner: {GetPlayerById(winnerId).GetRealName().RemoveHtmlTags()}", "MoveAndStop");

                CustomWinnerHolder.WinnerIds =
                [
                    winnerId
                ];

                Main.DoBlockNameChange = true;

                return true;
            }
            else if (Main.AllAlivePlayerControls.Any(x => x.GetTaskState().IsTaskFinished))
            {
                var winner = Main.AllAlivePlayerControls.FirstOrDefault(x => x.GetTaskState().IsTaskFinished);

                Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "MoveAndStop");

                CustomWinnerHolder.WinnerIds =
                [
                    winner.PlayerId
                ];

                Main.DoBlockNameChange = true;

                return true;
            }
            else if (Main.AllAlivePlayerControls.Length == 0)
            {
                MoveAndStopManager.RoundTime = 0;
                Logger.Warn("No players alive. Force ending the game", "MoveAndStop");
                return false;
            }
            else return false;
        }
    }

    public abstract class GameEndPredicate
    {
        /// <summary>Checks the game ending condition and stores the value in CustomWinnerHolder. </summary>
        /// <params name="reason">GameOverReason used for vanilla game end processing</params>
        /// <returns>Whether the conditions for ending the game are met</returns>
        public abstract bool CheckForEndGame(out GameOverReason reason);

        /// <summary>Determine whether the task can be won based on GameData.TotalTasks and CompletedTasks.</summary>
        public virtual bool CheckGameEndByTask(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (Options.DisableTaskWin.GetBool() || TaskState.InitialTotalTasks == 0) return false;

            if (GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
            {
                reason = GameOverReason.HumansByTask;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
                return true;
            }
            return false;
        }
        /// <summary>Determines whether sabotage victory is possible based on the elements in ShipStatus.Systems.</summary>
        public virtual bool CheckGameEndBySabotage(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (ShipStatus.Instance.Systems == null) return false;

            // TryGetValue is not available
            var systems = ShipStatus.Instance.Systems;
            LifeSuppSystemType LifeSupp;
            if (systems.ContainsKey(SystemTypes.LifeSupp) && // Confirmation of sabotage existence
                (LifeSupp = systems[SystemTypes.LifeSupp].TryCast<LifeSuppSystemType>()) != null && // Confirmation that cast is possible
                LifeSupp.Countdown < 0f) // Time up confirmation
            {
                // oxygen sabotage
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorBySabotage;
                LifeSupp.Countdown = 10000f;
                return true;
            }

            ISystemType sys = null;
            if (systems.ContainsKey(SystemTypes.Reactor)) sys = systems[SystemTypes.Reactor];
            else if (systems.ContainsKey(SystemTypes.Laboratory)) sys = systems[SystemTypes.Laboratory];
            else if (systems.ContainsKey(SystemTypes.HeliSabotage)) sys = systems[SystemTypes.HeliSabotage];

            ICriticalSabotage critical;
            if (sys != null && // Confirmation of sabotage existence
                (critical = sys.TryCast<ICriticalSabotage>()) != null && // Confirmation that cast is possible
                critical.Countdown < 0f) // Time up confirmation
            {
                // reactor sabotage
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorBySabotage;
                critical.ClearSabotage();
                return true;
            }

            return false;
        }
    }
}