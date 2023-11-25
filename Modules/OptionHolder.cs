using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TOHE.Modules;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.AddOns.Impostor;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE;

[Flags]
public enum CustomGameMode
{
    Standard = 0x01,
    SoloKombat = 0x02,
    FFA = 0x03,
    All = int.MaxValue
}

[HarmonyPatch]
public static class Options
{
    static Task taskOptionsLoad;
    [HarmonyPatch(typeof(TranslationController), nameof(TranslationController.Initialize)), HarmonyPostfix]
    public static void OptionsLoadStart()
    {
        Logger.Info("Options.Load Start", "Options");
        taskOptionsLoad = Task.Run(Load);
        taskOptionsLoad.ContinueWith(t => { Logger.Info("Options.Load End", "Load Options"); });
    }
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPostfix]
    public static void WaitOptionsLoad()
    {
        return;
        //taskOptionsLoad.Wait();
        //Logger.Info("Options.Load End", "Options");
    }

    // プリセット
    private static readonly string[] presets =
    [
        Main.Preset1.Value, Main.Preset2.Value, Main.Preset3.Value,
        Main.Preset4.Value, Main.Preset5.Value
    ];

    // ゲームモード
    public static OptionItem GameMode;
    public static CustomGameMode CurrentGameMode
        => GameMode.GetInt() switch
        {
            1 => CustomGameMode.SoloKombat,
            2 => CustomGameMode.FFA,
            _ => CustomGameMode.Standard
        };

    public static readonly string[] gameModes =
    [
        "Standard", "SoloKombat", "FFA"
    ];

    // MapActive
    public static bool IsActiveSkeld => Main.NormalOptions.MapId == 0;
    public static bool IsActiveMiraHQ => Main.NormalOptions.MapId == 1;
    public static bool IsActivePolus => Main.NormalOptions.MapId == 2;
    public static bool IsActiveAirship => Main.NormalOptions.MapId == 4;
    public static bool IsActiveFungle => Main.NormalOptions.MapId == 5;

    // 役職数・確率
    public static Dictionary<CustomRoles, int> roleCounts;
    public static Dictionary<CustomRoles, float> roleSpawnChances;
    public static Dictionary<CustomRoles, OptionItem> CustomRoleCounts;
    public static Dictionary<CustomRoles, StringOptionItem> CustomRoleSpawnChances;
    public static Dictionary<CustomRoles, IntegerOptionItem> CustomAdtRoleSpawnRate;
    public static readonly string[] rates =
    [
        "Rate0",  "Rate5",  "Rate10", "Rate20", "Rate30", "Rate40",
        "Rate50", "Rate60", "Rate70", "Rate80", "Rate90", "Rate100",
    ];
    public static readonly string[] ratesZeroOne =
    [
        "RoleOff", /*"Rate10", "Rate20", "Rate30", "Rate40", "Rate50",
        "Rate60", "Rate70", "Rate80", "Rate90", */"RoleRate",
    ];
    public static readonly string[] ratesToggle =
    [
        "RoleOff", "RoleRate", "RoleOn"
    ];
    public static readonly string[] CheatResponsesName =
    [
        "Ban", "Kick", "NoticeMe","NoticeEveryone"
    ];
    public static readonly string[] ConfirmEjectionsMode =
    [
        "ConfirmEjections.None",
        "ConfirmEjections.Team",
        "ConfirmEjections.Role"
    ];
    public static readonly string[] CamouflageMode =
    [
        "CamouflageMode.Default",
        "CamouflageMode.Host",
        "CamouflageMode.Karpe",
        "CamouflageMode.Lauryn",
        "CamouflageMode.Moe",
        "CamouflageMode.Pyro",
        "CamouflageMode.ryuk",
        "CamouflageMode.Gurge44",
        "CamouflageMode.TommyXL"
    ];
    public static readonly string[] PetToAssign =
    [
        "pet_Bedcrab",
        "pet_BredPet",
        "pet_YuleGoatPet",
        "pet_Bush",
        "pet_Charles",
        "pet_ChewiePet",
        "pet_clank",
        "pet_coaltonpet",
        "pet_Cube",
        "pet_Doggy",
        "pet_Ellie",
        "pet_frankendog",
        "pet_D2GhostPet",
        "pet_test",
        "pet_GuiltySpark",
        "pet_Stickmin",
        "pet_HamPet",
        "pet_Hamster",
        "pet_Alien",
        "pet_poro",
        "pet_HamPet",
        "pet_Lava",
        "pet_Crewmate",
        "pet_D2PoukaPet",
        "pet_Pusheen",
        "pet_Robot",
        "pet_Snow",
        "pet_Squig",
        "pet_nuggetPet",
        "pet_Charles_Red",
        "pet_UFO",
        "pet_D2WormPet",
        "pet_RANDOM_FOR_EVERYONE"
    ];

    // 各役職の詳細設定
    public static OptionItem EnableGM;
    public static float DefaultKillCooldown = Main.NormalOptions?.KillCooldown ?? 25;

    public static OptionItem DisableMeeting;
    public static OptionItem DisableCloseDoor;
    public static OptionItem DisableSabotage;
    public static OptionItem DisableTaskWin;

    public static OptionItem KillFlashDuration;
    public static OptionItem EnableKillerLeftCommand;
    public static OptionItem SeeEjectedRolesInMeeting;
    //public static OptionItem ShareLobby;
    //public static OptionItem ShareLobbyMinPlayer;
    public static OptionItem DisableShieldAnimations;
    public static OptionItem DisableCrackedGlass;
    public static OptionItem DisableShapeshiftAnimations;
    public static OptionItem DisableKillAnimationOnGuess;
    public static OptionItem DisableVanillaRoles;
    public static OptionItem DisableHiddenRoles;
    public static OptionItem DisableSunnyboy;
    public static OptionItem SabotageCooldownControl;
    public static OptionItem SabotageCooldown;
    public static OptionItem SunnyboyChance;
    public static OptionItem DisableBard;
    public static OptionItem BardChance;
    public static OptionItem DisableSaboteur;
    public static OptionItem CEMode;
    public static OptionItem ConfirmEjectionsNK;
    public static OptionItem ConfirmEjectionsNonNK;
    public static OptionItem ConfirmEjectionsNeutralAsImp;
    public static OptionItem ShowImpRemainOnEject;
    public static OptionItem ShowNKRemainOnEject;
    public static OptionItem ShowTeamNextToRoleNameOnEject;
    public static OptionItem CheatResponses;
    public static OptionItem LowLoadMode;
    public static OptionItem DeepLowLoad;

    // Dummy Settings
    public static OptionItem SpawnSidekickAlone;


    // Detailed Ejections //
    public static OptionItem ExtendedEjections;
    public static OptionItem ConfirmEgoistOnEject;
    public static OptionItem ConfirmSidekickOnEject;
    public static OptionItem ConfirmLoversOnEject;


    public static OptionItem NonNeutralKillingRolesMinPlayer;
    public static OptionItem NonNeutralKillingRolesMaxPlayer;
    public static OptionItem NeutralKillingRolesMinPlayer;
    public static OptionItem NeutralKillingRolesMaxPlayer;
    public static OptionItem NeutralRoleWinTogether;
    public static OptionItem NeutralWinTogether;

    public static OptionItem DefaultShapeshiftCooldown;
    public static OptionItem DeadImpCantSabotage;
    public static OptionItem ImpKnowAlliesRole;
    public static OptionItem ImpKnowWhosMadmate;
    public static OptionItem MadmateKnowWhosImp;
    public static OptionItem MadmateKnowWhosMadmate;
    public static OptionItem MadmateHasImpostorVision;
    //public static OptionItem MadmateCanFixSabotage;
    public static OptionItem ImpCanKillMadmate;
    public static OptionItem MadmateCanKillImp;
    public static OptionItem JackalCanKillSidekick;
    public static OptionItem SidekickCanKillJackal;
    public static OptionItem SidekickKnowOtherSidekick;
    public static OptionItem SidekickKnowOtherSidekickRole;
    public static OptionItem SidekickCanKillSidekick;

    public static OptionItem ShapeMasterShapeshiftDuration;
    public static OptionItem EGCanGuessImp;
    public static OptionItem EGCanGuessAdt;
    public static OptionItem EGCanGuessTaskDoneSnitch;
    public static OptionItem EGCanGuessTime;
    public static OptionItem EGTryHideMsg;
    public static OptionItem WarlockCanKillAllies;
    public static OptionItem WarlockCanKillSelf;
    public static OptionItem WarlockShiftDuration;
    public static OptionItem ScavengerKillCooldown;
    public static OptionItem ZombieKillCooldown;
    public static OptionItem ZombieSpeedReduce;
    public static OptionItem EvilWatcherChance;
    public static OptionItem GGCanGuessCrew;
    public static OptionItem GGCanGuessAdt;
    public static OptionItem GGCanGuessTime;
    public static OptionItem GGTryHideMsg;
    public static OptionItem LuckeyProbability;
    public static OptionItem LuckyProbability;
    public static OptionItem PuppeteerKCD;
    public static OptionItem PuppeteerCD;
    public static OptionItem PuppeteerCanKillNormally;
    public static OptionItem PuppeteerManipulationBypassesLazy;
    public static OptionItem PuppeteerManipulationBypassesLazyGuy;
    public static OptionItem PuppeteerPuppetCanKillPuppeteer;
    public static OptionItem PuppeteerPuppetCanKillImpostors;
    public static OptionItem PuppeteerMaxPuppets;
    public static OptionItem PuppeteerDiesAfterMaxPuppets;
    public static OptionItem PuppeteerMinDelay;
    public static OptionItem PuppeteerMaxDelay;
    public static OptionItem PuppeteerManipulationEndsAfterFixedTime;
    public static OptionItem PuppeteerManipulationEndsAfterTime;
    public static OptionItem VindicatorAdditionalVote;
    public static OptionItem VindicatorHideVote;
    public static OptionItem MayorAdditionalVote;
    public static OptionItem MayorHasPortableButton;
    public static OptionItem MayorNumOfUseButton;
    public static OptionItem MayorHideVote;
    public static OptionItem MayorRevealWhenDoneTasks;
    public static OptionItem OppoImmuneToAttacksWhenTasksDone;
    public static OptionItem DoctorTaskCompletedBatteryCharge;
    public static OptionItem SpeedBoosterUpSpeed;
    public static OptionItem SpeedBoosterTimes;
    public static OptionItem GlitchCanVote;
    public static OptionItem TrapperBlockMoveTime;
    public static OptionItem DetectiveCanknowKiller;
    public static OptionItem TransporterTeleportMax;
    public static OptionItem CanTerroristSuicideWin;
    public static OptionItem InnocentCanWinByImp;
    public static OptionItem WorkaholicVentCooldown;
    public static OptionItem WorkaholicCannotWinAtDeath;
    public static OptionItem WorkaholicVisibleToEveryone;
    public static OptionItem WorkaholicGiveAdviceAlive;
    public static OptionItem BaitNotification;
    public static OptionItem DoctorVisibleToEveryone;
    public static OptionItem JackalWinWithSidekick;
    public static OptionItem ArsonistDouseTime;
    public static OptionItem ArsonistCooldown;
    public static OptionItem ArsonistKeepsGameGoing;
    public static OptionItem ArsonistCanIgniteAnytime;
    public static OptionItem ArsonistMinPlayersToIgnite;
    public static OptionItem ArsonistMaxPlayersToIgnite;
    public static OptionItem JesterCanUseButton;
    public static OptionItem JesterCanVent;
    public static OptionItem LegacyMafia;
    public static OptionItem NotifyGodAlive;
    public static OptionItem MarioVentNumWin;
    public static OptionItem MarioVentCD;
    public static OptionItem VeteranSkillCooldown;
    public static OptionItem VeteranSkillDuration;
    public static OptionItem TimeMasterSkillCooldown;
    public static OptionItem TimeMasterSkillDuration;
    public static OptionItem TimeMasterMaxUses;
    public static OptionItem TimeMasterAbilityUseGainWithEachTaskCompleted;
    public static OptionItem VeteranSkillMaxOfUseage;
    public static OptionItem VeteranAbilityUseGainWithEachTaskCompleted;
    public static OptionItem VentguardAbilityUseGainWithEachTaskCompleted;
    public static OptionItem VentguardMaxGuards;
    public static OptionItem VentguardBlockDoesNotAffectCrew;
    public static OptionItem BodyguardProtectRadius;
    public static OptionItem BodyguardKillsKiller;
    public static OptionItem WitnessCD;
    public static OptionItem WitnessTime;
    public static OptionItem DQNumOfKillsNeeded;
    public static OptionItem ParanoiaNumOfUseButton;
    public static OptionItem ParanoiaVentCooldown;
    public static OptionItem ImpKnowCyberStarDead;
    public static OptionItem NeutralKnowCyberStarDead;
    public static OptionItem DemolitionistVentTime;
    public static OptionItem DemolitionistKillerDiesOnMeetingCall;
    public static OptionItem ExpressSpeed;
    public static OptionItem ExpressSpeedDur;
    public static OptionItem EveryOneKnowSuperStar;
    public static OptionItem MNKillCooldown;
    public static OptionItem MafiaCanKillNum;
    public static OptionItem RetributionistCanKillNum;
    public static OptionItem MinimumPlayersAliveToRetri;
    public static OptionItem CanOnlyRetributeWithTasksDone;
    public static OptionItem BomberRadius;
    public static OptionItem BomberCanKill;
    public static OptionItem BomberKillCD;
    public static OptionItem BombCooldown;
    public static OptionItem ImpostorsSurviveBombs;
    public static OptionItem BomberDiesInExplosion;
    public static OptionItem NukerChance;
    public static OptionItem NukeRadius;
    public static OptionItem NukeCooldown;
    public static OptionItem SpeedrunnerNotifyKillers;
    public static OptionItem SpeedrunnerNotifyAtXTasksLeft;

    public static OptionItem SkeldChance;
    public static OptionItem MiraChance;
    public static OptionItem PolusChance;
    public static OptionItem AirshipChance;
    public static OptionItem FungleChance;

    // UNDERDOG
    public static OptionItem UnderdogKillCooldown;
    public static OptionItem UnderdogMaximumPlayersNeededToKill;
    public static OptionItem UnderdogCanKillWithMorePlayersAlive;
    public static OptionItem UnderdogKillCooldownWithMorePlayersAlive;

    public static OptionItem CleanerKillCooldown;
    public static OptionItem KillCooldownAfterCleaning;
    public static OptionItem GuardSpellTimes;
    public static OptionItem FlashWhenTrapBoobyTrap;
    public static OptionItem CapitalismSkillCooldown;
    public static OptionItem CapitalismKillCooldown;
    public static OptionItem GrenadierSkillCooldown;
    public static OptionItem GrenadierSkillDuration;
    public static OptionItem GrenadierCauseVision;
    public static OptionItem GrenadierCanAffectNeutral;
    public static OptionItem GrenadierSkillMaxOfUseage;
    public static OptionItem GrenadierAbilityUseGainWithEachTaskCompleted;
    public static OptionItem LighterVisionNormal;
    public static OptionItem LighterVisionOnLightsOut;
    public static OptionItem LighterSkillCooldown;
    public static OptionItem LighterSkillDuration;
    public static OptionItem LighterSkillMaxOfUseage;
    public static OptionItem LighterAbilityUseGainWithEachTaskCompleted;
    public static OptionItem SecurityGuardSkillCooldown;
    public static OptionItem SecurityGuardSkillDuration;
    public static OptionItem SecurityGuardSkillMaxOfUseage;
    public static OptionItem SecurityGuardAbilityUseGainWithEachTaskCompleted;
    public static OptionItem EscapeeSSDuration;
    public static OptionItem EscapeeSSCD;
    public static OptionItem MinerSSDuration;
    public static OptionItem MinerSSCD;
    public static OptionItem RevolutionistDrawTime;
    public static OptionItem RevolutionistCooldown;
    public static OptionItem RevolutionistDrawCount;
    public static OptionItem RevolutionistKillProbability;
    public static OptionItem RevolutionistVentCountDown;
    public static OptionItem ShapeImperiusCurseShapeshiftDuration;
    public static OptionItem ImperiusCurseShapeshiftCooldown;
    public static OptionItem CrewpostorCanKillAllies;
    public static OptionItem CrewpostorKnowsAllies;
    public static OptionItem AlliesKnowCrewpostor;
    public static OptionItem CrewpostorKillAfterTask;
    public static OptionItem CrewpostorLungeKill;
    public static OptionItem ImpCanBeSeer;
    public static OptionItem CrewCanBeSeer;
    public static OptionItem NeutralCanBeSeer;
    public static OptionItem ImpCanBeAutopsy;
    public static OptionItem CrewCanBeAutopsy;
    public static OptionItem NeutralCanBeAutopsy;
    public static OptionItem ImpCanBeBewilder;
    public static OptionItem CrewCanBeBewilder;
    public static OptionItem NeutralCanBeBewilder;
    public static OptionItem ImpCanBeSunglasses;
    public static OptionItem CrewCanBeSunglasses;
    public static OptionItem NeutralCanBeSunglasses;
    public static OptionItem ImpCanBeGlow;
    public static OptionItem CrewCanBeGlow;
    public static OptionItem NeutralCanBeGlow;
    public static OptionItem ImpCanBeGuesser;
    public static OptionItem CrewCanBeGuesser;
    public static OptionItem NeutralCanBeGuesser;
    public static OptionItem ImpCanBeWatcher;
    public static OptionItem CrewCanBeWatcher;
    public static OptionItem NeutralCanBeWatcher;
    public static OptionItem ImpCanBeNecroview;
    public static OptionItem CrewCanBeNecroview;
    public static OptionItem NeutralCanBeNecroview;
    public static OptionItem ImpCanBeOblivious;
    public static OptionItem CrewCanBeOblivious;
    public static OptionItem NeutralCanBeOblivious;
    public static OptionItem ObliviousBaitImmune;
    public static OptionItem ImpCanBeTiebreaker;
    public static OptionItem CrewCanBeTiebreaker;
    public static OptionItem NeutralCanBeTiebreaker;
    public static OptionItem CrewmateCanBeSidekick;
    public static OptionItem NeutralCanBeSidekick;
    public static OptionItem ImpostorCanBeSidekick;
    public static OptionItem ImpCanBeOnbound;
    public static OptionItem CrewCanBeOnbound;
    public static OptionItem NeutralCanBeOnbound;
    public static OptionItem ImpCanBeInLove;
    public static OptionItem CrewCanBeInLove;
    public static OptionItem NeutralCanBeInLove;
    public static OptionItem ImpCanBeReflective;
    public static OptionItem CrewCanBeReflective;
    public static OptionItem NeutralCanBeReflective;
    public static OptionItem ImpCanBeUnreportable;
    public static OptionItem CrewCanBeUnreportable;
    public static OptionItem NeutralCanBeUnreportable;
    public static OptionItem ImpCanBeLucky;
    public static OptionItem CrewCanBeLucky;
    public static OptionItem NeutralCanBeLucky;
    public static OptionItem ImpCanBeUnlucky;
    public static OptionItem CrewCanBeUnlucky;
    public static OptionItem NeutralCanBeUnlucky;
    public static OptionItem UnluckyTaskSuicideChance;
    public static OptionItem UnluckyKillSuicideChance;
    public static OptionItem UnluckyVentSuicideChance;
    public static OptionItem UnluckyReportSuicideChance;
    public static OptionItem UnluckySabotageSuicideChance;
    // RASCAL //
    public static OptionItem RascalAppearAsMadmate;


    // ROGUE
    public static OptionItem ImpCanBeRogue;
    public static OptionItem CrewCanBeRogue;
    public static OptionItem NeutralCanBeRogue;
    public static OptionItem RogueKnowEachOther;
    public static OptionItem RogueKnowEachOtherRoles;

    // Gravestone
    public static OptionItem ImpCanBeGravestone;
    public static OptionItem CrewCanBeGravestone;
    public static OptionItem NeutralCanBeGravestone;

    // Mare Add-on
    public static OptionItem MareKillCD;
    public static OptionItem MareKillCDNormally;
    public static OptionItem MareHasIncreasedSpeed;
    public static OptionItem MareSpeedDuringLightsOut;

    public static OptionItem AutoPlayAgain;
    public static OptionItem AutoPlayAgainCountdown;
    public static OptionItem AutoStartTimer;
    
    public static OptionItem ControlCooldown;
    public static OptionItem InhibitorCD;
    public static OptionItem InhibitorCDAfterMeetings;
    public static OptionItem SaboteurCD;
    public static OptionItem SaboteurCDAfterMeetings;
    public static OptionItem JesterVision;
    public static OptionItem PhantomCanVent;
    public static OptionItem PhantomSnatchesWin;
    // public static OptionItem LawyerVision;
    public static OptionItem ImpCanBeDiseased;
    public static OptionItem CrewCanBeDiseased;
    public static OptionItem NeutralCanBeDiseased;
    public static OptionItem DiseasedCDOpt;
    public static OptionItem DiseasedCDReset;

    public static OptionItem ImpCanBeAntidote;
    public static OptionItem CrewCanBeAntidote;
    public static OptionItem NeutralCanBeAntidote;
    public static OptionItem AntidoteCDOpt;
    public static OptionItem AntidoteCDReset;

    public static OptionItem ImpCanBeBait;
    public static OptionItem CrewCanBeBait;
    public static OptionItem NeutralCanBeBait;
    public static OptionItem BaitDelayMin;
    public static OptionItem BaitDelayMax;
    public static OptionItem BaitDelayNotify;
    public static OptionItem ImpCanBeTrapper;
    public static OptionItem CrewCanBeTrapper;
    public static OptionItem NeutralCanBeTrapper;
    public static OptionItem ImpCanBeFool;
    public static OptionItem CrewCanBeFool;
    public static OptionItem NeutralCanBeFool;
    public static OptionItem TorchVision;
    public static OptionItem TorchAffectedByLights;
    public static OptionItem TasklessCrewCanBeLazy;
    public static OptionItem TaskBasedCrewCanBeLazy;
    public static OptionItem DovesOfNeaceCooldown;
    public static OptionItem DovesOfNeaceMaxOfUseage;
    public static OptionItem DovesOfNeaceAbilityUseGainWithEachTaskCompleted;
    public static OptionItem BTKillCooldown;
    public static OptionItem TrapOnlyWorksOnTheBodyBoobyTrap;
    public static OptionItem ImpCanBeDoubleShot;
    public static OptionItem CrewCanBeDoubleShot;
    public static OptionItem killAttacker;
    public static OptionItem NeutralCanBeDoubleShot;
    public static OptionItem MimicCanSeeDeadRoles;
    public static OptionItem ResetDoorsEveryTurns;
    public static OptionItem DoorsResetMode;

    //public static OptionItem NSerialKillerKillCD;
    //public static OptionItem NSerialKillerHasImpostorVision;
    //public static OptionItem NSerialKillerCanVent;

    public static OptionItem ParasiteCD;

    public static OptionItem ShapeshiftCD;
    public static OptionItem ShapeshiftDur;

    public static OptionItem MafiaShapeshiftCD;
    public static OptionItem MafiaShapeshiftDur;

    public static OptionItem ScientistDur;
    public static OptionItem ScientistCD;

    public static OptionItem GCanGuessImp;
    public static OptionItem GCanGuessCrew;
    public static OptionItem GCanGuessAdt;
    public static OptionItem GCanGuessTaskDoneSnitch;
    public static OptionItem GTryHideMsg;

    // Masochist
    //public static OptionItem MasochistKillMax;


    //Task Management
    public static OptionItem DisableShortTasks;
    public static OptionItem DisableCleanVent;
    public static OptionItem DisableCalibrateDistributor;
    public static OptionItem DisableChartCourse;
    public static OptionItem DisableStabilizeSteering;
    public static OptionItem DisableCleanO2Filter;
    public static OptionItem DisableUnlockManifolds;
    public static OptionItem DisablePrimeShields;
    public static OptionItem DisableMeasureWeather;
    public static OptionItem DisableBuyBeverage;
    public static OptionItem DisableAssembleArtifact;
    public static OptionItem DisableSortSamples;
    public static OptionItem DisableProcessData;
    public static OptionItem DisableRunDiagnostics;
    public static OptionItem DisableRepairDrill;
    public static OptionItem DisableAlignTelescope;
    public static OptionItem DisableRecordTemperature;
    public static OptionItem DisableFillCanisters;
    public static OptionItem DisableMonitorTree;
    public static OptionItem DisableStoreArtifacts;
    public static OptionItem DisablePutAwayPistols;
    public static OptionItem DisablePutAwayRifles;
    public static OptionItem DisableMakeBurger;
    public static OptionItem DisableCleanToilet;
    public static OptionItem DisableDecontaminate;
    public static OptionItem DisableSortRecords;
    public static OptionItem DisableFixShower;
    public static OptionItem DisablePickUpTowels;
    public static OptionItem DisablePolishRuby;
    public static OptionItem DisableDressMannequin;
    public static OptionItem DisableCommonTasks;
    public static OptionItem DisableSwipeCard;
    public static OptionItem DisableFixWiring;
    public static OptionItem DisableEnterIdCode;
    public static OptionItem DisableInsertKeys;
    public static OptionItem DisableScanBoardingPass;
    public static OptionItem DisableLongTasks;
    public static OptionItem DisableSubmitScan;
    public static OptionItem DisableUnlockSafe;
    public static OptionItem DisableStartReactor;
    public static OptionItem DisableResetBreaker;
    public static OptionItem DisableAlignEngineOutput;
    public static OptionItem DisableInspectSample;
    public static OptionItem DisableEmptyChute;
    public static OptionItem DisableClearAsteroids;
    public static OptionItem DisableWaterPlants;
    public static OptionItem DisableOpenWaterways;
    public static OptionItem DisableReplaceWaterJug;
    public static OptionItem DisableRebootWifi;
    public static OptionItem DisableDevelopPhotos;
    public static OptionItem DisableRewindTapes;
    public static OptionItem DisableStartFans;
    public static OptionItem DisableOtherTasks;
    public static OptionItem DisableUploadData;
    public static OptionItem DisableEmptyGarbage;
    public static OptionItem DisableFuelEngines;
    public static OptionItem DisableDivertPower;
    public static OptionItem DisableActivateWeatherNodes;
    public static OptionItem DisableRoastMarshmallow;
    public static OptionItem DisableCollectSamples;
    public static OptionItem DisableReplaceParts;
    public static OptionItem DisableCollectVegetables;
    public static OptionItem DisableMineOres;
    public static OptionItem DisableExtractFuel;
    public static OptionItem DisableCatchFish;
    public static OptionItem DisablePolishGem;
    public static OptionItem DisableHelpCritter;
    public static OptionItem DisableHoistSupplies;
    public static OptionItem DisableFixAntenna;
    public static OptionItem DisableBuildSandcastle;
    public static OptionItem DisableCrankGenerator;
    public static OptionItem DisableMonitorMushroom;
    public static OptionItem DisablePlayVideoGame;
    public static OptionItem DisableFindSignal;
    public static OptionItem DisableThrowFisbee;
    public static OptionItem DisableLiftWeights;
    public static OptionItem DisableCollectShells;

    // Merchant Filters //
    public static OptionItem BaitCanBeSold;
    public static OptionItem WatcherCanBeSold;
    public static OptionItem SeerCanBeSold;
    public static OptionItem TrapperCanBeSold;
    public static OptionItem TiebreakerCanBeSold;
    public static OptionItem KnightedCanBeSold;
    public static OptionItem NecroviewCanBeSold;
    public static OptionItem SoullessCanBeSold;
    public static OptionItem SchizoCanBeSold;
    public static OptionItem OnboundCanBeSold;
    public static OptionItem GuesserCanBeSold;
    public static OptionItem UnreportableCanBeSold;
    public static OptionItem LuckyCanBeSold;
    public static OptionItem ObliviousCanBeSold;
    public static OptionItem BewilderCanBeSold;

    //デバイスブロック
    public static OptionItem DisableDevices;
    public static OptionItem DisableSkeldDevices;
    public static OptionItem DisableSkeldAdmin;
    public static OptionItem DisableSkeldCamera;
    public static OptionItem DisableMiraHQDevices;
    public static OptionItem DisableMiraHQAdmin;
    public static OptionItem DisableMiraHQDoorLog;
    public static OptionItem DisablePolusDevices;
    public static OptionItem DisablePolusAdmin;
    public static OptionItem DisablePolusCamera;
    public static OptionItem DisablePolusVital;
    public static OptionItem DisableAirshipDevices;
    public static OptionItem DisableAirshipCockpitAdmin;
    public static OptionItem DisableAirshipRecordsAdmin;
    public static OptionItem DisableAirshipCamera;
    public static OptionItem DisableAirshipVital;
    public static OptionItem DisableFungleDevices;
    public static OptionItem DisableFungleCamera;
    public static OptionItem DisableFungleVital;
    public static OptionItem DisableDevicesIgnoreConditions;
    public static OptionItem DisableDevicesIgnoreImpostors;
    public static OptionItem DisableDevicesIgnoreNeutrals;
    public static OptionItem DisableDevicesIgnoreCrewmates;
    public static OptionItem DisableDevicesIgnoreAfterAnyoneDied;

    // Maps
    public static OptionItem RandomMapsMode;
    public static OptionItem AddedTheSkeld;
    public static OptionItem AddedMiraHQ;
    public static OptionItem AddedPolus;
    public static OptionItem AddedTheAirship;
    public static OptionItem AddedDleks;
    public static OptionItem RandomSpawn;
    public static OptionItem AirshipAdditionalSpawn;
    public static OptionItem AirshipVariableElectrical;
    public static OptionItem DisableAirshipMovingPlatform;
    public static OptionItem DisableSporeTriggerOnFungle;
    public static OptionItem DisableZiplineOnFungle;
    public static OptionItem DisableZiplineFromTop;
    public static OptionItem DisableZiplineFromUnder;
    public static OptionItem DisableZiplineForImps;
    public static OptionItem DisableZiplineForNeutrals;
    public static OptionItem DisableZiplineForCrew;
    public static OptionItem ZiplineTravelTimeFromBottom;
    public static OptionItem ZiplineTravelTimeFromTop;

    // Sabotage
    public static OptionItem CommsCamouflage;
    public static OptionItem CommsCamouflageDisableOnFungle;
    public static OptionItem DisableReportWhenCC;
    public static OptionItem SabotageTimeControl;
    public static OptionItem SkeldReactorTimeLimit;
    public static OptionItem SkeldO2TimeLimit;
    public static OptionItem MiraReactorTimeLimit;
    public static OptionItem MiraO2TimeLimit;
    public static OptionItem PolusReactorTimeLimit;
    public static OptionItem AirshipReactorTimeLimit;
    public static OptionItem FungleReactorTimeLimit;
    public static OptionItem FungleMushroomMixupDuration;
    public static OptionItem LightsOutSpecialSettings;
    public static OptionItem DisableAirshipViewingDeckLightsPanel;
    public static OptionItem DisableAirshipGapRoomLightsPanel;
    public static OptionItem DisableAirshipCargoLightsPanel;

    //Guesser Mode//
    public static OptionItem GuesserMode;
    public static OptionItem CrewmatesCanGuess;
    public static OptionItem ImpostorsCanGuess;
    public static OptionItem NeutralKillersCanGuess;
    public static OptionItem PassiveNeutralsCanGuess;
    public static OptionItem HideGuesserCommands;
    public static OptionItem CanGuessAddons;
    public static OptionItem ImpCanGuessImp;
    public static OptionItem CrewCanGuessCrew;

    // Guesser Mode - Addon Config //
    public static OptionItem AddonSettingsCrew;
    public static OptionItem ClaimAddonSettingsCrew;
    public static OptionItem BetrayalAddonSettingsCrew;
    public static OptionItem ImpOnlyAddonSettingsCrew;
    public static OptionItem CrewOnlyAddonSettingsCrew;
    public static OptionItem NeutralAddonSettingsCrew;
    public static OptionItem BasicAddonSettingsCrew;

    public static OptionItem AddonSettingsImp;
    public static OptionItem ClaimAddonSettingsImp;
    public static OptionItem BetrayalAddonSettingsImp;
    public static OptionItem ImpOnlyAddonSettingsImp;
    public static OptionItem CrewOnlyAddonSettingsImp;
    public static OptionItem NeutralAddonSettingsImp;
    public static OptionItem BasicAddonSettingsImp;
    public static OptionItem AddonSettingsNeut;
    public static OptionItem ClaimAddonSettingsNeut;
    public static OptionItem BetrayalAddonSettingsNeut;
    public static OptionItem ImpOnlyAddonSettingsNeut;
    public static OptionItem CrewOnlyAddonSettingsNeut;
    public static OptionItem NeutralAddonSettingsNeut;
    public static OptionItem BasicAddonSettingsNeut;


    // 投票モード
    public static OptionItem VoteMode;
    public static OptionItem WhenSkipVote;
    public static OptionItem WhenSkipVoteIgnoreFirstMeeting;
    public static OptionItem WhenSkipVoteIgnoreNoDeadBody;
    public static OptionItem WhenSkipVoteIgnoreEmergency;
    public static OptionItem WhenNonVote;
    public static OptionItem WhenTie;
    public static readonly string[] voteModes =
    [
        "Default", "Suicide", "SelfVote", "Skip"
    ];
    public static readonly string[] tieModes =
    [
        "TieMode.Default", "TieMode.All", "TieMode.Random"
    ];
    /* public static readonly string[] addonGuessModeCrew =
     {
         "GuesserMode.All", "GuesserMode.Harmful", "GuesserMode.Random"
     }; */
    public static readonly string[] madmateSpawnMode =
    [
        "MadmateSpawnMode.Assign",
        "MadmateSpawnMode.FirstKill",
        "MadmateSpawnMode.SelfVote",
    ];
    public static readonly string[] madmateCountMode =
    [
        "MadmateCountMode.None",
        "MadmateCountMode.Imp",
        "MadmateCountMode.Crew",
    ];
    public static readonly string[] sidekickCountMode =
    [
        "SidekickCountMode.Jackal",
        "SidekickCountMode.None",
        "SidekickCountMode.Original",
    ];
    public static VoteMode GetWhenSkipVote() => (VoteMode)WhenSkipVote.GetValue();
    public static VoteMode GetWhenNonVote() => (VoteMode)WhenNonVote.GetValue();

    // ボタン回数
    public static OptionItem SyncButtonMode;
    public static OptionItem SyncedButtonCount;
    public static int UsedButtonCount;

    // 全員生存時の会議時間
    public static OptionItem AllAliveMeeting;
    public static OptionItem AllAliveMeetingTime;

    // 追加の緊急ボタンクールダウン
    public static OptionItem AdditionalEmergencyCooldown;
    public static OptionItem AdditionalEmergencyCooldownThreshold;
    public static OptionItem AdditionalEmergencyCooldownTime;

    //転落死
    public static OptionItem LadderDeath;
    public static OptionItem LadderDeathChance;

    // タスク上書き
    public static OverrideTasksData TerroristTasks;
    public static OverrideTasksData TransporterTasks;
    public static OverrideTasksData WorkaholicTasks;
    public static OverrideTasksData SpeedrunnerTasks;
    public static OverrideTasksData CrewpostorTasks;
    public static OverrideTasksData PhantomTasks;
    public static OverrideTasksData GuardianTasks;
    public static OverrideTasksData OpportunistTasks;
    public static OverrideTasksData MayorTasks;
    public static OverrideTasksData RetributionistTasks;
    public static OverrideTasksData TimeManagerTasks;

    // その他
    public static OptionItem FixFirstKillCooldown;
    public static OptionItem StartingKillCooldown;
    public static OptionItem ShieldPersonDiedFirst;
    public static OptionItem GhostCanSeeOtherRoles;
    public static OptionItem GhostCanSeeOtherVotes;
    public static OptionItem GhostCanSeeDeathReason;
    //public static OptionItem GhostIgnoreTasks;
    public static OptionItem KPDCamouflageMode;

    // Guess Restrictions //
    public static OptionItem TerroristCanGuess;
    public static OptionItem WorkaholicCanGuess;
    public static OptionItem PhantomCanGuess;
    public static OptionItem GodCanGuess;

    // プリセット対象外
    public static OptionItem AllowConsole;
    public static OptionItem NoGameEnd;
    public static OptionItem DontUpdateDeadPlayers;
    public static OptionItem AutoDisplayLastRoles;
    public static OptionItem AutoDisplayKillLog;
    public static OptionItem AutoDisplayLastResult;
    public static OptionItem SuffixMode;
    public static OptionItem HideGameSettings;
    public static OptionItem FormatNameMode;
    public static OptionItem ColorNameMode;
    public static OptionItem DisableEmojiName;
    public static OptionItem ChangeNameToRoleInfo;
    public static OptionItem SendRoleDescriptionFirstMeeting;
    public static OptionItem RoleAssigningAlgorithm;
    public static OptionItem EndWhenPlayerBug;

    public static OptionItem UsePets;
    public static OptionItem PetToAssignToEveryone;
    public static OptionItem UseVoteCancelling;
    public static OptionItem EnableUpMode;
    public static OptionItem AutoKickStart;
    public static OptionItem AutoKickStartAsBan;
    public static OptionItem AutoKickStartTimes;
    public static OptionItem AutoKickStopWords;
    public static OptionItem AutoKickStopWordsAsBan;
    public static OptionItem AutoKickStopWordsTimes;
    public static OptionItem KickAndroidPlayer;
    public static OptionItem KickXboxPlayer;
    public static OptionItem KickPlayStationPlayer;
    public static OptionItem KickNintendoPlayer;
    public static OptionItem ApplyDenyNameList;
    public static OptionItem KickPlayerFriendCodeNotExist;
    public static OptionItem KickLowLevelPlayer;
    public static OptionItem ApplyBanList;
    public static OptionItem ApplyModeratorList;
    public static OptionItem ApplyAllowList;
    public static OptionItem AutoWarnStopWords;

    public static OptionItem AllowSayCommand;
    public static OptionItem ApplyReminderMsg;
    public static OptionItem TimeForReminder;

    public static OptionItem DIYGameSettings;
    public static OptionItem PlayerCanSetColor;

    //Add-Ons
    public static OptionItem NameDisplayAddons;
    public static OptionItem AddBracketsToAddons;
    public static OptionItem NoLimitAddonsNumMax;
    public static OptionItem BewilderVision;
    public static OptionItem JesterHasImpostorVision;
    public static OptionItem SunglassesVision;
    public static OptionItem ImpCanBeAvanger;
    public static OptionItem MadmateSpawnMode;
    public static OptionItem MadmateCountMode;
    public static OptionItem SheriffCanBeMadmate;
    public static OptionItem MayorCanBeMadmate;
    public static OptionItem NGuesserCanBeMadmate;
    public static OptionItem SnitchCanBeMadmate;
    public static OptionItem JudgeCanBeMadmate;
    public static OptionItem MarshallCanBeMadmate;
    public static OptionItem RetributionistCanBeMadmate;
    public static OptionItem FarseerCanBeMadmate;
    public static OptionItem MadSnitchTasks;
    public static OptionItem FlashmanSpeed;
    public static OptionItem ButtonBarryButtons;
    public static OptionItem LoverSpawnChances;
    public static OptionItem LoverKnowRoles;
    public static OptionItem LoverSuicide;
    public static OptionItem ImpCanBeEgoist;
    public static OptionItem ImpEgoistVisibalToAllies;
    public static OptionItem CrewCanBeEgoist;
    public static OptionItem TicketsPerKill;
    public static OptionItem ImpCanBeDualPersonality;
    public static OptionItem CrewCanBeDualPersonality;
    public static OptionItem DualVotes;
    public static OptionItem HideDualVotes;
    public static OptionItem ImpCanBeLoyal;
    public static OptionItem CrewCanBeLoyal;
    public static OptionItem MinWaitAutoStart;
    public static OptionItem MaxWaitAutoStart;
    public static OptionItem PlayerAutoStart;
    //public static OptionItem SidekickCountMode;

    public static readonly string[] suffixModes =
    [
        "SuffixMode.None",
        "SuffixMode.Version",
        "SuffixMode.Streaming",
        "SuffixMode.Recording",
        "SuffixMode.RoomHost",
        "SuffixMode.OriginalName",
        "SuffixMode.DoNotKillMe",
        "SuffixMode.NoAndroidPlz",
        "SuffixMode.AutoHost"    ];
    public static readonly string[] roleAssigningAlgorithms =
    [
        "RoleAssigningAlgorithm.Default",
        "RoleAssigningAlgorithm.NetRandom",
        "RoleAssigningAlgorithm.HashRandom",
        "RoleAssigningAlgorithm.Xorshift",
        "RoleAssigningAlgorithm.MersenneTwister",
    ];
    public static readonly string[] formatNameModes =
    [
        "FormatNameModes.None",
        "FormatNameModes.Color",
        "FormatNameModes.Snacks",
    ];
    public static SuffixModes GetSuffixMode() => (SuffixModes)SuffixMode.GetValue();

    public static int SnitchExposeTaskLeft = 1;

    public static bool IsLoaded;

    public static int LoadingPercentage;
    public static string MainLoadingText = string.Empty;
    public static string RoleLoadingText = string.Empty;

    static Options()
    {
        ResetRoleCounts();
    }
    public static void ResetRoleCounts()
    {
        roleCounts = [];
        roleSpawnChances = [];

        foreach (var role in Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>())
        {
            roleCounts.Add(role, 0);
            roleSpawnChances.Add(role, 0);
        }
    }

    public static void SetRoleCount(CustomRoles role, int count)
    {
        roleCounts[role] = count;

        if (CustomRoleCounts.TryGetValue(role, out var option))
        {
            option.SetValue(count - 1);
        }
    }

    public static int GetRoleSpawnMode(CustomRoles role)
    {
        var mode = CustomRoleSpawnChances.TryGetValue(role, out var sc) ? sc.GetChance() : 0;
        return mode switch
        {
            0 => 0,
            1 => 1,
            2 => 2,
            100 => 1,
            _ => 1,
        };
    }
    public static int GetRoleCount(CustomRoles role)
    {
        var mode = GetRoleSpawnMode(role);
        return mode is 0 ? 0 : CustomRoleCounts.TryGetValue(role, out var option) ? option.GetInt() : roleCounts[role];
    }
    public static float GetRoleChance(CustomRoles role)
    {
        return CustomRoleSpawnChances.TryGetValue(role, out var option) ? option.GetValue()/* / 10f */ : roleSpawnChances[role];
    }
    public static void Load()
    {
        LoadingPercentage = 0;
        MainLoadingText = "Building system settings";

        if (IsLoaded) return;

        OptionSaver.Initialize();

        // 预设
        int defaultPresetNumber = OptionSaver.GetDefaultPresetNumber();
        _ = PresetOptionItem.Create(defaultPresetNumber, TabGroup.SystemSettings)
            .SetColor(new Color32(255, 235, 4, byte.MaxValue))
            .SetHeader(true);

        // 游戏模式
        GameMode = StringOptionItem.Create(1, "GameMode", gameModes, 0, TabGroup.GameSettings, false)
            .SetHeader(true);

        #region 职业详细设置
        CustomRoleCounts = [];
        CustomRoleSpawnChances = [];
        CustomAdtRoleSpawnRate = [];

        // GM
        EnableGM = BooleanOptionItem.Create(100, "GM", false, TabGroup.GameSettings, false)
            .SetColor(Utils.GetRoleColor(CustomRoles.GM))
            .SetHeader(true);

        MainLoadingText = "Building general settings";

        // 各职业的总体设定
        ImpKnowAlliesRole = BooleanOptionItem.Create(150, "ImpKnowAlliesRole", true, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard)
           .SetHeader(true);
        ImpKnowWhosMadmate = BooleanOptionItem.Create(151, "ImpKnowWhosMadmate", false, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);
        ImpCanKillMadmate = BooleanOptionItem.Create(152, "ImpCanKillMadmate", true, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 1;

        MadmateKnowWhosMadmate = BooleanOptionItem.Create(153, "MadmateKnowWhosMadmate", false, TabGroup.ImpostorRoles, false)
            .SetHeader(true)
            .SetGameMode(CustomGameMode.Standard);
        MadmateKnowWhosImp = BooleanOptionItem.Create(154, "MadmateKnowWhosImp", true, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);
        MadmateCanKillImp = BooleanOptionItem.Create(155, "MadmateCanKillImp", true, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);
        MadmateHasImpostorVision = BooleanOptionItem.Create(156, "MadmateHasImpostorVision", true, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);
        //MadmateCanFixSabotage = BooleanOptionItem.Create(157, "MadmateCanFixSabotage", false, TabGroup.ImpostorRoles, false)
        //.SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 2;


        DefaultShapeshiftCooldown = FloatOptionItem.Create(200, "DefaultShapeshiftCooldown", new(5f, 180f, 5f), 15f, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Seconds);
        DeadImpCantSabotage = BooleanOptionItem.Create(201, "DeadImpCantSabotage", false, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);

        NonNeutralKillingRolesMinPlayer = IntegerOptionItem.Create(202, "NonNeutralKillingRolesMinPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Players);
        NonNeutralKillingRolesMaxPlayer = IntegerOptionItem.Create(203, "NonNeutralKillingRolesMaxPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);

        NeutralKillingRolesMinPlayer = IntegerOptionItem.Create(204, "NeutralKillingRolesMinPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Players);
        NeutralKillingRolesMaxPlayer = IntegerOptionItem.Create(205, "NeutralKillingRolesMaxPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);

        LoadingPercentage = 3;


        //     CovenRolesMinPlayer = IntegerOptionItem.Create(206, "CovenRolesMinPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles, false)
        //         .SetGameMode(CustomGameMode.Standard)
        //         .SetHeader(true)
        //        .SetValueFormat(OptionFormat.Players);
        //    CovenRolesMaxPlayer = IntegerOptionItem.Create(207, "CovenRolesMaxPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles, false)
        //        .SetGameMode(CustomGameMode.Standard)
        //        .SetValueFormat(OptionFormat.Players);

        NeutralRoleWinTogether = BooleanOptionItem.Create(208, "NeutralRoleWinTogether", false, TabGroup.NeutralRoles, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);
        NeutralWinTogether = BooleanOptionItem.Create(209, "NeutralWinTogether", false, TabGroup.NeutralRoles, false)
        .SetParent(NeutralRoleWinTogether)
            .SetGameMode(CustomGameMode.Standard);

        NameDisplayAddons = BooleanOptionItem.Create(210, "NameDisplayAddons", true, TabGroup.Addons, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);
        NoLimitAddonsNumMax = IntegerOptionItem.Create(211, "NoLimitAddonsNumMax", new(1, 15, 1), 1, TabGroup.Addons, false)
            .SetGameMode(CustomGameMode.Standard);

        //==================================================================================================================================//


        LoadingPercentage = 4;
        MainLoadingText = "Building role settings";
        RoleLoadingText = "Vanilla roles\nImpostor";


        // Impostor

        SetupRoleOptions(300, TabGroup.ImpostorRoles, CustomRoles.ImpostorTOHE);
        RoleLoadingText = "Vanilla roles\nShapeshifter";
        SetupRoleOptions(400, TabGroup.ImpostorRoles, CustomRoles.ShapeshifterTOHE);
        ShapeshiftCD = FloatOptionItem.Create(402, "ShapeshiftCooldown", new(1f, 180f, 1f), 30f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ShapeshifterTOHE])
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftDur = FloatOptionItem.Create(403, "ShapeshiftDuration", new(1f, 60f, 1f), 10f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ShapeshifterTOHE])
            .SetValueFormat(OptionFormat.Seconds);

        RoleLoadingText = "Impostor roles\nEvil Tracker";

        LoadingPercentage = 5;

        EvilTracker.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nArrogance";
        Sans.SetupCustomOption();//Arrogance

        LoadingPercentage = 6;

        RoleLoadingText = "Impostor roles\nEvil Guesser";
        SetupRoleOptions(1200, TabGroup.ImpostorRoles, CustomRoles.EvilGuesser);
        EGCanGuessTime = IntegerOptionItem.Create(1205, "GuesserCanGuessTimes", new(1, 15, 1), 15, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser])
            .SetValueFormat(OptionFormat.Times);
        EGCanGuessImp = BooleanOptionItem.Create(1206, "EGCanGuessImp", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser]);
        EGCanGuessAdt = BooleanOptionItem.Create(1207, "EGCanGuessAdt", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser]);
        EGCanGuessTaskDoneSnitch = BooleanOptionItem.Create(1208, "EGCanGuessTaskDoneSnitch", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser]);
        EGTryHideMsg = BooleanOptionItem.Create(1209, "GuesserTryHideMsg", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser])
            .SetColor(Color.green);
        RoleLoadingText = "Impostor roles\nBomber";
        SetupRoleOptions(2400, TabGroup.ImpostorRoles, CustomRoles.Bomber);
        BomberRadius = FloatOptionItem.Create(2018, "BomberRadius", new(0.5f, 5f, 0.5f), 2f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber])
            .SetValueFormat(OptionFormat.Multiplier);
        BomberCanKill = BooleanOptionItem.Create(2015, "CanKill", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber]);
        BomberKillCD = FloatOptionItem.Create(2020, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
            .SetParent(BomberCanKill)
            .SetValueFormat(OptionFormat.Seconds);
        BombCooldown = FloatOptionItem.Create(2030, "BombCooldown", new(5f, 180f, 2.5f), 60f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber])
            .SetValueFormat(OptionFormat.Seconds);
        ImpostorsSurviveBombs = BooleanOptionItem.Create(2031, "ImpostorsSurviveBombs", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber]);
        BomberDiesInExplosion = BooleanOptionItem.Create(2032, "BomberDiesInExplosion", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber]);
        NukerChance = IntegerOptionItem.Create(2033, "NukerChance", new(0, 100, 5), 0, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber])
            .SetValueFormat(OptionFormat.Percent);
        NukeCooldown = FloatOptionItem.Create(2035, "NukeCooldown", new(5f, 180f, 2.5f), 60f, TabGroup.ImpostorRoles, false)
            .SetParent(NukerChance)
            .SetValueFormat(OptionFormat.Seconds);
        NukeRadius = FloatOptionItem.Create(2034, "NukeRadius", new(5f, 100f, 1f), 25f, TabGroup.ImpostorRoles, false)
            .SetParent(NukerChance)
            .SetValueFormat(OptionFormat.Multiplier);
        RoleLoadingText = "Impostor roles\nBounty Hunter";
        BountyHunter.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nCouncillor";
        Councillor.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nCursed Wolf";
        SetupRoleOptions(1000, TabGroup.ImpostorRoles, CustomRoles.CursedWolf); //TOH_Y
        GuardSpellTimes = IntegerOptionItem.Create(1010, "GuardSpellTimes", new(1, 15, 1), 3, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CursedWolf])
            .SetValueFormat(OptionFormat.Times);
        killAttacker = BooleanOptionItem.Create(1011, "killAttacker", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.CursedWolf]);
        RoleLoadingText = "Impostor roles\nDeathpact";
        Deathpact.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nGreedy";
        Greedier.SetupCustomOption(); //TOH_Y
        RoleLoadingText = "Impostor roles\nHangman";
        Hangman.SetupCustomOption();

        LoadingPercentage = 7;
        RoleLoadingText = "Impostor roles\nInhibitor";

        SetupRoleOptions(1500, TabGroup.ImpostorRoles, CustomRoles.Inhibitor);
        InhibitorCD = FloatOptionItem.Create(1510, "KillCooldown", new(0f, 180f, 2.5f), 20f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Inhibitor])
            .SetValueFormat(OptionFormat.Seconds);
        InhibitorCDAfterMeetings = FloatOptionItem.Create(1511, "AfterMeetingKillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Inhibitor])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Impostor roles\nCantankerous";
        Cantankerous.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nDuellist";
        Duellist.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nYin Yanger";
        YinYanger.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nConsort";
        Consort.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nMafioso";
        Mafioso.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nNullifier";
        Nullifier.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nChronomancer";
        Chronomancer.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nStealth";
        Stealth.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nPenguin";
        Penguin.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nSapper";
        Sapper.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nMastermind";
        Mastermind.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nGambler";
        Gambler.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nRiftMaker";
        RiftMaker.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nHitman";
        Hitman.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nLurker";
        Lurker.SetupCustomOption();
        //     Mare.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nMercenary";
        SerialKiller.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nNinja";
        Assassin.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nUndertaker";
        Undertaker.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nQuick Shooter";
        QuickShooter.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nSaboteur";
        SetupRoleOptions(10005, TabGroup.ImpostorRoles, CustomRoles.Saboteur);
        SaboteurCD = FloatOptionItem.Create(10015, "KillCooldown", new(0f, 180f, 2.5f), 17.5f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Saboteur])
            .SetValueFormat(OptionFormat.Seconds);
        SaboteurCDAfterMeetings = FloatOptionItem.Create(10016, "AfterMeetingKillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Saboteur])
            .SetValueFormat(OptionFormat.Seconds);

        RoleLoadingText = "Impostor roles\nSniper";
        Sniper.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nDoppelganger";
        Doppelganger.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nBlackmailer";
        Blackmailer.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nWitch";
        Witch.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nUnderdog";
        SetupRoleOptions(10025, TabGroup.ImpostorRoles, CustomRoles.Underdog);
        UnderdogMaximumPlayersNeededToKill = IntegerOptionItem.Create(10030, "UnderdogMaximumPlayersNeededToKill", new(1, 15, 1), 5, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Underdog])
            .SetValueFormat(OptionFormat.Players);
        UnderdogKillCooldown = FloatOptionItem.Create(10031, "KillCooldown", new(0f, 180f, 2.5f), 15f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Underdog])
            .SetValueFormat(OptionFormat.Seconds);
        UnderdogKillCooldownWithMorePlayersAlive = FloatOptionItem.Create(10032, "UnderdogKillCooldownWithMorePlayersAlive", new(0f, 180f, 2.5f), 35f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Underdog])
            .SetValueFormat(OptionFormat.Seconds);

        LoadingPercentage = 8;
        RoleLoadingText = "Impostor roles\nCamouflager";

        Camouflager.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nCleaner";
        SetupRoleOptions(2600, TabGroup.ImpostorRoles, CustomRoles.Cleaner);
        CleanerKillCooldown = FloatOptionItem.Create(2610, "KillCooldown", new(5f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cleaner])
            .SetValueFormat(OptionFormat.Seconds);
        KillCooldownAfterCleaning = FloatOptionItem.Create(2611, "KillCooldownAfterCleaning", new(5f, 180f, 2.5f), 60f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cleaner])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Impostor roles\nConsigliere";
        EvilDiviner.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nDisruptor";
        AntiAdminer.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nFireworker";
        FireWorks.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nGangster";
        Gangster.SetupCustomOption();

        LoadingPercentage = 9;
        RoleLoadingText = "Impostor roles\nGodfather";

        SetupRoleOptions(555420, TabGroup.ImpostorRoles, CustomRoles.Godfather);
        RoleLoadingText = "Impostor roles\nMorphling";
        Morphling.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nNemesis";
        SetupRoleOptions(3100, TabGroup.ImpostorRoles, CustomRoles.Mafia);
        MafiaCanKillNum = IntegerOptionItem.Create(3200, "MafiaCanKillNum", new(0, 15, 1), 1, TabGroup.ImpostorRoles, false)
        .SetParent(CustomRoleSpawnChances[CustomRoles.Mafia])
            .SetValueFormat(OptionFormat.Players);
        LegacyMafia = BooleanOptionItem.Create(3210, "LegacyMafia", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mafia]);
        MafiaShapeshiftCD = FloatOptionItem.Create(3211, "ShapeshiftCooldown", new(1f, 180f, 1f), 15f, TabGroup.ImpostorRoles, false)
            .SetParent(LegacyMafia)
            .SetValueFormat(OptionFormat.Seconds);
        MafiaShapeshiftDur = FloatOptionItem.Create(3212, "ShapeshiftDuration", new(1f, 180f, 1f), 30f, TabGroup.ImpostorRoles, false)
            .SetParent(LegacyMafia)
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Impostor roles\nTime Thief";
        TimeThief.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nVindicator";
        SetupRoleOptions(3400, TabGroup.ImpostorRoles, CustomRoles.Vindicator);
        VindicatorAdditionalVote = IntegerOptionItem.Create(3410, "MayorAdditionalVote", new(1, 10, 1), 1, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vindicator])
            .SetValueFormat(OptionFormat.Votes);
        VindicatorHideVote = BooleanOptionItem.Create(3411, "MayorHideVote", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vindicator]);
        RoleLoadingText = "Impostor roles\nVisionary";
        SetupRoleOptions(16150, TabGroup.ImpostorRoles, CustomRoles.Visionary);

        LoadingPercentage = 10;
        RoleLoadingText = "Impostor roles\nEscapist";

        SetupRoleOptions(3600, TabGroup.ImpostorRoles, CustomRoles.Escapee);
        //EscapeeSSDuration = FloatOptionItem.Create(3610, "ShapeshiftDuration", new(1f, 180f, 1f), 1, TabGroup.ImpostorRoles, false)
        //    .SetParent(CustomRoleSpawnChances[CustomRoles.Escapee])
        //    .SetValueFormat(OptionFormat.Seconds);
        EscapeeSSCD = FloatOptionItem.Create(3611, "ShapeshiftCooldown", new(1f, 180f, 1f), 5f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Escapee])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Impostor roles\nSoul Catcher";
        SetupRoleOptions(3700, TabGroup.ImpostorRoles, CustomRoles.ImperiusCurse);
        ShapeImperiusCurseShapeshiftDuration = FloatOptionItem.Create(3710, "ShapeshiftDuration", new(2.5f, 300f, 2.5f), 20f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ImperiusCurse])
            .SetValueFormat(OptionFormat.Seconds);
        ImperiusCurseShapeshiftCooldown = FloatOptionItem.Create(3711, "ShapeshiftCooldown", new(1f, 180f, 1f), 30f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ImperiusCurse])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Impostor roles\nMiner";
        SetupRoleOptions(3800, TabGroup.ImpostorRoles, CustomRoles.Miner);
        //MinerSSDuration = FloatOptionItem.Create(3810, "ShapeshiftDuration", new(1f, 180f, 1f), 1, TabGroup.ImpostorRoles, false)
        //    .SetParent(CustomRoleSpawnChances[CustomRoles.Miner])
        //    .SetValueFormat(OptionFormat.Seconds);
        MinerSSCD = FloatOptionItem.Create(3811, "ShapeshiftCooldown", new(1f, 180f, 1f), 15f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Miner])
            .SetValueFormat(OptionFormat.Seconds);

        LoadingPercentage = 11;
        RoleLoadingText = "Impostor roles\nPuppeteer";

        SetupRoleOptions(3900, TabGroup.ImpostorRoles, CustomRoles.Puppeteer);
        PuppeteerCD = FloatOptionItem.Create(3911, "PuppeteerCD", new(2.5f, 60f, 2.5f), 22.5f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
            .SetValueFormat(OptionFormat.Seconds);
        PuppeteerCanKillNormally = BooleanOptionItem.Create(3917, "PuppeteerCanKillNormally", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
        PuppeteerKCD = FloatOptionItem.Create(3912, "PuppeteerKCD", new(2.5f, 60f, 2.5f), 25f, TabGroup.ImpostorRoles, false)
            .SetParent(PuppeteerCanKillNormally)
            .SetValueFormat(OptionFormat.Seconds);
        PuppeteerMinDelay = IntegerOptionItem.Create(3913, "PuppeteerMinDelay", new(0, 20, 1), 3, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
            .SetValueFormat(OptionFormat.Seconds);
        PuppeteerMaxDelay = IntegerOptionItem.Create(3914, "PuppeteerMaxDelay", new(0, 20, 1), 7, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
            .SetValueFormat(OptionFormat.Seconds);
        PuppeteerManipulationEndsAfterFixedTime = BooleanOptionItem.Create(3915, "PuppeteerManipulationEndsAfterFixedTime", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
        PuppeteerManipulationEndsAfterTime = IntegerOptionItem.Create(3916, "PuppeteerManipulationEndsAfterTime", new(0, 60, 1), 30, TabGroup.ImpostorRoles, false)
            .SetParent(PuppeteerManipulationEndsAfterFixedTime)
            .SetValueFormat(OptionFormat.Seconds);
        PuppeteerManipulationBypassesLazy = BooleanOptionItem.Create(3918, "PuppeteerManipulationBypassesLazy", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
        PuppeteerManipulationBypassesLazyGuy = BooleanOptionItem.Create(3922, "PuppeteerManipulationBypassesLazyGuy", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
        PuppeteerPuppetCanKillImpostors = BooleanOptionItem.Create(3919, "PuppeteerPuppetCanKillImpostors", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
        PuppeteerPuppetCanKillPuppeteer = BooleanOptionItem.Create(3920, "PuppeteerPuppetCanKillPuppeteer", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
        PuppeteerMaxPuppets = IntegerOptionItem.Create(3921, "PuppeteerMaxPuppets", new(0, 20, 1), 5, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
            .SetValueFormat(OptionFormat.Times);
        PuppeteerDiesAfterMaxPuppets = BooleanOptionItem.Create(3923, "PuppeteerDiesAfterMaxPuppets", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
        RoleLoadingText = "Impostor roles\nScavenger";
        SetupRoleOptions(4000, TabGroup.ImpostorRoles, CustomRoles.Scavenger);
        ScavengerKillCooldown = FloatOptionItem.Create(4010, "KillCooldown", new(5f, 180f, 2.5f), 40f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Scavenger])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Impostor roles\nShapemaster";
        SetupRoleOptions(4100, TabGroup.ImpostorRoles, CustomRoles.ShapeMaster);
        ShapeMasterShapeshiftDuration = FloatOptionItem.Create(4110, "ShapeshiftDuration", new(1, 180, 1), 10, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ShapeMaster])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Impostor roles\nSwooper";
        Swooper.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nTrickster";
        SetupRoleOptions(4300, TabGroup.ImpostorRoles, CustomRoles.Trickster);
        RoleLoadingText = "Impostor roles\nVampire";
        Vampire.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nWarlock";
        SetupRoleOptions(4600, TabGroup.ImpostorRoles, CustomRoles.Warlock);
        WarlockCanKillAllies = BooleanOptionItem.Create(4610, "CanKillAllies", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Warlock]);
        WarlockCanKillSelf = BooleanOptionItem.Create(4611, "CanKillSelf", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Warlock]);
        WarlockShiftDuration = FloatOptionItem.Create(4612, "ShapeshiftDuration", new(1, 180, 1), 1, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Warlock])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Impostor roles\nWildling";
        Wildling.SetupCustomOption();

        LoadingPercentage = 12;
        RoleLoadingText = "Impostor roles\nAnonymous";

        Hacker.SetupCustomOption(); //anonymous
        RoleLoadingText = "Impostor roles\nDazzler";
        Dazzler.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nDevourer";
        Devourer.SetupCustomOption();
        RoleLoadingText = "Impostor roles\nTwister";
        Twister.SetupCustomOption();

        LoadingPercentage = 13;
        RoleLoadingText = "Madmate roles\nCrewpostor";

        SetupRoleOptions(4800, TabGroup.ImpostorRoles, CustomRoles.Crewpostor);
        CrewpostorCanKillAllies = BooleanOptionItem.Create(4810, "CanKillAllies", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Crewpostor]);
        CrewpostorKnowsAllies = BooleanOptionItem.Create(4811, "CrewpostorKnowsAllies", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Crewpostor]);
        AlliesKnowCrewpostor = BooleanOptionItem.Create(4812, "AlliesKnowCrewpostor", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Crewpostor]);
        CrewpostorLungeKill = BooleanOptionItem.Create(4813, "CrewpostorLungeKill", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Crewpostor]);
        CrewpostorKillAfterTask = IntegerOptionItem.Create(4814, "CrewpostorKillAfterTask", new(1, 50, 1), 1, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Crewpostor]);
        CrewpostorTasks = OverrideTasksData.Create(4815, TabGroup.ImpostorRoles, CustomRoles.Crewpostor);
        RoleLoadingText = "Madmate roles\nParasite";
        SetupSingleRoleOptions(4900, TabGroup.ImpostorRoles, CustomRoles.Parasite, 1, zeroOne: false);
        ParasiteCD = FloatOptionItem.Create(4910, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Parasite])
            .SetValueFormat(OptionFormat.Seconds);
        //==================================================================================================================================//


        LoadingPercentage = 14;
        RoleLoadingText = "Vanilla roles\nCrewmate";

        SetupRoleOptions(5050, TabGroup.CrewmateRoles, CustomRoles.CrewmateTOHE);
        RoleLoadingText = "Vanilla roles\nEngineer";
        SetupRoleOptions(5000, TabGroup.CrewmateRoles, CustomRoles.EngineerTOHE);
        RoleLoadingText = "Vanilla roles\nScientist";
        SetupRoleOptions(5100, TabGroup.CrewmateRoles, CustomRoles.ScientistTOHE);
        ScientistCD = FloatOptionItem.Create(5110, "VitalsCooldown", new(1f, 250f, 1f), 3f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistTOHE])
            .SetValueFormat(OptionFormat.Seconds);
        ScientistDur = FloatOptionItem.Create(5111, "VitalsDuration", new(1f, 250f, 1f), 15f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistTOHE])
            .SetValueFormat(OptionFormat.Seconds);

        RoleLoadingText = "Crewmate roles\nAddict";

        Addict.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nAlchemist";
        Alchemist.SetupCustomOption();

        LoadingPercentage = 15;
        RoleLoadingText = "Crewmate roles\nCelebrity";

        SetupRoleOptions(5300, TabGroup.CrewmateRoles, CustomRoles.CyberStar);
        ImpKnowCyberStarDead = BooleanOptionItem.Create(5400, "ImpKnowCyberStarDead", false, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);
        NeutralKnowCyberStarDead = BooleanOptionItem.Create(5500, "NeutralKnowCyberStarDead", false, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);
        RoleLoadingText = "Crewmate roles\nCleanser";
        Cleanser.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nVentguard";
        SetupSingleRoleOptions(5525, TabGroup.CrewmateRoles, CustomRoles.Ventguard, 1);
        VentguardAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(5527, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard])
            .SetValueFormat(OptionFormat.Times);
        VentguardMaxGuards = IntegerOptionItem.Create(5528, "VentguardMaxGuards", new(1, 20, 1), 3, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard]);
        VentguardBlockDoesNotAffectCrew = BooleanOptionItem.Create(5529, "VentguardBlockDoesNotAffectCrew", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard]);
        RoleLoadingText = "Crewmate roles\nDemolitionist";
        SetupSingleRoleOptions(5550, TabGroup.CrewmateRoles, CustomRoles.Demolitionist, 1);
        DemolitionistVentTime = FloatOptionItem.Create(5552, "DemolitionistVentTime", new(1f, 30f, 1f), 5f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demolitionist])
            .SetValueFormat(OptionFormat.Seconds);
        DemolitionistKillerDiesOnMeetingCall = BooleanOptionItem.Create(5553, "DemolitionistKillerDiesOnMeetingCall", false, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demolitionist]);
        RoleLoadingText = "Crewmate roles\nTask Manager";
        SetupSingleRoleOptions(5575, TabGroup.CrewmateRoles, CustomRoles.TaskManager, 1);
        RoleLoadingText = "Crewmate roles\nDrainer";
        Drainer.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nGuess Manager";
        GuessManagerRole.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nAltruist";
        Altruist.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nTransmitter";
        Transmitter.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nAutocrat";
        Autocrat.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nBenefactor";
        Benefactor.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nNightmare";
        Nightmare.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nCamera Man";
        CameraMan.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nHacker";
        NiceHacker.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nDoormaster";
        Doormaster.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nRicochet";
        Ricochet.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nEscort";
        Escort.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nAnalyzer";
        Analyzer.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nDonut Delivery";
        DonutDelivery.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nAid";
        Aid.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nTether";
        Tether.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nSpy";
        Spy.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nEnigma";
        Enigma.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nExpress";
        SetupRoleOptions(5585, TabGroup.CrewmateRoles, CustomRoles.Express);
        ExpressSpeed = FloatOptionItem.Create(5587, "ExpressSpeed", new(0.25f, 5f, 0.25f), 1.5f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Express])
            .SetValueFormat(OptionFormat.Multiplier);
        ExpressSpeedDur = IntegerOptionItem.Create(5588, "ExpressSpeedDur", new(1, 20, 1), 5, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Express])
            .SetValueFormat(OptionFormat.Seconds);

        LoadingPercentage = 16;
        RoleLoadingText = "Crewmate roles\nNice Eraser";

        NiceEraser.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nDoctor";
        SetupRoleOptions(5600, TabGroup.CrewmateRoles, CustomRoles.Doctor);
        DoctorTaskCompletedBatteryCharge = FloatOptionItem.Create(5610, "DoctorTaskCompletedBatteryCharge", new(0f, 250f, 1f), 50f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Doctor])
            .SetValueFormat(OptionFormat.Seconds);
        DoctorVisibleToEveryone = BooleanOptionItem.Create(5611, "DoctorVisibleToEveryone", false, TabGroup.CrewmateRoles, false)
        .SetParent(CustomRoleSpawnChances[CustomRoles.Doctor]);
        RoleLoadingText = "Crewmate roles\nLazy Guy";
        SetupRoleOptions(5700, TabGroup.CrewmateRoles, CustomRoles.Needy);
        RoleLoadingText = "Crewmate roles\nLuckey";
        SetupRoleOptions(5800, TabGroup.CrewmateRoles, CustomRoles.Luckey);
        LuckeyProbability = IntegerOptionItem.Create(5900, "LuckeyProbability", new(0, 100, 5), 50, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Luckey])
            .SetValueFormat(OptionFormat.Percent);
        RoleLoadingText = "Crewmate roles\nSuper Star";
        SetupRoleOptions(6000, TabGroup.CrewmateRoles, CustomRoles.SuperStar);
        EveryOneKnowSuperStar = BooleanOptionItem.Create(6010, "EveryOneKnowSuperStar", true, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SuperStar]);
        RoleLoadingText = "Crewmate roles\nTracefinder";
        Tracefinder.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nTransporter";
        SetupRoleOptions(6200, TabGroup.CrewmateRoles, CustomRoles.Transporter);
        TransporterTeleportMax = IntegerOptionItem.Create(6210, "TransporterTeleportMax", new(1, 20, 1), 5, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Transporter])
            .SetValueFormat(OptionFormat.Times);
        TransporterTasks = OverrideTasksData.Create(6211, TabGroup.CrewmateRoles, CustomRoles.Transporter);

        LoadingPercentage = 17;
        RoleLoadingText = "Crewmate roles\nAdmirer";

        Admirer.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nChameleon";
        Chameleon.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nCoroner";
        Bloodhound.SetupCustomOption();

        LoadingPercentage = 18;
        RoleLoadingText = "Crewmate roles\nDeputy";

        Deputy.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nDetective";
        SetupRoleOptions(6600, TabGroup.CrewmateRoles, CustomRoles.Detective);
        DetectiveCanknowKiller = BooleanOptionItem.Create(6610, "DetectiveCanknowKiller", true, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Detective]);
        RoleLoadingText = "Crewmate roles\nFortune Teller";
        Divinator.SetupCustomOption(); // Fortune Teller
        RoleLoadingText = "Crewmate roles\nGrenadier";
        SetupRoleOptions(6800, TabGroup.CrewmateRoles, CustomRoles.Grenadier);
        GrenadierSkillCooldown = FloatOptionItem.Create(6810, "GrenadierSkillCooldown", new(1f, 180f, 1f), 25f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Seconds);
        GrenadierSkillDuration = FloatOptionItem.Create(6811, "GrenadierSkillDuration", new(1f, 180f, 1f), 10f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Seconds);
        GrenadierCauseVision = FloatOptionItem.Create(6812, "GrenadierCauseVision", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Multiplier);
        GrenadierCanAffectNeutral = BooleanOptionItem.Create(6813, "GrenadierCanAffectNeutral", false, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier]);
        GrenadierSkillMaxOfUseage = IntegerOptionItem.Create(6814, "GrenadierSkillMaxOfUseage", new(0, 180, 1), 2, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Times);
        GrenadierAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(6815, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Times);

        LoadingPercentage = 19;
        RoleLoadingText = "Crewmate roles\nLighter";

        SetupSingleRoleOptions(6850, TabGroup.CrewmateRoles, CustomRoles.Lighter, 1);
        LighterSkillCooldown = FloatOptionItem.Create(6852, "LighterSkillCooldown", new(1f, 180f, 1f), 25f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Seconds);
        LighterSkillDuration = FloatOptionItem.Create(6853, "LighterSkillDuration", new(1f, 180f, 1f), 10f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Seconds);
        LighterVisionNormal = FloatOptionItem.Create(6854, "LighterVisionNormal", new(0f, 5f, 0.05f), 0.9f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Multiplier);
        LighterVisionOnLightsOut = FloatOptionItem.Create(6855, "LighterVisionOnLightsOut", new(0f, 5f, 0.05f), 0.35f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Multiplier);
        LighterSkillMaxOfUseage = IntegerOptionItem.Create(6856, "AbilityUseLimit", new(0, 180, 1), 2, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Times);
        LighterAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(6857, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1.5f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Times);

        RoleLoadingText = "Crewmate roles\nSecurity Guard";

        SetupRoleOptions(6860, TabGroup.CrewmateRoles, CustomRoles.SecurityGuard);
        SecurityGuardSkillCooldown = FloatOptionItem.Create(6862, "SecurityGuardSkillCooldown", new(1f, 180f, 1f), 15f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
            .SetValueFormat(OptionFormat.Seconds);
        SecurityGuardSkillDuration = FloatOptionItem.Create(6863, "SecurityGuardSkillDuration", new(1f, 180f, 1f), 10f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
            .SetValueFormat(OptionFormat.Seconds);
        SecurityGuardSkillMaxOfUseage = IntegerOptionItem.Create(6866, "AbilityUseLimit", new(0, 180, 1), 1, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
            .SetValueFormat(OptionFormat.Times);
        SecurityGuardAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(6867, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.4f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
            .SetValueFormat(OptionFormat.Times);

        LoadingPercentage = 20;
        RoleLoadingText = "Crewmate roles\nInspector";

        ParityCop.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nMechanic";
        SabotageMaster.SetupCustomOption(); //Mechanic
        RoleLoadingText = "Crewmate roles\nMedic";
        Medic.SetupCustomOption();

        LoadingPercentage = 21;
        RoleLoadingText = "Crewmate roles\nMedium";

        Mediumshiper.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nIgnitor";
        Ignitor.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nMerchant";
        Merchant.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nMortician";
        Mortician.SetupCustomOption();

        LoadingPercentage = 22;
        RoleLoadingText = "Crewmate roles\nObserver";

        SetupRoleOptions(7500, TabGroup.CrewmateRoles, CustomRoles.Observer);
        RoleLoadingText = "Crewmate roles\nOracle";
        Oracle.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nPacifist";
        SetupRoleOptions(7700, TabGroup.CrewmateRoles, CustomRoles.DovesOfNeace);
        DovesOfNeaceCooldown = FloatOptionItem.Create(7710, "DovesOfNeaceCooldown", new(1f, 180f, 1f), 7f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
            .SetValueFormat(OptionFormat.Seconds);
        DovesOfNeaceMaxOfUseage = IntegerOptionItem.Create(7711, "DovesOfNeaceMaxOfUseage", new(0, 180, 1), 0, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
            .SetValueFormat(OptionFormat.Times);
        DovesOfNeaceAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(7712, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
            .SetValueFormat(OptionFormat.Times);
        RoleLoadingText = "Crewmate roles\nParanoid";
        SetupRoleOptions(7800, TabGroup.CrewmateRoles, CustomRoles.Paranoia);
        ParanoiaNumOfUseButton = IntegerOptionItem.Create(7810, "ParanoiaNumOfUseButton", new(1, 10, 1), 3, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Paranoia])
            .SetValueFormat(OptionFormat.Times);
        ParanoiaVentCooldown = FloatOptionItem.Create(7811, "ParanoiaVentCooldown", new(0, 180, 1), 10, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Paranoia])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Crewmate roles\nPsychic";
        Psychic.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nSnitch";
        Snitch.SetupCustomOption();

        LoadingPercentage = 23;
        RoleLoadingText = "Crewmate roles\nSpiritualist";

        Spiritualist.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nTime Manager";
        TimeManager.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nTime Master";
        SetupRoleOptions(8950, TabGroup.CrewmateRoles, CustomRoles.TimeMaster);
        TimeMasterSkillCooldown = FloatOptionItem.Create(8960, "TimeMasterSkillCooldown", new(1f, 180f, 1f), 20f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Seconds);
        TimeMasterSkillDuration = FloatOptionItem.Create(8961, "TimeMasterSkillDuration", new(1f, 180f, 1f), 10f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Seconds);
        TimeMasterMaxUses = IntegerOptionItem.Create(8962, "TimeMasterMaxUses", new(0, 180, 1), 1, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Times);
        TimeMasterAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(8963, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.4f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Times);
        RoleLoadingText = "Crewmate roles\nTracker";

        Tracker.SetupCustomOption();

        LoadingPercentage = 24;
        RoleLoadingText = "Crewmate roles\nBodyguard";

        SetupRoleOptions(8400, TabGroup.CrewmateRoles, CustomRoles.Bodyguard);
        BodyguardProtectRadius = FloatOptionItem.Create(8410, "BodyguardProtectRadius", new(0.5f, 5f, 0.5f), 1.5f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bodyguard])
            .SetValueFormat(OptionFormat.Multiplier);
        BodyguardKillsKiller = BooleanOptionItem.Create(8411, "BodyguardKillsKiller", false, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bodyguard]);
        RoleLoadingText = "Crewmate roles\nCrusader";
        Crusader.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nJailor";
        //Counterfeiter.SetupCustomOption();
        Jailor.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nWitness";
        SetupSingleRoleOptions(8550, TabGroup.CrewmateRoles, CustomRoles.Witness, 1);
        WitnessCD = FloatOptionItem.Create(8552, "AbilityCD", new(0f, 60f, 2.5f), 15f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Witness])
            .SetValueFormat(OptionFormat.Seconds);
        WitnessTime = IntegerOptionItem.Create(8553, "WitnessTime", new(1, 30, 1), 10, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Witness])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Crewmate roles\nVigilante";
        SwordsMan.SetupCustomOption();
        //Reverie.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nRetributionist";
        SetupRoleOptions(8700, TabGroup.CrewmateRoles, CustomRoles.Retributionist);
        RetributionistCanKillNum = IntegerOptionItem.Create(8710, "RetributionistCanKillNum", new(1, 15, 1), 1, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Retributionist])
            .SetValueFormat(OptionFormat.Players);
        MinimumPlayersAliveToRetri = IntegerOptionItem.Create(8718, "MinimumPlayersAliveToRetri", new(0, 15, 1), 5, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Retributionist])
            .SetValueFormat(OptionFormat.Players);
        CanOnlyRetributeWithTasksDone = BooleanOptionItem.Create(8715, "CanOnlyRetributeWithTasksDone", true, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Retributionist]);
        RetributionistTasks = OverrideTasksData.Create(8720, TabGroup.CrewmateRoles, CustomRoles.Retributionist);

        LoadingPercentage = 25;
        RoleLoadingText = "Crewmate roles\nSheriff";

        Sheriff.SetupCustomOption();

        LoadingPercentage = 26;

        RoleLoadingText = "Crewmate roles\nVeteran";
        SetupRoleOptions(8900, TabGroup.CrewmateRoles, CustomRoles.Veteran);
        VeteranSkillCooldown = FloatOptionItem.Create(8910, "VeteranSkillCooldown", new(1f, 180f, 1f), 20f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
            .SetValueFormat(OptionFormat.Seconds);
        VeteranSkillDuration = FloatOptionItem.Create(8911, "VeteranSkillDuration", new(1f, 180f, 1f), 20f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
            .SetValueFormat(OptionFormat.Seconds);
        VeteranSkillMaxOfUseage = IntegerOptionItem.Create(8912, "VeteranSkillMaxOfUseage", new(0, 180, 1), 1, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
            .SetValueFormat(OptionFormat.Times);
        VeteranAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(8913, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.3f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
            .SetValueFormat(OptionFormat.Times);
        RoleLoadingText = "Crewmate roles\nNice Guesser";
        SetupRoleOptions(8600, TabGroup.CrewmateRoles, CustomRoles.NiceGuesser);
        GGCanGuessTime = IntegerOptionItem.Create(8610, "GuesserCanGuessTimes", new(1, 15, 1), 15, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser])
            .SetValueFormat(OptionFormat.Times);
        GGCanGuessCrew = BooleanOptionItem.Create(8611, "GGCanGuessCrew", true, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser]);
        GGCanGuessAdt = BooleanOptionItem.Create(8612, "GGCanGuessAdt", false, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser]);
        GGTryHideMsg = BooleanOptionItem.Create(8613, "GuesserTryHideMsg", true, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser])
            .SetColor(Color.green);

        RoleLoadingText = "Crewmate roles\nDictator";

        SetupRoleOptions(9100, TabGroup.CrewmateRoles, CustomRoles.Dictator);
        RoleLoadingText = "Crewmate roles\nSwapper";
        NiceSwapper.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nGuardian";
        SetupRoleOptions(9200, TabGroup.CrewmateRoles, CustomRoles.Guardian);
        GuardianTasks = OverrideTasksData.Create(9210, TabGroup.CrewmateRoles, CustomRoles.Guardian);
        RoleLoadingText = "Crewmate roles\nJudge";
        Judge.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nLookout";
        SetupRoleOptions(9150, TabGroup.CrewmateRoles, CustomRoles.Lookout);
        RoleLoadingText = "Crewmate roles\nSpeedrunner";
        SetupRoleOptions(9170, TabGroup.CrewmateRoles, CustomRoles.Speedrunner);
        SpeedrunnerNotifyKillers = BooleanOptionItem.Create(9178, "SpeedrunnerNotifyKillers", true, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Speedrunner]);
        SpeedrunnerNotifyAtXTasksLeft = IntegerOptionItem.Create(9179, "SpeedrunnerNotifyAtXTasksLeft", new(1, 10, 1), 3, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Speedrunner]);
        SpeedrunnerTasks = OverrideTasksData.Create(9180, TabGroup.CrewmateRoles, CustomRoles.Speedrunner);
        RoleLoadingText = "Crewmate roles\nMarshall";
        Marshall.SetupCustomOption();

        LoadingPercentage = 27;
        RoleLoadingText = "Crewmate roles\nMayor";

        SetupRoleOptions(9500, TabGroup.CrewmateRoles, CustomRoles.Mayor);
        MayorAdditionalVote = IntegerOptionItem.Create(9510, "MayorAdditionalVote", new(1, 10, 1), 3, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor])
            .SetValueFormat(OptionFormat.Votes);
        MayorHasPortableButton = BooleanOptionItem.Create(9511, "MayorHasPortableButton", false, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);
        MayorNumOfUseButton = IntegerOptionItem.Create(9512, "MayorNumOfUseButton", new(1, 10, 1), 1, TabGroup.CrewmateRoles, false)
            .SetParent(MayorHasPortableButton)
            .SetValueFormat(OptionFormat.Times);
        MayorHideVote = BooleanOptionItem.Create(9513, "MayorHideVote", false, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);
        MayorRevealWhenDoneTasks = BooleanOptionItem.Create(9514, "MayorRevealWhenDoneTasks", false, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);
        MayorTasks = OverrideTasksData.Create(9515, TabGroup.CrewmateRoles, CustomRoles.Mayor);
        RoleLoadingText = "Crewmate roles\nMonarch";
        Monarch.SetupCustomOption();
        RoleLoadingText = "Crewmate roles\nFarseer";
        Farseer.SetupCustomOption();

        LoadingPercentage = 28;
        RoleLoadingText = "Crewmate roles\nTelecommunication";

        Monitor.SetupCustomOption();

        RoleLoadingText = "Neutral roles\nAmnesiac";

        Amnesiac.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nFollower";
        Totocalcio.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nRomantic";
        Romantic.SetupCustomOption();

        LoadingPercentage = 29;
        RoleLoadingText = "Neutral roles\nHater";

        FFF.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nLawyer";
        Lawyer.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nMaverick";
        Maverick.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nOpportunist";
        SetupRoleOptions(10100, TabGroup.NeutralRoles, CustomRoles.Opportunist);
        OppoImmuneToAttacksWhenTasksDone = BooleanOptionItem.Create(10110, "ImmuneToAttacksWhenTasksDone", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Opportunist]);
        OpportunistTasks = OverrideTasksData.Create(10111, TabGroup.NeutralRoles, CustomRoles.Opportunist);

        RoleLoadingText = "Neutral roles\nPursuer";
        Pursuer.SetupCustomOption();
        //NWitch.SetupCustomOption();
        /*  SetupSingleRoleOptions(6050530, TabGroup.NeutralRoles, CustomRoles.NWitch, 1, zeroOne: false);
          ControlCooldown = FloatOptionItem.Create(6050532, "ControlCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.NeutralRoles, false)
          .SetParent(CustomRoleSpawnChances[CustomRoles.NWitch])
              .SetValueFormat(OptionFormat.Seconds); */

        LoadingPercentage = 30;
        RoleLoadingText = "Neutral roles\nCursed Soul";


        CursedSoul.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nDemon";
        Gamer.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nExecutioner";
        Executioner.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nDoomsayer";
        Doomsayer.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nInnocent";
        SetupRoleOptions(10800, TabGroup.NeutralRoles, CustomRoles.Innocent);
        InnocentCanWinByImp = BooleanOptionItem.Create(10810, "InnocentCanWinByImp", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Innocent]);
        RoleLoadingText = "Neutral roles\nJester";
        SetupRoleOptions(10900, TabGroup.NeutralRoles, CustomRoles.Jester);
        JesterCanUseButton = BooleanOptionItem.Create(10910, "JesterCanUseButton", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
        JesterCanVent = BooleanOptionItem.Create(10911, "CanVent", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
        JesterHasImpostorVision = BooleanOptionItem.Create(10913, "ImpostorVision", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
        SunnyboyChance = IntegerOptionItem.Create(10912, "SunnyboyChance", new(0, 100, 5), 0, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jester])
            .SetValueFormat(OptionFormat.Percent);
        //SetupRoleOptions(10950, TabGroup.NeutralRoles, CustomRoles.Masochist);
        //MasochistKillMax = IntegerOptionItem.Create(10955, "MasochistKillMax", new(1, 20, 1), 5, TabGroup.NeutralRoles, false)
        //.SetParent(CustomRoleSpawnChances[CustomRoles.Masochist])
        //.SetValueFormat(OptionFormat.Times);

        LoadingPercentage = 31;
        RoleLoadingText = "Neutral roles\nCollector";

        //   Baker.SetupCustomOption();
        Collector.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nCultist";
        Succubus.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nPhantom";
        SetupRoleOptions(11400, TabGroup.NeutralRoles, CustomRoles.Phantom);
        PhantomCanVent = BooleanOptionItem.Create(11410, "CanVent", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Phantom]);
        PhantomSnatchesWin = BooleanOptionItem.Create(11411, "SnatchesWin", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Phantom]);
        PhantomCanGuess = BooleanOptionItem.Create(11412, "CanGuess", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Phantom]);
        PhantomTasks = OverrideTasksData.Create(11413, TabGroup.NeutralRoles, CustomRoles.Phantom);
        //Pirate.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nTerrorist";
        SetupRoleOptions(11500, TabGroup.NeutralRoles, CustomRoles.Terrorist);
        CanTerroristSuicideWin = BooleanOptionItem.Create(11510, "CanTerroristSuicideWin", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
        TerroristCanGuess = BooleanOptionItem.Create(11511, "CanGuess", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
        //50220~50223を使用
        TerroristTasks = OverrideTasksData.Create(11512, TabGroup.NeutralRoles, CustomRoles.Terrorist);
        RoleLoadingText = "Neutral roles\nVulture";
        Vulture.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nWorkaholic";
        SetupRoleOptions(11700, TabGroup.NeutralRoles, CustomRoles.Workaholic); //TOH_Y
        WorkaholicCannotWinAtDeath = BooleanOptionItem.Create(11710, "WorkaholicCannotWinAtDeath", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
        WorkaholicVentCooldown = FloatOptionItem.Create(11711, "VentCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic])
            .SetValueFormat(OptionFormat.Seconds);
        WorkaholicVisibleToEveryone = BooleanOptionItem.Create(11712, "WorkaholicVisibleToEveryone", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
        WorkaholicGiveAdviceAlive = BooleanOptionItem.Create(11713, "WorkaholicGiveAdviceAlive", false, TabGroup.NeutralRoles, false)
            .SetParent(WorkaholicVisibleToEveryone);
        WorkaholicTasks = OverrideTasksData.Create(11714, TabGroup.NeutralRoles, CustomRoles.Workaholic);
        WorkaholicCanGuess = BooleanOptionItem.Create(11725, "CanGuess", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);

        LoadingPercentage = 32;
        RoleLoadingText = "Neutral roles\nArsonist";


        SetupRoleOptions(10400, TabGroup.NeutralRoles, CustomRoles.Arsonist);
        ArsonistDouseTime = FloatOptionItem.Create(10410, "ArsonistDouseTime", new(0f, 10f, 1f), 3f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Arsonist])
            .SetValueFormat(OptionFormat.Seconds);
        ArsonistCooldown = FloatOptionItem.Create(10411, "Cooldown", new(0f, 60f, 1f), 10f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Arsonist])
            .SetValueFormat(OptionFormat.Seconds);
        ArsonistCanIgniteAnytime = BooleanOptionItem.Create(10413, "ArsonistCanIgniteAnytime", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Arsonist]);
        ArsonistMinPlayersToIgnite = IntegerOptionItem.Create(10414, "ArsonistMinPlayersToIgnite", new(1, 14, 1), 1, TabGroup.NeutralRoles, false)
            .SetParent(ArsonistCanIgniteAnytime);
        ArsonistMaxPlayersToIgnite = IntegerOptionItem.Create(10415, "ArsonistMaxPlayersToIgnite", new(1, 14, 1), 3, TabGroup.NeutralRoles, false)
            .SetParent(ArsonistCanIgniteAnytime);
        ArsonistKeepsGameGoing = BooleanOptionItem.Create(10412, "ArsonistKeepsGameGoing", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Arsonist]);
        RoleLoadingText = "Neutral roles\nBlood Knight";
        BloodKnight.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nGlitch";
        Glitch.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nHex Master";
        HexMaster.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nBandit";
        Bandit.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nAgitater";
        Agitater.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nInfectious";
        Infectious.SetupCustomOption();

        LoadingPercentage = 33;
        RoleLoadingText = "Neutral roles\nJackal";

        Jackal.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nJinx";
        Jinx.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nJuggernaut";
        Juggernaut.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nMedusa";
        Medusa.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nPelican";
        Pelican.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nPickpocket";
        Pickpocket.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nPlaguebearer";
        PlagueBearer.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nPoisoner";
        Poisoner.SetupCustomOption();

        LoadingPercentage = 34;
        RoleLoadingText = "Neutral roles\nSerial Killer";

        NSerialKiller.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nCurse";
        PlagueDoctor.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nPostman";
        Postman.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nReckless";
        Reckless.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nMagician";
        Magician.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nWeaponMaster";
        WeaponMaster.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nPyromaniac";
        Pyromaniac.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nEclipse";
        Eclipse.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nHead Hunter";
        HeadHunter.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nVengeance";
        Vengeance.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nImitator";
        Imitator.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nWerewolf";
        Werewolf.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nStalker";
        DarkHide.SetupCustomOption(); //TOH_Y
        RoleLoadingText = "Neutral roles\nRitualist";
        Ritualist.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nTraitor";
        Traitor.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nVirus";
        Virus.SetupCustomOption();
        RoleLoadingText = "Neutral roles\nWraith";
        Wraith.SetupCustomOption();

        LoadingPercentage = 35;
        RoleLoadingText = "Add-ons\n.";

        // Add-Ons
        AddBracketsToAddons = BooleanOptionItem.Create(13500, "BracketAddons", false, TabGroup.Addons, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);


        RoleLoadingText = "Add-ons\nAntidote";
        SetupAdtRoleOptions(222420, CustomRoles.Antidote, canSetNum: true);
        ImpCanBeAntidote = BooleanOptionItem.Create(222426, "ImpCanBeAntidote", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
        CrewCanBeAntidote = BooleanOptionItem.Create(222427, "CrewCanBeAntidote", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
        NeutralCanBeAntidote = BooleanOptionItem.Create(222423, "NeutralCanBeAntidote", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
        AntidoteCDOpt = FloatOptionItem.Create(222424, "AntidoteCDOpt", new(0f, 180f, 1f), 5f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote])
            .SetValueFormat(OptionFormat.Seconds);
        AntidoteCDReset = BooleanOptionItem.Create(222425, "AntidoteCDReset", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
        RoleLoadingText = "Add-ons\nAutopsy";
        SetupAdtRoleOptions(13600, CustomRoles.Autopsy, canSetNum: true);
        ImpCanBeAutopsy = BooleanOptionItem.Create(13610, "ImpCanBeAutopsy", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Autopsy]);
        CrewCanBeAutopsy = BooleanOptionItem.Create(13611, "CrewCanBeAutopsy", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Autopsy]);
        NeutralCanBeAutopsy = BooleanOptionItem.Create(13612, "NeutralCanBeAutopsy", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Autopsy]);

        LoadingPercentage = 36;
        RoleLoadingText = "Add-ons\nBait";

        SetupAdtRoleOptions(13700, CustomRoles.Bait, canSetNum: true);
        ImpCanBeBait = BooleanOptionItem.Create(13710, "ImpCanBeBait", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);
        CrewCanBeBait = BooleanOptionItem.Create(13711, "CrewCanBeBait", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);
        NeutralCanBeBait = BooleanOptionItem.Create(13712, "NeutralCanBeBait", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);
        BaitDelayMin = FloatOptionItem.Create(13713, "BaitDelayMin", new(0f, 5f, 1f), 0f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait])
            .SetValueFormat(OptionFormat.Seconds);
        BaitDelayMax = FloatOptionItem.Create(13714, "BaitDelayMax", new(0f, 10f, 1f), 0f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait])
            .SetValueFormat(OptionFormat.Seconds);
        BaitDelayNotify = BooleanOptionItem.Create(13715, "BaitDelayNotify", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);
        BaitNotification = BooleanOptionItem.Create(13716, "BaitNotification", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);
        RoleLoadingText = "Add-ons\nBeartrap";
        SetupAdtRoleOptions(13800, CustomRoles.Trapper, canSetNum: true);
        ImpCanBeTrapper = BooleanOptionItem.Create(13810, "ImpCanBeTrapper", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Trapper]);
        CrewCanBeTrapper = BooleanOptionItem.Create(13811, "CrewCanBeTrapper", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Trapper]);
        NeutralCanBeTrapper = BooleanOptionItem.Create(13812, "NeutralCanBeTrapper", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Trapper]);
        TrapperBlockMoveTime = FloatOptionItem.Create(13813, "TrapperBlockMoveTime", new(1f, 180f, 1f), 5f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Trapper])
            .SetValueFormat(OptionFormat.Seconds);

        LoadingPercentage = 37;
        RoleLoadingText = "Add-ons\nDouble Shot";

        SetupAdtRoleOptions(13900, CustomRoles.DoubleShot, canSetNum: true, tab: TabGroup.Addons);
        ImpCanBeDoubleShot = BooleanOptionItem.Create(13910, "ImpCanBeDoubleShot", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DoubleShot]);
        CrewCanBeDoubleShot = BooleanOptionItem.Create(13911, "CrewCanBeDoubleShot", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DoubleShot]);
        NeutralCanBeDoubleShot = BooleanOptionItem.Create(13912, "NeutralCanBeDoubleShot", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DoubleShot]);
        RoleLoadingText = "Add-ons\nGlow";
        SetupAdtRoleOptions(14020, CustomRoles.Glow, canSetNum: true);
        ImpCanBeGlow = BooleanOptionItem.Create(14030, "ImpCanBeGlow", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glow]);
        CrewCanBeGlow = BooleanOptionItem.Create(14031, "CrewCanBeGlow", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glow]);
        NeutralCanBeGlow = BooleanOptionItem.Create(14032, "NeutralCanBeGlow", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glow]);
        RoleLoadingText = "Add-ons\nGravestone";
        SetupAdtRoleOptions(14000, CustomRoles.Gravestone, canSetNum: true);
        ImpCanBeGravestone = BooleanOptionItem.Create(14010, "ImpCanBeGravestone", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Gravestone]);
        CrewCanBeGravestone = BooleanOptionItem.Create(14011, "CrewCanBeGravestone", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Gravestone]);
        NeutralCanBeGravestone = BooleanOptionItem.Create(14012, "NeutralCanBeGravestone", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Gravestone]);

        LoadingPercentage = 38;
        RoleLoadingText = "Add-ons\nLazy";

        SetupAdtRoleOptions(14100, CustomRoles.Lazy, canSetNum: true);
        TasklessCrewCanBeLazy = BooleanOptionItem.Create(14110, "TasklessCrewCanBeLazy", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lazy]);
        TaskBasedCrewCanBeLazy = BooleanOptionItem.Create(14120, "TaskBasedCrewCanBeLazy", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lazy]);
        RoleLoadingText = "Add-ons\nTorch";
        SetupAdtRoleOptions(14200, CustomRoles.Torch, canSetNum: true);
        TorchVision = FloatOptionItem.Create(14210, "TorchVision", new(0.5f, 5f, 0.25f), 1.25f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Torch])
            .SetValueFormat(OptionFormat.Multiplier);
        TorchAffectedByLights = BooleanOptionItem.Create(14220, "TorchAffectedByLights", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Torch]);
        RoleLoadingText = "Add-ons\nLoyal";
        SetupAdtRoleOptions(15500, CustomRoles.Loyal, canSetNum: true);
        ImpCanBeLoyal = BooleanOptionItem.Create(15510, "ImpCanBeLoyal", true, TabGroup.Addons, false)
        .SetParent(CustomRoleSpawnChances[CustomRoles.Loyal]);
        CrewCanBeLoyal = BooleanOptionItem.Create(15511, "CrewCanBeLoyal", true, TabGroup.Addons, false)
        .SetParent(CustomRoleSpawnChances[CustomRoles.Loyal]);

        LoadingPercentage = 39;
        RoleLoadingText = "Add-ons\nLucky";

        SetupAdtRoleOptions(14300, CustomRoles.Lucky, canSetNum: true);
        LuckyProbability = IntegerOptionItem.Create(14310, "LuckyProbability", new(0, 100, 5), 50, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lucky])
            .SetValueFormat(OptionFormat.Percent);
        ImpCanBeLucky = BooleanOptionItem.Create(14311, "ImpCanBeLucky", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lucky]);
        CrewCanBeLucky = BooleanOptionItem.Create(14312, "CrewCanBeLucky", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lucky]);
        NeutralCanBeLucky = BooleanOptionItem.Create(14313, "NeutralCanBeLucky", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lucky]);
        RoleLoadingText = "Add-ons\nNecroview";
        SetupAdtRoleOptions(14400, CustomRoles.Necroview, canSetNum: true, tab: TabGroup.Addons);
        ImpCanBeNecroview = BooleanOptionItem.Create(14410, "ImpCanBeNecroview", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Necroview]);
        CrewCanBeNecroview = BooleanOptionItem.Create(14411, "CrewCanBeNecroview", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Necroview]);
        NeutralCanBeNecroview = BooleanOptionItem.Create(14412, "NeutralCanBeNecroview", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Necroview]);
        RoleLoadingText = "Add-ons\nReach";
        SetupAdtRoleOptions(14600, CustomRoles.Reach, canSetNum: true);

        LoadingPercentage = 40;
        RoleLoadingText = "Add-ons\nSchizophrenic";
        SetupAdtRoleOptions(14700, CustomRoles.DualPersonality, canSetNum: true);
        ImpCanBeDualPersonality = BooleanOptionItem.Create(14710, "ImpCanBeDualPersonality", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DualPersonality]);
        CrewCanBeDualPersonality = BooleanOptionItem.Create(14711, "CrewCanBeDualPersonality", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DualPersonality]);
        DualVotes = BooleanOptionItem.Create(14712, "DualVotes", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DualPersonality]);
        //    HideDualVotes = BooleanOptionItem.Create(14713, "HideDualVotes", true, TabGroup.Addons, false)
        //   .SetParent(DualVotes);

        LoadingPercentage = 41;
        RoleLoadingText = "Add-ons\nSeer";

        SetupAdtRoleOptions(14800, CustomRoles.Seer, canSetNum: true);
        ImpCanBeSeer = BooleanOptionItem.Create(14810, "ImpCanBeSeer", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Seer]);
        CrewCanBeSeer = BooleanOptionItem.Create(14811, "CrewCanBeSeer", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Seer]);
        NeutralCanBeSeer = BooleanOptionItem.Create(14812, "NeutralCanBeSeer", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Seer]);
        RoleLoadingText = "Add-ons\nTiebreaker";
        SetupAdtRoleOptions(14900, CustomRoles.Brakar, canSetNum: true);
        ImpCanBeTiebreaker = BooleanOptionItem.Create(14910, "ImpCanBeTiebreaker", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Brakar]);
        CrewCanBeTiebreaker = BooleanOptionItem.Create(14911, "CrewCanBeTiebreaker", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Brakar]);
        NeutralCanBeTiebreaker = BooleanOptionItem.Create(14912, "NeutralCanBeTiebreaker", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Brakar]);
        RoleLoadingText = "Add-ons\nWatcher";
        SetupAdtRoleOptions(15000, CustomRoles.Watcher, canSetNum: true);
        ImpCanBeWatcher = BooleanOptionItem.Create(15010, "ImpCanBeWatcher", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Watcher]);
        CrewCanBeWatcher = BooleanOptionItem.Create(15011, "CrewCanBeWatcher", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Watcher]);
        NeutralCanBeWatcher = BooleanOptionItem.Create(15012, "NeutralCanBeWatcher", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Watcher]);

        LoadingPercentage = 42;
        RoleLoadingText = "Add-ons\nAvenger";


        SetupAdtRoleOptions(15100, CustomRoles.Avanger, canSetNum: true);
        ImpCanBeAvanger = BooleanOptionItem.Create(15110, "ImpCanBeAvanger", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Avanger]);
        RoleLoadingText = "Add-ons\nBewilder";
        SetupAdtRoleOptions(15200, CustomRoles.Bewilder, canSetNum: true);
        BewilderVision = FloatOptionItem.Create(15210, "BewilderVision", new(0f, 5f, 0.05f), 0.6f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bewilder])
            .SetValueFormat(OptionFormat.Multiplier);
        ImpCanBeBewilder = BooleanOptionItem.Create(15211, "ImpCanBeBewilder", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bewilder]);
        CrewCanBeBewilder = BooleanOptionItem.Create(15212, "CrewCanBeBewilder", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bewilder]);
        NeutralCanBeBewilder = BooleanOptionItem.Create(15213, "NeutralCanBeBewilder", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bewilder]);

        LoadingPercentage = 43;
        RoleLoadingText = "Add-ons\nDiseased";

        /*     SetupAdtRoleOptions(15350, CustomRoles.Diseased, canSetNum: true);
             DiseasedMultiplier = FloatOptionItem.Create(15363, "DiseasedMultiplier", new(1.5f, 5f, 0.1f), 2f, TabGroup.Addons, false)
                 .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased])
                 .SetValueFormat(OptionFormat.Multiplier);
             ImpCanBeDiseased = BooleanOptionItem.Create(15360, "ImpCanBeDiseased", true, TabGroup.Addons, false)
                 .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
             CrewCanBeDiseased = BooleanOptionItem.Create(15361, "CrewCanBeDiseased", true, TabGroup.Addons, false)
                 .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
             NeutralCanBeDiseased = BooleanOptionItem.Create(15362, "NeutralCanBeDiseased", true, TabGroup.Addons, false)
                 .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]); */
        SetupAdtRoleOptions(111420, CustomRoles.Diseased, canSetNum: true);
        ImpCanBeDiseased = BooleanOptionItem.Create(111426, "ImpCanBeDiseased", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
        CrewCanBeDiseased = BooleanOptionItem.Create(111427, "CrewCanBeDiseased", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
        NeutralCanBeDiseased = BooleanOptionItem.Create(111423, "NeutralCanBeDiseased", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
        DiseasedCDOpt = FloatOptionItem.Create(111424, "DiseasedCDOpt", new(0f, 180f, 1f), 25f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased])
            .SetValueFormat(OptionFormat.Seconds);
        DiseasedCDReset = BooleanOptionItem.Create(111425, "DiseasedCDReset", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);

        LoadingPercentage = 44;
        RoleLoadingText = "Add-ons\nDisregarded";

        SetupAdtRoleOptions(15300, CustomRoles.Unreportable, canSetNum: true);
        ImpCanBeUnreportable = BooleanOptionItem.Create(15310, "ImpCanBeUnreportable", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unreportable]);
        CrewCanBeUnreportable = BooleanOptionItem.Create(15311, "CrewCanBeUnreportable", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unreportable]);
        NeutralCanBeUnreportable = BooleanOptionItem.Create(15312, "NeutralCanBeUnreportable", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unreportable]);
        RoleLoadingText = "Add-ons\nGhoul";
        SetupAdtRoleOptions(15250, CustomRoles.Ghoul, canSetNum: true);
        RoleLoadingText = "Add-ons\nOblivious";
        SetupAdtRoleOptions(15400, CustomRoles.Oblivious, canSetNum: true);
        ImpCanBeOblivious = BooleanOptionItem.Create(15410, "ImpCanBeOblivious", true, TabGroup.Addons, false)
        .SetParent(CustomRoleSpawnChances[CustomRoles.Oblivious]);
        CrewCanBeOblivious = BooleanOptionItem.Create(15411, "CrewCanBeOblivious", true, TabGroup.Addons, false)
        .SetParent(CustomRoleSpawnChances[CustomRoles.Oblivious]);
        NeutralCanBeOblivious = BooleanOptionItem.Create(15412, "NeutralCanBeOblivious", true, TabGroup.Addons, false)
        .SetParent(CustomRoleSpawnChances[CustomRoles.Oblivious]);
        ObliviousBaitImmune = BooleanOptionItem.Create(15413, "ObliviousBaitImmune", false, TabGroup.Addons, false)
        .SetParent(CustomRoleSpawnChances[CustomRoles.Oblivious]);
        RoleLoadingText = "Add-ons\nRascal";
        SetupAdtRoleOptions(15600, CustomRoles.Rascal, canSetNum: true, tab: TabGroup.Addons);
        RascalAppearAsMadmate = BooleanOptionItem.Create(15610, "RascalAppearAsMadmate", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Rascal]);

        LoadingPercentage = 45;
        RoleLoadingText = "Add-ons\nSunglasses";

        SetupAdtRoleOptions(15450, CustomRoles.Sunglasses, canSetNum: true);
        SunglassesVision = FloatOptionItem.Create(15460, "SunglassesVision", new(0f, 5f, 0.05f), 0.75f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sunglasses])
            .SetValueFormat(OptionFormat.Multiplier);
        ImpCanBeSunglasses = BooleanOptionItem.Create(15461, "ImpCanBeSunglasses", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sunglasses]);
        CrewCanBeSunglasses = BooleanOptionItem.Create(15462, "CrewCanBeSunglasses", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sunglasses]);
        NeutralCanBeSunglasses = BooleanOptionItem.Create(15463, "NeutralCanBeSunglasses", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sunglasses]);

        LoadingPercentage = 46;
        RoleLoadingText = "Add-ons\nUnlucky";

        SetupAdtRoleOptions(14350, CustomRoles.Unlucky, canSetNum: true);
        UnluckyKillSuicideChance = IntegerOptionItem.Create(14364, "UnluckyKillSuicideChance", new(0, 100, 1), 2, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
            .SetValueFormat(OptionFormat.Percent);
        UnluckyTaskSuicideChance = IntegerOptionItem.Create(14365, "UnluckyTaskSuicideChance", new(0, 100, 1), 5, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
            .SetValueFormat(OptionFormat.Percent);
        UnluckyVentSuicideChance = IntegerOptionItem.Create(14366, "UnluckyVentSuicideChance", new(0, 100, 1), 3, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
            .SetValueFormat(OptionFormat.Percent);
        UnluckyReportSuicideChance = IntegerOptionItem.Create(14367, "UnluckyReportSuicideChance", new(0, 100, 1), 1, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
            .SetValueFormat(OptionFormat.Percent);
        UnluckySabotageSuicideChance = IntegerOptionItem.Create(14368, "UnluckySabotageSuicideChance", new(0, 100, 1), 4, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
            .SetValueFormat(OptionFormat.Percent);
        ImpCanBeUnlucky = BooleanOptionItem.Create(14361, "ImpCanBeUnlucky", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky]);
        CrewCanBeUnlucky = BooleanOptionItem.Create(14362, "CrewCanBeUnlucky", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky]);
        NeutralCanBeUnlucky = BooleanOptionItem.Create(14363, "NeutralCanBeUnlucky", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky]);

        LoadingPercentage = 47;
        RoleLoadingText = "Add-ons\nWorkhorse";

        Workhorse.SetupCustomOption();
        RoleLoadingText = "Add-ons\nMadmate";

        SetupAdtRoleOptions(15800, CustomRoles.Madmate, canSetNum: true, canSetChance: false);
        MadmateSpawnMode = StringOptionItem.Create(15810, "MadmateSpawnMode", madmateSpawnMode, 0, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);
        MadmateCountMode = StringOptionItem.Create(15811, "MadmateCountMode", madmateCountMode, 0, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);
        SheriffCanBeMadmate = BooleanOptionItem.Create(15812, "SheriffCanBeMadmate", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);
        MayorCanBeMadmate = BooleanOptionItem.Create(15813, "MayorCanBeMadmate", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);
        NGuesserCanBeMadmate = BooleanOptionItem.Create(15814, "NGuesserCanBeMadmate", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);
        MarshallCanBeMadmate = BooleanOptionItem.Create(15815, "MarshallCanBeMadmate", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);
        FarseerCanBeMadmate = BooleanOptionItem.Create(15816, "FarseerCanBeMadmate", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);
        RetributionistCanBeMadmate = BooleanOptionItem.Create(15817, "RetributionistCanBeMadmate", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);
        SnitchCanBeMadmate = BooleanOptionItem.Create(15818, "SnitchCanBeMadmate", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);
        MadSnitchTasks = IntegerOptionItem.Create(15819, "MadSnitchTasks", new(1, 99, 1), 3, TabGroup.Addons, false)
            .SetParent(SnitchCanBeMadmate)
            .SetValueFormat(OptionFormat.Pieces);
        JudgeCanBeMadmate = BooleanOptionItem.Create(15820, "JudgeCanBeMadmate", false, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);

        LoadingPercentage = 48;
        RoleLoadingText = "Add-ons\nLast Impostor";

        LastImpostor.SetupCustomOption();
        RoleLoadingText = "Add-ons\nMare";
        SetupAdtRoleOptions(1600, CustomRoles.Mare, canSetNum: true, tab: TabGroup.Addons);
        MareKillCD = FloatOptionItem.Create(1605, "KillCooldown", new(0f, 60f, 1f), 15f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mare])
            .SetValueFormat(OptionFormat.Seconds);
        MareKillCDNormally = FloatOptionItem.Create(1606, "KillCooldownNormally", new(0f, 90f, 1f), 40f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mare])
            .SetValueFormat(OptionFormat.Seconds);
        MareHasIncreasedSpeed = BooleanOptionItem.Create(1607, "MareHasIncreasedSpeed", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mare]);
        MareSpeedDuringLightsOut = FloatOptionItem.Create(1608, "MareSpeedDuringLightsOut", new(0.5f, 3f, 0.05f), 1.75f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mare])
            .SetValueFormat(OptionFormat.Multiplier);
        RoleLoadingText = "Add-ons\nMimic";
        SetupAdtRoleOptions(16000, CustomRoles.Mimic, canSetNum: true, tab: TabGroup.Addons);
        MimicCanSeeDeadRoles = BooleanOptionItem.Create(16010, "MimicCanSeeDeadRoles", true, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mimic]);

        LoadingPercentage = 49;
        RoleLoadingText = "Add-ons\nStealer";

        SetupAdtRoleOptions(16100, CustomRoles.TicketsStealer, canSetNum: true, tab: TabGroup.Addons);
        TicketsPerKill = FloatOptionItem.Create(16110, "TicketsPerKill", new(0.1f, 10f, 0.1f), 0.5f, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TicketsStealer]);
        SetupAdtRoleOptions(16050, CustomRoles.Swift, canSetNum: true, tab: TabGroup.Addons);
        RoleLoadingText = "Add-ons\nDamocles";
        Damocles.SetupCustomOption();
        RoleLoadingText = "Add-ons\nDeadly Quota";
        SetupAdtRoleOptions(14650, CustomRoles.DeadlyQuota, canSetNum: true);
        DQNumOfKillsNeeded = IntegerOptionItem.Create(14660, "DQNumOfKillsNeeded", new(1, 14, 1), 3, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DeadlyQuota]);
        //    SetupAdtRoleOptions(16300, CustomRoles.Minimalism, canSetNum: true, tab: TabGroup.Addons);


        SetupLoversRoleOptionsToggle(16200);





        /*   SetupAdtRoleOptions(6050900, CustomRoles.Rogue, canSetNum: true);
           ImpCanBeRogue = BooleanOptionItem.Create(6050903, "ImpCanBeRogue", true, TabGroup.Addons, false)
           .SetParent(CustomRoleSpawnChances[CustomRoles.Rogue]);
           CrewCanBeRogue = BooleanOptionItem.Create(6050904, "CrewCanBeRogue", true, TabGroup.Addons, false)
           .SetParent(CustomRoleSpawnChances[CustomRoles.Rogue]);
           NeutralCanBeRogue = BooleanOptionItem.Create(6050905, "NeutralCanBeRogue", true, TabGroup.Addons, false)
           .SetParent(CustomRoleSpawnChances[CustomRoles.Rogue]);
           RogueKnowEachOther = BooleanOptionItem.Create(6050906, "RogueKnowEachOther", false, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Rogue]);
           RogueKnowEachOtherRoles = BooleanOptionItem.Create(6050907, "RogueKnowEachOtherRoles", false, TabGroup.Addons, false).SetParent(RogueKnowEachOther);
   */

        LoadingPercentage = 50;
        RoleLoadingText = "Experimental roles\nKilling Machine";

        // 乐子职业


        SetupRoleOptions(16300, TabGroup.OtherRoles, CustomRoles.Minimalism);
        MNKillCooldown = FloatOptionItem.Create(16310, "KillCooldown", new(2.5f, 180f, 2.5f), 10f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Minimalism])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Experimental roles\nZombie";
        SetupRoleOptions(16400, TabGroup.OtherRoles, CustomRoles.Zombie);
        ZombieKillCooldown = FloatOptionItem.Create(16410, "KillCooldown", new(0f, 180f, 2.5f), 5f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Zombie])
            .SetValueFormat(OptionFormat.Seconds);
        ZombieSpeedReduce = FloatOptionItem.Create(16411, "ZombieSpeedReduce", new(0.0f, 1.0f, 0.1f), 0.1f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Zombie])
            .SetValueFormat(OptionFormat.Multiplier);

        LoadingPercentage = 51;
        RoleLoadingText = "Experimental roles\nTrapster";

        SetupRoleOptions(16500, TabGroup.OtherRoles, CustomRoles.BoobyTrap);
        BTKillCooldown = FloatOptionItem.Create(16510, "KillCooldown", new(2.5f, 180f, 2.5f), 20f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.BoobyTrap])
            .SetValueFormat(OptionFormat.Seconds);
        TrapOnlyWorksOnTheBodyBoobyTrap = BooleanOptionItem.Create(16511, "TrapOnlyWorksOnTheBodyBoobyTrap", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.BoobyTrap]);
        RoleLoadingText = "Experimental roles\nCapitalist";
        SetupRoleOptions(16600, TabGroup.OtherRoles, CustomRoles.Capitalism);
        CapitalismSkillCooldown = FloatOptionItem.Create(16610, "CapitalismSkillCooldown", new(0f, 60f, 1f), 10f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Capitalism])
            .SetValueFormat(OptionFormat.Seconds);
        CapitalismKillCooldown = FloatOptionItem.Create(16611, "KillCooldown", new(2.5f, 60f, 2.5f), 25f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Capitalism])
            .SetValueFormat(OptionFormat.Seconds);

        LoadingPercentage = 52;
        RoleLoadingText = "Experimental roles\nLightning";

        BallLightning.SetupCustomOption();
        RoleLoadingText = "Experimental roles\nEraser";
        Eraser.SetupCustomOption();
        RoleLoadingText = "Experimental roles\nButcher";
        SetupRoleOptions(16900, TabGroup.OtherRoles, CustomRoles.OverKiller);
        RoleLoadingText = "Experimental roles\nDisperser";
        Disperser.SetupCustomOption();
        RoleLoadingText = "Experimental roles\nCopyCat";

        /*   SetupRoleOptions(18000, TabGroup.OtherRoles, CustomRoles.SpeedBooster);
           SpeedBoosterUpSpeed = FloatOptionItem.Create(18010, "SpeedBoosterUpSpeed", new(0.1f, 1.0f, 0.1f), 0.2f, TabGroup.OtherRoles, false)
           .SetParent(CustomRoleSpawnChances[CustomRoles.SpeedBooster])
               .SetValueFormat(OptionFormat.Multiplier);
           SpeedBoosterTimes = IntegerOptionItem.Create(18011, "SpeedBoosterTimes", new(1, 99, 1), 5, TabGroup.OtherRoles, false)
           .SetParent(CustomRoleSpawnChances[CustomRoles.SpeedBooster])
               .SetValueFormat(OptionFormat.Times); */
        /*   SetupRoleOptions(18100, TabGroup.OtherRoles, CustomRoles.Glitch);
           GlitchCanVote = BooleanOptionItem.Create(18110, "GlitchCanVote", true, TabGroup.OtherRoles, false)
               .SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]); */
        //     Divinator.SetupCustomOption();
        // 中立


        CopyCat.SetupCustomOption();

        LoadingPercentage = 53;
        RoleLoadingText = "Experimental roles\nGod";

        SetupRoleOptions(18200, TabGroup.OtherRoles, CustomRoles.God);
        NotifyGodAlive = BooleanOptionItem.Create(18210, "NotifyGodAlive", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
        GodCanGuess = BooleanOptionItem.Create(18211, "CanGuess", false, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
        RoleLoadingText = "Experimental roles\nVector";
        SetupRoleOptions(18300, TabGroup.OtherRoles, CustomRoles.Mario);
        MarioVentNumWin = IntegerOptionItem.Create(18310, "MarioVentNumWin", new(5, 900, 5), 40, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mario])
            .SetValueFormat(OptionFormat.Times);
        MarioVentCD = FloatOptionItem.Create(18311, "VentCooldown", new(0f, 180f, 1f), 15f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mario])
            .SetValueFormat(OptionFormat.Seconds);

        LoadingPercentage = 54;
        RoleLoadingText = "Experimental roles\nRevolutionist";

        SetupRoleOptions(18400, TabGroup.OtherRoles, CustomRoles.Revolutionist);
        RevolutionistDrawTime = FloatOptionItem.Create(18410, "RevolutionistDrawTime", new(0f, 10f, 1f), 3f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
            .SetValueFormat(OptionFormat.Seconds);
        RevolutionistCooldown = FloatOptionItem.Create(18411, "RevolutionistCooldown", new(5f, 100f, 1f), 10f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
            .SetValueFormat(OptionFormat.Seconds);
        RevolutionistDrawCount = IntegerOptionItem.Create(18412, "RevolutionistDrawCount", new(1, 14, 1), 6, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
            .SetValueFormat(OptionFormat.Players);
        RevolutionistKillProbability = IntegerOptionItem.Create(18413, "RevolutionistKillProbability", new(0, 100, 5), 15, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
            .SetValueFormat(OptionFormat.Percent);
        RevolutionistVentCountDown = FloatOptionItem.Create(18414, "RevolutionistVentCountDown", new(1f, 180f, 1f), 15f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
            .SetValueFormat(OptionFormat.Seconds);
        RoleLoadingText = "Experimental roles\nProvocateur";
        SetupRoleOptions(18500, TabGroup.OtherRoles, CustomRoles.Provocateur);
        RoleLoadingText = "Experimental roles\nSpiritcaller";
        Spiritcaller.SetupCustomOption();

        LoadingPercentage = 55;
        //RoleLoadingText = "Experimental roles\nNeptune";


        //SetupAdtRoleOptions(18600, CustomRoles.Ntr, tab: TabGroup.OtherRoles);
        RoleLoadingText = "Experimental roles\nFlash";
        SetupAdtRoleOptions(18700, CustomRoles.Flashman, canSetNum: true, tab: TabGroup.OtherRoles);
        FlashmanSpeed = FloatOptionItem.Create(6050335, "FlashmanSpeed", new(0.25f, 3f, 0.25f), 2.5f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Flashman])
            .SetValueFormat(OptionFormat.Multiplier);

        RoleLoadingText = "Experimental roles\nYouTuber";
        SetupAdtRoleOptions(18800, CustomRoles.Youtuber, canSetNum: true, tab: TabGroup.OtherRoles);

        LoadingPercentage = 56;
        RoleLoadingText = "Experimental roles\nEgoist";

        SetupAdtRoleOptions(18900, CustomRoles.Egoist, canSetNum: true, tab: TabGroup.OtherRoles);
        CrewCanBeEgoist = BooleanOptionItem.Create(18910, "CrewCanBeEgoist", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Egoist]);
        ImpCanBeEgoist = BooleanOptionItem.Create(18911, "ImpCanBeEgoist", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Egoist]);
        ImpEgoistVisibalToAllies = BooleanOptionItem.Create(18912, "ImpEgoistVisibalToAllies", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Egoist]);
        /*    SetupAdtRoleOptions(19000, CustomRoles.Sidekick, canSetNum: true, tab: TabGroup.OtherRoles);
            SidekickCountMode = StringOptionItem.Create(19010, "SidekickCountMode", sidekickCountMode, 0, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sidekick]);
            CrewmateCanBeSidekick = BooleanOptionItem.Create(19011, "CrewmatesCanBeSidekick", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sidekick]);
            NeutralCanBeSidekick = BooleanOptionItem.Create(19012, "NeutralsCanBeSidekick", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sidekick]);
            ImpostorCanBeSidekick = BooleanOptionItem.Create(19013, "ImpostorsCanBeSidekick", false, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sidekick]);
            SidekickCanKillJackal = BooleanOptionItem.Create(19014, "SidekickCanKillJackal", false, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sidekick]);
         //   JackalWinWithSidekick = BooleanOptionItem.Create(19015, "JackalWinWithSidekick", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sidekick]);
            SidekickKnowOtherSidekick = BooleanOptionItem.Create(19016, "SidekickKnowOtherSidekick", false, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sidekick]);
            SidekickKnowOtherSidekickRole = BooleanOptionItem.Create(19017, "SidekickKnowOtherSidekickRole", false, TabGroup.OtherRoles, false)
            .SetParent(SidekickKnowOtherSidekick);
            SidekickCanKillSidekick = BooleanOptionItem.Create(19018, "SidekickCanKillSidekick", false, TabGroup.OtherRoles, false)
            .SetParent(SidekickKnowOtherSidekick); */

        LoadingPercentage = 57;
        RoleLoadingText = "Experimental roles\nGuesser";

        SetupAdtRoleOptions(19100, CustomRoles.Guesser, canSetNum: true, tab: TabGroup.OtherRoles);
        ImpCanBeGuesser = BooleanOptionItem.Create(19110, "ImpCanBeGuesser", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
        CrewCanBeGuesser = BooleanOptionItem.Create(19111, "CrewCanBeGuesser", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
        NeutralCanBeGuesser = BooleanOptionItem.Create(19112, "NeutralCanBeGuesser", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);

        LoadingPercentage = 58;

        //   GCanGuessImp = BooleanOptionItem.Create(19113, "GCanGuessImp", false, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
        //   GCanGuessCrew = BooleanOptionItem.Create(19114, "GCanGuessCrew", false, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
        GCanGuessAdt = BooleanOptionItem.Create(19115, "GCanGuessAdt", false, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
        GCanGuessTaskDoneSnitch = BooleanOptionItem.Create(19116, "GCanGuessTaskDoneSnitch", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
        GTryHideMsg = BooleanOptionItem.Create(19117, "GuesserTryHideMsg", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser])
            .SetColor(Color.green);

        LoadingPercentage = 59;
        RoleLoadingText = "Experimental roles\nFool";

        SetupAdtRoleOptions(19200, CustomRoles.Fool, canSetNum: true, tab: TabGroup.OtherRoles);
        ImpCanBeFool = BooleanOptionItem.Create(19210, "ImpCanBeFool", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Fool]);
        CrewCanBeFool = BooleanOptionItem.Create(19211, "CrewCanBeFool", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Fool]);
        NeutralCanBeFool = BooleanOptionItem.Create(19212, "NeutralCanBeFool", true, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Fool]);

        RoleLoadingText = string.Empty;


        #endregion

        #region 系统设置

        LoadingPercentage = 60;
        MainLoadingText = "Building TOHE settings";

        KickLowLevelPlayer = IntegerOptionItem.Create(19300, "KickLowLevelPlayer", new(0, 100, 1), 0, TabGroup.SystemSettings, false)
            .SetValueFormat(OptionFormat.Level)
            .SetHeader(true);
        EnableKillerLeftCommand = BooleanOptionItem.Create(44428, "EnableKillerLeftCommand", true, TabGroup.SystemSettings, false)
            .SetColor(Color.green);
        SeeEjectedRolesInMeeting = BooleanOptionItem.Create(44429, "SeeEjectedRolesInMeeting", true, TabGroup.SystemSettings, false)
            .SetColor(Color.green);
        KickAndroidPlayer = BooleanOptionItem.Create(19301, "KickAndroidPlayer", false, TabGroup.SystemSettings, false);
        KickPlayerFriendCodeNotExist = BooleanOptionItem.Create(19302, "KickPlayerFriendCodeNotExist", false, TabGroup.SystemSettings, true);
        ApplyDenyNameList = BooleanOptionItem.Create(19303, "ApplyDenyNameList", true, TabGroup.SystemSettings, true);
        ApplyBanList = BooleanOptionItem.Create(19304, "ApplyBanList", true, TabGroup.SystemSettings, true);
        ApplyModeratorList = BooleanOptionItem.Create(19305, "ApplyModeratorList", false, TabGroup.SystemSettings, false);

        LoadingPercentage = 61;

        //    ApplyAllowList = BooleanOptionItem.Create(19306, "ApplyAllowList", false, TabGroup.SystemSettings, false);
        //   AllowSayCommand = BooleanOptionItem.Create(19307, "AllowSayCommand", false, TabGroup.SystemSettings, false);
        //     ApplyReminderMsg = BooleanOptionItem.Create(19308, "ApplyReminderMsg", false, TabGroup.SystemSettings, false);
        /*     TimeForReminder = IntegerOptionItem.Create(19309, "TimeForReminder", new(0, 99, 1), 3, TabGroup.SystemSettings, false)
                 .SetParent(TimeForReminder)
                 .SetValueFormat(OptionFormat.Seconds); */
        AutoKickStart = BooleanOptionItem.Create(19310, "AutoKickStart", false, TabGroup.SystemSettings, false);
        AutoKickStartTimes = IntegerOptionItem.Create(19311, "AutoKickStartTimes", new(0, 99, 1), 1, TabGroup.SystemSettings, false)
            .SetParent(AutoKickStart)
            .SetValueFormat(OptionFormat.Times);
        AutoKickStartAsBan = BooleanOptionItem.Create(19312, "AutoKickStartAsBan", false, TabGroup.SystemSettings, false)
            .SetParent(AutoKickStart);
        AutoKickStopWords = BooleanOptionItem.Create(19313, "AutoKickStopWords", false, TabGroup.SystemSettings, false);
        AutoKickStopWordsTimes = IntegerOptionItem.Create(19314, "AutoKickStopWordsTimes", new(0, 99, 1), 3, TabGroup.SystemSettings, false)
            .SetParent(AutoKickStopWords)
            .SetValueFormat(OptionFormat.Times);
        AutoKickStopWordsAsBan = BooleanOptionItem.Create(19315, "AutoKickStopWordsAsBan", false, TabGroup.SystemSettings, false)
            .SetParent(AutoKickStopWords);

        LoadingPercentage = 62;

        AutoWarnStopWords = BooleanOptionItem.Create(19316, "AutoWarnStopWords", false, TabGroup.SystemSettings, false);
        MinWaitAutoStart = FloatOptionItem.Create(44420, "MinWaitAutoStart", new(0f, 10f, 0.5f), 1.5f, TabGroup.SystemSettings, false);
        MaxWaitAutoStart = FloatOptionItem.Create(44421, "MaxWaitAutoStart", new(0f, 10f, 0.5f), 1.5f, TabGroup.SystemSettings, false);
        PlayerAutoStart = IntegerOptionItem.Create(44422, "PlayerAutoStart", new(1, 15, 1), 14, TabGroup.SystemSettings, false);
        AutoStartTimer = IntegerOptionItem.Create(44423, "AutoStartTimer", new(10, 600, 1), 20, TabGroup.SystemSettings, false)
            .SetValueFormat(OptionFormat.Seconds);
        AutoPlayAgain = BooleanOptionItem.Create(44424, "AutoPlayAgain", false, TabGroup.SystemSettings, false);
        AutoPlayAgainCountdown = IntegerOptionItem.Create(44425, "AutoPlayAgainCountdown", new(1, 20, 1), 10, TabGroup.SystemSettings, false)
            .SetParent(AutoPlayAgain);

        LowLoadMode = BooleanOptionItem.Create(19317, "LowLoadMode", true, TabGroup.SystemSettings, false)
            .SetHeader(true)
            .SetColor(Color.green);

        DeepLowLoad = BooleanOptionItem.Create(19325, "DeepLowLoad", false, TabGroup.SystemSettings, false)
            .SetColor(Color.red);

        DontUpdateDeadPlayers = BooleanOptionItem.Create(19326, "DontUpdateDeadPlayers", true, TabGroup.SystemSettings, false)
            .SetColor(Color.red);

        EndWhenPlayerBug = BooleanOptionItem.Create(19318, "EndWhenPlayerBug", true, TabGroup.SystemSettings, false)
            .SetHeader(true)
            .SetColor(Color.blue);

        CheatResponses = StringOptionItem.Create(19319, "CheatResponses", CheatResponsesName, 0, TabGroup.SystemSettings, false)
            .SetHeader(true);

        //HighLevelAntiCheat = StringOptionItem.Create(19320, "HighLevelAntiCheat", CheatResponsesName, 0, TabGroup.SystemSettings, false)
        //.SetHeader(true);

        LoadingPercentage = 63;


        AutoDisplayKillLog = BooleanOptionItem.Create(19321, "AutoDisplayKillLog", true, TabGroup.SystemSettings, false)
            .SetHeader(true);
        AutoDisplayLastRoles = BooleanOptionItem.Create(19322, "AutoDisplayLastRoles", true, TabGroup.SystemSettings, false);
        AutoDisplayLastResult = BooleanOptionItem.Create(19323, "AutoDisplayLastResult", true, TabGroup.SystemSettings, false);

        SuffixMode = StringOptionItem.Create(19324, "SuffixMode", suffixModes, 0, TabGroup.SystemSettings, true)
            .SetHeader(true);
        HideGameSettings = BooleanOptionItem.Create(19400, "HideGameSettings", false, TabGroup.SystemSettings, false);
        DIYGameSettings = BooleanOptionItem.Create(19401, "DIYGameSettings", false, TabGroup.SystemSettings, false);
        PlayerCanSetColor = BooleanOptionItem.Create(19402, "PlayerCanSetColor", false, TabGroup.SystemSettings, false);
        FormatNameMode = StringOptionItem.Create(19403, "FormatNameMode", formatNameModes, 0, TabGroup.SystemSettings, false);
        DisableEmojiName = BooleanOptionItem.Create(19404, "DisableEmojiName", true, TabGroup.SystemSettings, false);
        ChangeNameToRoleInfo = BooleanOptionItem.Create(19405, "ChangeNameToRoleInfo", true, TabGroup.SystemSettings, false);
        SendRoleDescriptionFirstMeeting = BooleanOptionItem.Create(19406, "SendRoleDescriptionFirstMeeting", true, TabGroup.SystemSettings, false);
        NoGameEnd = BooleanOptionItem.Create(19407, "NoGameEnd", false, TabGroup.SystemSettings, false)
            .SetColor(Color.red);
        AllowConsole = BooleanOptionItem.Create(19408, "AllowConsole", false, TabGroup.SystemSettings, false)
            .SetColor(Color.red);
        RoleAssigningAlgorithm = StringOptionItem.Create(19409, "RoleAssigningAlgorithm", roleAssigningAlgorithms, 4, TabGroup.SystemSettings, true)
           .RegisterUpdateValueEvent(
                (object obj, OptionItem.UpdateValueEventArgs args) => IRandom.SetInstanceById(args.CurrentValue)
            );
        KPDCamouflageMode = StringOptionItem.Create(19500, "KPDCamouflageMode", CamouflageMode, 0, TabGroup.SystemSettings, false)
            .SetHeader(true)
            .SetColor(new Color32(255, 192, 203, byte.MaxValue));

        LoadingPercentage = 64;


        //DebugModeManager.SetupCustomOption();

        EnableUpMode = BooleanOptionItem.Create(19600, "EnableYTPlan", false, TabGroup.SystemSettings, false)
            .SetColor(Color.cyan)
            .SetHeader(true);

        #endregion

        #region 游戏设置

        MainLoadingText = "Building Settings for Other Gamemodes";

        //SoloKombat
        SoloKombatManager.SetupCustomOption();
        //FFA
        FFAManager.SetupCustomOption();


        LoadingPercentage = 65;
        MainLoadingText = "Building game settings";

        //驱逐相关设定
        TextOptionItem.Create(100023, "MenuTitle.Ejections", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        CEMode = StringOptionItem.Create(19800, "ConfirmEjectionsMode", ConfirmEjectionsMode, 2, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ShowImpRemainOnEject = BooleanOptionItem.Create(19810, "ShowImpRemainOnEject", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ShowNKRemainOnEject = BooleanOptionItem.Create(19811, "ShowNKRemainOnEject", true, TabGroup.GameSettings, false)
        .SetParent(ShowImpRemainOnEject)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        LoadingPercentage = 66;

        ShowTeamNextToRoleNameOnEject = BooleanOptionItem.Create(19812, "ShowTeamNextToRoleNameOnEject", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ConfirmEgoistOnEject = BooleanOptionItem.Create(19813, "ConfirmEgoistOnEject", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue))
            .SetHeader(true);
        /*            ConfirmSidekickOnEject = BooleanOptionItem.Create(19814, "ConfirmSidekickOnEject", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue)); */
        ConfirmLoversOnEject = BooleanOptionItem.Create(19815, "ConfirmLoversOnEject", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        LoadingPercentage = 67;


        //Maps Settings
        TextOptionItem.Create(100024, "MenuTitle.MapsSettings", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Random Maps Mode
        RandomMapsMode = BooleanOptionItem.Create(19900, "RandomMapsMode", false, TabGroup.GameSettings, false)
            .SetHeader(true)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        //AddedTheSkeld = BooleanOptionItem.Create(19910, "AddedTheSkeld", false, TabGroup.GameSettings, false)
        //    .SetParent(RandomMapsMode);
        //AddedMiraHQ = BooleanOptionItem.Create(19911, "AddedMIRAHQ", false, TabGroup.GameSettings, false)
        //    .SetParent(RandomMapsMode);

        LoadingPercentage = 68;

        //AddedPolus = BooleanOptionItem.Create(19912, "AddedPolus", false, TabGroup.GameSettings, false)
        //    .SetParent(RandomMapsMode);
        //AddedTheAirship = BooleanOptionItem.Create(19913, "AddedTheAirship", false, TabGroup.GameSettings, false)
        //    .SetParent(RandomMapsMode);
        // MapDleks = CustomOption.Create(19914, Color.white, "AddedDleks", false, RandomMapMode);
        SkeldChance = IntegerOptionItem.Create(19910, "SkeldChance", new(0, 100, 5), 0, TabGroup.GameSettings, false)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        MiraChance = IntegerOptionItem.Create(19911, "MiraChance", new(0, 100, 5), 0, TabGroup.GameSettings, false)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        PolusChance = IntegerOptionItem.Create(19912, "PolusChance", new(0, 100, 5), 0, TabGroup.GameSettings, false)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        AirshipChance = IntegerOptionItem.Create(19913, "AirshipChance", new(0, 100, 5), 0, TabGroup.GameSettings, false)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        FungleChance = IntegerOptionItem.Create(19922, "FungleChance", new(0, 100, 5), 10, TabGroup.GameSettings, false)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);

        LoadingPercentage = 69;


        // Random Spawn
        RandomSpawn = BooleanOptionItem.Create(22000, "RandomSpawn", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        AirshipAdditionalSpawn = BooleanOptionItem.Create(22010, "AirshipAdditionalSpawn", false, TabGroup.GameSettings, false)
        .SetParent(RandomSpawn)
            .SetGameMode(CustomGameMode.Standard);

        // Airship Variable Electrical
        AirshipVariableElectrical = BooleanOptionItem.Create(22100, "AirshipVariableElectrical", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Disable Airship Moving Platform
        DisableAirshipMovingPlatform = BooleanOptionItem.Create(22110, "DisableAirshipMovingPlatform", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Disable Spore Triggers on Fungle
        DisableSporeTriggerOnFungle = BooleanOptionItem.Create(22130, "DisableSporeTriggerOnFungle", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Disable Zipline On Fungle
        DisableZiplineOnFungle = BooleanOptionItem.Create(22305, "DisableZiplineOnFungle", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Disable Zipline From Top
        DisableZiplineFromTop = BooleanOptionItem.Create(22308, "DisableZiplineFromTop", false, TabGroup.GameSettings, false)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Disable Zipline From Under
        DisableZiplineFromUnder = BooleanOptionItem.Create(22310, "DisableZiplineFromUnder", false, TabGroup.GameSettings, false)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        DisableZiplineForCrew = BooleanOptionItem.Create(22316, "DisableZiplineForCrew", false, TabGroup.GameSettings, false)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        DisableZiplineForImps = BooleanOptionItem.Create(22318, "DisableZiplineForImps", false, TabGroup.GameSettings, false)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        DisableZiplineForNeutrals = BooleanOptionItem.Create(22320, "DisableZiplineForNeutrals", false, TabGroup.GameSettings, false)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        ZiplineTravelTimeFromBottom = FloatOptionItem.Create(22312, "ZiplineTravelTimeFromBottom", new(0.5f, 10f, 0.5f), 4f, TabGroup.GameSettings, false)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        ZiplineTravelTimeFromTop = FloatOptionItem.Create(22314, "ZiplineTravelTimeFromTop", new(0.5f, 10f, 0.5f), 2f, TabGroup.GameSettings, false)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Reset Doors After Meeting
        ResetDoorsEveryTurns = BooleanOptionItem.Create(22120, "ResetDoorsEveryTurns", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Reset Doors Mode
        DoorsResetMode = StringOptionItem.Create(22122, "DoorsResetMode", EnumHelper.GetAllNames<DoorsReset.ResetMode>(), 2, TabGroup.GameSettings, false)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(ResetDoorsEveryTurns);

        LoadingPercentage = 70;

        // Sabotage
        TextOptionItem.Create(100025, "MenuTitle.Sabotage", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetHeader(true);

        // CommsCamouflage
        CommsCamouflage = BooleanOptionItem.Create(22200, "CommsCamouflage", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));
        CommsCamouflageDisableOnFungle = BooleanOptionItem.Create(22202, "CommsCamouflageDisableOnFungle", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflage)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));
        DisableReportWhenCC = BooleanOptionItem.Create(22300, "DisableReportWhenCC", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        LoadingPercentage = 71;


        // Sabotage Cooldown Control
        SabotageCooldownControl = BooleanOptionItem.Create(22400, "SabotageCooldownControl", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        SabotageCooldown = FloatOptionItem.Create(22405, "SabotageCooldown", new(1f, 60f, 1f), 30f, TabGroup.GameSettings, false)
            .SetParent(SabotageCooldownControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        // Sabotage Duration Control
        SabotageTimeControl = BooleanOptionItem.Create(22410, "SabotageTimeControl", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);

        // The Skeld
        SkeldReactorTimeLimit = FloatOptionItem.Create(22418, "SkeldReactorTimeLimit", new(5f, 90f, 1f), 30f, TabGroup.GameSettings, false)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        SkeldO2TimeLimit = FloatOptionItem.Create(22419, "SkeldO2TimeLimit", new(5f, 90f, 1f), 30f, TabGroup.GameSettings, false)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // Mira HQ
        MiraReactorTimeLimit = FloatOptionItem.Create(22422, "MiraReactorTimeLimit", new(5f, 90f, 1f), 45f, TabGroup.GameSettings, false)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        MiraO2TimeLimit = FloatOptionItem.Create(22423, "MiraO2TimeLimit", new(5f, 90f, 1f), 45f, TabGroup.GameSettings, false)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // Polus
        PolusReactorTimeLimit = FloatOptionItem.Create(22424, "PolusReactorTimeLimit", new(5f, 90f, 1f), 60f, TabGroup.GameSettings, false)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // The Airship
        AirshipReactorTimeLimit = FloatOptionItem.Create(22425, "AirshipReactorTimeLimit", new(5f, 90f, 1f), 90f, TabGroup.GameSettings, false)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // The Fungle
        FungleReactorTimeLimit = FloatOptionItem.Create(22426, "FungleReactorTimeLimit", new(5f, 90f, 1f), 60f, TabGroup.GameSettings, false)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        FungleMushroomMixupDuration = FloatOptionItem.Create(22427, "FungleMushroomMixupDuration", new(5f, 90f, 1f), 10f, TabGroup.GameSettings, false)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 72;

        // LightsOutSpecialSettings
        LightsOutSpecialSettings = BooleanOptionItem.Create(22500, "LightsOutSpecialSettings", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipViewingDeckLightsPanel = BooleanOptionItem.Create(22510, "DisableAirshipViewingDeckLightsPanel", false, TabGroup.GameSettings, false)
            .SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipGapRoomLightsPanel = BooleanOptionItem.Create(22511, "DisableAirshipGapRoomLightsPanel", false, TabGroup.GameSettings, false)
            .SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipCargoLightsPanel = BooleanOptionItem.Create(22512, "DisableAirshipCargoLightsPanel", false, TabGroup.GameSettings, false)
            .SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 73;


        //禁用相关设定
        TextOptionItem.Create(100026, "MenuTitle.Disable", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableShieldAnimations = BooleanOptionItem.Create(22601, "DisableShieldAnimations", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableKillAnimationOnGuess = BooleanOptionItem.Create(22602, "DisableKillAnimationOnGuess", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableCrackedGlass = BooleanOptionItem.Create(22603, "DisableCrackedGlass", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHidden(true)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableShapeshiftAnimations = BooleanOptionItem.Create(22604, "DisableShapeshiftAnimations", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableVanillaRoles = BooleanOptionItem.Create(22600, "DisableVanillaRoles", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        /*     DisableHiddenRoles = BooleanOptionItem.Create(22610, "DisableHiddenRoles", true, TabGroup.GameSettings, false)
                 .SetGameMode(CustomGameMode.Standard)
                 .SetColor(new Color32(255, 153, 153, byte.MaxValue));
             DisableSunnyboy = BooleanOptionItem.Create(22620, "DisableSunnyboy", true, TabGroup.GameSettings, false)
                 .SetGameMode(CustomGameMode.Standard)
                 .SetParent(DisableHiddenRoles)
                 .SetColor(new Color32(255, 153, 153, byte.MaxValue));
             DisableBard = BooleanOptionItem.Create(22630, "DisableBard", true, TabGroup.GameSettings, false)
                 .SetGameMode(CustomGameMode.Standard)
                 .SetParent(DisableHiddenRoles)
                 .SetColor(new Color32(255, 153, 153, byte.MaxValue)); */
        /*   DisableSaboteur = BooleanOptionItem.Create(22640, "DisableSaboteur", true, TabGroup.GameSettings, false)
               .SetGameMode(CustomGameMode.Standard)
               .SetParent(DisableHiddenRoles)
               .SetColor(new Color32(255, 153, 153, byte.MaxValue)); */
        DisableTaskWin = BooleanOptionItem.Create(22650, "DisableTaskWin", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 74;


        // 禁用任务

        DisableMeeting = BooleanOptionItem.Create(22700, "DisableMeeting", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableSabotage = BooleanOptionItem.Create(22800, "DisableSabotage", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableCloseDoor = BooleanOptionItem.Create(22810, "DisableCloseDoor", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 75;

        //禁用设备
        DisableDevices = BooleanOptionItem.Create(22900, "DisableDevices", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableSkeldDevices = BooleanOptionItem.Create(22905, "DisableSkeldDevices", false, TabGroup.GameSettings, false)
            .SetParent(DisableDevices);
        DisableSkeldAdmin = BooleanOptionItem.Create(22906, "DisableSkeldAdmin", false, TabGroup.GameSettings, false)
            .SetParent(DisableSkeldDevices);
        DisableSkeldCamera = BooleanOptionItem.Create(22907, "DisableSkeldCamera", false, TabGroup.GameSettings, false)
            .SetParent(DisableSkeldDevices);

        LoadingPercentage = 76;

        DisableMiraHQDevices = BooleanOptionItem.Create(22908, "DisableMiraHQDevices", false, TabGroup.GameSettings, false)
            .SetParent(DisableDevices);
        DisableMiraHQAdmin = BooleanOptionItem.Create(22909, "DisableMiraHQAdmin", false, TabGroup.GameSettings, false)
            .SetParent(DisableMiraHQDevices);
        DisableMiraHQDoorLog = BooleanOptionItem.Create(22910, "DisableMiraHQDoorLog", false, TabGroup.GameSettings, false)
            .SetParent(DisableMiraHQDevices);
        DisablePolusDevices = BooleanOptionItem.Create(22911, "DisablePolusDevices", false, TabGroup.GameSettings, false)
            .SetParent(DisableDevices);
        DisablePolusAdmin = BooleanOptionItem.Create(22912, "DisablePolusAdmin", false, TabGroup.GameSettings, false)
            .SetParent(DisablePolusDevices);
        DisablePolusCamera = BooleanOptionItem.Create(22913, "DisablePolusCamera", false, TabGroup.GameSettings, false)
            .SetParent(DisablePolusDevices);
        DisablePolusVital = BooleanOptionItem.Create(22914, "DisablePolusVital", false, TabGroup.GameSettings, false)
            .SetParent(DisablePolusDevices);
        DisableAirshipDevices = BooleanOptionItem.Create(22915, "DisableAirshipDevices", false, TabGroup.GameSettings, false)
            .SetParent(DisableDevices);
        DisableAirshipCockpitAdmin = BooleanOptionItem.Create(22916, "DisableAirshipCockpitAdmin", false, TabGroup.GameSettings, false)
            .SetParent(DisableAirshipDevices);

        LoadingPercentage = 77;

        DisableAirshipRecordsAdmin = BooleanOptionItem.Create(22917, "DisableAirshipRecordsAdmin", false, TabGroup.GameSettings, false)
            .SetParent(DisableAirshipDevices);
        DisableAirshipCamera = BooleanOptionItem.Create(22918, "DisableAirshipCamera", false, TabGroup.GameSettings, false)
            .SetParent(DisableAirshipDevices);
        DisableAirshipVital = BooleanOptionItem.Create(22919, "DisableAirshipVital", false, TabGroup.GameSettings, false)
            .SetParent(DisableAirshipDevices);
        DisableFungleDevices = BooleanOptionItem.Create(22925, "DisableFungleDevices", false, TabGroup.GameSettings, false)
            .SetParent(DisableDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableFungleCamera = BooleanOptionItem.Create(22926, "DisableFungleCamera", false, TabGroup.GameSettings, false)
            .SetParent(DisableFungleDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableFungleVital = BooleanOptionItem.Create(22927, "DisableFungleVital", false, TabGroup.GameSettings, false)
            .SetParent(DisableFungleDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevicesIgnoreConditions = BooleanOptionItem.Create(22920, "IgnoreConditions", false, TabGroup.GameSettings, false)
            .SetParent(DisableDevices);
        DisableDevicesIgnoreImpostors = BooleanOptionItem.Create(22921, "IgnoreImpostors", false, TabGroup.GameSettings, false)
            .SetParent(DisableDevicesIgnoreConditions);
        DisableDevicesIgnoreNeutrals = BooleanOptionItem.Create(22922, "IgnoreNeutrals", false, TabGroup.GameSettings, false)
            .SetParent(DisableDevicesIgnoreConditions);
        DisableDevicesIgnoreCrewmates = BooleanOptionItem.Create(22923, "IgnoreCrewmates", false, TabGroup.GameSettings, false)
            .SetParent(DisableDevicesIgnoreConditions);
        DisableDevicesIgnoreAfterAnyoneDied = BooleanOptionItem.Create(22924, "IgnoreAfterAnyoneDied", false, TabGroup.GameSettings, false)
            .SetParent(DisableDevicesIgnoreConditions)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 78;


        UsePets = BooleanOptionItem.Create(23850, "UsePets", false, TabGroup.TaskSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(60, 0, 255, byte.MaxValue));
        PetToAssignToEveryone = StringOptionItem.Create(23854, "PetToAssign", PetToAssign, 24, TabGroup.TaskSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(UsePets)
            .SetColor(new Color32(60, 0, 255, byte.MaxValue));

        UseVoteCancelling = BooleanOptionItem.Create(23852, "UseVoteCancelling", false, TabGroup.TaskSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(0, 65, 196, byte.MaxValue));


        //Disable Short Tasks
        DisableShortTasks = BooleanOptionItem.Create(23000, "DisableShortTasks", false, TabGroup.TaskSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableCleanVent = BooleanOptionItem.Create(23001, "DisableCleanVent", false, TabGroup.TaskSettings, false)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCalibrateDistributor = BooleanOptionItem.Create(23002, "DisableCalibrateDistributor", false, TabGroup.TaskSettings, false)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableChartCourse = BooleanOptionItem.Create(23003, "DisableChartCourse", false, TabGroup.TaskSettings, false)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 79;

        DisableStabilizeSteering = BooleanOptionItem.Create(23004, "DisableStabilizeSteering", false, TabGroup.TaskSettings, false)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCleanO2Filter = BooleanOptionItem.Create(23005, "DisableCleanO2Filter", false, TabGroup.TaskSettings, false)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableUnlockManifolds = BooleanOptionItem.Create(23006, "DisableUnlockManifolds", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePrimeShields = BooleanOptionItem.Create(23007, "DisablePrimeShields", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMeasureWeather = BooleanOptionItem.Create(23008, "DisableMeasureWeather", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 80;

        DisableBuyBeverage = BooleanOptionItem.Create(23009, "DisableBuyBeverage", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableAssembleArtifact = BooleanOptionItem.Create(23010, "DisableAssembleArtifact", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableSortSamples = BooleanOptionItem.Create(23011, "DisableSortSamples", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableProcessData = BooleanOptionItem.Create(23012, "DisableProcessData", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRunDiagnostics = BooleanOptionItem.Create(23013, "DisableRunDiagnostics", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 81;

        DisableRepairDrill = BooleanOptionItem.Create(23014, "DisableRepairDrill", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableAlignTelescope = BooleanOptionItem.Create(23015, "DisableAlignTelescope", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRecordTemperature = BooleanOptionItem.Create(23016, "DisableRecordTemperature", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFillCanisters = BooleanOptionItem.Create(23017, "DisableFillCanisters", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 82;

        DisableMonitorTree = BooleanOptionItem.Create(23018, "DisableMonitorTree", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableStoreArtifacts = BooleanOptionItem.Create(23019, "DisableStoreArtifacts", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePutAwayPistols = BooleanOptionItem.Create(23020, "DisablePutAwayPistols", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePutAwayRifles = BooleanOptionItem.Create(23021, "DisablePutAwayRifles", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMakeBurger = BooleanOptionItem.Create(23022, "DisableMakeBurger", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 83;

        DisableCleanToilet = BooleanOptionItem.Create(23023, "DisableCleanToilet", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDecontaminate = BooleanOptionItem.Create(23024, "DisableDecontaminate", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableSortRecords = BooleanOptionItem.Create(23025, "DisableSortRecords", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFixShower = BooleanOptionItem.Create(23026, "DisableFixShower", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePickUpTowels = BooleanOptionItem.Create(23027, "DisablePickUpTowels", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePolishRuby = BooleanOptionItem.Create(23028, "DisablePolishRuby", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDressMannequin = BooleanOptionItem.Create(23029, "DisableDressMannequin", false, TabGroup.TaskSettings, false)
        .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRoastMarshmallow = BooleanOptionItem.Create(23030, "DisableRoastMarshmallow", false, TabGroup.TaskSettings, false)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCollectSamples = BooleanOptionItem.Create(23031, "DisableCollectSamples", false, TabGroup.TaskSettings, false)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableReplaceParts = BooleanOptionItem.Create(23032, "DisableReplaceParts", false, TabGroup.TaskSettings, false)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 84;


        //Disable Common Tasks
        DisableCommonTasks = BooleanOptionItem.Create(23100, "DisableCommonTasks", false, TabGroup.TaskSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableSwipeCard = BooleanOptionItem.Create(23101, "DisableSwipeCardTask", false, TabGroup.TaskSettings, false)
        .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFixWiring = BooleanOptionItem.Create(23102, "DisableFixWiring", false, TabGroup.TaskSettings, false)
        .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableEnterIdCode = BooleanOptionItem.Create(23103, "DisableEnterIdCode", false, TabGroup.TaskSettings, false)
        .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableInsertKeys = BooleanOptionItem.Create(23104, "DisableInsertKeys", false, TabGroup.TaskSettings, false)
        .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableScanBoardingPass = BooleanOptionItem.Create(23105, "DisableScanBoardingPass", false, TabGroup.TaskSettings, false)
        .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCollectVegetables = BooleanOptionItem.Create(23106, "DisableCollectVegetables", false, TabGroup.TaskSettings, false)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMineOres = BooleanOptionItem.Create(23107, "DisableMineOres", false, TabGroup.TaskSettings, false)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableExtractFuel = BooleanOptionItem.Create(23108, "DisableExtractFuel", false, TabGroup.TaskSettings, false)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCatchFish = BooleanOptionItem.Create(23109, "DisableCatchFish", false, TabGroup.TaskSettings, false)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePolishGem = BooleanOptionItem.Create(23110, "DisablePolishGem", false, TabGroup.TaskSettings, false)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableHelpCritter = BooleanOptionItem.Create(23111, "DisableHelpCritter", false, TabGroup.TaskSettings, false)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableHoistSupplies = BooleanOptionItem.Create(23112, "DisableHoistSupplies", false, TabGroup.TaskSettings, false)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 85;

        //Disable Long Tasks
        DisableLongTasks = BooleanOptionItem.Create(23150, "DisableLongTasks", false, TabGroup.TaskSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableSubmitScan = BooleanOptionItem.Create(23151, "DisableSubmitScanTask", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableUnlockSafe = BooleanOptionItem.Create(23152, "DisableUnlockSafeTask", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableStartReactor = BooleanOptionItem.Create(23153, "DisableStartReactorTask", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableResetBreaker = BooleanOptionItem.Create(23154, "DisableResetBreakerTask", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 86;

        DisableAlignEngineOutput = BooleanOptionItem.Create(23155, "DisableAlignEngineOutput", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableInspectSample = BooleanOptionItem.Create(23156, "DisableInspectSample", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableEmptyChute = BooleanOptionItem.Create(23157, "DisableEmptyChute", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableClearAsteroids = BooleanOptionItem.Create(23158, "DisableClearAsteroids", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableWaterPlants = BooleanOptionItem.Create(23159, "DisableWaterPlants", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableOpenWaterways = BooleanOptionItem.Create(23160, "DisableOpenWaterways", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 87;

        DisableReplaceWaterJug = BooleanOptionItem.Create(23161, "DisableReplaceWaterJug", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRebootWifi = BooleanOptionItem.Create(23162, "DisableRebootWifi", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevelopPhotos = BooleanOptionItem.Create(23163, "DisableDevelopPhotos", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRewindTapes = BooleanOptionItem.Create(23164, "DisableRewindTapes", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableStartFans = BooleanOptionItem.Create(23165, "DisableStartFans", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFixAntenna = BooleanOptionItem.Create(23166, "DisableFixAntenna", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableBuildSandcastle = BooleanOptionItem.Create(23167, "DisableBuildSandcastle", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 88;

        DisableCrankGenerator = BooleanOptionItem.Create(23168, "DisableCrankGenerator", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMonitorMushroom = BooleanOptionItem.Create(23169, "DisableMonitorMushroom", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePlayVideoGame = BooleanOptionItem.Create(23170, "DisablePlayVideoGame", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFindSignal = BooleanOptionItem.Create(23171, "DisableFindSignal", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableThrowFisbee = BooleanOptionItem.Create(23172, "DisableThrowFisbee", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableLiftWeights = BooleanOptionItem.Create(23173, "DisableLiftWeights", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCollectShells = BooleanOptionItem.Create(23174, "DisableCollectShells", false, TabGroup.TaskSettings, false)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 89;


        //Disable Divert Power, Weather Nodes and etc. situational Tasks
        DisableOtherTasks = BooleanOptionItem.Create(23200, "DisableOtherTasks", false, TabGroup.TaskSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableUploadData = BooleanOptionItem.Create(23205, "DisableUploadDataTask", false, TabGroup.TaskSettings, false)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableEmptyGarbage = BooleanOptionItem.Create(23206, "DisableEmptyGarbage", false, TabGroup.TaskSettings, false)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFuelEngines = BooleanOptionItem.Create(23207, "DisableFuelEngines", false, TabGroup.TaskSettings, false)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDivertPower = BooleanOptionItem.Create(23208, "DisableDivertPower", false, TabGroup.TaskSettings, false)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableActivateWeatherNodes = BooleanOptionItem.Create(23209, "DisableActivateWeatherNodes", false, TabGroup.TaskSettings, false)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 90;
        MainLoadingText = "Building Guesser Mode settings";

        TextOptionItem.Create(100022, "MenuTitle.Guessers", TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.yellow)
            .SetHeader(true);
        GuesserMode = BooleanOptionItem.Create(19700, "GuesserMode", false, TabGroup.TaskSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.yellow)
            .SetHeader(true);
        CrewmatesCanGuess = BooleanOptionItem.Create(19710, "CrewmatesCanGuess", false, TabGroup.TaskSettings, false)
            .SetParent(GuesserMode);
        ImpostorsCanGuess = BooleanOptionItem.Create(19711, "ImpostorsCanGuess", false, TabGroup.TaskSettings, false)
            .SetParent(GuesserMode);

        LoadingPercentage = 91;

        NeutralKillersCanGuess = BooleanOptionItem.Create(19712, "NeutralKillersCanGuess", false, TabGroup.TaskSettings, false)
            .SetParent(GuesserMode);
        PassiveNeutralsCanGuess = BooleanOptionItem.Create(19713, "PassiveNeutralsCanGuess", false, TabGroup.TaskSettings, false)
            .SetParent(GuesserMode);
        CanGuessAddons = BooleanOptionItem.Create(19714, "CanGuessAddons", true, TabGroup.TaskSettings, false)
            .SetParent(GuesserMode);
        CrewCanGuessCrew = BooleanOptionItem.Create(19715, "CrewCanGuessCrew", true, TabGroup.TaskSettings, false)
            .SetParent(GuesserMode);
        ImpCanGuessImp = BooleanOptionItem.Create(19716, "ImpCanGuessImp", true, TabGroup.TaskSettings, false)
            .SetParent(GuesserMode);
        HideGuesserCommands = BooleanOptionItem.Create(19717, "GuesserTryHideMsg", true, TabGroup.TaskSettings, false)
            .SetParent(GuesserMode)
            .SetColor(Color.green);

        LoadingPercentage = 92;


        TextOptionItem.Create(100050, "MenuTitle.GuesserModeRoles", TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.yellow)
            .SetHeader(true);
        SetupAdtRoleOptions(14500, CustomRoles.Onbound, canSetNum: true, tab: TabGroup.TaskSettings);
        ImpCanBeOnbound = BooleanOptionItem.Create(14510, "ImpCanBeOnbound", true, TabGroup.TaskSettings, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Onbound]);
        CrewCanBeOnbound = BooleanOptionItem.Create(14511, "CrewCanBeOnbound", true, TabGroup.TaskSettings, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Onbound]);
        NeutralCanBeOnbound = BooleanOptionItem.Create(14512, "NeutralCanBeOnbound", true, TabGroup.TaskSettings, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Onbound]);


        LoadingPercentage = 93;
        MainLoadingText = "Building game settings";


        //会议相关设定
        TextOptionItem.Create(100027, "MenuTitle.Meeting", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        // 会议限制次数
        SyncButtonMode = BooleanOptionItem.Create(23300, "SyncButtonMode", false, TabGroup.GameSettings, false)
            .SetHeader(true)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        SyncedButtonCount = IntegerOptionItem.Create(23310, "SyncedButtonCount", new(0, 100, 1), 10, TabGroup.GameSettings, false)
            .SetParent(SyncButtonMode)
            .SetValueFormat(OptionFormat.Times)
            .SetGameMode(CustomGameMode.Standard);

        // 全员存活时的会议时间
        AllAliveMeeting = BooleanOptionItem.Create(23400, "AllAliveMeeting", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));
        AllAliveMeetingTime = FloatOptionItem.Create(23410, "AllAliveMeetingTime", new(1f, 300f, 1f), 10f, TabGroup.GameSettings, false)
            .SetParent(AllAliveMeeting)
            .SetValueFormat(OptionFormat.Seconds);

        LoadingPercentage = 94;


        // 附加紧急会议
        AdditionalEmergencyCooldown = BooleanOptionItem.Create(23500, "AdditionalEmergencyCooldown", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));
        AdditionalEmergencyCooldownThreshold = IntegerOptionItem.Create(23510, "AdditionalEmergencyCooldownThreshold", new(1, 15, 1), 1, TabGroup.GameSettings, false)
            .SetParent(AdditionalEmergencyCooldown)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);
        AdditionalEmergencyCooldownTime = FloatOptionItem.Create(23511, "AdditionalEmergencyCooldownTime", new(1f, 60f, 1f), 1f, TabGroup.GameSettings, false)
                .SetParent(AdditionalEmergencyCooldown)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Seconds);

        LoadingPercentage = 95;

        // 投票相关设定
        VoteMode = BooleanOptionItem.Create(23600, "VoteMode", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVote = StringOptionItem.Create(23610, "WhenSkipVote", voteModes[0..3], 0, TabGroup.GameSettings, false)
            .SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreFirstMeeting = BooleanOptionItem.Create(23611, "WhenSkipVoteIgnoreFirstMeeting", false, TabGroup.GameSettings, false)
            .SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreNoDeadBody = BooleanOptionItem.Create(23612, "WhenSkipVoteIgnoreNoDeadBody", false, TabGroup.GameSettings, false)
            .SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreEmergency = BooleanOptionItem.Create(23613, "WhenSkipVoteIgnoreEmergency", false, TabGroup.GameSettings, false)
            .SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenNonVote = StringOptionItem.Create(23700, "WhenNonVote", voteModes, 0, TabGroup.GameSettings, false)
            .SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);
        WhenTie = StringOptionItem.Create(23750, "WhenTie", tieModes, 0, TabGroup.GameSettings, false)
            .SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 96;



        // 其它设定
        TextOptionItem.Create(100028, "MenuTitle.Other", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        // 梯子摔死
        LadderDeath = BooleanOptionItem.Create(23800, "LadderDeath", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));
        LadderDeathChance = StringOptionItem.Create(23810, "LadderDeathChance", rates[1..], 0, TabGroup.GameSettings, false)
            .SetParent(LadderDeath);

        LoadingPercentage = 97;


        // 修正首刀时间
        FixFirstKillCooldown = BooleanOptionItem.Create(23900, "FixFirstKillCooldown", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
           .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        StartingKillCooldown = FloatOptionItem.Create(23950, "StartingKillCooldown", new(1, 60, 1), 18, TabGroup.GameSettings, false)
           .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        // 首刀保护
        ShieldPersonDiedFirst = BooleanOptionItem.Create(24000, "ShieldPersonDiedFirst", false, TabGroup.GameSettings, false)
           .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        LoadingPercentage = 98;


        // 杀戮闪烁持续
        KillFlashDuration = FloatOptionItem.Create(24100, "KillFlashDuration", new(0.1f, 0.45f, 0.05f), 0.3f, TabGroup.GameSettings, false)
           .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        // 幽灵相关设定
        TextOptionItem.Create(100029, "MenuTitle.Ghost", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));

        LoadingPercentage = 99;


        // 幽灵设置
        //GhostIgnoreTasks = BooleanOptionItem.Create(24200, "GhostIgnoreTasks", false, TabGroup.GameSettings, false)
        //    .SetGameMode(CustomGameMode.Standard)
        //    .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeOtherRoles = BooleanOptionItem.Create(24300, "GhostCanSeeOtherRoles", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeOtherVotes = BooleanOptionItem.Create(24400, "GhostCanSeeOtherVotes", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
             .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeDeathReason = BooleanOptionItem.Create(24500, "GhostCanSeeDeathReason", true, TabGroup.GameSettings, false)
            .SetHeader(true)
            .SetGameMode(CustomGameMode.Standard)
           .SetColor(new Color32(217, 218, 255, byte.MaxValue));

        LoadingPercentage = 100;

        #endregion

        OptionSaver.Load();

        IsLoaded = true;
    }

    public static void SetupRoleOptions(int id, TabGroup tab, CustomRoles role, CustomGameMode customGameMode = CustomGameMode.Standard, bool zeroOne = false)
    {
        var spawnOption = StringOptionItem.Create(id, role.ToString(), zeroOne ? ratesZeroOne : ratesToggle, 0, tab, false).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;
        var countOption = IntegerOptionItem.Create(id + 1, "Maximum", new(1, 15, 1), 1, tab, false)
        .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Players)
            .SetGameMode(customGameMode);

        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
    }
    private static void SetupLoversRoleOptionsToggle(int id, CustomGameMode customGameMode = CustomGameMode.Standard)
    {
        var role = CustomRoles.Lovers;
        var spawnOption = StringOptionItem.Create(id, role.ToString(), ratesZeroOne, 0, TabGroup.Addons, false).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        LoverSpawnChances = IntegerOptionItem.Create(id + 2, "LoverSpawnChances", new(0, 100, 5), 50, TabGroup.Addons, false)
        .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Percent)
            .SetGameMode(customGameMode);

        LoverKnowRoles = BooleanOptionItem.Create(id + 4, "LoverKnowRoles", true, TabGroup.Addons, false)
        .SetParent(spawnOption)
            .SetGameMode(customGameMode);

        LoverSuicide = BooleanOptionItem.Create(id + 3, "LoverSuicide", true, TabGroup.Addons, false)
        .SetParent(spawnOption)
            .SetGameMode(customGameMode);

        ImpCanBeInLove = BooleanOptionItem.Create(id + 5, "ImpCanBeInLove", true, TabGroup.Addons, false)
        .SetParent(spawnOption)
            .SetGameMode(customGameMode);

        CrewCanBeInLove = BooleanOptionItem.Create(id + 6, "CrewCanBeInLove", true, TabGroup.Addons, false)
        .SetParent(spawnOption)
            .SetGameMode(customGameMode);

        NeutralCanBeInLove = BooleanOptionItem.Create(id + 7, "NeutralCanBeInLove", true, TabGroup.Addons, false)
        .SetParent(spawnOption)
            .SetGameMode(customGameMode);


        var countOption = IntegerOptionItem.Create(id + 1, "NumberOfLovers", new(2, 2, 1), 2, TabGroup.Addons, false)
        .SetParent(spawnOption)
            .SetHidden(true)
            .SetGameMode(customGameMode);

        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
    }

    public static void SetupAdtRoleOptions(int id, CustomRoles role, CustomGameMode customGameMode = CustomGameMode.Standard, bool canSetNum = false, TabGroup tab = TabGroup.Addons, bool canSetChance = true)
    {
        var spawnOption = StringOptionItem.Create(id, role.ToString(), ratesZeroOne, 0, tab, false).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        var countOption = IntegerOptionItem.Create(id + 1, "Maximum", new(1, canSetNum ? 10 : 1, 1), 1, tab, false)
        .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Players)
            .SetHidden(!canSetNum)
            .SetGameMode(customGameMode);

        var spawnRateOption = IntegerOptionItem.Create(id + 2, "AdditionRolesSpawnRate", new(0, 100, 5), canSetChance ? 65 : 100, tab, false)
        .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Percent)
            .SetHidden(!canSetChance)
            .SetGameMode(customGameMode) as IntegerOptionItem;

        CustomAdtRoleSpawnRate.Add(role, spawnRateOption);
        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
    }

    public static void SetupSingleRoleOptions(int id, TabGroup tab, CustomRoles role, int count, CustomGameMode customGameMode = CustomGameMode.Standard, bool zeroOne = false)
    {
        var spawnOption = StringOptionItem.Create(id, role.ToString(), zeroOne ? ratesZeroOne : ratesToggle, 0, tab, false).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;
        // 初期値,最大値,最小値が同じで、stepが0のどうやっても変えることができない個数オプション
        var countOption = IntegerOptionItem.Create(id + 1, "Maximum", new(count, count, count), count, tab, false)
        .SetParent(spawnOption)
            .SetGameMode(customGameMode);

        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
    }
    public class OverrideTasksData
    {
        public static Dictionary<CustomRoles, OverrideTasksData> AllData = [];
        public CustomRoles Role { get; private set; }
        public int IdStart { get; private set; }
        public OptionItem doOverride;
        public OptionItem assignCommonTasks;
        public OptionItem numLongTasks;
        public OptionItem numShortTasks;

        public OverrideTasksData(int idStart, TabGroup tab, CustomRoles role)
        {
            IdStart = idStart;
            Role = role;
            Dictionary<string, string> replacementDic = new() { { "%role%", Utils.ColorString(Utils.GetRoleColor(role), Utils.GetRoleName(role)) } };
            doOverride = BooleanOptionItem.Create(idStart++, "doOverride", false, tab, false)
        .SetParent(CustomRoleSpawnChances[role])
                .SetValueFormat(OptionFormat.None);
            doOverride.ReplacementDictionary = replacementDic;
            assignCommonTasks = BooleanOptionItem.Create(idStart++, "assignCommonTasks", true, tab, false)
        .SetParent(doOverride)
                .SetValueFormat(OptionFormat.None);
            assignCommonTasks.ReplacementDictionary = replacementDic;
            numLongTasks = IntegerOptionItem.Create(idStart++, "roleLongTasksNum", new(0, 99, 1), 3, tab, false)
        .SetParent(doOverride)
                .SetValueFormat(OptionFormat.Pieces);
            numLongTasks.ReplacementDictionary = replacementDic;
            numShortTasks = IntegerOptionItem.Create(idStart++, "roleShortTasksNum", new(0, 99, 1), 3, tab, false)
        .SetParent(doOverride)
                .SetValueFormat(OptionFormat.Pieces);
            numShortTasks.ReplacementDictionary = replacementDic;

            if (!AllData.ContainsKey(role)) AllData.Add(role, this);
            else Logger.Warn("重複したCustomRolesを対象とするOverrideTasksDataが作成されました", "OverrideTasksData");
        }
        public static OverrideTasksData Create(int idStart, TabGroup tab, CustomRoles role)
        {
            return new OverrideTasksData(idStart, tab, role);
        }
    }
}
