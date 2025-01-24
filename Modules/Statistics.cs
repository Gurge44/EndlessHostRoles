using System;
using System.Linq;
using EHR.AddOns.Common;
using EHR.Crewmate;
using EHR.Neutral;

namespace EHR.Modules;

public static class Statistics
{
    private const int MinPlayers = 3;
    private static bool OnlyVotingForKillersAsCrew = true;
    private static bool VotedBySomeone;
    public static int VentTimes;
    public static bool HasUsedAnyCommand;

    public static void OnGameEnd()
    {
        if (CustomWinnerHolder.WinnerTeam is CustomWinner.None or CustomWinner.Draw or CustomWinner.Error || Main.AllPlayerControls.Length <= MinPlayers) return;

        var lp = PlayerControl.LocalPlayer;
        var role = lp.GetCustomRole();
        var addons = lp.GetCustomSubRoles();

        try
        {
            bool won = CustomWinnerHolder.WinnerIds.Contains(lp.PlayerId) || CustomWinnerHolder.WinnerRoles.Contains(role) || (CustomWinnerHolder.WinnerTeam == CustomWinner.Bloodlust && addons.Contains(CustomRoles.Bloodlust));

            CustomGameMode gm = Options.CurrentGameMode;

            switch (gm)
            {
                case CustomGameMode.FFA when won:
                    Achievements.Type.SerialKiller.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.SoloKombat when won:
                    Achievements.Type.PVPMaster.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.MoveAndStop when won:
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

                    if (Main.AllPlayerControls.Select(x => (pc: x, time: CaptureTheFlag.GetFlagTime(x.PlayerId))).MaxBy(x => x.time).pc.PlayerId == lp.PlayerId)
                        Achievements.Type.FlagMaster.CompleteAfterGameEnd();

                    if (Main.AllPlayerControls.Select(x => (pc: x, time: CaptureTheFlag.GetTagCount(x.PlayerId))).MaxBy(x => x.time).pc.PlayerId == lp.PlayerId)
                        Achievements.Type.Tag.CompleteAfterGameEnd();

                    return;
                case CustomGameMode.RoomRush when won:
                    Achievements.Type.BestReactionTime.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.Standard:
                    Reset();
                    break;
            }

            if (won && addons.Contains(CustomRoles.Undead) && CustomWinnerHolder.WinnerIds.ToValidPlayers().Count(x => x.Is(CustomRoles.Necromancer)) >= 3)
                Achievements.Type.IdeasLiveOn.CompleteAfterGameEnd();

            if (Main.PlayerStates.Values.Count(x => x.GetRealKiller() == lp.PlayerId) >= 7)
                Achievements.Type.TheKillingMachine2Point0.CompleteAfterGameEnd();

            if (won && lp.IsCrewmate() && Main.AllAlivePlayerControls.Length == 1)
                Achievements.Type.TheLastSurvivor.CompleteAfterGameEnd();

            if (addons.Contains(CustomRoles.Spurt) && Spurt.LocalPlayerAvoidsZeroAndOneHundredPrecent)
                Achievements.Type.ExpertControl.CompleteAfterGameEnd();

            if (Utils.GameStartTimeStamp + 90 > Utils.TimeStamp)
                Achievements.Type.Speedrun.CompleteAfterGameEnd();

            if (won && CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor && lp.IsMadmate() && !Main.AllAlivePlayerControls.Any(x => x.IsImpostor()))
                Achievements.Type.Carried.CompleteAfterGameEnd();

            if (won && lp.IsNeutralKiller())
                Achievements.Type.CommonEnemyNo1.CompleteAfterGameEnd();

            if (won && lp.IsImpostor())
            {
                if (Main.PlayerStates.Values.All(x => x.GetRealKiller() != lp.PlayerId))
                    Achievements.Type.InnocentImpostor.CompleteAfterGameEnd();

                if (Main.NormalOptions.NumImpostors > 1 && Main.AllPlayerControls.Where(x => x.IsImpostor()).All(x => x.IsAlive()))
                    Achievements.Type.ImpostorGang.CompleteAfterGameEnd();
            }

            switch (CustomWinnerHolder.WinnerTeam)
            {
                case CustomWinner.None when Main.PlayerStates.Values.FindFirst(x => x.SubRoles.Contains(CustomRoles.Avanger), out var state) && state.GetRealKiller().GetPlayer().Is(CustomRoles.OverKiller):
                    Achievements.Type.FuriousAvenger.CompleteAfterGameEnd();
                    break;
                case CustomWinner.Crewmate when won && Main.AllPlayerControls.Count(x => x.IsImpostor() || x.IsNeutralKiller()) >= 2 && MeetingStates.MeetingNum < 3:
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
                case CustomRoles.Snitch when Snitch.IsExposed.TryGetValue(lp.PlayerId, out var exposed) && exposed && CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate && won:
                    Achievements.Type.CrewHero.Complete();
                    break;
                case CustomRoles.Minimalism when Main.PlayerStates.Values.Count(x => x.GetRealKiller() == lp.PlayerId) >= 8:
                    Achievements.Type.Bloodbath.CompleteAfterGameEnd();
                    break;
                case CustomRoles.Technician when Technician.LocalPlayerFixedSabotageTypes.Count >= 4:
                    Achievements.Type.AntiSaboteur.CompleteAfterGameEnd();
                    break;
                case CustomRoles.Bargainer when won && Bargainer.PurchasedItems.Count >= 3:
                    Achievements.Type.P2W.CompleteAfterGameEnd();
                    break;
                case CustomRoles.Lawyer when Lawyer.Target.TryGetValue(lp.PlayerId, out var ltg) && Main.PlayerStates.TryGetValue(ltg, out var ltgState) && ltgState.IsDead && ltgState.MainRole.IsCrewmate() && !ltgState.SubRoles.Contains(CustomRoles.Bloodlust) && won && CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor:
                    Achievements.Type.LiarLiar.CompleteAfterGameEnd();
                    break;
            }

            int correctGuesses = Main.PlayerStates.Values.Count(x => !x.Player.IsLocalPlayer() && x.GetRealKiller() == lp.PlayerId && x.deathReason == PlayerState.DeathReason.Gambled);

            if (correctGuesses >= 1) Achievements.Type.LuckOrObservation.CompleteAfterGameEnd();
            if (correctGuesses >= 2) Achievements.Type.BeginnerGuesser.CompleteAfterGameEnd();
            if (correctGuesses >= 4) Achievements.Type.GuessMaster.CompleteAfterGameEnd();
            if (correctGuesses >= 6) Achievements.Type.BestGuesserAward.CompleteAfterGameEnd();


            if (gm == CustomGameMode.Standard) return;

            Main.NumWinsPerGM.TryAdd(gm, []);
            Main.NumWinsPerGM[gm].AddRange(CustomWinnerHolder.WinnerIds.ToValidPlayers().ToDictionary(x => x.GetClient().GetHashedPuid(), _ => 0), overrideExistingKeys: false);
            Main.NumWinsPerGM[gm].AdjustAllValues(x => ++x);
        }
        catch (Exception e) { Utils.ThrowException(e); }

        return;

        void Reset()
        {
            if (OnlyVotingForKillersAsCrew && lp.IsCrewmate() && !MeetingStates.FirstMeeting) Achievements.Type.MasterDetective.CompleteAfterGameEnd();
            OnlyVotingForKillersAsCrew = true;

            if (!VotedBySomeone && (lp.IsImpostor() || lp.IsNeutralKiller()) && !MeetingStates.FirstMeeting) Achievements.Type.Unsuspected.CompleteAfterGameEnd();
            VotedBySomeone = false;

            if (VentTimes >= 50) Achievements.Type.Vectory.CompleteAfterGameEnd();
            VentTimes = 0;

            if (!HasUsedAnyCommand) Achievements.Type.AndForWhatDidICodeTheseCommandsForIfYouDontUseThemAtAll.CompleteAfterGameEnd();
            HasUsedAnyCommand = false;
        }
    }

