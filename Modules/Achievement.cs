namespace EHR.Modules
{
    public class Achievement
    {
        public enum AchievementType
        {
            IdeasLiveOn, // As Undead, win with at least 3 Necromancers
            HowCouldIBelieveThem, // Vote for Jester that was ejected
            WhatKindOfGeniusCameUpWithThisRoleIdea, // Get Dad role
            StabbingTheBack, // Kill a Crewmate as Backstabber
            Honk, // Drag someone as Goose
            Bruh, // Get vanilla Crewmate role
            HowDoICraftThisAgain, // Craft something as Adventurer
            ImCrewISwear, // Kill the last Impostor with all Neutral Killers dead
            Covid20, // Infect 2 people as Virus
            TheresThisGameMyDadTaughtMeItsCalledSwitch, // Swap as or be Swapped by Shifter at least 3 times in one round
            FirstDayOnTheJob, // As an Amnesiac, remember that you were a Sidekick or Deathknight
            UnderNewManagement, // As an Amnesiac, remember that you were a Cultist or Virus
            OutOfTime, // Suicide or lose due to your role or an addon
            TooCold, // Get frozen by Freezer, Beartrap, etc.
            APerfectTimeToRewindIt, // Rewind time as Time Master
            SerialKiller, // Kill everyone in FFA mode
            PVPMaster, // Win in Solo PVP mode
            HarderThanDrivingThroughTrafficLightsRight, // Win in Stop and Go mode
            TwoFast4You, // Win in Speedrun mode
            TooHotForMe, // Win in Hot Potato mode
            SeekAndHide, // Win in Hide and Seek mode
            Two012, // Win in Natural Disasters mode
            YourFlagIsMine, // Win in Capture The Flag mode
            BestReactionTime, // Win in Room Rush mode
            KimJongUnExperience, // Vote for someone as Dictator
            ImUnstoppable, // Get Medic's shield as Snitch
            ThereCanOnlyBeOne, // Kill someone who had the same role as you
            CantTouchThis, // As Detour, redirect at least 3 interactions
            ItsMorbinTime, // Shapeshift into someone
            WhatHaveIDone, // Kill your Lover, Romantic, Lawyer or Follower
            BetrayalLevel100, // Kill Madmate as Impostor or Impostor as Madmate
            TheRealTraitor, // Kill renegade as traitor (the neutral one)
            DiePleaseDie, // Try to kill the same player with Medic's shield 2 times, both unsuccessfully
            MasterDetective, // As Crewmate, vote only for Impostors and Neutral Killers
        }
    }
}