using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using InnerNet;
using TOHE.Modules;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.CustomWinnerHolder;
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

        if (Options.NoGameEnd.GetBool() && WinnerTeam is not CustomWinner.Draw and not CustomWinner.Error) return false;

        predicate.CheckForEndGame(out GameOverReason reason);

        if (Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato)
        {
            if (WinnerIds.Count > 0 || WinnerTeam != CustomWinner.Default)
            {
                ShipStatus.Instance.enabled = false;
                StartEndGame(reason);
                predicate = null;
            }

            return false;
        }

        if (WinnerTeam != CustomWinner.Default)
        {
            NameNotifyManager.Reset();
            NotifyRoles(ForceLoop: true);

            Main.AllPlayerControls.Do(pc => Camouflage.RpcSetSkin(pc, ForceRevert: true, RevertToDefault: true));

            if (reason == GameOverReason.ImpostorBySabotage && (CustomRoles.Jackal.RoleExist() || CustomRoles.Sidekick.RoleExist()) && Jackal.CanWinBySabotageWhenNoImpAlive.GetBool() && !Main.AllAlivePlayerControls.Any(x => x.GetCustomRole().IsImpostorTeam()))
            {
                reason = GameOverReason.ImpostorByKill;
                WinnerIds.Clear();
                ResetAndSetWinner(CustomWinner.Jackal);
                WinnerRoles.Add(CustomRoles.Jackal);
            }

            switch (WinnerTeam)
            {
                case CustomWinner.Crewmate:
                    Main.AllPlayerControls
                        .Where(pc => (pc.Is(CustomRoleTypes.Crewmate) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Madmate) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.EvilSpirit) && !pc.Is(CustomRoles.Recruit)))
                        .Do(pc => WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Impostor:
                    Main.AllPlayerControls
                        .Where(pc => ((pc.Is(CustomRoleTypes.Impostor) && (!pc.Is(CustomRoles.DeadlyQuota) || Main.PlayerStates.Count(x => x.Value.GetRealKiller() == pc.PlayerId) >= Options.DQNumOfKillsNeeded.GetInt())) || pc.Is(CustomRoles.Madmate) || pc.Is(CustomRoles.Crewpostor) || pc.Is(CustomRoles.Refugee)) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.EvilSpirit) && !pc.Is(CustomRoles.Recruit))
                        .Do(pc => WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Succubus:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Succubus) || (pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Rogue)))
                        .Do(pc => WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Necromancer:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Necromancer) || pc.Is(CustomRoles.Deathknight) || pc.Is(CustomRoles.Undead) && !pc.Is(CustomRoles.Rogue))
                        .Do(pc => WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Virus:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Virus) || (pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.Rogue)))
                        .Do(pc => WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Jackal:
                    Main.AllPlayerControls
                        .Where(pc => (pc.Is(CustomRoles.Jackal) || pc.Is(CustomRoles.Sidekick) || pc.Is(CustomRoles.Recruit)) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Rogue))
                        .Do(pc => WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Spiritcaller:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Spiritcaller) || pc.Is(CustomRoles.EvilSpirit))
                        .Do(pc => WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.RuthlessRomantic:
                    WinnerIds.Add(Romantic.PartnerId);
                    break;
            }

            if (WinnerTeam is not CustomWinner.Draw and not CustomWinner.None and not CustomWinner.Error)
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    switch (pc.GetCustomRole())
                    {
                        case CustomRoles.DarkHide when !pc.Data.IsDead && ((WinnerTeam == CustomWinner.Impostor && !reason.Equals(GameOverReason.ImpostorBySabotage)) || WinnerTeam == CustomWinner.DarkHide || (WinnerTeam == CustomWinner.Crewmate && !reason.Equals(GameOverReason.HumansByTask) && Main.PlayerStates[pc.PlayerId].Role is DarkHide { IsWinKill: true } && DarkHide.SnatchesWin.GetBool())):
                            ResetAndSetWinner(CustomWinner.DarkHide);
                            WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Phantom when pc.GetTaskState().IsTaskFinished && pc.Data.IsDead && Options.PhantomSnatchesWin.GetBool():
                            reason = GameOverReason.ImpostorByKill;
                            ResetAndSetWinner(CustomWinner.Phantom);
                            WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Phantom when !Options.PhantomSnatchesWin.GetBool() && !pc.IsAlive() && pc.GetTaskState().IsTaskFinished:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Phantom);
                            break;
                        case CustomRoles.Opportunist when pc.IsAlive():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Opportunist);
                            break;
                        case CustomRoles.Pursuer when pc.IsAlive() && WinnerTeam is not CustomWinner.Jester and not CustomWinner.Lovers and not CustomWinner.Terrorist and not CustomWinner.Executioner and not CustomWinner.Collector and not CustomWinner.Innocent and not CustomWinner.Youtuber:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Pursuer);
                            break;
                        case CustomRoles.Sunnyboy when !pc.IsAlive():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Sunnyboy);
                            break;
                        case CustomRoles.Maverick when pc.IsAlive() && Main.PlayerStates[pc.PlayerId].Role is Maverick mr && mr.NumOfKills >= Maverick.MinKillsToWin.GetInt():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Maverick);
                            break;
                        case CustomRoles.Provocateur when Main.Provoked.TryGetValue(pc.PlayerId, out var tar) && !WinnerIds.Contains(tar):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Provocateur);
                            break;
                        case CustomRoles.FFF when (Main.PlayerStates[pc.PlayerId].Role as FFF).isWon:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.FFF);
                            break;
                        case CustomRoles.Totocalcio when Main.PlayerStates[pc.PlayerId].Role is Totocalcio tc && tc.BetPlayer != byte.MaxValue && (WinnerIds.Contains(tc.BetPlayer) || (Main.PlayerStates.TryGetValue(tc.BetPlayer, out var ps) && WinnerRoles.Contains(ps.MainRole))):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Totocalcio);
                            break;
                        case CustomRoles.Romantic when WinnerIds.Contains(Romantic.PartnerId) || (Main.PlayerStates.TryGetValue(Romantic.PartnerId, out var ps) && WinnerRoles.Contains(ps.MainRole)):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Romantic);
                            break;
                        case CustomRoles.VengefulRomantic when VengefulRomantic.HasKilledKiller:
                            WinnerIds.Add(pc.PlayerId);
                            WinnerIds.Add(Romantic.PartnerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.VengefulRomantic);
                            break;
                        case CustomRoles.Lawyer when Lawyer.Target.TryGetValue(pc.PlayerId, out var lawyertarget) && (WinnerIds.Contains(lawyertarget) || (Main.PlayerStates.TryGetValue(lawyertarget, out var ps) && WinnerRoles.Contains(ps.MainRole))):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Lawyer);
                            break;
                        case CustomRoles.Postman when (Main.PlayerStates[pc.PlayerId].Role as Postman).IsFinished:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Postman);
                            break;
                        case CustomRoles.SoulHunter when (Main.PlayerStates[pc.PlayerId].Role as SoulHunter).Souls >= SoulHunter.NumOfSoulsToWin.GetInt():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.SoulHunter);
                            break;
                    }
                }

                if (WinnerTeam == CustomWinner.Impostor)
                {
                    var aliveImps = Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoleTypes.Impostor));
                    var imps = aliveImps as PlayerControl[] ?? aliveImps.ToArray();
                    var aliveImpCount = imps.Length;

                    switch (aliveImpCount)
                    {
                        // If there's an Egoist, and there is at least 1 non-Egoist impostor alive, Egoist loses
                        case > 1 when WinnerIds.Any(x => GetPlayerById(x).Is(CustomRoles.Egoist)):
                            WinnerIds.RemoveWhere(x => GetPlayerById(x).Is(CustomRoles.Egoist));
                            break;
                        // If there's only 1 impostor alive, and all alive impostors are Egoists, the Egoist wins alone
                        case 1 when imps.All(x => x.Is(CustomRoles.Egoist)):
                        {
                            var pc = imps.FirstOrDefault();
                            reason = GameOverReason.ImpostorByKill;
                            ResetAndSetWinner(CustomWinner.Egoist);
                            WinnerIds.Add(pc.PlayerId);
                            break;
                        }
                    }
                }

                if (CustomRoles.God.RoleExist())
                {
                    ResetAndSetWinner(CustomWinner.God);
                    Main.AllPlayerControls
                        .Where(p => p.Is(CustomRoles.God) && p.IsAlive())
                        .Do(p => WinnerIds.Add(p.PlayerId));
                }

                if (WinnerIds.Any(x => GetPlayerById(x).Is(CustomRoles.Lovers)))
                {
                    AdditionalWinnerTeams.Add(AdditionalWinners.Lovers);
                    Main.AllPlayerControls
                        .Where(p => p.Is(CustomRoles.Lovers))
                        .Do(p => WinnerIds.Add(p.PlayerId));
                }

                if (Options.NeutralWinTogether.GetBool() && WinnerIds.Any(x => GetPlayerById(x) != null && GetPlayerById(x).GetCustomRole().IsNeutral()))
                {
                    foreach (PlayerControl pc in Main.AllPlayerControls)
                    {
                        if (pc.GetCustomRole().IsNeutral())
                            WinnerIds.Add(pc.PlayerId);
                    }
                }
                else if (Options.NeutralRoleWinTogether.GetBool())
                {
                    foreach (var id in WinnerIds)
                    {
                        var pc = GetPlayerById(id);
                        if (pc == null || !pc.GetCustomRole().IsNeutral()) continue;
                        foreach (PlayerControl tar in Main.AllPlayerControls)
                        {
                            if (!WinnerIds.Contains(tar.PlayerId) && tar.GetCustomRole() == pc.GetCustomRole())
                                WinnerIds.Add(tar.PlayerId);
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

    private static IEnumerator CoEndGame(InnerNetClient self, GameOverReason reason)
    {
        Blackmailer.ForBlackmailer.Clear();

        // Set ghost role
        List<byte> ReviveRequiredPlayerIds = [];
        var winner = WinnerTeam;
        foreach (var pc in Main.AllPlayerControls)
        {
            if (winner == CustomWinner.Draw)
            {
                SetGhostRole(ToGhostImpostor: true);
                continue;
            }

            bool canWin = WinnerIds.Contains(pc.PlayerId) || WinnerRoles.Contains(pc.GetCustomRole());
            bool isCrewmateWin = reason.Equals(GameOverReason.HumansByVote) || reason.Equals(GameOverReason.HumansByTask);
            SetGhostRole(ToGhostImpostor: canWin ^ isCrewmateWin); // XOR

            SetEverythingUpPatch.LastWinsReason = winner is CustomWinner.Crewmate or CustomWinner.Impostor ? GetString($"GameOverReason.{reason}") : string.Empty;
            continue;

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
        }

        // Sync of CustomWinnerHolder info
        var winnerWriter = self.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.EndGame, SendOption.Reliable);
        WriteTo(winnerWriter);
        self.FinishRpcImmediately(winnerWriter);

        // Delay to ensure that resuscitation is delivered after the ghost roll setting
        yield return new WaitForSeconds(EndGameDelay);

        if (ReviveRequiredPlayerIds.Count > 0)
        {
            // Resuscitation Resuscitate one person per transmission to prevent the packet from swelling up and dying
            foreach (var playerId in ReviveRequiredPlayerIds)
            {
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
    public static void SetPredicateToHotPotato() => predicate = new HotPotatoGameEndPredicate();

    class NormalGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (WinnerTeam != CustomWinner.Default) return false;
            return CheckGameEndBySabotage(out reason) || CheckGameEndByTask(out reason) || CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (CustomRoles.Sunnyboy.RoleExist() && Main.AllAlivePlayerControls.Length > 1) return false;

            int Imp = AlivePlayersCount(CountTypes.Impostor);
            int Crew = AlivePlayersCount(CountTypes.Crew);

            Dictionary<(CustomRoles? ROLE, CustomWinner WINNER), int> roleCounts = [];

            foreach (var role in EnumHelper.GetAllValues<CustomRoles>())
            {
                if (!role.IsNK() || role.IsMadmate() || role is CustomRoles.Sidekick || (role == CustomRoles.Arsonist && !Options.ArsonistCanIgniteAnytime.GetBool()) || (role == CustomRoles.DarkHide && DarkHide.SnatchesWin.GetBool())) continue;

                CustomRoles? keyRole = role.IsRecruitingRole() ? null : role;
                CustomWinner keyWinner = (CustomWinner)role;
                int value = AlivePlayersCount(role.GetCountTypes());

                roleCounts[(keyRole, keyWinner)] = value;
            }

            if (CustomRoles.DualPersonality.IsEnable())
            {
                foreach (PlayerControl x in Main.AllAlivePlayerControls)
                {
                    CustomRoles role = x.GetCustomRole();
                    if ((x.Is(CustomRoles.Madmate) && x.Is(CustomRoles.DualPersonality)) ||
                        (role.IsImpostor() && x.Is(CustomRoles.DualPersonality))) Imp++;
                    if (role.IsCrewmate() && x.Is(CustomRoles.DualPersonality)) Crew++;
                    if (x.Is(CustomRoles.Charmed) && x.Is(CustomRoles.DualPersonality)) roleCounts[(null, CustomWinner.Succubus)]++;
                    if (x.Is(CustomRoles.Undead) && x.Is(CustomRoles.DualPersonality)) roleCounts[(null, CustomWinner.Necromancer)]++;
                    if (x.Is(CustomRoles.Sidekick) && x.Is(CustomRoles.DualPersonality)) roleCounts[(null, CustomWinner.Jackal)]++;
                    if (x.Is(CustomRoles.Recruit) && x.Is(CustomRoles.DualPersonality)) roleCounts[(null, CustomWinner.Jackal)]++;
                    if (x.Is(CustomRoles.Contagious) && x.Is(CustomRoles.DualPersonality)) roleCounts[(null, CustomWinner.Virus)]++;
                }
            }

            int totalNKAlive = roleCounts.Values.Sum();

            CustomWinner? winner = null;
            CustomRoles? rl = null;

            if (Main.AllAlivePlayerControls.All(p => p.Is(CustomRoles.Lovers)))
            {
                reason = GameOverReason.ImpostorByKill;
                ResetAndSetWinner(CustomWinner.Lovers);
                return true;
            }

            if (totalNKAlive == 0)
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

                ResetAndSetWinner((CustomWinner)winner);
                return true;
            }

            if (Imp >= 1) return false; // both imps and NKs are alive, game must continue
            if (Crew > totalNKAlive) return false; // Imps are dead, but crew still outnumbers NKs, game must continue
            // Imps dead, Crew <= NK, Checking if all NKs alive are in 1 team
            var aliveCounts = roleCounts.Values.Where(x => x > 0).ToList();
            switch (aliveCounts.Count)
            {
                case > 1:
                    return false; // There are multiple types of NKs alive, game must continue
                // There is only one type of NK alive, they've won
                case 1:
                {
                    if (aliveCounts[0] != roleCounts.Values.Max()) Logger.Warn("There is something wrong here.", "CheckGameEndPatch");
                    foreach (var keyValuePair in roleCounts.Where(keyValuePair => keyValuePair.Value == aliveCounts[0]))
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = keyValuePair.Key.WINNER;
                        rl = keyValuePair.Key.ROLE;
                        break;
                    }

                    break;
                }
                default:
                    Logger.Fatal("Error while selecting NK winner", "CheckGameEndPatch.CheckGameEndByLivingPlayers");
                    Logger.SendInGame("There was an error while selecting the winner. Please report this bug to the developer! (Do /dump to get logs)");
                    ResetAndSetWinner(CustomWinner.Error);
                    return true;
            }

            if (winner != null) ResetAndSetWinner((CustomWinner)winner);
            if (rl != null) WinnerRoles.Add((CustomRoles)rl);
            return true;
        }
    }

    class SoloKombatGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (SoloKombatManager.RoundTime > 0) return false;

            var winner = Main.AllPlayerControls.FirstOrDefault(x => !x.Is(CustomRoles.GM) && SoloKombatManager.GetRankOfScore(x.PlayerId) == 1) ?? Main.AllAlivePlayerControls[0];

            WinnerIds =
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
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (FFAManager.RoundTime <= 0)
            {
                var winner = Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && x != null).OrderBy(x => FFAManager.GetRankOfScore(x.PlayerId)).First();

                byte winnerId = winner.PlayerId;

                Logger.Warn($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "FFA");

                WinnerIds =
                [
                    winnerId
                ];

                Main.DoBlockNameChange = true;

                return true;
            }

            switch (Main.AllAlivePlayerControls.Length)
            {
                case 1:
                {
                    var winner = Main.AllAlivePlayerControls.First();

                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "FFA");

                    WinnerIds =
                    [
                        winner.PlayerId
                    ];

                    Main.DoBlockNameChange = true;

                    return true;
                }
                case 0:
                    FFAManager.RoundTime = 0;
                    Logger.Warn("No players alive. Force ending the game", "FFA");
                    return false;
                default:
                    return false;
            }
        }
    }

    class MoveAndStopGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (MoveAndStopManager.RoundTime <= 0)
            {
                var winner = Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && x != null).OrderBy(x => MoveAndStopManager.GetRankOfScore(x.PlayerId)).ThenBy(x => x.IsAlive()).First();

                byte winnerId = winner.PlayerId;

                Logger.Warn($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "MoveAndStop");

                WinnerIds =
                [
                    winnerId
                ];

                Main.DoBlockNameChange = true;

                return true;
            }

            if (Main.AllAlivePlayerControls.Any(x => x.GetTaskState().IsTaskFinished))
            {
                var winner = Main.AllAlivePlayerControls.First(x => x.GetTaskState().IsTaskFinished);

                Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "MoveAndStop");

                WinnerIds =
                [
                    winner.PlayerId
                ];

                Main.DoBlockNameChange = true;

                return true;
            }

            if (Main.AllAlivePlayerControls.Length == 0)
            {
                MoveAndStopManager.RoundTime = 0;
                Logger.Warn("No players alive. Force ending the game", "MoveAndStop");
                return false;
            }

            return false;
        }
    }

    class HotPotatoGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            switch (Main.AllAlivePlayerControls.Length)
            {
                case 1:
                    var winner = Main.AllAlivePlayerControls[0];
                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "HotPotato");
                    WinnerIds = [winner.PlayerId];
                    Main.DoBlockNameChange = true;
                    return true;
                case 0:
                    ResetAndSetWinner(CustomWinner.Error);
                    Logger.Warn("No players alive. Force ending the game", "HotPotato");
                    return false;
                default:
                    return false;
            }
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
            if (Options.DisableTaskWinIfAllCrewsAreDead.GetBool() && !Main.AllAlivePlayerControls.Any(x => x.Is(CustomRoleTypes.Crewmate))) return false;

            if (GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
            {
                reason = GameOverReason.HumansByTask;
                ResetAndSetWinner(CustomWinner.Crewmate);
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
                LifeSupp.Countdown <= 0f) // Time up confirmation
            {
                // oxygen sabotage
                ResetAndSetWinner(CustomWinner.Impostor);
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
                critical.Countdown <= 0f) // Time up confirmation
            {
                // reactor sabotage
                ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorBySabotage;
                critical.ClearSabotage();
                return true;
            }

            return false;
        }
    }
}