    public static void OnVotingComplete(MeetingHud.VoterState[] states, NetworkedPlayerInfo exiledPlayer, bool tie, bool dictator)
    {
        try
        {
            if (!CustomGameMode.Standard.IsActiveOrIntegrated() || Main.AllPlayerControls.Length <= MinPlayers) return;

            PlayerControl lp = PlayerControl.LocalPlayer;

            bool amDictator = dictator && lp.Is(CustomRoles.Dictator);

            if (amDictator) Achievements.Type.KimJongUnExperience.Complete();

            bool lpHasVS = states.FindFirst(x => x.VoterId == lp.PlayerId, out var lpVS);

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

                if (!voteTarget.IsCrewmate() && !voteTarget.IsConverted())
                    OnlyVotingForKillersAsCrew = false;
            }

            if (states.Any(x => x.VotedForId == lp.PlayerId))
                VotedBySomeone = true;

            if (exiled && exiledPlayer.PlayerId == lp.PlayerId && Main.PlayerStates.Values.Any(x => x.SubRoles.Contains(CustomRoles.Bait) && x.GetRealKiller() == lp.PlayerId))
                Achievements.Type.Gotcha.Complete();

            if (lp.Is(CustomRoles.Lyncher) && Main.PlayerStates.Values.Count(x => x.deathReason == PlayerState.DeathReason.Gambled && x.GetRealKiller() == lp.PlayerId) >= 3)
                Achievements.Type.GetLynched.Complete();

            LateTask.New(() =>
            {
                if (GameStates.IsEnded) return;

                var aapc = Main.AllAlivePlayerControls;

                if (aapc.Length == 2 && lp.IsAlive() && aapc.All(x => x.IsNeutralKiller() || x.IsImpostor()))
                    Achievements.Type.Duel.Complete();
            }, 12f, log: false);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static void OnRoleSelectionComplete()
    {
        try
        {
            if (!CustomRoleSelector.RoleResult.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out CustomRoles role) || Main.AllPlayerControls.Length <= MinPlayers) return;

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
            if (!CustomGameMode.Standard.IsActiveOrIntegrated() || killer.PlayerId == target.PlayerId || Main.AllPlayerControls.Length <= MinPlayers) return;

            if (killer.IsLocalPlayer())
            {
                if (Main.AliveImpostorCount == 0 && killer.IsCrewmate() && target.IsImpostor() && !Main.AllAlivePlayerControls.Any(x => x.IsNeutralKiller()))
                    Achievements.Type.ImCrewISwear.Complete();

                if ((killer.IsImpostor() && target.IsMadmate()) || (killer.IsMadmate() && target.IsImpostor()))
                    Achievements.Type.BetrayalLevel100.CompleteAfterGameEnd();

                PlayerState killerState = Main.PlayerStates[killer.PlayerId];
                PlayerState targetState = Main.PlayerStates[target.PlayerId];

                if ((targetState.MainRole == CustomRoles.Romantic && Romantic.PartnerId == killer.PlayerId) ||
                    (targetState.SubRoles.Contains(CustomRoles.Lovers) && killer.Is(CustomRoles.Lovers)) ||
                    (targetState.MainRole == CustomRoles.Lawyer && Lawyer.Target.TryGetValue(target.PlayerId, out var ltg) && ltg == killer.PlayerId) ||
                    (targetState.Role is Totocalcio tc && tc.BetPlayer == killer.PlayerId))
                    Achievements.Type.WhatHaveIDone.CompleteAfterGameEnd();

                if (targetState.MainRole == CustomRoles.Snitch && Snitch.IsExposed.TryGetValue(target.PlayerId, out bool exposed) && exposed)
                    Achievements.Type.ThatWasClose.CompleteAfterGameEnd();

                switch (killerState.MainRole)
                {
                    case CustomRoles.Traitor when targetState.MainRole == CustomRoles.Refugee:
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
            }

            if (target.IsLocalPlayer())
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
            if (!CustomGameMode.Standard.IsActiveOrIntegrated() || Main.AllPlayerControls.Length <= MinPlayers) return;

            if (shapeshifter.IsLocalPlayer() && shapeshifting && animated)
            {
                Achievements.Type.ItsMorbinTime.Complete();
                if (shapeshifter.Is(CustomRoles.Disco)) Achievements.Type.Prankster.Complete();
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}