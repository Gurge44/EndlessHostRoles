using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using EHR.AddOns.Common;
using EHR.AddOns.GhostRoles;
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

[HarmonyPatch(typeof(GameManager), nameof(GameManager.RpcEndGame))]
static class RpcEndGamePatch
{
    public static bool Prefix() => false;
}

[HarmonyPatch(typeof(LogicGameFlowHnS), nameof(LogicGameFlowHnS.CheckEndCriteria))]
static class HnsEndPatch
{
    public static bool Prefix() => false;
}

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
internal static class GameEndChecker
{
    private const float EndGameDelay = 0.2f;
    public static GameEndPredicate Predicate;
    public static bool ShouldNotCheck = false;
    public static bool Ended;
    public static bool LoadingEndScreen;

    public static bool Prefix()
    {
        return !AmongUsClient.Instance.AmHost;
    }

    public static void CheckCustomEndCriteria()
    {
        if (Predicate == null || ShouldNotCheck || Main.HasJustStarted) return;
        if (Options.NoGameEnd.GetBool() && WinnerTeam is not CustomWinner.Draw and not CustomWinner.Error) return;

        Ended = false;

        Predicate.CheckForGameEnd(out GameOverReason reason);

        if (Options.CurrentGameMode != CustomGameMode.Standard)
        {
            if (WinnerIds.Count > 0 || WinnerTeam != CustomWinner.Default)
            {
                Statistics.OnGameEnd();
                Ended = true;
                LoadingEndScreen = true;
                ShipStatus.Instance.enabled = false;
                StartEndGame(reason);
                Predicate = null;
            }

            return;
        }

        if (WinnerTeam != CustomWinner.Default)
        {
            Ended = true;
            LoadingEndScreen = true;

            Main.AllPlayerControls.Do(pc => Camouflage.RpcSetSkin(pc, true, true, true));

            NameNotifyManager.Reset();
            NotifyRoles(ForceLoop: true);

            CustomSabotage.Reset();

            int saboWinner = Options.WhoWinsBySabotageIfNoImpAlive.GetValue();

            if (reason == GameOverReason.ImpostorsBySabotage && saboWinner != 0 && !Main.AllAlivePlayerControls.Any(x => x.Is(Team.Impostor)))
            {
                bool anyNKAlive = Main.AllAlivePlayerControls.Any(x => x.IsNeutralKiller());
                bool anyCovenAlive = Main.AllPlayerControls.Any(x => x.Is(Team.Coven));

                switch (saboWinner)
                {
                    case 1 when anyNKAlive:
                        NKWins();
                        break;
                    case 2 when anyCovenAlive:
                        CovenWins();
                        break;
                    default:
                        switch (Options.IfSelectedTeamIsDead.GetValue())
                        {
                            case 0:
                                goto Continue;
                            case 1:
                                NKWins();
                                break;
                            case 2:
                                CovenWins();
                                break;
                        }

                        break;
                }

                void NKWins()
                {
                    ResetAndSetWinner(CustomWinner.Neutrals);
                    WinnerIds.UnionWith(Main.AllAlivePlayerControls.Where(x => x.IsNeutralKiller()).Select(x => x.PlayerId));
                }

                void CovenWins()
                {
                    ResetAndSetWinner(CustomWinner.Coven);
                    WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.Is(Team.Coven)).Select(x => x.PlayerId));
                }
            }

            Continue:

