using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using EHR.AddOns.GhostRoles;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using static EHR.CustomWinnerHolder;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR;

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
class GameEndChecker
{
    private const float EndGameDelay = 0.2f;
    public static GameEndPredicate Predicate;
    public static bool ShouldNotCheck = false;

    public static bool Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (Predicate == null || ShouldNotCheck) return false;

        if (Options.NoGameEnd.GetBool() && WinnerTeam is not CustomWinner.Draw and not CustomWinner.Error) return false;

        Predicate.CheckForEndGame(out GameOverReason reason);

        if (Options.CurrentGameMode != CustomGameMode.Standard)
        {
            if (WinnerIds.Count > 0 || WinnerTeam != CustomWinner.Default)
            {
                ShipStatus.Instance.enabled = false;
                StartEndGame(reason);
                Predicate = null;
            }

            return false;
        }

        if (WinnerTeam != CustomWinner.Default)
        {
            NameNotifyManager.Reset();
            NotifyRoles(ForceLoop: true);

            Main.AllPlayerControls.Do(pc => Camouflage.RpcSetSkin(pc, ForceRevert: true, RevertToDefault: true, GameEnd: true));

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
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoleTypes.Crewmate) && !pc.Is(CustomRoles.Madmate) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.EvilSpirit))
                        .Select(pc => pc.PlayerId));
                    break;
                case CustomWinner.Impostor:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => ((pc.Is(CustomRoleTypes.Impostor) && (!pc.Is(CustomRoles.DeadlyQuota) || Main.PlayerStates.Count(x => x.Value.GetRealKiller() == pc.PlayerId) >= Options.DQNumOfKillsNeeded.GetInt())) || pc.Is(CustomRoles.Madmate) || pc.Is(CustomRoles.Crewpostor) || pc.Is(CustomRoles.Refugee)) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.EvilSpirit))
                        .Select(pc => pc.PlayerId));
                    break;
                case CustomWinner.Succubus:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Succubus) || (pc.Is(CustomRoles.Charmed)))
                        .Select(pc => pc.PlayerId));
                    break;
                case CustomWinner.Necromancer:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Necromancer) || pc.Is(CustomRoles.Deathknight) || pc.Is(CustomRoles.Undead))
                        .Select(pc => pc.PlayerId));
                    break;
                case CustomWinner.Virus:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Virus) || (pc.Is(CustomRoles.Contagious)))
                        .Select(pc => pc.PlayerId));
                    break;
                case CustomWinner.Jackal:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => (pc.Is(CustomRoles.Jackal) || pc.Is(CustomRoles.Sidekick) || pc.Is(CustomRoles.Recruit)))
                        .Select(pc => pc.PlayerId));
                    break;
                case CustomWinner.Spiritcaller:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Spiritcaller) || pc.Is(CustomRoles.EvilSpirit))
                        .Select(pc => pc.PlayerId));
                    WinnerRoles.Add(CustomRoles.Spiritcaller);
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
                        case CustomRoles.Phantasm when pc.GetTaskState().IsTaskFinished && pc.Data.IsDead && Options.PhantomSnatchesWin.GetBool():
                            reason = GameOverReason.ImpostorByKill;
                            ResetAndSetWinner(CustomWinner.Phantom);
                            WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Phantasm when !Options.PhantomSnatchesWin.GetBool() && !pc.IsAlive() && pc.GetTaskState().IsTaskFinished:
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
                        case CustomRoles.Provocateur when Provocateur.Provoked.TryGetValue(pc.PlayerId, out var tar) && !WinnerIds.Contains(tar):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Provocateur);
                            break;
                        case CustomRoles.FFF when (Main.PlayerStates[pc.PlayerId].Role as FFF).IsWon:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.FFF);
                            break;
                        case CustomRoles.Totocalcio when Main.PlayerStates[pc.PlayerId].Role is Totocalcio tc && tc.BetPlayer != byte.MaxValue && (WinnerIds.Contains(tc.BetPlayer) || (Main.PlayerStates.TryGetValue(tc.BetPlayer, out var ps) && (WinnerRoles.Contains(ps.MainRole) || (WinnerTeam == CustomWinner.Bloodlust && ps.SubRoles.Contains(CustomRoles.Bloodlust))))):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Totocalcio);
                            break;
                        case CustomRoles.Romantic when WinnerIds.Contains(Romantic.PartnerId) || (Main.PlayerStates.TryGetValue(Romantic.PartnerId, out var ps) && (WinnerRoles.Contains(ps.MainRole) || (WinnerTeam == CustomWinner.Bloodlust && ps.SubRoles.Contains(CustomRoles.Bloodlust)))):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Romantic);
                            break;
                        case CustomRoles.VengefulRomantic when VengefulRomantic.HasKilledKiller:
                            WinnerIds.Add(pc.PlayerId);
                            WinnerIds.Add(Romantic.PartnerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.VengefulRomantic);
                            break;
                        case CustomRoles.Lawyer when Lawyer.Target.TryGetValue(pc.PlayerId, out var lawyertarget) && (WinnerIds.Contains(lawyertarget) || (Main.PlayerStates.TryGetValue(lawyertarget, out var ps) && (WinnerRoles.Contains(ps.MainRole) || (WinnerTeam == CustomWinner.Bloodlust && ps.SubRoles.Contains(CustomRoles.Bloodlust))))):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Lawyer);
                            break;
                        case CustomRoles.Postman when (Main.PlayerStates[pc.PlayerId].Role as Postman).IsFinished:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Postman);
                            break;
                        case CustomRoles.Impartial when (Main.PlayerStates[pc.PlayerId].Role as Impartial).IsWon:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Impartial);
                            break;
                        case CustomRoles.Predator when (Main.PlayerStates[pc.PlayerId].Role as Predator).IsWon:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Predator);
                            break;
                        case CustomRoles.SoulHunter when (Main.PlayerStates[pc.PlayerId].Role as SoulHunter).Souls >= SoulHunter.NumOfSoulsToWin.GetInt():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.SoulHunter);
                            break;
                        case CustomRoles.SchrodingersCat when WinnerTeam == CustomWinner.Crewmate && SchrodingersCat.WinsWithCrewIfNotAttacked.GetBool():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.SchrodingersCat);
                            break;
                        case CustomRoles.SchrodingersCat:
                            WinnerIds.Remove(pc.PlayerId);
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

                var winningSpecters = GhostRolesManager.AssignedGhostRoles.Where(x => x.Value.Instance is Specter { IsWon: true }).Select(x => x.Key).ToArray();
                if (winningSpecters.Length > 0)
                {
                    AdditionalWinnerTeams.Add(AdditionalWinners.Specter);
                    WinnerIds.UnionWith(winningSpecters);
                }

                if (CustomRoles.God.RoleExist())
                {
                    ResetAndSetWinner(CustomWinner.God);
                    Main.AllPlayerControls
                        .Where(p => p.Is(CustomRoles.God) && p.IsAlive())
                        .Do(p => WinnerIds.Add(p.PlayerId));
                }

                if (WinnerTeam != CustomWinner.CustomTeam && CustomTeamManager.EnabledCustomTeams.Count > 0)
                {
                    Main.AllPlayerControls
                        .Select(x => new { Team = CustomTeamManager.GetCustomTeam(x.PlayerId), Player = x })
                        .Where(x => x.Team != null)
                        .GroupBy(x => x.Team)
                        .ToDictionary(x => x.Key, x => x.Select(y => y.Player.PlayerId))
                        .Do(x =>
                        {
                            bool canWin = CustomTeamManager.IsSettingEnabledForTeam(x.Key, CTAOption.WinWithOriginalTeam);
                            if (!canWin) WinnerIds.ExceptWith(x.Value);
                            else WinnerIds.UnionWith(x.Value);
                        });
                }

                if ((WinnerTeam == CustomWinner.Lovers || WinnerIds.Any(x => Main.PlayerStates[x].SubRoles.Contains(CustomRoles.Lovers))) && Main.LoversPlayers.All(x => x.IsAlive()) && reason != GameOverReason.HumansByTask)
                {
                    if (WinnerTeam != CustomWinner.Lovers) AdditionalWinnerTeams.Add(AdditionalWinners.Lovers);
                    WinnerIds.UnionWith(Main.LoversPlayers.Select(x => x.PlayerId));
                }

                if (Options.NeutralWinTogether.GetBool() && (WinnerRoles.Any(x => x.IsNeutral()) || WinnerIds.Select(x => GetPlayerById(x)).Any(x => x != null && x.GetCustomRole().IsNeutral() && !x.IsMadmate())))
                {
                    WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.GetCustomRole().IsNeutral()).Select(x => x.PlayerId));
                }
                else if (Options.NeutralRoleWinTogether.GetBool())
                {
                    foreach (var id in WinnerIds.ToArray())
                    {
                        var pc = GetPlayerById(id);
                        if (pc == null || !pc.GetCustomRole().IsNeutral()) continue;
                        foreach (PlayerControl tar in Main.AllPlayerControls)
                        {
                            if (!WinnerIds.Contains(tar.PlayerId) && tar.GetCustomRole() == pc.GetCustomRole())
                                WinnerIds.Add(tar.PlayerId);
                        }
                    }

                    foreach (var role in WinnerRoles)
                    {
                        WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.GetCustomRole() == role).Select(x => x.PlayerId));
                    }
                }

                WinnerIds.RemoveWhere(x => Main.PlayerStates[x].MainRole == CustomRoles.Shifter);
            }

            Camouflage.BlockCamouflage = true;
            ShipStatus.Instance.enabled = false;
            StartEndGame(reason);
            Predicate = null;
        }

        return false;
    }

    private static void StartEndGame(GameOverReason reason)
    {
        AmongUsClient.Instance.StartCoroutine(CoEndGame(AmongUsClient.Instance, reason).WrapToIl2Cpp());
    }

    private static IEnumerator CoEndGame(InnerNetClient self, GameOverReason reason)
    {
        Silencer.ForSilencer.Clear();

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

            bool canWin = WinnerIds.Contains(pc.PlayerId) || WinnerRoles.Contains(pc.GetCustomRole()) || (winner == CustomWinner.Bloodlust && pc.Is(CustomRoles.Bloodlust));
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
                playerInfo.SetDirtyBit(0b_1u << playerId);
                AmongUsClient.Instance.SendAllStreamedObjects();
            }

            // Delay to ensure that the end of the game is delivered at the end of the game
            yield return new WaitForSeconds(EndGameDelay);
        }

        // Start End Game
        GameManager.Instance.RpcEndGame(reason, false);
    }

    public static void SetPredicateToNormal() => Predicate = new NormalGameEndPredicate();
    public static void SetPredicateToSoloKombat() => Predicate = new SoloKombatGameEndPredicate();
    public static void SetPredicateToFFA() => Predicate = new FFAGameEndPredicate();
    public static void SetPredicateToMoveAndStop() => Predicate = new MoveAndStopGameEndPredicate();
    public static void SetPredicateToHotPotato() => Predicate = new HotPotatoGameEndPredicate();
    public static void SetPredicateToSpeedrun() => Predicate = new SpeedrunGameEndPredicate();
    public static void SetPredicateToHideAndSeek() => Predicate = new HideAndSeekGameEndPredicate();

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

            if (CustomTeamManager.CheckCustomTeamGameEnd()) return true;

            if (Main.AllAlivePlayerControls.All(x => Main.LoversPlayers.Any(l => l.PlayerId == x.PlayerId)) && !Main.LoversPlayers.All(x => x.Is(Team.Crewmate)))
            {
                ResetAndSetWinner(CustomWinner.Lovers);
                return true;
            }

            var sheriffCount = AlivePlayersCount(CountTypes.Sheriff);

            int Imp = AlivePlayersCount(CountTypes.Impostor);
            int Crew = AlivePlayersCount(CountTypes.Crew) + sheriffCount;

            Dictionary<(CustomRoles? ROLE, CustomWinner WINNER), int> roleCounts = [];

            foreach (var role in Enum.GetValues<CustomRoles>())
            {
                if ((!role.IsNK() && role != CustomRoles.Bloodlust) || role.IsMadmate() || role is CustomRoles.Sidekick) continue;

                var countTypes = role.GetCountTypes();
                if (countTypes is CountTypes.Crew or CountTypes.Impostor or CountTypes.None or CountTypes.OutOfGame) continue;

                CustomRoles? keyRole = role.IsRecruitingRole() ? null : role;
                CustomWinner keyWinner = (CustomWinner)role;
                int value = AlivePlayersCount(countTypes);

                roleCounts[(keyRole, keyWinner)] = value;
            }

            if (CustomRoles.DualPersonality.IsEnable())
            {
                foreach (PlayerControl x in Main.AllAlivePlayerControls)
                {
                    if (!x.Is(CustomRoles.DualPersonality)) continue;

                    CustomRoles role = x.GetCustomRole();

                    if (role.Is(Team.Crewmate)) Crew++;
                    if (role.Is(Team.Impostor)) Imp++;

                    if (x.Is(CustomRoles.Charmed)) roleCounts[(null, CustomWinner.Succubus)]++;
                    if (x.Is(CustomRoles.Undead)) roleCounts[(null, CustomWinner.Necromancer)]++;
                    if (x.Is(CustomRoles.Sidekick)) roleCounts[(null, CustomWinner.Jackal)]++;
                    if (x.Is(CustomRoles.Recruit)) roleCounts[(null, CustomWinner.Jackal)]++;
                    if (x.Is(CustomRoles.Contagious)) roleCounts[(null, CustomWinner.Virus)]++;
                }
            }

            int totalNKAlive = roleCounts.Values.Sum();

            CustomWinner? winner = null;
            CustomRoles? rl = null;

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
                // There are multiple types of NKs alive, game must continue
                case > 1:
                    return false;
                // If the Sheriff keeps the game going, the game must continue
                case 1 when Sheriff.KeepsGameGoing.GetBool() && sheriffCount > 0:
                    return false;
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
                var winner = Main.GM.Value && Main.AllPlayerControls.Length == 1 ? PlayerControl.LocalPlayer : Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && x != null).OrderBy(x => FFAManager.GetRankOfScore(x.PlayerId)).First();

                byte winnerId = winner.PlayerId;

                Logger.Warn($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "FFA");

                WinnerIds = [winnerId];

                Main.DoBlockNameChange = true;

                return true;
            }

            if (FFAManager.FFATeamMode.GetBool())
            {
                var teams = FFAManager.PlayerTeams.GroupBy(x => x.Value, x => x.Key).Select(x => x.Where(p =>
                {
                    var pc = GetPlayerById(p);
                    return pc != null && !pc.Data.Disconnected;
                }).ToHashSet()).Where(x => x.Count > 0);

                foreach (var team in teams)
                {
                    if (Main.AllAlivePlayerControls.All(x => team.Contains(x.PlayerId)))
                    {
                        WinnerIds = team;

                        Main.DoBlockNameChange = true;
                        return true;
                    }
                }
            }

            switch (Main.AllAlivePlayerControls.Length)
            {
                case 1:
                {
                    var winner = Main.AllAlivePlayerControls[0];

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
                var winner = Main.GM.Value && Main.AllPlayerControls.Length == 1 ? PlayerControl.LocalPlayer : Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && x != null).OrderBy(x => MoveAndStopManager.GetRankOfScore(x.PlayerId)).ThenByDescending(x => x.IsAlive()).First();

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
                    return true;
                default:
                    return false;
            }
        }
    }

    class HideAndSeekGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return HnSManager.CheckForGameEnd(out reason);
        }
    }

    class SpeedrunGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return SpeedrunManager.CheckForGameEnd(out reason);
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
            if (Options.DisableTaskWinIfAllCrewsAreConverted.GetBool() && Main.AllPlayerControls.Where(x => x.Is(Team.Crewmate) && x.GetRoleTypes() is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Scientist or RoleTypes.Noisemaker or RoleTypes.Tracker or RoleTypes.CrewmateGhost or RoleTypes.GuardianAngel).All(x => x.GetCustomSubRoles().Any(y => y.IsConverted()))) return false;

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

[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckEndGameViaTasks))]
class CheckGameEndPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (GameEndChecker.ShouldNotCheck)
        {
            __result = false;
            return false;
        }

        __result = GameEndChecker.Predicate?.CheckGameEndByTask(out _) ?? false;
        return false;
    }
}