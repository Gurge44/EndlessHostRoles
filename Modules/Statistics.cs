using System.Linq;

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
            
            if (won && addons.Contains(CustomRoles.Undead) && CustomWinnerHolder.WinnerIds.ToValidPlayers().Count(x => x.Is(CustomRoles.Necromancer)) >= 3)
                Achievements.Type.IdeasLiveOn.Complete();
        }

        public static void OnVotingComplete(MeetingHud.VoterState[] states, NetworkedPlayerInfo exiledPlayer, bool tie, bool dictator)
        {
            const float delay = 8f;
            if (!tie && exiledPlayer != null && exiledPlayer.Object != null && exiledPlayer.Object.Is(CustomRoles.Jester))
            {
                if (states.Any(x => x.VoterId == PlayerControl.LocalPlayer.PlayerId && x.VotedForId == exiledPlayer.PlayerId))
                    Achievements.Type.HowCouldIBelieveThem.CompleteAfterDelay(delay);
                
                if (dictator && PlayerControl.LocalPlayer.Is(CustomRoles.Dictator) && states.FindFirst(x => x.VoterId == PlayerControl.LocalPlayer.PlayerId, out var lpVS) && lpVS.VotedForId == exiledPlayer.PlayerId)
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
    }
}