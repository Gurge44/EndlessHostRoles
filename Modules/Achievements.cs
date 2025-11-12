using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using UnityEngine;
using UnityEngine.Networking;

// ReSharper disable InconsistentNaming

namespace EHR.Modules;

public static class Achievements
{
    public enum Type
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
        OutOfTime, // Suicide or lose due to a time limit
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
        Superhero, // Kill all Impostors (including Renegade and Parasite) and Neutral Killers alone as Sheriff
        TheKillingMachine2Point0, // Kill >= 7 players in the same game as Impostor/Neutral Killer
        ALightInTheShadows, // Walk near the Beacon during the lights sabotage so they increase your vision
        IForgotThisRoleExists, // Try to kill someone who is protected by Guardian Angel
        TheLastSurvivor, // Win as a Crewmate, when you are the only alive player left
        YouDidTheExactOppositeOfWhatYouWereSupposedToDo, // Die first as Maverick or Opportunist
        WheresTheBlueShell, // Finish second in Room Rush
        InnocentKiller, // Kill someone without having a kill button
        YouWontTellAnyone, // Silence Snitch with all tasks done as Silencer
        ExpertControl, // As Spurt, avoid reaching 0% and 100% speed for a round (If it was only to avoid 0 then they could just afk. Having to avoid both forces them to keep moving)
        YoureMyFriendNow, // Recruit anyone as Gangster/Jackal/Infection/Virus/Cultist
        ThatWasClose, // As a killer, kill Snitch with all their tasks done (or when Snitch is revealed)
        CrewHero, // As Snitch, complete all your tasks, eject all Impostors and Neutral Killers, be alive until the end and win with crewmates
        Transformer, // Complete at least 3 transformations in the same game (transformation is changing roles. For example Amnesiac => Sheriff => Sidekick)
        InnocentImpostor, // Win as Impostor, not making any kill
        ImpostorGang, // Win as Impostor with all your partners alive
        FuriousAvenger, // Find yourself in a situation when Butcher kills Avenger and everyone dies
        BrainStorm, // Get rid of all killers as a Crewmate and win with crewmates in less than 3 meetings (there must be at least 2 killers at the start of the game)
        NothingCanStopLove, // Win as Lover/Loving Impostor with your partner alone (not with other teams)
        SimonSays, // Complete the Simon's instruction
        Unsuspected, // Win a game as a killer, not being voted by anyone in all meetings
        YoureTooLate, // As Pestilence, have a killer try to kill you, but unsuccessfully for them
        Armageddon, // Survive the Tremor's doom
        GetDownMrPresident, // Protect the President as the Bodyguard
        EconomicCompetition, // Kill Merchant as Bargainer, or kill Bargainer as Merchant
        Gotcha, // Kill Bait and then get ejected
        Speedrun, // Have a game end in under 1 minute
        Carried, // Win as a madmate while the other impostors are dead
        SorryToBurstYourBubble, // Explode 5 people with 1 encased player as the Bubble
        GetDecrypted, // Successfully guess 3 roles as Decryptor
        FlagMaster, // Carry the flag the longest in CTF
        Tag, // Tag the most players in CTF
        Vectory, // Vent 50 times in one game
        Bloodbath, // Kill 8 people in the same game as Killing Machine
        AntiSaboteur, // Fix all types of sabotages (Reactor, o2, Lights, Comms) as Technician (not necessarily in the same game)
        TheBestInSchool, // Be the first who answer the Mathematician's question correctly
        Awww, // Drag Penguin as Goose (this achievement is gain after the game end)
        Ohhh, // Drag Goose as Penguin (this achievement is gain after the game end)
        Hypnosis, // Try to report a dead body during the Hypnotist's ability
        Mimicry, // Change your appearance at least 3 times as a Doppelgänger
        P2W, // Buy all items as Bargainer and win in the same game
        LuckOrObservation, // Guess 1 player correctly
        BeginnerGuesser, // Guess 2 players correctly in the same game
        GuessMaster, // Guess 4 players correctly in the same game
        BestGuesserAward, // Guess 6 players correctly in the same game
        BadLuckOrBadObservation, // Guess incorrectly and die
        Duel, // Find yourself in a situation when you are 1vs1 against another killer
        Delicious, // Get Donut Delivery's donut or a Chef's dish
        Inquisition, // Kill Vampire/Witch/Warlock/Hex Master as Crusader
        Censorship, // Silence 5 people in the same game as Silencer
        LiarLiar, // As Lawyer, fail to protect your client that is crewmate, and then win with impostors somehow
        IWishIReported, // Revive an Impostor/Converted Crewmate/Neutral Killer as Altruist
        WellMeetAgainSomeSunnyDay, // Mark someone as Hookshot
        AndForWhatDidICodeTheseCommandsForIfYouDontUseThemAtAll, // Don't use any command for an entire game
        Prankster, // Shapeshift as Disco
        WhatTheHell, // Type 666 in chat as Demon. Will others notice this? >:)
        WhyJustWhy, // As Dictator, vote out the Jester
        CommonEnemyNo1, // Win as any NK
        CoordinatedAttack, // As Jester, Executioner or Innocent, win with any of the other 2 roles
        ItsJustAPrankBro, // As Bomber, Kill half the lobby in 1 bomb
        DrivingTestFailed, // Propel someone as the Car
        MasterOfTheStones, // Get all infinity stones as Thanos
        FastestRunner, // Win in Deathrace gamemode
        CloseCall, // Survive russian roulette with 5 bullets as Roulette Grandeur
        IKnowYourNames, // Get 3 peoples name guessed as Note Killer
        ThisAintSquidGames, // Win in Mingle game mode
        ItsGamblingTime, // As Sheriff, shoot a killer 10 seconds into the game
        FriendlyFire, // Accidentally blow up your impostor partner as Sapper/Bomber/Fireworker
        MyBad, // As Tree, kill everyone in ur radius by falling
        MindReader, // As Perceiver, get all killers in your ability radius
        OhNo, // Kill the Bait as your first kill
        Why, // As Pawn, choose vanilla Crewmate after doing your tasks
        GetMuted, // As Banshee, make everyone not have a chat button in a meeting
        BadEncounter, // As Veteran, get killed while alerted (pestilence,pelican, bypass abilities, etc)
        DestinysChoice, // Die to the Wyrd's fate countdown
        YouUnderestimatedMe, // Get shielded by the Medic then kill them
        Abstain, // Don't vote any player for the entire game
        Collapse, // With Fragile, get ambushed by the Ambusher
        YouCopiedMyWholeFlow, // As Pelican, have your body scavenged
        Bloodthirsty, // As Juggernaut or Arrogance, reach your minimum kill cooldown
        Lumberjack, // As Weapon Master, kill the Tree with your Axe
        Massacre, // As Chronomancer, kill 4 or more people at once in a Slaughter
        PayUp, // Kill a player as Clerk
        AlarmClock, // Lose Sleepy from Glow getting near you
        Eavesdropper, // Hear 3 or more messages with Listener in a single meeting
        YouCopycat, // Have a Rift Maker go through your portal as Portal Maker
        Easypeasy, // Correctly guess God as Decryptor
        YourZoneIsMine, // Win in King Of The Zones game mode
        Checkmate, // Change your role as Pawn, win as your new role
        HeyRabek // Make H2SO4 as the Chemist
    }

    private static readonly string SaveFilePath = $"{Main.DataPath}/EHR_DATA/Achievements.json";

    private const string ApiBaseUrl = "https://gurge44.pythonanywhere.com/achievements";
    private const string ApiSaveEndpoint = $"{ApiBaseUrl}/save";
    private const string ApiLoadEndpoint = $"{ApiBaseUrl}/load";

    public static readonly HashSet<Type> WaitingAchievements = [];
    public static HashSet<Type> CompletedAchievements = [];

    public static void Complete(this Type type)
    {
        if (!CompletedAchievements.Add(type)) return;

        if (GameStates.IsEnded) WaitingAchievements.Add(type);
        else ShowAchievementCompletion(type);

        SaveAllData();
    }

    public static void CompleteAfterGameEnd(this Type type)
    {
        if (!CompletedAchievements.Add(type)) return;
        WaitingAchievements.Add(type);

        SaveAllData();
    }

    public static void CompleteAfterDelay(this Type type, float delay)
    {
        if (!CompletedAchievements.Add(type)) return;
        Main.Instance.StartCoroutine(CompleteAchievementAfterDelayAsync());
        SaveAllData();
        return;

        IEnumerator CompleteAchievementAfterDelayAsync()
        {
            var timer = 0f;

            while (!GameStates.IsEnded && timer < delay)
            {
                yield return null;
                timer += Time.deltaTime;
            }

            if (GameStates.IsEnded)
            {
                WaitingAchievements.Add(type);
                yield break;
            }

            ShowAchievementCompletion(type);
        }
    }

    public static void ShowWaitingAchievements()
    {
        WaitingAchievements.Do(ShowAchievementCompletion);
        WaitingAchievements.Clear();
    }

    private static void ShowAchievementCompletion(Type type)
    {
        string title = Translator.GetString("AchievementCompletedTitle");
        string description = Translator.GetString($"Achievement.{type}.Description");
        var message = $"<b>{Translator.GetString($"Achievement.{type}")}</b>\n{description}";

        ChatBubbleShower.ShowChatBubbleInRound(message, title);
    }

    private static void SaveAllData()
    {
        string json = JsonSerializer.Serialize(CompletedAchievements);
        File.WriteAllText(SaveFilePath, json);

        if (!Options.StoreCompletedAchievementsOnEHRDatabase.GetBool()) return;
        Main.Instance.StartCoroutine(SendAchievementsToApiAsync());
        return;

        IEnumerator SendAchievementsToApiAsync()
        {
            while (PlayerControl.LocalPlayer == null) yield return null;

            string userId = PlayerControl.LocalPlayer.GetClient().GetHashedPuid();

            var data = new
            {
                userId,
                achievements = CompletedAchievements
            };

            string payload = JsonSerializer.Serialize(data);

            var request = new UnityWebRequest(ApiSaveEndpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload)),
                downloadHandler = new DownloadHandlerBuffer()
            };

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("User-Agent", $"{Main.ModName} v{Main.PluginVersion}");
            yield return request.SendWebRequest();

            Logger.Msg(request.result != UnityWebRequest.Result.Success ? $"Error saving achievements: {request.error}" : "Achievements saved successfully", "Achievements.SaveAllData");
        }
    }

    public static void LoadAllData()
    {
        if (File.Exists(SaveFilePath))
        {
            string json = File.ReadAllText(SaveFilePath);
            CompletedAchievements = JsonSerializer.Deserialize<HashSet<Type>>(json);
        }
        else if (Options.StoreCompletedAchievementsOnEHRDatabase.GetBool())
        {
            Main.Instance.StartCoroutine(FetchAchievementsFromApiAsync());
            return;

            IEnumerator FetchAchievementsFromApiAsync()
            {
                while (PlayerControl.LocalPlayer == null) yield return null;
                yield return new WaitForSeconds(3f);

                string userId = PlayerControl.LocalPlayer.GetClient().GetHashedPuid();
                var url = $"{ApiLoadEndpoint}?userId={userId}";

                UnityWebRequest request = UnityWebRequest.Get(url);
                request.SetRequestHeader("User-Agent", $"{Main.ModName} v{Main.PluginVersion}");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                    Logger.Error($"Error loading achievements: {request.error}", "Achievements.LoadAllData");
                else
                {
                    string json = request.downloadHandler.text;
                    CompletedAchievements = JsonSerializer.Deserialize<HashSet<Type>>(json);
                    File.WriteAllText(SaveFilePath, json);
                    Logger.Info("Achievements loaded successfully.", "Achievements.LoadAllData");
                }
            }
        }
    }
}