            switch (WinnerTeam)
            {
                case CustomWinner.Crewmate:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => (pc.Is(CustomRoleTypes.Crewmate) || (pc.Is(CustomRoles.Haunter) && Haunter.CanWinWithCrew(pc))) && !pc.IsMadmate() && !pc.IsConverted() && !pc.Is(CustomRoles.EvilSpirit))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Impostor:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => ((pc.Is(CustomRoleTypes.Impostor) && (!pc.Is(CustomRoles.DeadlyQuota) || Main.PlayerStates.Count(x => x.Value.GetRealKiller() == pc.PlayerId) >= Options.DQNumOfKillsNeeded.GetInt())) || pc.IsMadmate()) && !pc.IsConverted() && !pc.Is(CustomRoles.EvilSpirit))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Coven:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(Team.Coven) && !pc.IsMadmate() && (!pc.IsConverted() || pc.Is(CustomRoles.Entranced)) && !pc.Is(CustomRoles.EvilSpirit))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Cultist:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Cultist) || pc.Is(CustomRoles.Charmed))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Necromancer:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Necromancer) || pc.Is(CustomRoles.Deathknight) || pc.Is(CustomRoles.Undead))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Virus:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Virus) || pc.Is(CustomRoles.Contagious))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Jackal:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Jackal) || pc.Is(CustomRoles.Sidekick))
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
                    CustomRoles role = pc.GetCustomRole();
                    RoleBase roleBase = Main.PlayerStates[pc.PlayerId].Role;

                    if (GhostRolesManager.AssignedGhostRoles.TryGetValue(pc.PlayerId, out var ghostRole) && ghostRole.Instance is Shade shade && shade.Protected.IsSupersetOf(Main.AllAlivePlayerControls.Select(x => x.PlayerId)))
                    {
                        AdditionalWinnerTeams.Add(AdditionalWinners.Shade);
                        WinnerIds.Add(pc.PlayerId);
                    }

                    switch (role)
                    {
                        case CustomRoles.Stalker when pc.IsAlive() && ((WinnerTeam == CustomWinner.Impostor && !reason.Equals(GameOverReason.ImpostorsBySabotage)) || WinnerTeam == CustomWinner.Stalker || (WinnerTeam == CustomWinner.Crewmate && !reason.Equals(GameOverReason.CrewmatesByTask) && roleBase is Stalker { IsWinKill: true } && Stalker.SnatchesWin.GetBool())):
                            ResetAndSetWinner(CustomWinner.Stalker);
                            WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Specter when pc.GetTaskState().RemainingTasksCount <= 0 && !pc.IsAlive() && WinnerTeam != CustomWinner.Specter:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Specter);
                            break;
                        case CustomRoles.VengefulRomantic when VengefulRomantic.HasKilledKiller:
                            WinnerIds.Add(pc.PlayerId);
                            WinnerIds.Add(Romantic.PartnerId);
                            AdditionalWinnerTeams.Add((AdditionalWinners)role);
                            break;
                        case CustomRoles.SchrodingersCat when !pc.IsConverted():
                            WinnerIds.Remove(pc.PlayerId);
                            break;
                        case CustomRoles.Opportunist when pc.IsAlive():
                        case CustomRoles.Pursuer when pc.IsAlive() && WinnerTeam is not CustomWinner.Jester and not CustomWinner.Lovers and not CustomWinner.Terrorist and not CustomWinner.Executioner and not CustomWinner.Collector and not CustomWinner.Innocent and not CustomWinner.Youtuber:
                        case CustomRoles.Sunnyboy when !pc.IsAlive():
                        case CustomRoles.Maverick when pc.IsAlive() && roleBase is Maverick mr && mr.NumOfKills >= Maverick.MinKillsToWin.GetInt():
                        case CustomRoles.Provocateur when Provocateur.Provoked.TryGetValue(pc.PlayerId, out byte tar) && !WinnerIds.Contains(tar):
                        case CustomRoles.Hater when (roleBase as Hater).IsWon:
                        case CustomRoles.Follower when roleBase is Follower tc && tc.BetPlayer != byte.MaxValue && (WinnerIds.Contains(tc.BetPlayer) || (Main.PlayerStates.TryGetValue(tc.BetPlayer, out PlayerState ps) && (WinnerRoles.Contains(ps.MainRole) || (WinnerTeam == CustomWinner.Bloodlust && ps.SubRoles.Contains(CustomRoles.Bloodlust))))):
                        case CustomRoles.Romantic when WinnerIds.Contains(Romantic.PartnerId) || (Main.PlayerStates.TryGetValue(Romantic.PartnerId, out PlayerState ps1) && (WinnerRoles.Contains(ps1.MainRole) || (WinnerTeam == CustomWinner.Bloodlust && ps1.SubRoles.Contains(CustomRoles.Bloodlust)))):
                        case CustomRoles.Lawyer when Lawyer.Target.TryGetValue(pc.PlayerId, out byte lawyertarget) && (WinnerIds.Contains(lawyertarget) || (Main.PlayerStates.TryGetValue(lawyertarget, out PlayerState ps2) && (WinnerRoles.Contains(ps2.MainRole) || (WinnerTeam == CustomWinner.Bloodlust && ps2.SubRoles.Contains(CustomRoles.Bloodlust))))):
                        case CustomRoles.Postman when (roleBase as Postman).IsFinished:
                        case CustomRoles.Dealer when (roleBase as Dealer).IsWon:
                        case CustomRoles.Impartial when (roleBase as Impartial).IsWon:
                        case CustomRoles.Tank when (roleBase as Tank).IsWon:
                        case CustomRoles.Technician when (roleBase as Technician).IsWon:
                        case CustomRoles.Backstabber when (roleBase as Backstabber).CheckWin():
                        case CustomRoles.Predator when (roleBase as Predator).IsWon:
                        case CustomRoles.Gaslighter when (roleBase as Gaslighter).AddAsAdditionalWinner():
                        case CustomRoles.SoulHunter when (roleBase as SoulHunter).Souls >= SoulHunter.NumOfSoulsToWin.GetInt():
                        case CustomRoles.SchrodingersCat when WinnerTeam == CustomWinner.Crewmate && SchrodingersCat.WinsWithCrewIfNotAttacked.GetBool():
                        case CustomRoles.Curser when WinnerTeam != CustomWinner.Crewmate:
                        case CustomRoles.NoteKiller when !NoteKiller.CountsAsNeutralKiller && NoteKiller.Kills >= NoteKiller.NumKillsNeededToWin:
                        case CustomRoles.NecroGuesser when (roleBase as NecroGuesser).GuessedPlayers >= NecroGuesser.NumGuessesToWin.GetInt():
                        case CustomRoles.RoomRusher when (roleBase as RoomRusher).Won:
                        case CustomRoles.Clerk when WinnerTeam != CustomWinner.Crewmate || reason == GameOverReason.CrewmatesByTask:
                        case CustomRoles.Auditor when WinnerTeam != CustomWinner.Crewmate:
                        case CustomRoles.Magistrate when WinnerTeam != CustomWinner.Crewmate:
                        case CustomRoles.Seamstress when WinnerTeam != CustomWinner.Crewmate:
                        case CustomRoles.Spirit when WinnerTeam != CustomWinner.Crewmate:
                        case CustomRoles.Starspawn when WinnerTeam != CustomWinner.Crewmate:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add((AdditionalWinners)role);
                            break;
                    }
                }

                if (WinnerTeam == CustomWinner.Impostor)
                {
                    IEnumerable<PlayerControl> aliveImps = Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoleTypes.Impostor));
                    PlayerControl[] imps = aliveImps as PlayerControl[] ?? aliveImps.ToArray();
                    int aliveImpCount = imps.Length;

                    switch (aliveImpCount)
                    {
                        // If there's an Egoist, and there is at least 1 non-Egoist impostor alive, Egoist loses
                        case > 1 when WinnerIds.Any(x => GetPlayerById(x).Is(CustomRoles.Egoist)):
                            WinnerIds.RemoveWhere(x => GetPlayerById(x).Is(CustomRoles.Egoist));
                            break;
                        // If there's only 1 impostor alive, and all living impostors are Egoists, the Egoist wins alone
                        case 1 when imps.All(x => x.Is(CustomRoles.Egoist)):
                            PlayerControl pc = imps[0];
                            reason = GameOverReason.ImpostorsByKill;
                            WinnerTeam = CustomWinner.Egoist;
                            WinnerIds.RemoveWhere(x => Main.PlayerStates[x].MainRole.IsImpostor() || x.GetPlayer().IsMadmate());
                            WinnerIds.Add(pc.PlayerId);
                            break;
                    }
                }

                byte[] winningPhantasm = GhostRolesManager.AssignedGhostRoles.Where(x => x.Value.Instance is Phantasm { IsWon: true }).Select(x => x.Key).ToArray();

                if (winningPhantasm.Length > 0)
                {
                    AdditionalWinnerTeams.Add(AdditionalWinners.Phantasm);
                    WinnerIds.UnionWith(winningPhantasm);
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
                    Dictionary<CustomTeamManager.CustomTeam, byte[]> teams = Main.AllPlayerControls
                        .Select(x => new { Team = CustomTeamManager.GetCustomTeam(x.PlayerId), Player = x })
                        .Where(x => x.Team != null)
                        .GroupBy(x => x.Team)
                        .ToDictionary(x => x.Key, x => x.Select(y => y.Player.PlayerId).ToArray());

                    foreach ((CustomTeamManager.CustomTeam team, byte[] playerIds) in teams)
                    {
                        bool winWithOriginalTeam = CustomTeamManager.IsSettingEnabledForTeam(team, CTAOption.WinWithOriginalTeam);

                        if (CustomTeamManager.IsSettingEnabledForTeam(team, CTAOption.OriginalWinCondition) && playerIds.Any(WinnerIds.Contains) && (winWithOriginalTeam || WinnerTeam is not (CustomWinner.Impostor or CustomWinner.Crewmate or CustomWinner.Coven or CustomWinner.Neutrals)))
                            WinnerIds.UnionWith(playerIds);
                        else if (!winWithOriginalTeam)
                            WinnerIds.ExceptWith(playerIds);
                    }
                }

                if ((WinnerTeam == CustomWinner.Lovers || WinnerIds.Any(x => Main.PlayerStates[x].SubRoles.Contains(CustomRoles.Lovers))) && Main.LoversPlayers.TrueForAll(x => x.IsAlive()) && reason != GameOverReason.CrewmatesByTask)
                {
                    if (WinnerTeam != CustomWinner.Lovers)
                        AdditionalWinnerTeams.Add(AdditionalWinners.Lovers);

                    WinnerIds.UnionWith(Main.LoversPlayers.Select(x => x.PlayerId));
                }

                if (Options.NeutralWinTogether.GetBool() && (WinnerRoles.Any(x => x.IsNeutral()) || WinnerIds.Select(x => GetPlayerById(x)).Any(x => x != null && x.GetCustomRole().IsNeutral() && !x.IsMadmate())))
                    WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.GetCustomRole().IsNeutral()).Select(x => x.PlayerId));
                else if (Options.NeutralRoleWinTogether.GetBool())
                {
                    foreach (byte id in WinnerIds.ToArray())
                    {
                        PlayerControl pc = GetPlayerById(id);
                        if (pc == null || !pc.GetCustomRole().IsNeutral()) continue;

                        foreach (PlayerControl tar in Main.AllPlayerControls)
                        {
                            if (!WinnerIds.Contains(tar.PlayerId) && tar.GetCustomRole() == pc.GetCustomRole())
                                WinnerIds.Add(tar.PlayerId);
                        }
                    }

                    foreach (CustomRoles role in WinnerRoles) WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.GetCustomRole() == role).Select(x => x.PlayerId));
                }

                WinnerIds.RemoveWhere(x => Main.PlayerStates[x].MainRole == CustomRoles.Shifter);

                // Investor win condition should be checked after all winners are set
                byte[] winningInvestors = Main.PlayerStates.Values.Where(x => x.Role is Investor { IsWon: true }).Select(x => x.Player.PlayerId).ToArray();

                if (winningInvestors.Length > 0)
                {
                    AdditionalWinnerTeams.Add(AdditionalWinners.Investor);
                    WinnerIds.UnionWith(winningInvestors);
                }
            }

            Statistics.OnGameEnd();

            Camouflage.BlockCamouflage = true;
            ShipStatus.Instance.enabled = false;
            StartEndGame(reason);
            Predicate = null;
        }
    }

    private static void StartEndGame(GameOverReason reason)
    {
        try { LobbySharingAPI.NotifyLobbyStatusChanged(LobbyStatus.Ended); }
        catch (Exception e) { ThrowException(e); }

        string msg = GetString("NotifyGameEnding");

        Main.AllPlayerControls
            .Where(x => x.GetClient() != null && !x.Data.Disconnected)
            .Select(x => new Message("\n", x.PlayerId, msg))
            .SendMultipleMessages();

        SetEverythingUpPatch.LastWinsReason = WinnerTeam is CustomWinner.Crewmate or CustomWinner.Impostor ? GetString($"GameOverReason.{reason}") : string.Empty;
        var self = AmongUsClient.Instance;
        self.StartCoroutine(CoEndGame(self, reason).WrapToIl2Cpp());
    }

    private static IEnumerator CoEndGame(InnerNetClient self, GameOverReason reason)
    {
        Silencer.ForSilencer.Clear();

        // Set ghost role
        List<byte> playersToRevive = [];
        CustomWinner winner = WinnerTeam;

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            if (winner == CustomWinner.Draw)
            {
                SetGhostRole(true);
                continue;
            }

            bool canWin = WinnerIds.Contains(pc.PlayerId) || WinnerRoles.Contains(pc.GetCustomRole()) || (winner == CustomWinner.Bloodlust && pc.Is(CustomRoles.Bloodlust));
            bool isCrewmateWin = reason.Equals(GameOverReason.CrewmatesByVote) || reason.Equals(GameOverReason.CrewmatesByTask);
            SetGhostRole(canWin ^ isCrewmateWin); // XOR
            continue;

            void SetGhostRole(bool toGhostImpostor)
            {
                if (!pc.Data.IsDead) playersToRevive.Add(pc.PlayerId);

                if (toGhostImpostor)
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
        MessageWriter winnerWriter = self.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.EndGame, SendOption.Reliable);
        WriteTo(winnerWriter);
        self.FinishRpcImmediately(winnerWriter);

        // Delay to ensure that resuscitation is delivered after the ghost roll setting
        yield return new WaitForSeconds(EndGameDelay);

        if (playersToRevive.Count > 0)
        {
            // Resuscitation Resuscitate one person per transmission to prevent the packet from swelling up and dying
            foreach (byte playerId in playersToRevive)
            {
                NetworkedPlayerInfo playerInfo = GameData.Instance.GetPlayerById(playerId);
                // resuscitation
                playerInfo.IsDead = false;
                // transmission
                playerInfo.SetDirtyBit(0b_1u << playerId);
                self.SendAllStreamedObjects();
            }

            // Delay to ensure that the end of the game is delivered at the end of the game
            yield return new WaitForSeconds(EndGameDelay);
        }

        // Start End Game
        GameManager.Instance.ShouldCheckForGameEnd = false;
        MessageWriter msg = self.StartEndGame();
        msg.Write((byte)reason);
        msg.Write(false);
        self.FinishEndGame(msg);
    }

    private static bool WouldWinIfCrewLost(PlayerState state)
    {
        try
        {
            switch (state.MainRole)
            {
                case CustomRoles.Specter when state.TaskState.RemainingTasksCount <= 0 && state.IsDead:
                case CustomRoles.VengefulRomantic when VengefulRomantic.HasKilledKiller:
                case CustomRoles.SchrodingersCat when !state.SubRoles.Any(x => x.IsConverted()):
                case CustomRoles.Opportunist when !state.IsDead:
                case CustomRoles.Sunnyboy when state.IsDead:
                case CustomRoles.Maverick when !state.IsDead && state.Role is Maverick mr && mr.NumOfKills >= Maverick.MinKillsToWin.GetInt():
                case CustomRoles.Provocateur when Provocateur.Provoked.TryGetValue(state.Player.PlayerId, out byte tar) && !WinnerIds.Contains(tar):
                case CustomRoles.Hater when ((Hater)state.Role).IsWon:
                case CustomRoles.Postman when ((Postman)state.Role).IsFinished:
                case CustomRoles.Dealer when ((Dealer)state.Role).IsWon:
                case CustomRoles.Impartial when ((Impartial)state.Role).IsWon:
                case CustomRoles.Tank when ((Tank)state.Role).IsWon:
                case CustomRoles.Technician when ((Technician)state.Role).IsWon:
                case CustomRoles.Backstabber when ((Backstabber)state.Role).CheckWin():
                case CustomRoles.Predator when ((Predator)state.Role).IsWon:
                case CustomRoles.Gaslighter when ((Gaslighter)state.Role).AddAsAdditionalWinner():
                case CustomRoles.SoulHunter when ((SoulHunter)state.Role).Souls >= SoulHunter.NumOfSoulsToWin.GetInt():
                case CustomRoles.Curser:
                case CustomRoles.NoteKiller when !NoteKiller.CountsAsNeutralKiller && NoteKiller.Kills >= NoteKiller.NumKillsNeededToWin:
                case CustomRoles.NecroGuesser when ((NecroGuesser)state.Role).GuessedPlayers >= NecroGuesser.NumGuessesToWin.GetInt():
                case CustomRoles.RoomRusher when ((RoomRusher)state.Role).Won:
                case CustomRoles.Auditor:
                case CustomRoles.Clerk:
                case CustomRoles.Magistrate:
                case CustomRoles.Seamstress:
                case CustomRoles.Spirit:
                case CustomRoles.Starspawn:
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception e)
        {
            ThrowException(e);
            return false;
        }
    }

    public static void SetPredicateToNormal()
    {
        Predicate = new NormalGameEndPredicate();
    }

    public static void SetPredicateToSoloPVP()
    {
        Predicate = new SoloPVPGameEndPredicate();
    }

    public static void SetPredicateToFFA()
    {
        Predicate = new FFAGameEndPredicate();
    }

    public static void SetPredicateToStopAndGo()
    {
        Predicate = new StopAndGoGameEndPredicate();
    }

    public static void SetPredicateToHotPotato()
    {
        Predicate = new HotPotatoGameEndPredicate();
    }

    public static void SetPredicateToSpeedrun()
    {
        Predicate = new SpeedrunGameEndPredicate();
    }

    public static void SetPredicateToHideAndSeek()
    {
        Predicate = new HideAndSeekGameEndPredicate();
    }

    public static void SetPredicateToCaptureTheFlag()
    {
        Predicate = new CaptureTheFlagGameEndPredicate();
    }

    public static void SetPredicateToNaturalDisasters()
    {
        Predicate = new NaturalDisastersGameEndPredicate();
    }

    public static void SetPredicateToRoomRush()
    {
        Predicate = new RoomRushGameEndPredicate();
    }

    public static void SetPredicateToKingOfTheZones()
    {
        Predicate = new KingOfTheZonesGameEndPredicate();
    }

    public static void SetPredicateToQuiz()
    {
        Predicate = new QuizGameEndPredicate();
    }

    public static void SetPredicateToTheMindGame()
    {
        Predicate = new TheMindGameGameEndPredicate();
    }

    public static void SetPredicateToBedWars()
    {
        Predicate = new BedWarsGameEndPredicate();
    }
    
    public static void SetPredicateToDeathrace()
    {
        Predicate = new DeathraceGameEndPredicate();
    }
    
    public static void SetPredicateToMingle()
    {
        Predicate = new MingleGameEndPredicate();
    }

    public static void SetPredicateToSnowdown()
    {
        Predicate = new SnowdownGameEndPredicate();
    }

    private class NormalGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (WinnerTeam != CustomWinner.Default) return false;

            return CheckGameEndBySabotage(out reason) || CheckGameEndByTask(out reason) || CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;

            if (Main.HasJustStarted) return false;

            PlayerControl[] aapc = Main.AllAlivePlayerControls;

            if (CustomRoles.Sunnyboy.RoleExist() && aapc.Length > 1 && aapc.Any(x => x.CanUseKillButton() && !x.IsCrewmate())) return false;

            if (CustomTeamManager.CheckCustomTeamGameEnd()) return true;

            if (aapc.Length > 0 && aapc.All(x => Main.LoversPlayers.Exists(l => l.PlayerId == x.PlayerId)) && (!Main.LoversPlayers.TrueForAll(x => x.Is(Team.Crewmate)) || !Lovers.CrewLoversWinWithCrew.GetBool()))
            {
                ResetAndSetWinner(CustomWinner.Lovers);
                WinnerIds.UnionWith(Main.LoversPlayers.ConvertAll(x => x.PlayerId));
                return true;
            }

            if (Main.AllAlivePlayerControls.Any(x => x.GetCountTypes() == CountTypes.CustomTeam)) return false;
            if (Pawn.KeepsGameGoing.GetBool() && CustomRoles.Pawn.RoleExist()) return false;

            PlayerState[] statesCoutingAsCrew = Main.PlayerStates.Values.Where(x => x.countTypes == CountTypes.Crew).ToArray();

            if (statesCoutingAsCrew.Length > 0 && statesCoutingAsCrew.All(WouldWinIfCrewLost))
                statesCoutingAsCrew.Do(x => x.countTypes = CountTypes.None);

            int imp = AlivePlayersCount(CountTypes.Impostor);
            int crew = AlivePlayersCount(CountTypes.Crew);
            int coven = AlivePlayersCount(CountTypes.Coven);

            var crewKeepsGameGoing = false;
            
            if (Options.GuessersKeepTheGameGoing.GetBool())
            {
                bool restrictions = Options.GuesserNumRestrictions.GetBool();
                crewKeepsGameGoing |= statesCoutingAsCrew.Any(x => !x.IsDead && x.Player != null && GuessManager.StartMeetingPatch.CanGuess(x.Player, restrictions));
            }

            foreach (PlayerState playerState in statesCoutingAsCrew)
            {
                if (playerState.IsDead || !Options.CrewAdvancedGameEndCheckingSettings.TryGetValue(playerState.MainRole, out var option) || !option.GetBool()) continue;
                playerState.Role.ManipulateGameEndCheckCrew(playerState, out bool keepGameGoing, out int countsAs);
                crewKeepsGameGoing |= keepGameGoing;
                crew += countsAs - 1;
            }

            Dictionary<(CustomRoles? Role, CustomWinner Winner), int> roleCounts = [];

            foreach (CustomRoles role in Enum.GetValues<CustomRoles>())
            {
                if ((!role.IsNK() && role is not CustomRoles.Bloodlust and not CustomRoles.Gaslighter) || role.IsMadmate() || role is CustomRoles.Sidekick) continue;

                CountTypes countTypes = role.GetCountTypes();
                if (countTypes is CountTypes.Crew or CountTypes.Impostor or CountTypes.None or CountTypes.OutOfGame or CountTypes.CustomTeam or CountTypes.Coven) continue;

                CustomRoles? keyRole = role.IsRecruitingRole() ? null : role;
                var keyWinner = (CustomWinner)role;
                int value = AlivePlayersCount(countTypes);

                roleCounts[(keyRole, keyWinner)] = value;
            }

            if (CustomRoles.Schizophrenic.IsEnable())
            {
                foreach (PlayerControl x in aapc)
                {
                    if (!x.Is(CustomRoles.Schizophrenic)) continue;

                    if (x.Is(Team.Impostor)) imp++;
                    else if (x.Is(Team.Crewmate)) crew++;
                    else if (x.Is(Team.Coven)) coven++;

                    if (x.Is(CustomRoles.Charmed)) roleCounts[(null, CustomWinner.Cultist)]++;
                    if (x.Is(CustomRoles.Undead)) roleCounts[(null, CustomWinner.Necromancer)]++;
                    if (x.Is(CustomRoles.Sidekick)) roleCounts[(null, CustomWinner.Jackal)]++;
                    if (x.Is(CustomRoles.Contagious)) roleCounts[(null, CustomWinner.Virus)]++;
                }
            }

            int totalNKAlive = roleCounts.Values.Sum();

            CustomWinner? winner = null;
            CustomRoles? rl = null;

            if (totalNKAlive == 0)
            {
                if (coven == 0)
                {
                    if (crew == 0 && imp == 0)
                    {
                        reason = GameOverReason.ImpostorsByKill;
                        winner = CustomWinner.None;
                    }
                    else if (crew <= imp && !crewKeepsGameGoing)
                    {
                        reason = GameOverReason.ImpostorsByKill;
                        winner = CustomWinner.Impostor;
                    }
                    else if (imp == 0)
                    {
                        reason = GameOverReason.CrewmatesByVote;
                        winner = CustomWinner.Crewmate;
                    }
                    else
                        return false;

                    Logger.Info($"Crew: {crew}, Imp: {imp}, Coven: {coven}", "CheckGameEndPatch.CheckGameEndByLivingPlayers");
                    ResetAndSetWinner((CustomWinner)winner);

                    if (winner == CustomWinner.Crewmate && Main.AllAlivePlayerControls.All(x => x.GetCustomRole().IsNeutral()))
                    {
                        AdditionalWinnerTeams.Add(AdditionalWinners.AliveNeutrals);
                        WinnerIds.UnionWith(Main.AllAlivePlayerControls.Select(x => x.PlayerId));
                    }
                }
                else
                {
                    if (imp >= 1) return false;
                    if (crew > coven) return false;
                    if (crewKeepsGameGoing) return false;

                    Logger.Info($"Crew: {crew}, Imp: {imp}, Coven: {coven}", "CheckGameEndPatch.CheckGameEndByLivingPlayers");
                    reason = GameOverReason.ImpostorsByKill;
                    ResetAndSetWinner(CustomWinner.Coven);
                }

                return true;
            }

            if (imp >= 1) return false; // both imps and NKs are alive, game must continue
            if (coven >= 1) return false; // both covens and NKs are alive, game must continue
            if (crew > totalNKAlive || crewKeepsGameGoing) return false; // Imps are dead, but crew still outnumbers NKs, game must continue

            // Imps dead, Crew <= NK, Checking if all NKs alive are in 1 team
            List<int> aliveCounts = roleCounts.Values.Where(x => x > 0).ToList();

            switch (aliveCounts.Count)
            {
                // There are multiple types of NKs alive, the game must continue
                case > 1:
                    return false;
                // There is only one type of NK alive, they've won
                case 1:
                {
                    if (aliveCounts[0] != roleCounts.Values.Max()) Logger.Warn("There is something wrong here.", "CheckGameEndPatch");

                    foreach (KeyValuePair<(CustomRoles? Role, CustomWinner Winner), int> keyValuePair in roleCounts)
                    {
                        if (keyValuePair.Value == aliveCounts[0])
                        {
                            reason = GameOverReason.ImpostorsByKill;
                            winner = keyValuePair.Key.Winner;
                            rl = keyValuePair.Key.Role;
                            break;
                        }
                    }

                    break;
                }
                default:
                    Logger.Fatal("Error while selecting NK winner", "CheckGameEndPatch.CheckGameEndByLivingPlayers");
                    Logger.SendInGame("There was an error while selecting the winner. Please report this bug to the developer! (Do /dump to get logs)", Color.red);
                    ResetAndSetWinner(CustomWinner.Error);
                    return true;
            }

            if (winner != null) ResetAndSetWinner((CustomWinner)winner);

            if (rl != null)
            {
                WinnerRoles.Add((CustomRoles)rl);
                WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.GetCustomRole() == rl).Select(x => x.PlayerId));
            }

            return true;
        }
    }

    private class SoloPVPGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;

            if (SoloPVP.RoundTime > 0) return false;

            HashSet<byte> winners = [Main.AllPlayerControls.FirstOrDefault(x => !x.Is(CustomRoles.GM) && SoloPVP.GetRankFromScore(x.PlayerId) == 1)?.PlayerId ?? Main.AllAlivePlayerControls[0].PlayerId];
            int kills = SoloPVP.PlayerScore[winners.First()];
            winners.UnionWith(SoloPVP.PlayerScore.Where(x => x.Value == kills).Select(x => x.Key));

            WinnerIds = winners;

            Main.DoBlockNameChange = true;

            return true;
        }
    }

    private class FFAGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;

            if (FreeForAll.RoundTime <= 0)
            {
                PlayerControl winner = Main.GM.Value && Main.AllPlayerControls.Length == 1 ? PlayerControl.LocalPlayer : Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && x != null).OrderBy(x => FreeForAll.GetRankFromScore(x.PlayerId)).First();
                byte winnerId = winner.PlayerId;
                Logger.Warn($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "FFA");
                WinnerIds = [winnerId];

                Main.DoBlockNameChange = true;

                return true;
            }

            if (FreeForAll.FFATeamMode.GetBool())
            {
                IEnumerable<HashSet<byte>> teams = FreeForAll.PlayerTeams.GroupBy(x => x.Value, x => x.Key).Select(x => x.Where(p =>
                {
                    PlayerControl pc = GetPlayerById(p);
                    return pc != null && !pc.Data.Disconnected;
                }).ToHashSet()).Where(x => x.Count > 0);

                foreach (HashSet<byte> team in teams)
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
                    PlayerControl winner = Main.AllAlivePlayerControls[0];

                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "FFA");

                    WinnerIds =
                    [
                        winner.PlayerId
                    ];

                    Main.DoBlockNameChange = true;

                    return true;
                }
                case 0:
                    FreeForAll.RoundTime = 0;
                    Logger.Warn("No players alive. Force ending the game", "FFA");
                    return false;
                default:
                    return false;
            }
        }
    }

    private class StopAndGoGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;

            if (StopAndGo.RoundTime <= 0)
            {
                PlayerControl[] apc = Main.AllPlayerControls;
                SetWinner(Main.GM.Value && apc.Length == 1 ? PlayerControl.LocalPlayer : apc.Where(x => !x.Is(CustomRoles.GM) && x != null).OrderBy(x => StopAndGo.GetRankFromScore(x.PlayerId)).ThenByDescending(x => x.IsAlive()).First());
                return true;
            }

            PlayerControl[] aapc = Main.AllAlivePlayerControls;

            if (aapc.Any(x => x.GetTaskState().IsTaskFinished))
            {
                SetWinner(aapc.First(x => x.GetTaskState().IsTaskFinished));
                return true;
            }

            switch (aapc.Length)
            {
                case 1 when !GameStates.IsLocalGame:
                    SetWinner(aapc[0]);
                    return true;
                case 0:
                    StopAndGo.RoundTime = 0;
                    Logger.Warn("No players alive. Force ending the game", "StopAndGo");
                    break;
            }

            return false;

            void SetWinner(PlayerControl winner)
            {
                Logger.Warn($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "StopAndGo");
                WinnerIds = [winner.PlayerId];
                Main.DoBlockNameChange = true;
            }
        }
    }

    private class HotPotatoGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;

            switch (Main.AllAlivePlayerControls.Length)
            {
                case 1:
                    PlayerControl winner = Main.AllAlivePlayerControls[0];
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

    private class HideAndSeekGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return CustomHnS.CheckForGameEnd(out reason);
        }
    }

    private class SpeedrunGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return Speedrun.CheckForGameEnd(out reason);
        }
    }

    private class CaptureTheFlagGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return CaptureTheFlag.CheckForGameEnd(out reason);
        }
    }

    private class NaturalDisastersGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;

            switch (Main.AllAlivePlayerControls.Length)
            {
                case 1:
                    PlayerControl winner = Main.AllAlivePlayerControls[0];
                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "NaturalDisasters");
                    WinnerIds = [winner.PlayerId];
                    Main.DoBlockNameChange = true;
                    return true;
                case 0:
                    ResetAndSetWinner(CustomWinner.Error);
                    Logger.Warn("No players alive. Force ending the game", "NaturalDisasters");
                    return true;
                default:
                    return false;
            }
        }
    }

    private class RoomRushGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;

            PlayerControl[] appc = Main.AllAlivePlayerControls;

            switch (appc.Length)
            {
                case 1:
                    PlayerControl winner = appc[0];
                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "RoomRush");
                    WinnerIds = [winner.PlayerId];
                    Main.DoBlockNameChange = true;
                    return true;
                case 0:
                    ResetAndSetWinner(CustomWinner.None);
                    Logger.Warn("No players alive. Force ending the game", "RoomRush");
                    return true;
                default:
                    return false;
            }
        }
    }

    private class KingOfTheZonesGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return KingOfTheZones.CheckForGameEnd(out reason);
        }
    }

    private class QuizGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;

            if (Quiz.AllowKills) return false;

            PlayerControl[] appc = Main.AllAlivePlayerControls;

            switch (appc.Length)
            {
                case 1:
                    PlayerControl winner = appc[0];
                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "Quiz");
                    WinnerIds = [winner.PlayerId];
                    Main.DoBlockNameChange = true;
                    return true;
                case 0:
                    ResetAndSetWinner(CustomWinner.None);
                    Logger.Warn("No players alive. Force ending the game", "Quiz");
                    return true;
                default:
                    return false;
            }
        }
    }

    private class TheMindGameGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return TheMindGame.CheckForGameEnd(out reason);
        }
    }

    private class BedWarsGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return BedWars.CheckForGameEnd(out reason);
        }
    }

    private class DeathraceGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return Deathrace.CheckGameEnd(out reason);
        }
    }

    private class MingleGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return Mingle.CheckGameEnd(out reason);
        }
    }
    
    private class SnowdownGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return Snowdown.CheckGameEnd(out reason);
        }
    }

    public abstract class GameEndPredicate
    {
        /// <summary>Checks the game ending condition and stores the value in CustomWinnerHolder. </summary>
        /// <params name="reason">GameOverReason used for vanilla game end processing</params>
        /// <returns>Whether the conditions for ending the game are met</returns>
        public abstract bool CheckForGameEnd(out GameOverReason reason);

        /// <summary>Determine whether the task can be won based on GameData.TotalTasks and CompletedTasks.</summary>
        public virtual bool CheckGameEndByTask(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (Options.DisableTaskWin.GetBool() || TaskState.InitialTotalTasks == 0) return false;

            if ((GameData.Instance.TotalTasks == 0 && GameData.Instance.CompletedTasks == 0) || !Main.PlayerStates.Values.Any(x => x.TaskState.HasTasks)) return false;
            if (Options.DisableTaskWinIfAllCrewsAreDead.GetBool() && !Main.AllAlivePlayerControls.Any(x => x.Is(CustomRoleTypes.Crewmate))) return false;
            if (Options.DisableTaskWinIfAllCrewsAreConverted.GetBool() && Main.AllAlivePlayerControls.Where(x => x.Is(Team.Crewmate) && x.GetRoleTypes() is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Scientist or RoleTypes.Noisemaker or RoleTypes.Tracker or RoleTypes.Detective or RoleTypes.CrewmateGhost or RoleTypes.GuardianAngel).All(x => x.IsConverted())) return false;

            if (GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
            {
                reason = GameOverReason.CrewmatesByTask;
                ResetAndSetWinner(CustomWinner.Crewmate);
                return true;
            }

            return false;
        }

        /// <summary>Determines whether sabotage victory is possible based on the elements in ShipStatus.Systems.</summary>
        protected virtual bool CheckGameEndBySabotage(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (ShipStatus.Instance.Systems == null) return false;

            // TryGetValue is not available
            Il2CppSystem.Collections.Generic.Dictionary<SystemTypes, ISystemType> systems = ShipStatus.Instance.Systems;
            LifeSuppSystemType lifeSupp;

            if (systems.ContainsKey(SystemTypes.LifeSupp) && // Confirmation of sabotage existence
                (lifeSupp = systems[SystemTypes.LifeSupp].CastFast<LifeSuppSystemType>()) != null && // Confirmation that cast is possible
                lifeSupp.Countdown <= 0f) // Time up confirmation
            {
                // oxygen sabotage
                ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorsBySabotage;
                lifeSupp.Countdown = 10000f;
                return true;
            }

            ISystemType sys = null;

            if (systems.ContainsKey(SystemTypes.Reactor))
                sys = systems[SystemTypes.Reactor];
            else if (systems.ContainsKey(SystemTypes.Laboratory))
                sys = systems[SystemTypes.Laboratory];
            else if (systems.ContainsKey(SystemTypes.HeliSabotage))
                sys = systems[SystemTypes.HeliSabotage];

            ICriticalSabotage critical;

            if (sys != null && // Confirmation of sabotage existence
                (critical = sys.CastFast<ICriticalSabotage>()) != null && // Confirmation that cast is possible
                critical.Countdown <= 0f) // Time up confirmation
            {
                // reactor sabotage
                ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorsBySabotage;
                critical.ClearSabotage();
                return true;
            }

            return false;
        }
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckEndGameViaTasks))]
internal static class CheckEndGameViaTasksPatch
{
    public static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}