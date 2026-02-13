using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Gamemodes;
using EHR.Roles;

namespace EHR.Modules;

public static class Statistics
{
    private const int MinPlayers = 3;
    private static bool OnlyVotingForKillersAsCrew = true;
    private static bool HasVoted;
    private static bool VotedBySomeone;
    public static int VentTimes;
    public static bool HasUsedAnyCommand;

    public static string WinCountsForOutro = string.Empty;

    public static void OnGameEnd()
    {
        try
        {
            var apc = Main.AllPlayerControls;
            var aapc = Main.AllAlivePlayerControls;

            WinCountsForOutro = string.Empty;

            if (CustomWinnerHolder.WinnerTeam is CustomWinner.None or CustomWinner.Draw or CustomWinner.Error || apc.Count <= MinPlayers) return;

            PlayerControl lp = PlayerControl.LocalPlayer;
            CustomRoles role = lp.GetCustomRole();
            List<CustomRoles> addons = lp.GetCustomSubRoles();

            bool won = CustomWinnerHolder.WinnerIds.Contains(lp.PlayerId) || CustomWinnerHolder.WinnerRoles.Contains(role) || (CustomWinnerHolder.WinnerTeam == CustomWinner.Bloodlust && addons.Contains(CustomRoles.Bloodlust));

            CustomGameMode gm = Options.CurrentGameMode;

            if (GameStates.CurrentServerType is not GameStates.ServerType.Modded and not GameStates.ServerType.Niko)
            {
                try
                {
                    Dictionary<string, int> winners = CustomWinnerHolder.WinnerIds.ToValidPlayers().Select(x => x.GetClient()).Where(x => x != null).ToDictionary(x => x.GetHashedPuid(), _ => 0);
                    
                    Main.NumWinsPerGM.TryAdd(gm, []);
                    Main.NumWinsPerGM[gm].AddRange(winners, false);

                    foreach (string hashedpuid in Main.NumWinsPerGM[gm].Keys.Intersect(winners.Keys).ToArray())
                        Main.NumWinsPerGM[gm][hashedpuid]++;

                    if (Main.NumWinsPerGM[gm].Count != 0)
                    {
                        StringBuilder sb = new();
                        sb.AppendLine($"<#ffffff><u>{Translator.GetString("WinsCountTitle")}:</u></color>");
                        var any = false;

                        foreach ((string hashedpuid, int wins) in Main.NumWinsPerGM[gm])
                        {
                            if (!apc.FindFirst(x => x.GetClient()?.GetHashedPuid() == hashedpuid, out PlayerControl player)) continue;
                            sb.AppendLine($"{player.PlayerId.ColoredPlayerName()}: {wins}");
                            any = true;
                        }

                        WinCountsForOutro = any ? sb.ToString().Trim() : string.Empty;
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }

            switch (gm)
            {
                case CustomGameMode.FFA when won:
                    Achievements.Type.SerialKiller.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.SoloPVP when won:
                    Achievements.Type.PVPMaster.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.StopAndGo when won:
                    Achievements.Type.HarderThanDrivingThroughTrafficLightsRight.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.Speedrun when won:
                    Achievements.Type.TwoFast4You.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.HotPotato when won:
                    Achievements.Type.TooHotForMe.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.HideAndSeek when won:
                    Achievements.Type.SeekAndHide.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.NaturalDisasters when won:
                    Achievements.Type.Two012.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.CaptureTheFlag:
                    if (won) Achievements.Type.YourFlagIsMine.CompleteAfterGameEnd();

                    if (apc.Select(x => (pc: x, time: CaptureTheFlag.GetFlagTime(x.PlayerId))).MaxBy(x => x.time).pc.PlayerId == lp.PlayerId)
                        Achievements.Type.FlagMaster.CompleteAfterGameEnd();

                    if (apc.Select(x => (pc: x, time: CaptureTheFlag.GetTagCount(x.PlayerId))).MaxBy(x => x.time).pc.PlayerId == lp.PlayerId)
                        Achievements.Type.Tag.CompleteAfterGameEnd();

                    return;
                case CustomGameMode.RoomRush when won:
                    Achievements.Type.BestReactionTime.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.Deathrace when won:
                    Achievements.Type.FastestRunner.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.Mingle when won:
                    Achievements.Type.ThisAintSquidGames.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.KingOfTheZones when won:
                    Achievements.Type.YourZoneIsMine.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.Standard:
                    Reset();
                    break;
            }
            
            if (won && Main.PlayerStates.TryGetValue(lp.PlayerId, out var lpState) && lpState.RoleHistory.Contains(CustomRoles.Pawn))
                Achievements.Type.Checkmate.CompleteAfterGameEnd();

            if (won && addons.Contains(CustomRoles.Undead) && CustomWinnerHolder.WinnerIds.ToValidPlayers().Count(x => x.Is(CustomRoles.Necromancer)) >= 3)
                Achievements.Type.IdeasLiveOn.CompleteAfterGameEnd();

            if (Main.PlayerStates.Values.Count(x => x.GetRealKiller() == lp.PlayerId) >= 7)
                Achievements.Type.TheKillingMachine2Point0.CompleteAfterGameEnd();

            if (won && lp.IsCrewmate() && aapc.Count == 1 && lp.IsAlive())
                Achievements.Type.TheLastSurvivor.CompleteAfterGameEnd();

            if (addons.Contains(CustomRoles.Spurt) && Spurt.LocalPlayerAvoidsZeroAndOneHundredPrecent)
                Achievements.Type.ExpertControl.CompleteAfterGameEnd();

            if (IntroCutsceneDestroyPatch.IntroDestroyTS + 60 > Utils.TimeStamp)
                Achievements.Type.Speedrun.CompleteAfterGameEnd();

            if (won && CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor && lp.IsMadmate() && !aapc.Any(x => x.IsImpostor()))
                Achievements.Type.Carried.CompleteAfterGameEnd();

            if (won && lp.IsNeutralKiller())
                Achievements.Type.CommonEnemyNo1.CompleteAfterGameEnd();

            if (won && lp.IsImpostor())
            {
                if (Main.PlayerStates.Values.All(x => x.GetRealKiller() != lp.PlayerId))
                    Achievements.Type.InnocentImpostor.CompleteAfterGameEnd();

                if (Main.NormalOptions.NumImpostors > 1 && apc.Where(x => x.IsImpostor()).All(x => x.IsAlive()))
                    Achievements.Type.ImpostorGang.CompleteAfterGameEnd();
            }

            switch (CustomWinnerHolder.WinnerTeam)
            {
                case CustomWinner.None when Main.PlayerStates.Values.FindFirst(x => x.SubRoles.Contains(CustomRoles.Avenger), out PlayerState state) && state.GetRealKiller().GetPlayer().Is(CustomRoles.Butcher):
                    Achievements.Type.FuriousAvenger.CompleteAfterGameEnd();
                    break;
                case CustomWinner.Crewmate when won && apc.Count(x => x.IsImpostor() || x.IsNeutralKiller()) >= 2 && MeetingStates.MeetingNum < 3:
                    Achievements.Type.BrainStorm.CompleteAfterGameEnd();
                    break;
                case CustomWinner.Lovers when won && role is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor:
                    Achievements.Type.NothingCanStopLove.CompleteAfterGameEnd();
                    break;
                case CustomWinner.Executioner:
                case CustomWinner.Innocent:
                case CustomWinner.Jester:

                    if (won && role is CustomRoles.Executioner or CustomRoles.Innocent or CustomRoles.Jester &&
                        (CustomWinnerHolder.AdditionalWinnerTeams.Contains(AdditionalWinners.Executioner) ||
                         CustomWinnerHolder.WinnerIds.ToValidPlayers().Any(x => x.GetCustomRole() is CustomRoles.Executioner or CustomRoles.Innocent or CustomRoles.Jester)))
                        Achievements.Type.CoordinatedAttack.CompleteAfterGameEnd();

                    break;
            }

            switch (role)
            {
                case CustomRoles.Sheriff when won && lp.IsAlive() && Main.PlayerStates.Values.Where(x => x.IsDead && x.deathReason == PlayerState.DeathReason.Kill).All(x => x.GetRealKiller() == lp.PlayerId):
                    Achievements.Type.Superhero.CompleteAfterGameEnd();
                    break;
                case CustomRoles.Snitch when lp.IsAlive() && lp.AllTasksCompleted() && CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate && won:
                    Achievements.Type.CrewHero.Complete();
                    break;
                case CustomRoles.KillingMachine when Main.PlayerStates.Values.Count(x => x.GetRealKiller() == lp.PlayerId) >= 8:
                    Achievements.Type.Bloodbath.CompleteAfterGameEnd();
                    break;
                case CustomRoles.Technician when Technician.LocalPlayerFixedSabotageTypes.Count >= 4:
                    Achievements.Type.AntiSaboteur.CompleteAfterGameEnd();
                    break;
                case CustomRoles.Bargainer when won && Bargainer.PurchasedItems.Count >= 3:
                    Achievements.Type.P2W.CompleteAfterGameEnd();
                    break;
                case CustomRoles.Lawyer when Lawyer.Target.TryGetValue(lp.PlayerId, out byte ltg) && Main.PlayerStates.TryGetValue(ltg, out PlayerState ltgState) && ltgState.IsDead && ltgState.MainRole.IsCrewmate() && !ltgState.SubRoles.Contains(CustomRoles.Bloodlust) && won && CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor:
                    Achievements.Type.LiarLiar.CompleteAfterGameEnd();
                    break;
            }

            int correctGuesses = Main.PlayerStates.Values.Count(x => !x.Player.AmOwner && x.GetRealKiller() == lp.PlayerId && x.deathReason == PlayerState.DeathReason.Gambled);

            if (correctGuesses >= 1) Achievements.Type.LuckOrObservation.CompleteAfterGameEnd();
            if (correctGuesses >= 2) Achievements.Type.BeginnerGuesser.CompleteAfterGameEnd();
            if (correctGuesses >= 4) Achievements.Type.GuessMaster.CompleteAfterGameEnd();
            if (correctGuesses >= 6) Achievements.Type.BestGuesserAward.CompleteAfterGameEnd();

            void Reset()
            {
                if (won && OnlyVotingForKillersAsCrew && lp.IsCrewmate() && !MeetingStates.FirstMeeting) Achievements.Type.MasterDetective.CompleteAfterGameEnd();
                OnlyVotingForKillersAsCrew = true;
                
                if (!HasVoted && !MeetingStates.FirstMeeting) Achievements.Type.Abstain.CompleteAfterGameEnd();
                HasVoted = false;

                if (won && !VotedBySomeone && (lp.IsImpostor() || lp.IsNeutralKiller()) && !MeetingStates.FirstMeeting) Achievements.Type.Unsuspected.CompleteAfterGameEnd();
                VotedBySomeone = false;

                if (VentTimes >= 50) Achievements.Type.Vectory.CompleteAfterGameEnd();
                VentTimes = 0;

                if (!HasUsedAnyCommand) Achievements.Type.AndForWhatDidICodeTheseCommandsForIfYouDontUseThemAtAll.CompleteAfterGameEnd();
                HasUsedAnyCommand = false;
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static void OnVotingComplete(MeetingHud.VoterState[] states, NetworkedPlayerInfo exiledPlayer, bool tie, bool dictator)
    {
        try
        {
            if (Options.CurrentGameMode != CustomGameMode.Standard || Main.AllPlayerControls.Count <= MinPlayers) return;

            PlayerControl lp = PlayerControl.LocalPlayer;

            bool amDictator = dictator && lp.Is(CustomRoles.Dictator);

            if (amDictator) Achievements.Type.KimJongUnExperience.Complete();

            bool lpHasVS = states.FindFirst(x => x.VoterId == lp.PlayerId, out MeetingHud.VoterState lpVS);

            bool exiled = exiledPlayer != null && exiledPlayer.Object != null;

            if (!tie && exiled && exiledPlayer.Object.Is(CustomRoles.Jester))
            {
                if (states.Any(x => x.VoterId == lp.PlayerId && x.VotedForId == exiledPlayer.PlayerId))
                    Achievements.Type.HowCouldIBelieveThem.Complete();

                if (amDictator && lpHasVS && lpVS.VotedForId == exiledPlayer.PlayerId)
                    Achievements.Type.WhyJustWhy.Complete();
            }

            if (lpHasVS)
            {
                PlayerControl voteTarget = lpVS.VotedForId.GetPlayer();

                if (voteTarget != null)
                {
                    HasVoted = true;
                    
                    if (!voteTarget.IsCrewmate() && !voteTarget.IsConverted())
                        OnlyVotingForKillersAsCrew = false;
                }
            }

            if (states.Any(x => x.VotedForId == lp.PlayerId))
                VotedBySomeone = true;

            if (exiled && exiledPlayer.PlayerId == lp.PlayerId && Main.PlayerStates.Values.Any(x => x.SubRoles.Contains(CustomRoles.Bait) && x.GetRealKiller() == lp.PlayerId))
                Achievements.Type.Gotcha.Complete();

            if (lp.Is(CustomRoles.Decryptor) && Main.PlayerStates.Values.Count(x => x.deathReason == PlayerState.DeathReason.Gambled && x.GetRealKiller() == lp.PlayerId) >= 3)
                Achievements.Type.GetDecrypted.Complete();

            LateTask.New(() =>
            {
                if (GameStates.IsEnded) return;

                var aapc = Main.AllAlivePlayerControls;

                if (aapc.Count == 2 && lp.IsAlive() && aapc.All(x => x.IsNeutralKiller() || x.IsImpostor()))
                    Achievements.Type.Duel.Complete();
            }, 12f, log: false);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static void OnRoleSelectionComplete()
    {
        try
        {
            if (!CustomRoleSelector.RoleResult.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out CustomRoles role) || Main.AllPlayerControls.Count <= MinPlayers) return;

            const float delay = 15f;

            switch (role)
            {
                case CustomRoles.Dad:
                    Achievements.Type.WhatKindOfGeniusCameUpWithThisRoleIdea.CompleteAfterDelay(delay);
                    break;
                case CustomRoles.Crewmate:
                case CustomRoles.CrewmateEHR:
                    Achievements.Type.Bruh.CompleteAfterDelay(delay);
                    break;
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static void OnMurder(PlayerControl killer, PlayerControl target)
    {
        try
        {
            if (Options.CurrentGameMode != CustomGameMode.Standard || killer.PlayerId == target.PlayerId || Main.AllPlayerControls.Count <= MinPlayers) return;

            if (killer.AmOwner)
            {
                if (!Main.EnumerateAlivePlayerControls().Any(pc => pc.Is(CustomRoleTypes.Impostor)) && killer.IsCrewmate() && target.IsImpostor() && !Main.EnumerateAlivePlayerControls().Any(x => x.IsNeutralKiller()))
                    Achievements.Type.ImCrewISwear.Complete();

                if ((killer.IsImpostor() && target.IsMadmate()) || (killer.IsMadmate() && target.IsImpostor()))
                    Achievements.Type.BetrayalLevel100.CompleteAfterGameEnd();

                PlayerState killerState = Main.PlayerStates[killer.PlayerId];
                PlayerState targetState = Main.PlayerStates[target.PlayerId];

                if ((targetState.MainRole == CustomRoles.Romantic && Romantic.PartnerId == killer.PlayerId) ||
                    (targetState.SubRoles.Contains(CustomRoles.Lovers) && killer.Is(CustomRoles.Lovers)) ||
                    (targetState.MainRole == CustomRoles.Lawyer && Lawyer.Target.TryGetValue(target.PlayerId, out byte ltg) && ltg == killer.PlayerId) ||
                    (targetState.Role is Follower tc && tc.BetPlayer == killer.PlayerId))
                    Achievements.Type.WhatHaveIDone.CompleteAfterGameEnd();

                if (targetState.MainRole == CustomRoles.Snitch && Snitch.IsExposed.TryGetValue(target.PlayerId, out bool exposed) && exposed)
                    Achievements.Type.ThatWasClose.CompleteAfterGameEnd();

                switch (killerState.MainRole)
                {
                    case CustomRoles.Traitor when targetState.MainRole == CustomRoles.Renegade:
                        Achievements.Type.TheRealTraitor.CompleteAfterGameEnd();
                        break;
                    case CustomRoles.Bargainer when targetState.MainRole == CustomRoles.Merchant:
                    case CustomRoles.Merchant when targetState.MainRole == CustomRoles.Bargainer:
                        Achievements.Type.EconomicCompetition.CompleteAfterGameEnd();
                        break;
                    case CustomRoles.Crusader when targetState.MainRole is CustomRoles.Witch or CustomRoles.HexMaster or CustomRoles.Vampire or CustomRoles.Warlock:
                        Achievements.Type.Inquisition.CompleteAfterGameEnd();
                        break;
                }

                if (killerState.MainRole == targetState.MainRole)
                    Achievements.Type.ThereCanOnlyBeOne.CompleteAfterGameEnd();
                
                if (Medic.ProtectList.Contains(killer.PlayerId))
                    Achievements.Type.YouUnderestimatedMe.CompleteAfterGameEnd();
            }

            if (target.AmOwner)
            {
                PlayerState targetState = Main.PlayerStates[target.PlayerId];

                if (targetState.MainRole is CustomRoles.Maverick or CustomRoles.Opportunist)
                    Achievements.Type.YouDidTheExactOppositeOfWhatYouWereSupposedToDo.Complete();
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static void OnShapeshift(PlayerControl shapeshifter, bool shapeshifting, bool animated)
    {
        try
        {
            if (Options.CurrentGameMode != CustomGameMode.Standard || Main.AllPlayerControls.Count <= MinPlayers) return;

            if (shapeshifter.AmOwner && shapeshifting && animated)
            {
                Achievements.Type.ItsMorbinTime.Complete();
                if (shapeshifter.Is(CustomRoles.Disco)) Achievements.Type.Prankster.Complete();
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}