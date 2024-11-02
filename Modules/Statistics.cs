using System.Linq;
using EHR.Neutral;

namespace EHR.Modules
{
    public static class Statistics
    {
        public static void OnGameEnd()
        {
            var lp = PlayerControl.LocalPlayer;
            var role = lp.GetCustomRole();
            var addons = lp.GetCustomSubRoles();

            bool won = CustomWinnerHolder.WinnerIds.Contains(lp.PlayerId) || CustomWinnerHolder.WinnerRoles.Contains(role) || (CustomWinnerHolder.WinnerTeam == CustomWinner.Bloodlust && addons.Contains(CustomRoles.Bloodlust));

            switch (Options.CurrentGameMode)
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
                case CustomGameMode.CaptureTheFlag when won:
                    Achievements.Type.YourFlagIsMine.CompleteAfterGameEnd();
                    return;
                case CustomGameMode.RoomRush when won:
                    Achievements.Type.BestReactionTime.CompleteAfterGameEnd();
                    return;
            }

            if (won && addons.Contains(CustomRoles.Undead) && CustomWinnerHolder.WinnerIds.ToValidPlayers().Count(x => x.Is(CustomRoles.Necromancer)) >= 3)
                Achievements.Type.IdeasLiveOn.CompleteAfterGameEnd();
        }

        public static void OnVotingComplete(MeetingHud.VoterState[] states, NetworkedPlayerInfo exiledPlayer, bool tie, bool dictator)
        {
            const float delay = 8f;

            bool amDictator = dictator && PlayerControl.LocalPlayer.Is(CustomRoles.Dictator);

            if (amDictator) Achievements.Type.KimJongUnExperience.Complete();

            if (!tie && exiledPlayer != null && exiledPlayer.Object != null && exiledPlayer.Object.Is(CustomRoles.Jester))
            {
                if (states.Any(x => x.VoterId == PlayerControl.LocalPlayer.PlayerId && x.VotedForId == exiledPlayer.PlayerId))
                    Achievements.Type.HowCouldIBelieveThem.CompleteAfterDelay(delay);

                if (amDictator && states.FindFirst(x => x.VoterId == PlayerControl.LocalPlayer.PlayerId, out var lpVS) && lpVS.VotedForId == exiledPlayer.PlayerId)
                    Achievements.Type.WhyJustWhy.CompleteAfterDelay(delay);
            }
        }

        public static void OnRoleSelectionComplete()
        {
            if (!CustomRoleSelector.RoleResult.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out CustomRoles role)) return;

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

        public static void OnMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            {
                if (Main.AliveImpostorCount == 0 && killer.IsCrewmate() && target.IsImpostor() && !Main.AllAlivePlayerControls.Any(x => x.IsNeutralKiller()))
                    Achievements.Type.ImCrewISwear.Complete();

                if (killer.GetCustomRole() == target.GetCustomRole())
                    Achievements.Type.ThereCanOnlyBeOne.CompleteAfterGameEnd();

                PlayerState targetState = Main.PlayerStates[target.PlayerId];

                if ((targetState.MainRole == CustomRoles.Romantic && Romantic.PartnerId == killer.PlayerId) ||
                    (targetState.SubRoles.Contains(CustomRoles.Lovers) && killer.Is(CustomRoles.Lovers)) ||
                    (targetState.MainRole == CustomRoles.Lawyer && Lawyer.Target[target.PlayerId] == killer.PlayerId) ||
                    (targetState.Role is Totocalcio tc && tc.BetPlayer == killer.PlayerId))
                    Achievements.Type.WhatHaveIDone.CompleteAfterGameEnd();
            }
        }

        public static void OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting, bool animated)
        {
            if (shapeshifter.PlayerId == PlayerControl.LocalPlayer.PlayerId && shapeshifting && animated)
                Achievements.Type.ItsMorbinTime.Complete();
        }
    }
}