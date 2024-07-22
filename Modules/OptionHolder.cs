using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EHR.AddOns;
using EHR.Modules;
using HarmonyLib;
using UnityEngine;

// ReSharper disable AccessToModifiedClosure
// ReSharper disable InconsistentNaming

namespace EHR;

[Flags]
public enum CustomGameMode
{
    Standard = 0x01,
    SoloKombat = 0x02,
    FFA = 0x03,
    MoveAndStop = 0x04,
    HotPotato = 0x05,
    HideAndSeek = 0x06,
    Speedrun = 0x07,
    All = int.MaxValue
}

[HarmonyPatch]
public static class Options
{
    public enum GameStateInfo
    {
        ImpCount,
        MadmateCount,
        ConvertedCount,
        NNKCount,
        NKCount,
        CrewCount,
        RomanticState,
        LoversState,
        Tasks
    }

    public static Dictionary<TabGroup, OptionItem[]> GroupedOptions = [];
    public static Dictionary<AddonTypes, List<CustomRoles>> GroupedAddons = [];

    public static OptionItem GameMode;

    private static readonly string[] GameModes =
    [
        "Standard",
        "SoloKombat",
        "FFA",
        "MoveAndStop",
        "HotPotato",
        "HideAndSeek",
        "Speedrun"
    ];

    private static Dictionary<CustomRoles, int> roleCounts;
    private static Dictionary<CustomRoles, float> roleSpawnChances;
    public static Dictionary<CustomRoles, OptionItem> CustomRoleCounts;
    public static Dictionary<CustomRoles, StringOptionItem> CustomRoleSpawnChances;
    public static Dictionary<CustomRoles, IntegerOptionItem> CustomAdtRoleSpawnRate;

    public static readonly string[] Rates =
    [
        "Rate0",
        "Rate5",
        "Rate10",
        "Rate15",
        "Rate20",
        "Rate25",
        "Rate30",
        "Rate35",
        "Rate40",
        "Rate45",
        "Rate50",
        "Rate55",
        "Rate60",
        "Rate65",
        "Rate70",
        "Rate75",
        "Rate80",
        "Rate85",
        "Rate90",
        "Rate95",
        "Rate100"
    ];

    public static readonly string[] RatesZeroOne =
    [
        "RoleOff", /*"Rate10", "Rate20", "Rate30", "Rate40", "Rate50",
        "Rate60", "Rate70", "Rate80", "Rate90", */
        "RoleRate"
    ];

    private static readonly string[] CheatResponsesName =
    [
        "Ban",
        "Kick",
        "NoticeMe",
        "NoticeEveryone",
        "OnlyCancel"
    ];

    private static readonly string[] ConfirmEjectionsMode =
    [
        "ConfirmEjections.None",
        "ConfirmEjections.Team",
        "ConfirmEjections.Role"
    ];

    private static readonly string[] CamouflageMode =
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

    public static float DefaultKillCooldown = Main.NormalOptions?.KillCooldown ?? 25;

    public static Dictionary<GameStateInfo, OptionItem> GameStateSettings = [];
    public static OptionItem MinPlayersForGameStateCommand;

    public static OptionItem DisableMeeting;
    public static OptionItem DisableCloseDoor;
    public static OptionItem DisableSabotage;
    public static OptionItem DisableTaskWin;

    public static OptionItem KillFlashDuration;
    public static OptionItem EnableKillerLeftCommand;

    public static OptionItem SeeEjectedRolesInMeeting;

    public static OptionItem DisableShieldAnimations;
    public static OptionItem DisableShapeshiftAnimations;
    public static OptionItem DisableAllShapeshiftAnimations;
    public static OptionItem DisableKillAnimationOnGuess;
    public static OptionItem DisableVanillaRoles;
    public static OptionItem SabotageCooldownControl;
    public static OptionItem SabotageCooldown;
    public static OptionItem CEMode;
    public static OptionItem ShowImpRemainOnEject;
    public static OptionItem ShowNKRemainOnEject;
    public static OptionItem ShowTeamNextToRoleNameOnEject;
    public static OptionItem CheatResponses;
    public static OptionItem LowLoadMode;
    public static OptionItem DeepLowLoad;
    public static OptionItem DisableVoteBan;


    // Detailed Ejections //
    public static OptionItem ConfirmEgoistOnEject;
    public static OptionItem ConfirmLoversOnEject;

    public static OptionItem UniqueNeutralRevealScreen;


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

    public static OptionItem ImpCanKillMadmate;
    public static OptionItem MadmateCanKillImp;
    public static OptionItem JackalCanKillSidekick;
    public static OptionItem SidekickCanKillJackal;
    public static OptionItem SidekickCanKillSidekick;

    public static OptionItem EGCanGuessImp;
    public static OptionItem EGCanGuessAdt;
    public static OptionItem EGCanGuessTime;
    public static OptionItem EGTryHideMsg;
    public static OptionItem ScavengerKillCooldown;
    public static OptionItem ScavengerKillDuration;
    public static OptionItem GGCanGuessCrew;
    public static OptionItem GGCanGuessAdt;
    public static OptionItem GGCanGuessTime;
    public static OptionItem GGTryHideMsg;
    public static OptionItem LuckeyProbability;
    public static OptionItem LuckyProbability;
    public static OptionItem VindicatorAdditionalVote;
    public static OptionItem VindicatorHideVote;
    public static OptionItem DoctorTaskCompletedBatteryCharge;
    public static OptionItem TrapperBlockMoveTime;
    public static OptionItem DetectiveCanknowKiller;
    public static OptionItem TransporterTeleportMax;
    public static OptionItem CanTerroristSuicideWin;
    public static OptionItem InnocentCanWinByImp;
    public static OptionItem BaitNotification;
    public static OptionItem DoctorVisibleToEveryone;
    public static OptionItem ArsonistDouseTime;
    public static OptionItem ArsonistCooldown;
    public static OptionItem ArsonistKeepsGameGoing;
    public static OptionItem ArsonistCanIgniteAnytime;
    public static OptionItem ArsonistMinPlayersToIgnite;
    public static OptionItem ArsonistMaxPlayersToIgnite;
    public static OptionItem LegacyMafia;
    public static OptionItem NotifyGodAlive;
    public static OptionItem MarioVentNumWin;
    public static OptionItem MarioVentCD;
    public static OptionItem VeteranSkillCooldown;
    public static OptionItem VeteranSkillDuration;
    public static OptionItem TimeMasterSkillCooldown;
    public static OptionItem TimeMasterSkillDuration;
    public static OptionItem TimeMasterMaxUses;
    public static OptionItem VeteranSkillMaxOfUseage;
    public static OptionItem BodyguardProtectRadius;
    public static OptionItem BodyguardKillsKiller;
    public static OptionItem WitnessCD;
    public static OptionItem WitnessTime;
    public static OptionItem WitnessUsePet;
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
    public static OptionItem MafiaCanKillNum;
    public static OptionItem BomberRadius;
    public static OptionItem BomberCanKill;
    public static OptionItem BomberKillCD;
    public static OptionItem BombCooldown;
    public static OptionItem ImpostorsSurviveBombs;
    public static OptionItem BomberDiesInExplosion;
    public static OptionItem NukerChance;
    public static OptionItem NukeRadius;
    public static OptionItem NukeCooldown;
    public static OptionItem ReportBaitAtAllCost;

    public static OptionItem GuesserDoesntDieOnMisguess;

    public static OptionItem RefugeeKillCD;

    public static OptionItem SkeldChance;
    public static OptionItem MiraChance;
    public static OptionItem PolusChance;
    public static OptionItem DleksChance;
    public static OptionItem AirshipChance;
    public static OptionItem FungleChance;

    public static OptionItem UnderdogKillCooldown;
    public static OptionItem UnderdogMaximumPlayersNeededToKill;
    public static OptionItem UnderdogKillCooldownWithMorePlayersAlive;

    public static OptionItem GodfatherCancelVote;

    public static OptionItem GuardSpellTimes;
    public static OptionItem CapitalismSkillCooldown;
    public static OptionItem CapitalismKillCooldown;
    public static OptionItem GrenadierSkillCooldown;
    public static OptionItem GrenadierSkillDuration;
    public static OptionItem GrenadierCauseVision;
    public static OptionItem GrenadierCanAffectNeutral;
    public static OptionItem GrenadierSkillMaxOfUseage;
    public static OptionItem LighterVisionNormal;
    public static OptionItem LighterVisionOnLightsOut;
    public static OptionItem LighterSkillCooldown;
    public static OptionItem LighterSkillDuration;
    public static OptionItem LighterSkillMaxOfUseage;
    public static OptionItem SecurityGuardSkillCooldown;
    public static OptionItem SecurityGuardSkillDuration;
    public static OptionItem SecurityGuardSkillMaxOfUseage;
    public static OptionItem EscapeeSSCD;
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
    public static OptionItem ObliviousBaitImmune;
    public static OptionItem UnluckyTaskSuicideChance;
    public static OptionItem UnluckyKillSuicideChance;
    public static OptionItem UnluckyVentSuicideChance;
    public static OptionItem UnluckyReportSuicideChance;
    public static OptionItem UnluckySabotageSuicideChance;
    public static OptionItem AsthmaticMinRedTime;
    public static OptionItem AsthmaticMaxRedTime;
    public static OptionItem AsthmaticMinGreenTime;
    public static OptionItem AsthmaticMaxGreenTime;
    public static OptionItem DiscoChangeInterval;

    public static OptionItem TruantWaitingTime;

    // RASCAL //
    public static OptionItem RascalAppearAsMadmate;

    // Mare Add-on
    public static OptionItem MareKillCD;
    public static OptionItem MareKillCDNormally;
    public static OptionItem MareHasIncreasedSpeed;
    public static OptionItem MareSpeedDuringLightsOut;

    public static OptionItem AutoPlayAgain;
    public static OptionItem AutoPlayAgainCountdown;
    public static OptionItem AutoStartTimer;

    public static OptionItem InhibitorCD;
    public static OptionItem InhibitorCDAfterMeetings;
    public static OptionItem SaboteurCD;
    public static OptionItem SaboteurCDAfterMeetings;
    public static OptionItem PhantomCanVent;

    public static OptionItem PhantomSnatchesWin;

    public static OptionItem DiseasedCDOpt;
    public static OptionItem DiseasedCDReset;

    public static OptionItem AntidoteCDOpt;
    public static OptionItem AntidoteCDReset;

    public static OptionItem BaitDelayMin;
    public static OptionItem BaitDelayMax;
    public static OptionItem BaitDelayNotify;
    public static OptionItem TorchVision;
    public static OptionItem TorchAffectedByLights;
    public static OptionItem TasklessCrewCanBeLazy;
    public static OptionItem TaskBasedCrewCanBeLazy;
    public static OptionItem DovesOfNeaceCooldown;
    public static OptionItem DovesOfNeaceMaxOfUseage;
    public static OptionItem killAttacker;
    public static OptionItem MimicCanSeeDeadRoles;
    public static OptionItem ResetDoorsEveryTurns;
    public static OptionItem DoorsResetMode;
    public static OptionItem ChangeDecontaminationTime;
    public static OptionItem DecontaminationTimeOnMiraHQ;
    public static OptionItem DecontaminationTimeOnPolus;

    public static OptionItem MafiaShapeshiftCD;
    public static OptionItem MafiaShapeshiftDur;

    public static OptionItem DisableTaskWinIfAllCrewsAreDead;
    public static OptionItem DisableTaskWinIfAllCrewsAreConverted;

    // Task Management
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

    public static OptionItem BusyLongTasks;
    public static OptionItem BusyShortTasks;

    // Disable Devices
    public static OptionItem DisableDevices;
    private static OptionItem DisableSkeldDevices;
    public static OptionItem DisableSkeldAdmin;
    public static OptionItem DisableSkeldCamera;
    private static OptionItem DisableMiraHQDevices;
    public static OptionItem DisableMiraHQAdmin;
    public static OptionItem DisableMiraHQDoorLog;
    private static OptionItem DisablePolusDevices;
    public static OptionItem DisablePolusAdmin;
    public static OptionItem DisablePolusCamera;
    public static OptionItem DisablePolusVital;
    private static OptionItem DisableAirshipDevices;
    public static OptionItem DisableAirshipCockpitAdmin;
    public static OptionItem DisableAirshipRecordsAdmin;
    public static OptionItem DisableAirshipCamera;
    public static OptionItem DisableAirshipVital;
    private static OptionItem DisableFungleDevices;
    public static OptionItem DisableFungleCamera;
    public static OptionItem DisableFungleVital;
    private static OptionItem DisableDevicesIgnoreConditions;
    public static OptionItem DisableDevicesIgnoreImpostors;
    public static OptionItem DisableDevicesIgnoreNeutrals;
    public static OptionItem DisableDevicesIgnoreCrewmates;
    public static OptionItem DisableDevicesIgnoreAfterAnyoneDied;

    // Maps
    public static OptionItem RandomMapsMode;
    public static OptionItem RandomSpawn;
    public static OptionItem AirshipAdditionalSpawn;
    public static OptionItem AirshipVariableElectrical;
    public static OptionItem DisableAirshipMovingPlatform;
    public static OptionItem DisableSporeTriggerOnFungle;
    private static OptionItem DisableZiplineOnFungle;
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
    private static OptionItem LightsOutSpecialSettings;
    public static OptionItem DisableAirshipViewingDeckLightsPanel;
    public static OptionItem DisableAirshipGapRoomLightsPanel;
    public static OptionItem DisableAirshipCargoLightsPanel;

    // Guesser Mode
    public static OptionItem GuesserMode;
    public static OptionItem CrewmatesCanGuess;
    public static OptionItem ImpostorsCanGuess;
    public static OptionItem NeutralKillersCanGuess;
    public static OptionItem PassiveNeutralsCanGuess;
    public static OptionItem HideGuesserCommands;
    public static OptionItem CanGuessAddons;
    public static OptionItem ImpCanGuessImp;
    public static OptionItem CrewCanGuessCrew;

    public static OptionItem EveryoneCanVent;
    public static OptionItem OverrideScientistBasedRoles;
    public static OptionItem WhackAMole;

    public static OptionItem SpawnAdditionalRefugeeOnImpsDead;
    public static OptionItem SpawnAdditionalRefugeeWhenNKAlive;
    public static OptionItem SpawnAdditionalRefugeeMinAlivePlayers;

    public static OptionItem AprilFoolsMode;


    // Voting Modes
    public static OptionItem VoteMode;
    private static OptionItem WhenSkipVote;
    public static OptionItem WhenSkipVoteIgnoreFirstMeeting;
    public static OptionItem WhenSkipVoteIgnoreNoDeadBody;
    public static OptionItem WhenSkipVoteIgnoreEmergency;
    private static OptionItem WhenNonVote;
    public static OptionItem WhenTie;

    private static readonly string[] VoteModes =
    [
        "Default",
        "Suicide",
        "SelfVote",
        "Skip"
    ];

    private static readonly string[] TieModes =
    [
        "TieMode.Default",
        "TieMode.All",
        "TieMode.Random"
    ];

    public static readonly string[] MadmateSpawnModeStrings =
    [
        "MadmateSpawnMode.Assign",
        "MadmateSpawnMode.FirstKill",
        "MadmateSpawnMode.SelfVote"
    ];

    public static readonly string[] MadmateCountModeStrings =
    [
        "MadmateCountMode.None",
        "MadmateCountMode.Imp",
        "MadmateCountMode.Crew"
    ];

    public static readonly string[] SidekickCountMode =
    [
        "SidekickCountMode.Jackal",
        "SidekickCountMode.None",
        "SidekickCountMode.Original"
    ];

    public static OptionItem SyncButtonMode;
    public static OptionItem SyncedButtonCount;
    public static int UsedButtonCount;

    public static OptionItem AllAliveMeeting;
    public static OptionItem AllAliveMeetingTime;

    public static OptionItem AdditionalEmergencyCooldown;
    public static OptionItem AdditionalEmergencyCooldownThreshold;
    public static OptionItem AdditionalEmergencyCooldownTime;

    public static OptionItem LadderDeath;
    public static OptionItem LadderDeathChance;

    public static OptionItem FixFirstKillCooldown;
    public static OptionItem StartingKillCooldown;
    public static OptionItem ShieldPersonDiedFirst;
    public static OptionItem GhostCanSeeOtherRoles;
    public static OptionItem GhostCanSeeOtherVotes;

    public static OptionItem GhostCanSeeDeathReason;

    public static OptionItem KPDCamouflageMode;

    // Guess Restrictions //
    public static OptionItem TerroristCanGuess;
    public static OptionItem PhantomCanGuess;
    public static OptionItem GodCanGuess;

    public static OptionItem AllowConsole;
    public static OptionItem NoGameEnd;
    public static OptionItem DontUpdateDeadPlayers;
    public static OptionItem AutoDisplayLastRoles;
    public static OptionItem AutoDisplayLastAddOns;
    public static OptionItem AutoDisplayKillLog;
    public static OptionItem AutoDisplayLastResult;
    private static OptionItem SuffixMode;
    public static OptionItem HideGameSettings;
    public static OptionItem FormatNameMode;
    public static OptionItem DisableEmojiName;
    public static OptionItem ChangeNameToRoleInfo;
    public static OptionItem SendRoleDescriptionFirstMeeting;
    public static OptionItem RoleAssigningAlgorithm;
    public static OptionItem EndWhenPlayerBug;
    public static OptionItem RemovePetsAtDeadPlayers;

    public static OptionItem UsePets;
    public static OptionItem PetToAssignToEveryone;
    public static OptionItem UseUnshiftTrigger;
    public static OptionItem UseUnshiftTriggerForNKs;
    public static OptionItem UsePhantomBasis;
    public static OptionItem UsePhantomBasisForNKs;
    public static OptionItem UseVoteCancelling;
    public static OptionItem EnableUpMode;
    public static OptionItem AutoKickStart;
    public static OptionItem AutoKickStartAsBan;
    public static OptionItem AutoKickStartTimes;
    public static OptionItem AutoKickStopWords;
    public static OptionItem AutoKickStopWordsAsBan;
    public static OptionItem AutoKickStopWordsTimes;
    public static OptionItem KickAndroidPlayer;
    public static OptionItem ApplyDenyNameList;
    public static OptionItem KickPlayerFriendCodeNotExist;
    public static OptionItem KickLowLevelPlayer;
    public static OptionItem ApplyBanList;
    public static OptionItem ApplyModeratorList;
    public static OptionItem AutoWarnStopWords;

    public static OptionItem DIYGameSettings;
    public static OptionItem PlayerCanSetColor;
    public static OptionItem PlayerCanSetName;
    public static OptionItem PlayerCanTPInAndOut;

    // Add-Ons
    public static OptionItem NameDisplayAddons;
    public static OptionItem AddBracketsToAddons;
    public static OptionItem NoLimitAddonsNumMax;
    public static OptionItem BewilderVision;
    public static OptionItem SunglassesVision;
    public static OptionItem MadmateSpawnMode;
    public static OptionItem MadmateCountMode;
    public static OptionItem SheriffCanBeMadmate;
    public static OptionItem MayorCanBeMadmate;
    public static OptionItem NGuesserCanBeMadmate;
    public static OptionItem SnitchCanBeMadmate;
    public static OptionItem JudgeCanBeMadmate;

    public static OptionItem MarshallCanBeMadmate;

    public static OptionItem FarseerCanBeMadmate;
    public static OptionItem MadSnitchTasks;
    public static OptionItem FlashmanSpeed;
    public static OptionItem GiantSpeed;
    public static OptionItem ImpEgoistVisibalToAllies;
    public static OptionItem TicketsPerKill;
    public static OptionItem DualVotes;
    public static OptionItem ImpCanBeLoyal;
    public static OptionItem CrewCanBeLoyal;
    public static OptionItem MinWaitAutoStart;
    public static OptionItem MaxWaitAutoStart;
    public static OptionItem PlayerAutoStart;

    public static OptionItem DumpLogAfterGameEnd;

    private static readonly string[] SuffixModes =
    [
        "SuffixMode.None",
        "SuffixMode.Version",
        "SuffixMode.Streaming",
        "SuffixMode.Recording",
        "SuffixMode.RoomHost",
        "SuffixMode.OriginalName",
        "SuffixMode.DoNotKillMe",
        "SuffixMode.NoAndroidPlz",
        "SuffixMode.AutoHost"
    ];

    private static readonly string[] RoleAssigningAlgorithms =
    [
        "RoleAssigningAlgorithm.Default",
        "RoleAssigningAlgorithm.NetRandom",
        "RoleAssigningAlgorithm.HashRandom",
        "RoleAssigningAlgorithm.Xorshift",
        "RoleAssigningAlgorithm.MersenneTwister"
    ];

    private static readonly string[] FormatNameModes =
    [
        "FormatNameModes.None",
        "FormatNameModes.Color",
        "FormatNameModes.Snacks"
    ];

    public static bool IsLoaded;

    public static int LoadingPercentage;
    public static string MainLoadingText = string.Empty;
    public static string RoleLoadingText = string.Empty;

    public static readonly Dictionary<CustomRoles, (OptionItem Imp, OptionItem Neutral, OptionItem Crew)> AddonCanBeSettings = [];

    public static readonly HashSet<CustomRoles> SingleRoles = [];

    static Options()
    {
        ResetRoleCounts();
        CustomRolesHelper.CanCheck = true;
    }

    public static CustomGameMode CurrentGameMode
        => GameMode.GetInt() switch
        {
            1 => CustomGameMode.SoloKombat,
            2 => CustomGameMode.FFA,
            3 => CustomGameMode.MoveAndStop,
            4 => CustomGameMode.HotPotato,
            5 => CustomGameMode.HideAndSeek,
            6 => CustomGameMode.Speedrun,
            _ => CustomGameMode.Standard
        };

    [HarmonyPatch(typeof(TranslationController), nameof(TranslationController.Initialize)), HarmonyPostfix]
    public static void OptionsLoadStart()
    {
        Logger.Info("Options.Load Start", "Options");
        AddSteamID.AddSteamAppIdFile();
        Utils.LoadComboInfo();
        Main.LoadRoleClasses();
        ChatCommands.LoadCommands();

        Main.Instance.StartCoroutine(Load());
    }

    private static void PostLoadTasks()
    {
        Logger.Info("Options.Load End", "Options");
        GroupOptions();
        GroupAddons();
        //Process.Start(@".\EHR_DATA\SettingsUI.exe");

#if DEBUG
        // Used for generating the table of roles for the README
        try
        {
            var sb = new System.Text.StringBuilder();
            var grouped = Enum.GetValues<CustomRoles>().GroupBy(x =>
            {
                if (x is CustomRoles.GM or CustomRoles.Philantropist or CustomRoles.Konan or CustomRoles.NotAssigned or CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor or CustomRoles.Convict || x.IsForOtherGameMode() || x.IsVanilla() || x.ToString().Contains("EHR")) return 4;
                if (x.IsAdditionRole()) return 3;
                if (x.IsImpostor() || x.IsMadmate()) return 0;
                if (x.IsNeutral()) return 1;
                if (x.IsCrewmate()) return 2;
                return 4;
            }).ToDictionary(x => x.Key, x => x.ToArray());
            var max = grouped.Max(x => x.Value.Length);
            for (int i = 0; i < max; i++)
            {
                var cr = grouped[2].ElementAtOrDefault(i);
                var crew = Translator.GetString(cr.ToString());
                var ir = grouped[0].ElementAtOrDefault(i);
                var imp = Translator.GetString(ir.ToString());
                if (ir == default) imp = string.Empty;
                var nr = grouped[1].ElementAtOrDefault(i);
                var neu = Translator.GetString(nr.ToString());
                if (nr == default) neu = string.Empty;
                var a = grouped[3].ElementAtOrDefault(i);
                var add = Translator.GetString(a.ToString());
                if (a == default) add = string.Empty;
                sb.AppendLine($"| {crew,17} | {imp,17} | {neu,17} | {add,17} |");
            }

            const string path = "./roles.txt";
            if (!File.Exists(path)) File.Create(path).Close();
            File.WriteAllText(path, sb.ToString());
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
        }
#endif
    }

    private static void GroupOptions()
    {
        GroupedOptions = OptionItem.AllOptions
            .GroupBy(x => x.Tab)
            .OrderBy(x => (int)x.Key)
            .ToDictionary(x => x.Key, x => x.ToArray());

        HnSManager.AllHnSRoles = HnSManager.GetAllHnsRoles(HnSManager.GetAllHnsRoleTypes());
    }

    private static void GroupAddons()
    {
        GroupedAddons = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.GetInterfaces().ToList().Contains(typeof(IAddon)))
            .Select(x => (IAddon)Activator.CreateInstance(x))
            .Where(x => x != null)
            .GroupBy(x => x.Type)
            .ToDictionary(x => x.Key, x => x.Select(y => Enum.Parse<CustomRoles>(y.GetType().Name, true)).ToList());
    }

    public static VoteMode GetWhenSkipVote() => (VoteMode)WhenSkipVote.GetValue();
    public static VoteMode GetWhenNonVote() => (VoteMode)WhenNonVote.GetValue();

    public static SuffixModes GetSuffixMode() => (SuffixModes)SuffixMode.GetValue();

    private static void ResetRoleCounts()
    {
        roleCounts = [];
        roleSpawnChances = [];

        foreach (var role in Enum.GetValues<CustomRoles>())
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

    public static int GetRoleSpawnMode(CustomRoles role) => CustomRoleSpawnChances.TryGetValue(role, out var sc) ? sc.GetChance() : 0;

    public static int GetRoleCount(CustomRoles role)
    {
        var mode = GetRoleSpawnMode(role);
        return mode is 0 ? 0 : CustomRoleCounts.TryGetValue(role, out var option) ? option.GetInt() : roleCounts[role];
    }

    public static float GetRoleChance(CustomRoles role)
    {
        return CustomRoleSpawnChances.TryGetValue(role, out var option) ? option.GetValue() /* / 10f */ : roleSpawnChances[role];
    }

    private static System.Collections.IEnumerator Load()
    {
        LoadingPercentage = 0;
        MainLoadingText = "Building system settings";

        if (IsLoaded) yield break;

        OptionSaver.Initialize();

        yield return null;

        int defaultPresetNumber = OptionSaver.GetDefaultPresetNumber();
        _ = new PresetOptionItem(defaultPresetNumber, TabGroup.SystemSettings)
            .SetColor(new Color32(255, 235, 4, byte.MaxValue))
            .SetHeader(true);

        GameMode = new StringOptionItem(1, "GameMode", GameModes, 0, TabGroup.GameSettings)
            .SetHeader(true);

        #region Settings

        CustomRoleCounts = [];
        CustomRoleSpawnChances = [];
        CustomAdtRoleSpawnRate = [];

        MainLoadingText = "Building general settings";


        ImpKnowAlliesRole = new BooleanOptionItem(150, "ImpKnowAlliesRole", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);
        ImpKnowWhosMadmate = new BooleanOptionItem(151, "ImpKnowWhosMadmate", false, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);
        ImpCanKillMadmate = new BooleanOptionItem(152, "ImpCanKillMadmate", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);

        MadmateKnowWhosMadmate = new BooleanOptionItem(153, "MadmateKnowWhosMadmate", false, TabGroup.ImpostorRoles)
            .SetHeader(true)
            .SetGameMode(CustomGameMode.Standard);
        MadmateKnowWhosImp = new BooleanOptionItem(154, "MadmateKnowWhosImp", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);
        MadmateCanKillImp = new BooleanOptionItem(155, "MadmateCanKillImp", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);
        MadmateHasImpostorVision = new BooleanOptionItem(156, "MadmateHasImpostorVision", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);

        RefugeeKillCD = new FloatOptionItem(157, "RefugeeKillCD", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Seconds);

        DefaultShapeshiftCooldown = new FloatOptionItem(200, "DefaultShapeshiftCooldown", new(5f, 180f, 5f), 15f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Seconds);
        DeadImpCantSabotage = new BooleanOptionItem(201, "DeadImpCantSabotage", false, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);

        NonNeutralKillingRolesMinPlayer = new IntegerOptionItem(202, "NonNeutralKillingRolesMinPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Players);
        NonNeutralKillingRolesMaxPlayer = new IntegerOptionItem(203, "NonNeutralKillingRolesMaxPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);

        NeutralKillingRolesMinPlayer = new IntegerOptionItem(204, "NeutralKillingRolesMinPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Players);
        NeutralKillingRolesMaxPlayer = new IntegerOptionItem(205, "NeutralKillingRolesMaxPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);

        NeutralRoleWinTogether = new BooleanOptionItem(208, "NeutralRoleWinTogether", false, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);
        NeutralWinTogether = new BooleanOptionItem(209, "NeutralWinTogether", false, TabGroup.NeutralRoles)
            .SetParent(NeutralRoleWinTogether)
            .SetGameMode(CustomGameMode.Standard);

        NameDisplayAddons = new BooleanOptionItem(210, "NameDisplayAddons", true, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);
        NoLimitAddonsNumMax = new IntegerOptionItem(211, "NoLimitAddonsNumMax", new(1, 90, 1), 1, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard);


        RoleLoadingText = "Add-ons\n.";

        yield return null;


        AddBracketsToAddons = new BooleanOptionItem(13500, "BracketAddons", false, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);

        #region Roles/AddOns_Settings

        int titleId = 100100;

        LoadingPercentage = 5;
        MainLoadingText = "Building Add-on Settings";

        var IAddonType = typeof(IAddon);
        Dictionary<AddonTypes, IAddon[]> addonTypes = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => IAddonType.IsAssignableFrom(t) && !t.IsInterface)
            .OrderBy(t => Translator.GetString(t.Name))
            .Select(type => (IAddon)Activator.CreateInstance(type))
            .Where(x => x != null)
            .GroupBy(x => x.Type)
            .ToDictionary(x => x.Key, x => x.ToArray());

        foreach (var addonType in addonTypes)
        {
            MainLoadingText = $"Building Add-on Settings ({addonType.Key})";
            int index = 0;

            new TextOptionItem(titleId, $"ROT.AddonType.{addonType.Key}", TabGroup.Addons)
                .SetGameMode(CustomGameMode.Standard)
                .SetColor(addonType.Key.GetAddonTypeColor())
                .SetHeader(true);
            titleId += 10;

            foreach (var addon in addonType.Value)
            {
                index++;
                RoleLoadingText = $"{addon.GetType().Name} ({index}/{addonType.Value.Length})";
                Log();

                addon.SetupCustomOption();
            }

            yield return null;
        }

        LoadingPercentage = 15;
        MainLoadingText = "Building Role Settings";

        var IVanillaType = typeof(IVanillaSettingHolder);
        Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => IVanillaType.IsAssignableFrom(t) && !t.IsInterface)
            .OrderBy(t => Translator.GetString(t.Name))
            .Select(type => (IVanillaSettingHolder)Activator.CreateInstance(type))
            .Do(x =>
            {
                new TextOptionItem(titleId, "ROT.Vanilla", x.Tab)
                    .SetGameMode(CustomGameMode.Standard)
                    .SetColor(Color.white)
                    .SetHeader(true);
                titleId += 10;

                RoleLoadingText = x.GetType().Name;
                Log();

                x.SetupCustomOption();
            });

        var IType = typeof(ISettingHolder);
        Dictionary<SimpleRoleOptionType, ISettingHolder[]> simpleRoleClasses = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => IType.IsAssignableFrom(t) && !t.IsInterface)
            .OrderBy(t => Translator.GetString(t.Name))
            .GroupBy(x => ((CustomRoles)Enum.Parse(typeof(CustomRoles), ignoreCase: true, value: x.Name)).GetSimpleRoleOptionType())
            .ToDictionary(x => x.Key, x => x.Select(type => (ISettingHolder)Activator.CreateInstance(type)).ToArray());

        Dictionary<RoleOptionType, RoleBase[]> roleClassesDict = Main.AllRoleClasses
            .Where(x => x.GetType().Name != "VanillaRole")
            .GroupBy(x => ((CustomRoles)Enum.Parse(typeof(CustomRoles), ignoreCase: true, value: x.GetType().Name)).GetRoleOptionType())
            .ToDictionary(x => x.Key, x => x.ToArray());

        foreach (var roleClasses in roleClassesDict)
        {
            MainLoadingText = $"Building Role Settings: {roleClasses.Key} Roles";
            int allRoles = roleClasses.Value.Length;
            int index = 0;

            var tab = roleClasses.Key.GetTabFromOptionType();

            var key = roleClasses.Key.GetSimpleRoleOptionType();
            if (simpleRoleClasses.TryGetValue(key, out ISettingHolder[] value) && value.Length > 0)
            {
                var categorySuffix = roleClasses.Key switch
                {
                    RoleOptionType.Neutral_Killing => "NK",
                    RoleOptionType.Neutral_NonKilling => "NNK",
                    _ => string.Empty
                };
                new TextOptionItem(titleId, $"ROT.Basic{categorySuffix}", tab)
                    .SetGameMode(CustomGameMode.Standard)
                    .SetColor(Color.gray)
                    .SetHeader(true);
                titleId += 10;

                foreach (ISettingHolder holder in value)
                {
                    RoleLoadingText = $"(Simple {key}) {holder.GetType().Name} (total: {value.Length})";
                    Log();

                    holder.SetupCustomOption();
                }

                simpleRoleClasses.Remove(key);
            }

            new TextOptionItem(titleId, $"ROT.{roleClasses.Key}", tab)
                .SetHeader(true)
                .SetGameMode(CustomGameMode.Standard)
                .SetColor(roleClasses.Key.GetRoleOptionTypeColor());
            titleId += 10;

            foreach (var roleClass in roleClasses.Value)
            {
                index++;
                var type = roleClass.GetType();
                RoleLoadingText = $"{index}/{allRoles} ({type.Name})";
                Log();
                try
                {
                    type.GetMethod("SetupCustomOption")?.Invoke(roleClass, null);
                }
                catch (Exception e)
                {
                    Logger.Exception(e, $"{MainLoadingText} - {RoleLoadingText}");
                }

                yield return null;
            }

            if (roleClasses.Key == RoleOptionType.Impostor)
            {
                new TextOptionItem(titleId, "ROT.MadMates", TabGroup.ImpostorRoles)
                    .SetHeader(true)
                    .SetGameMode(CustomGameMode.Standard)
                    .SetColor(Palette.ImpostorRed);
                titleId += 10;
            }

            yield return null;
        }

        void Log() => Logger.Info(" " + RoleLoadingText, MainLoadingText);

        #endregion


        LoadingPercentage = 60;

        RoleLoadingText = string.Empty;

        #endregion

        #region EHRSettings

        MainLoadingText = "Building EHR settings";

        KickLowLevelPlayer = new IntegerOptionItem(19300, "KickLowLevelPlayer", new(0, 100, 1), 0, TabGroup.SystemSettings)
            .SetValueFormat(OptionFormat.Level)
            .SetHeader(true);
        KickAndroidPlayer = new BooleanOptionItem(19301, "KickAndroidPlayer", false, TabGroup.SystemSettings);
        KickPlayerFriendCodeNotExist = new BooleanOptionItem(19302, "KickPlayerFriendCodeNotExist", false, TabGroup.SystemSettings, true);
        ApplyDenyNameList = new BooleanOptionItem(19303, "ApplyDenyNameList", true, TabGroup.SystemSettings, true);
        ApplyBanList = new BooleanOptionItem(19304, "ApplyBanList", true, TabGroup.SystemSettings, true);
        ApplyModeratorList = new BooleanOptionItem(19305, "ApplyModeratorList", false, TabGroup.SystemSettings);

        LoadingPercentage = 61;

        AutoKickStart = new BooleanOptionItem(19310, "AutoKickStart", false, TabGroup.SystemSettings);
        AutoKickStartTimes = new IntegerOptionItem(19311, "AutoKickStartTimes", new(0, 90, 1), 1, TabGroup.SystemSettings)
            .SetParent(AutoKickStart)
            .SetValueFormat(OptionFormat.Times);
        AutoKickStartAsBan = new BooleanOptionItem(19312, "AutoKickStartAsBan", false, TabGroup.SystemSettings)
            .SetParent(AutoKickStart);
        AutoKickStopWords = new BooleanOptionItem(19313, "AutoKickStopWords", false, TabGroup.SystemSettings);
        AutoKickStopWordsTimes = new IntegerOptionItem(19314, "AutoKickStopWordsTimes", new(0, 90, 1), 3, TabGroup.SystemSettings)
            .SetParent(AutoKickStopWords)
            .SetValueFormat(OptionFormat.Times);
        AutoKickStopWordsAsBan = new BooleanOptionItem(19315, "AutoKickStopWordsAsBan", false, TabGroup.SystemSettings)
            .SetParent(AutoKickStopWords);

        LoadingPercentage = 62;

        AutoWarnStopWords = new BooleanOptionItem(19316, "AutoWarnStopWords", false, TabGroup.SystemSettings);
        MinWaitAutoStart = new FloatOptionItem(44420, "MinWaitAutoStart", new(0f, 10f, 0.5f), 1.5f, TabGroup.SystemSettings);
        MaxWaitAutoStart = new FloatOptionItem(44421, "MaxWaitAutoStart", new(0f, 10f, 0.5f), 1.5f, TabGroup.SystemSettings);
        PlayerAutoStart = new IntegerOptionItem(44422, "PlayerAutoStart", new(1, 15, 1), 14, TabGroup.SystemSettings);
        AutoStartTimer = new IntegerOptionItem(44423, "AutoStartTimer", new(10, 600, 1), 20, TabGroup.SystemSettings)
            .SetValueFormat(OptionFormat.Seconds);
        AutoPlayAgain = new BooleanOptionItem(44424, "AutoPlayAgain", false, TabGroup.SystemSettings);
        AutoPlayAgainCountdown = new IntegerOptionItem(44425, "AutoPlayAgainCountdown", new(1, 90, 1), 10, TabGroup.SystemSettings)
            .SetParent(AutoPlayAgain);

        LowLoadMode = new BooleanOptionItem(19317, "LowLoadMode", true, TabGroup.SystemSettings)
            .SetHeader(true)
            .SetColor(Color.green);

        DeepLowLoad = new BooleanOptionItem(19325, "DeepLowLoad", false, TabGroup.SystemSettings)
            .SetColor(Color.red);

        DontUpdateDeadPlayers = new BooleanOptionItem(19326, "DontUpdateDeadPlayers", true, TabGroup.SystemSettings)
            .SetColor(Color.red);

        DumpLogAfterGameEnd = new BooleanOptionItem(19327, "DumpLogAfterGameEnd", true, TabGroup.SystemSettings)
            .SetColor(Color.yellow);

        EndWhenPlayerBug = new BooleanOptionItem(19318, "EndWhenPlayerBug", true, TabGroup.SystemSettings)
            .SetHeader(true)
            .SetColor(Color.blue);

        RemovePetsAtDeadPlayers = new BooleanOptionItem(60294, "RemovePetsAtDeadPlayers", false, TabGroup.SystemSettings)
            .SetColor(Color.magenta);

        CheatResponses = new StringOptionItem(19319, "CheatResponses", CheatResponsesName, 2, TabGroup.SystemSettings)
            .SetHeader(true);


        DisableVoteBan = new BooleanOptionItem(19320, "DisableVoteBan", true, TabGroup.SystemSettings, true);


        LoadingPercentage = 63;


        AutoDisplayKillLog = new BooleanOptionItem(19321, "AutoDisplayKillLog", true, TabGroup.SystemSettings)
            .SetHeader(true);
        AutoDisplayLastRoles = new BooleanOptionItem(19322, "AutoDisplayLastRoles", true, TabGroup.SystemSettings);
        AutoDisplayLastAddOns = new BooleanOptionItem(19328, "AutoDisplayLastAddOns", true, TabGroup.SystemSettings);
        AutoDisplayLastResult = new BooleanOptionItem(19323, "AutoDisplayLastResult", true, TabGroup.SystemSettings);

        SuffixMode = new StringOptionItem(19324, "SuffixMode", SuffixModes, 0, TabGroup.SystemSettings, true)
            .SetHeader(true);
        HideGameSettings = new BooleanOptionItem(19400, "HideGameSettings", false, TabGroup.SystemSettings);
        DIYGameSettings = new BooleanOptionItem(19401, "DIYGameSettings", false, TabGroup.SystemSettings);
        PlayerCanSetColor = new BooleanOptionItem(19402, "PlayerCanSetColor", false, TabGroup.SystemSettings);
        PlayerCanSetName = new BooleanOptionItem(19410, "PlayerCanSetName", false, TabGroup.SystemSettings);
        PlayerCanTPInAndOut = new BooleanOptionItem(19411, "PlayerCanTPInAndOut", false, TabGroup.SystemSettings);
        FormatNameMode = new StringOptionItem(19403, "FormatNameMode", FormatNameModes, 0, TabGroup.SystemSettings);
        DisableEmojiName = new BooleanOptionItem(19404, "DisableEmojiName", true, TabGroup.SystemSettings);
        ChangeNameToRoleInfo = new BooleanOptionItem(19405, "ChangeNameToRoleInfo", true, TabGroup.SystemSettings);
        SendRoleDescriptionFirstMeeting = new BooleanOptionItem(19406, "SendRoleDescriptionFirstMeeting", true, TabGroup.SystemSettings);
        NoGameEnd = new BooleanOptionItem(19407, "NoGameEnd", false, TabGroup.SystemSettings)
            .SetColor(Color.red);
        AllowConsole = new BooleanOptionItem(19408, "AllowConsole", false, TabGroup.SystemSettings)
            .SetColor(Color.red);
        RoleAssigningAlgorithm = new StringOptionItem(19409, "RoleAssigningAlgorithm", RoleAssigningAlgorithms, 4, TabGroup.SystemSettings, true)
            .RegisterUpdateValueEvent((_, args) => IRandom.SetInstanceById(args.CurrentValue));
        KPDCamouflageMode = new StringOptionItem(19500, "KPDCamouflageMode", CamouflageMode, 0, TabGroup.SystemSettings)
            .SetHeader(true)
            .SetColor(new Color32(255, 192, 203, byte.MaxValue));

        LoadingPercentage = 64;


        EnableUpMode = new BooleanOptionItem(19600, "EnableYTPlan", false, TabGroup.SystemSettings)
            .SetColor(Color.cyan)
            .SetHeader(true);

        #endregion

        yield return null;

        #region Gamemodes

        MainLoadingText = "Building Settings for Other Gamemodes";

        // SoloKombat
        SoloKombatManager.SetupCustomOption();
        // FFA
        FFAManager.SetupCustomOption();
        // Move And Stop
        MoveAndStopManager.SetupCustomOption();
        // Hot Potato
        HotPotatoManager.SetupCustomOption();
        // Speedrun
        SpeedrunManager.SetupCustomOption();
        // Hide And Seek
        HnSManager.SetupCustomOption();

        yield return null;


        LoadingPercentage = 65;
        MainLoadingText = "Building game settings";

        new TextOptionItem(100023, "MenuTitle.Ejections", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        CEMode = new StringOptionItem(19800, "ConfirmEjectionsMode", ConfirmEjectionsMode, 2, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ShowImpRemainOnEject = new BooleanOptionItem(19810, "ShowImpRemainOnEject", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ShowNKRemainOnEject = new BooleanOptionItem(19811, "ShowNKRemainOnEject", true, TabGroup.GameSettings)
            .SetParent(ShowImpRemainOnEject)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        LoadingPercentage = 66;

        ShowTeamNextToRoleNameOnEject = new BooleanOptionItem(19812, "ShowTeamNextToRoleNameOnEject", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ConfirmEgoistOnEject = new BooleanOptionItem(19813, "ConfirmEgoistOnEject", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue))
            .SetHeader(true);
        ConfirmLoversOnEject = new BooleanOptionItem(19815, "ConfirmLoversOnEject", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        LoadingPercentage = 67;


        //Maps Settings
        new TextOptionItem(100024, "MenuTitle.MapsSettings", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Random Maps Mode
        RandomMapsMode = new BooleanOptionItem(19900, "RandomMapsMode", false, TabGroup.GameSettings)
            .SetHeader(true)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        LoadingPercentage = 68;

        SkeldChance = new IntegerOptionItem(19910, "SkeldChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        MiraChance = new IntegerOptionItem(19911, "MiraChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        PolusChance = new IntegerOptionItem(19912, "PolusChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        DleksChance = new IntegerOptionItem(19914, "DleksChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        AirshipChance = new IntegerOptionItem(19913, "AirshipChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        FungleChance = new IntegerOptionItem(19922, "FungleChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);

        LoadingPercentage = 69;


        // Random Spawn
        RandomSpawn = new BooleanOptionItem(22000, "RandomSpawn", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        AirshipAdditionalSpawn = new BooleanOptionItem(22010, "AirshipAdditionalSpawn", false, TabGroup.GameSettings)
            .SetParent(RandomSpawn)
            .SetGameMode(CustomGameMode.Standard);

        // Airship Variable Electrical
        AirshipVariableElectrical = new BooleanOptionItem(22100, "AirshipVariableElectrical", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Disable Airship Moving Platform
        DisableAirshipMovingPlatform = new BooleanOptionItem(22110, "DisableAirshipMovingPlatform", false, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Disable Spore Triggers on Fungle
        DisableSporeTriggerOnFungle = new BooleanOptionItem(22130, "DisableSporeTriggerOnFungle", false, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Disable Zipline On Fungle
        DisableZiplineOnFungle = new BooleanOptionItem(22305, "DisableZiplineOnFungle", false, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Disable Zipline From Top
        DisableZiplineFromTop = new BooleanOptionItem(22308, "DisableZiplineFromTop", false, TabGroup.GameSettings)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Disable Zipline From Under
        DisableZiplineFromUnder = new BooleanOptionItem(22310, "DisableZiplineFromUnder", false, TabGroup.GameSettings)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        DisableZiplineForCrew = new BooleanOptionItem(22316, "DisableZiplineForCrew", false, TabGroup.GameSettings)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        DisableZiplineForImps = new BooleanOptionItem(22318, "DisableZiplineForImps", false, TabGroup.GameSettings)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        DisableZiplineForNeutrals = new BooleanOptionItem(22320, "DisableZiplineForNeutrals", false, TabGroup.GameSettings)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        ZiplineTravelTimeFromBottom = new FloatOptionItem(22312, "ZiplineTravelTimeFromBottom", new(0.5f, 10f, 0.5f), 4f, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        ZiplineTravelTimeFromTop = new FloatOptionItem(22314, "ZiplineTravelTimeFromTop", new(0.5f, 10f, 0.5f), 2f, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Reset Doors After Meeting
        ResetDoorsEveryTurns = new BooleanOptionItem(22120, "ResetDoorsEveryTurns", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Reset Doors Mode
        DoorsResetMode = new StringOptionItem(22122, "DoorsResetMode", Enum.GetNames<DoorsReset.ResetMode>(), 2, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(ResetDoorsEveryTurns);

        // Change decontamination time on MiraHQ/Polus
        ChangeDecontaminationTime = new BooleanOptionItem(60503, "ChangeDecontaminationTime", false, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Decontamination time on MiraHQ
        DecontaminationTimeOnMiraHQ = new FloatOptionItem(60504, "DecontaminationTimeOnMiraHQ", new(0.5f, 10f, 0.25f), 3f, TabGroup.GameSettings)
            .SetParent(ChangeDecontaminationTime)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Decontamination time on Polus
        DecontaminationTimeOnPolus = new FloatOptionItem(60505, "DecontaminationTimeOnPolus", new(0.5f, 10f, 0.25f), 3f, TabGroup.GameSettings)
            .SetParent(ChangeDecontaminationTime)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));


        LoadingPercentage = 70;

        yield return null;

        // Sabotage
        new TextOptionItem(100025, "MenuTitle.Sabotage", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetHeader(true);

        // CommsCamouflage
        CommsCamouflage = new BooleanOptionItem(22200, "CommsCamouflage", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));
        CommsCamouflageDisableOnFungle = new BooleanOptionItem(22202, "CommsCamouflageDisableOnFungle", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflage)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));
        DisableReportWhenCC = new BooleanOptionItem(22300, "DisableReportWhenCC", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        LoadingPercentage = 71;


        // Sabotage Cooldown Control
        SabotageCooldownControl = new BooleanOptionItem(22400, "SabotageCooldownControl", false, TabGroup.GameSettings)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        SabotageCooldown = new FloatOptionItem(22405, "SabotageCooldown", new(1f, 60f, 1f), 30f, TabGroup.GameSettings)
            .SetParent(SabotageCooldownControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        // Sabotage Duration Control
        SabotageTimeControl = new BooleanOptionItem(22410, "SabotageTimeControl", false, TabGroup.GameSettings)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);

        // The Skeld
        SkeldReactorTimeLimit = new FloatOptionItem(22418, "SkeldReactorTimeLimit", new(5f, 90f, 1f), 30f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        SkeldO2TimeLimit = new FloatOptionItem(22419, "SkeldO2TimeLimit", new(5f, 90f, 1f), 30f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // Mira HQ
        MiraReactorTimeLimit = new FloatOptionItem(22422, "MiraReactorTimeLimit", new(5f, 90f, 1f), 45f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        MiraO2TimeLimit = new FloatOptionItem(22423, "MiraO2TimeLimit", new(5f, 90f, 1f), 45f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // Polus
        PolusReactorTimeLimit = new FloatOptionItem(22424, "PolusReactorTimeLimit", new(5f, 90f, 1f), 60f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // The Airship
        AirshipReactorTimeLimit = new FloatOptionItem(22425, "AirshipReactorTimeLimit", new(5f, 90f, 1f), 90f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // The Fungle
        FungleReactorTimeLimit = new FloatOptionItem(22426, "FungleReactorTimeLimit", new(5f, 90f, 1f), 60f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        FungleMushroomMixupDuration = new FloatOptionItem(22427, "FungleMushroomMixupDuration", new(5f, 90f, 1f), 10f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 72;

        // LightsOutSpecialSettings
        LightsOutSpecialSettings = new BooleanOptionItem(22500, "LightsOutSpecialSettings", false, TabGroup.GameSettings)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipViewingDeckLightsPanel = new BooleanOptionItem(22510, "DisableAirshipViewingDeckLightsPanel", false, TabGroup.GameSettings)
            .SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipGapRoomLightsPanel = new BooleanOptionItem(22511, "DisableAirshipGapRoomLightsPanel", false, TabGroup.GameSettings)
            .SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipCargoLightsPanel = new BooleanOptionItem(22512, "DisableAirshipCargoLightsPanel", false, TabGroup.GameSettings)
            .SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 73;


        new TextOptionItem(100026, "MenuTitle.Disable", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableShieldAnimations = new BooleanOptionItem(22601, "DisableShieldAnimations", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableKillAnimationOnGuess = new BooleanOptionItem(22602, "DisableKillAnimationOnGuess", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableShapeshiftAnimations = new BooleanOptionItem(22604, "DisableShapeshiftAnimations", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableAllShapeshiftAnimations = new BooleanOptionItem(22605, "DisableAllShapeshiftAnimations", false, TabGroup.GameSettings)
            .SetParent(DisableShapeshiftAnimations)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableVanillaRoles = new BooleanOptionItem(22600, "DisableVanillaRoles", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableTaskWin = new BooleanOptionItem(22650, "DisableTaskWin", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableTaskWinIfAllCrewsAreDead = new BooleanOptionItem(22651, "DisableTaskWinIfAllCrewsAreDead", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableTaskWinIfAllCrewsAreConverted = new BooleanOptionItem(22652, "DisableTaskWinIfAllCrewsAreConverted", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 74;


        DisableMeeting = new BooleanOptionItem(22700, "DisableMeeting", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableSabotage = new BooleanOptionItem(22800, "DisableSabotage", false, TabGroup.GameSettings)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableCloseDoor = new BooleanOptionItem(22810, "DisableCloseDoor", false, TabGroup.GameSettings)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 75;

        DisableDevices = new BooleanOptionItem(22900, "DisableDevices", false, TabGroup.GameSettings)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableSkeldDevices = new BooleanOptionItem(22905, "DisableSkeldDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices);
        DisableSkeldAdmin = new BooleanOptionItem(22906, "DisableSkeldAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableSkeldDevices);
        DisableSkeldCamera = new BooleanOptionItem(22907, "DisableSkeldCamera", false, TabGroup.GameSettings)
            .SetParent(DisableSkeldDevices);

        LoadingPercentage = 76;

        DisableMiraHQDevices = new BooleanOptionItem(22908, "DisableMiraHQDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices);
        DisableMiraHQAdmin = new BooleanOptionItem(22909, "DisableMiraHQAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableMiraHQDevices);
        DisableMiraHQDoorLog = new BooleanOptionItem(22910, "DisableMiraHQDoorLog", false, TabGroup.GameSettings)
            .SetParent(DisableMiraHQDevices);
        DisablePolusDevices = new BooleanOptionItem(22911, "DisablePolusDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices);
        DisablePolusAdmin = new BooleanOptionItem(22912, "DisablePolusAdmin", false, TabGroup.GameSettings)
            .SetParent(DisablePolusDevices);
        DisablePolusCamera = new BooleanOptionItem(22913, "DisablePolusCamera", false, TabGroup.GameSettings)
            .SetParent(DisablePolusDevices);
        DisablePolusVital = new BooleanOptionItem(22914, "DisablePolusVital", false, TabGroup.GameSettings)
            .SetParent(DisablePolusDevices);
        DisableAirshipDevices = new BooleanOptionItem(22915, "DisableAirshipDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices);
        DisableAirshipCockpitAdmin = new BooleanOptionItem(22916, "DisableAirshipCockpitAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices);

        LoadingPercentage = 77;

        DisableAirshipRecordsAdmin = new BooleanOptionItem(22917, "DisableAirshipRecordsAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices);
        DisableAirshipCamera = new BooleanOptionItem(22918, "DisableAirshipCamera", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices);
        DisableAirshipVital = new BooleanOptionItem(22919, "DisableAirshipVital", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices);
        DisableFungleDevices = new BooleanOptionItem(22925, "DisableFungleDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableFungleCamera = new BooleanOptionItem(22926, "DisableFungleCamera", false, TabGroup.GameSettings)
            .SetParent(DisableFungleDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableFungleVital = new BooleanOptionItem(22927, "DisableFungleVital", false, TabGroup.GameSettings)
            .SetParent(DisableFungleDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevicesIgnoreConditions = new BooleanOptionItem(22920, "IgnoreConditions", false, TabGroup.GameSettings)
            .SetParent(DisableDevices);
        DisableDevicesIgnoreImpostors = new BooleanOptionItem(22921, "IgnoreImpostors", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions);
        DisableDevicesIgnoreNeutrals = new BooleanOptionItem(22922, "IgnoreNeutrals", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions);
        DisableDevicesIgnoreCrewmates = new BooleanOptionItem(22923, "IgnoreCrewmates", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions);
        DisableDevicesIgnoreAfterAnyoneDied = new BooleanOptionItem(22924, "IgnoreAfterAnyoneDied", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 78;


        UsePets = new BooleanOptionItem(23850, "UsePets", false, TabGroup.TaskSettings)
            .SetHeader(true)
            .SetColor(new Color32(60, 0, 255, byte.MaxValue));
        PetToAssignToEveryone = new StringOptionItem(23854, "PetToAssign", PetToAssign, 24, TabGroup.TaskSettings)
            .SetParent(UsePets)
            .SetColor(new Color32(60, 0, 255, byte.MaxValue));

        UseUnshiftTrigger = new BooleanOptionItem(23871, "UseUnshiftTrigger", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 44, 44, byte.MaxValue));
        UseUnshiftTriggerForNKs = new BooleanOptionItem(23872, "UseUnshiftTriggerForNKs", false, TabGroup.TaskSettings)
            .SetParent(UseUnshiftTrigger)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 44, 44, byte.MaxValue));

        UsePhantomBasis = new BooleanOptionItem(23851, "UsePhantomBasis", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 255, 44, byte.MaxValue));
        UsePhantomBasisForNKs = new BooleanOptionItem(23864, "UsePhantomBasisForNKs", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(UsePhantomBasis)
            .SetColor(new Color32(255, 255, 44, byte.MaxValue));

        UseVoteCancelling = new BooleanOptionItem(23852, "UseVoteCancelling", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(0, 65, 196, byte.MaxValue));

        EveryoneCanVent = new BooleanOptionItem(23853, "EveryoneCanVent", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(Color.green);
        OverrideScientistBasedRoles = new BooleanOptionItem(23855, "OverrideScientistBasedRoles", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(EveryoneCanVent);
        WhackAMole = new BooleanOptionItem(23856, "WhackAMole", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(EveryoneCanVent);

        SpawnAdditionalRefugeeOnImpsDead = new BooleanOptionItem(23857, "SpawnAdditionalRefugeeOnImpsDead", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.magenta)
            .SetHeader(true);
        SpawnAdditionalRefugeeWhenNKAlive = new BooleanOptionItem(23858, "SpawnAdditionalRefugeeWhenNKAlive", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.magenta)
            .SetParent(SpawnAdditionalRefugeeOnImpsDead);
        SpawnAdditionalRefugeeMinAlivePlayers = new IntegerOptionItem(23859, "SpawnAdditionalRefugeeMinAlivePlayers", new(1, 14, 1), 7, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.magenta)
            .SetParent(SpawnAdditionalRefugeeOnImpsDead);

        AprilFoolsMode = new BooleanOptionItem(23860, "AprilFoolsMode", Main.IsAprilFools, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));


        // Disable Short Tasks
        DisableShortTasks = new BooleanOptionItem(23000, "DisableShortTasks", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableCleanVent = new BooleanOptionItem(23001, "DisableCleanVent", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCalibrateDistributor = new BooleanOptionItem(23002, "DisableCalibrateDistributor", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableChartCourse = new BooleanOptionItem(23003, "DisableChartCourse", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 79;

        DisableStabilizeSteering = new BooleanOptionItem(23004, "DisableStabilizeSteering", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCleanO2Filter = new BooleanOptionItem(23005, "DisableCleanO2Filter", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableUnlockManifolds = new BooleanOptionItem(23006, "DisableUnlockManifolds", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePrimeShields = new BooleanOptionItem(23007, "DisablePrimeShields", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMeasureWeather = new BooleanOptionItem(23008, "DisableMeasureWeather", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 80;

        DisableBuyBeverage = new BooleanOptionItem(23009, "DisableBuyBeverage", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableAssembleArtifact = new BooleanOptionItem(23010, "DisableAssembleArtifact", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableSortSamples = new BooleanOptionItem(23011, "DisableSortSamples", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableProcessData = new BooleanOptionItem(23012, "DisableProcessData", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRunDiagnostics = new BooleanOptionItem(23013, "DisableRunDiagnostics", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 81;

        DisableRepairDrill = new BooleanOptionItem(23014, "DisableRepairDrill", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableAlignTelescope = new BooleanOptionItem(23015, "DisableAlignTelescope", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRecordTemperature = new BooleanOptionItem(23016, "DisableRecordTemperature", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFillCanisters = new BooleanOptionItem(23017, "DisableFillCanisters", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 82;

        DisableMonitorTree = new BooleanOptionItem(23018, "DisableMonitorTree", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableStoreArtifacts = new BooleanOptionItem(23019, "DisableStoreArtifacts", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePutAwayPistols = new BooleanOptionItem(23020, "DisablePutAwayPistols", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePutAwayRifles = new BooleanOptionItem(23021, "DisablePutAwayRifles", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMakeBurger = new BooleanOptionItem(23022, "DisableMakeBurger", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 83;

        DisableCleanToilet = new BooleanOptionItem(23023, "DisableCleanToilet", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDecontaminate = new BooleanOptionItem(23024, "DisableDecontaminate", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableSortRecords = new BooleanOptionItem(23025, "DisableSortRecords", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFixShower = new BooleanOptionItem(23026, "DisableFixShower", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePickUpTowels = new BooleanOptionItem(23027, "DisablePickUpTowels", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePolishRuby = new BooleanOptionItem(23028, "DisablePolishRuby", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDressMannequin = new BooleanOptionItem(23029, "DisableDressMannequin", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRoastMarshmallow = new BooleanOptionItem(23030, "DisableRoastMarshmallow", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCollectSamples = new BooleanOptionItem(23031, "DisableCollectSamples", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableReplaceParts = new BooleanOptionItem(23032, "DisableReplaceParts", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 84;


        // Disable Common Tasks
        DisableCommonTasks = new BooleanOptionItem(23100, "DisableCommonTasks", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableSwipeCard = new BooleanOptionItem(23101, "DisableSwipeCardTask", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFixWiring = new BooleanOptionItem(23102, "DisableFixWiring", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableEnterIdCode = new BooleanOptionItem(23103, "DisableEnterIdCode", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableInsertKeys = new BooleanOptionItem(23104, "DisableInsertKeys", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableScanBoardingPass = new BooleanOptionItem(23105, "DisableScanBoardingPass", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCollectVegetables = new BooleanOptionItem(23106, "DisableCollectVegetables", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMineOres = new BooleanOptionItem(23107, "DisableMineOres", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableExtractFuel = new BooleanOptionItem(23108, "DisableExtractFuel", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCatchFish = new BooleanOptionItem(23109, "DisableCatchFish", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePolishGem = new BooleanOptionItem(23110, "DisablePolishGem", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableHelpCritter = new BooleanOptionItem(23111, "DisableHelpCritter", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableHoistSupplies = new BooleanOptionItem(23112, "DisableHoistSupplies", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 85;

        // Disable Long Tasks
        DisableLongTasks = new BooleanOptionItem(23150, "DisableLongTasks", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableSubmitScan = new BooleanOptionItem(23151, "DisableSubmitScanTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableUnlockSafe = new BooleanOptionItem(23152, "DisableUnlockSafeTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableStartReactor = new BooleanOptionItem(23153, "DisableStartReactorTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableResetBreaker = new BooleanOptionItem(23154, "DisableResetBreakerTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 86;

        DisableAlignEngineOutput = new BooleanOptionItem(23155, "DisableAlignEngineOutput", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableInspectSample = new BooleanOptionItem(23156, "DisableInspectSample", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableEmptyChute = new BooleanOptionItem(23157, "DisableEmptyChute", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableClearAsteroids = new BooleanOptionItem(23158, "DisableClearAsteroids", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableWaterPlants = new BooleanOptionItem(23159, "DisableWaterPlants", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableOpenWaterways = new BooleanOptionItem(23160, "DisableOpenWaterways", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 87;

        DisableReplaceWaterJug = new BooleanOptionItem(23161, "DisableReplaceWaterJug", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRebootWifi = new BooleanOptionItem(23162, "DisableRebootWifi", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevelopPhotos = new BooleanOptionItem(23163, "DisableDevelopPhotos", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRewindTapes = new BooleanOptionItem(23164, "DisableRewindTapes", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableStartFans = new BooleanOptionItem(23165, "DisableStartFans", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFixAntenna = new BooleanOptionItem(23166, "DisableFixAntenna", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableBuildSandcastle = new BooleanOptionItem(23167, "DisableBuildSandcastle", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 88;

        DisableCrankGenerator = new BooleanOptionItem(23168, "DisableCrankGenerator", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMonitorMushroom = new BooleanOptionItem(23169, "DisableMonitorMushroom", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePlayVideoGame = new BooleanOptionItem(23170, "DisablePlayVideoGame", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFindSignal = new BooleanOptionItem(23171, "DisableFindSignal", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableThrowFisbee = new BooleanOptionItem(23172, "DisableThrowFisbee", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableLiftWeights = new BooleanOptionItem(23173, "DisableLiftWeights", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCollectShells = new BooleanOptionItem(23174, "DisableCollectShells", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 89;


        // Disable Divert Power, Weather Nodes etc. situational Tasks
        DisableOtherTasks = new BooleanOptionItem(23200, "DisableOtherTasks", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableUploadData = new BooleanOptionItem(23205, "DisableUploadDataTask", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableEmptyGarbage = new BooleanOptionItem(23206, "DisableEmptyGarbage", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFuelEngines = new BooleanOptionItem(23207, "DisableFuelEngines", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDivertPower = new BooleanOptionItem(23208, "DisableDivertPower", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableActivateWeatherNodes = new BooleanOptionItem(23209, "DisableActivateWeatherNodes", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 90;
        MainLoadingText = "Building Guesser Mode settings";

        yield return null;

        new TextOptionItem(100022, "MenuTitle.Guessers", TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.yellow)
            .SetHeader(true);
        GuesserMode = new BooleanOptionItem(19700, "GuesserMode", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.yellow)
            .SetHeader(true);
        CrewmatesCanGuess = new BooleanOptionItem(19710, "CrewmatesCanGuess", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        ImpostorsCanGuess = new BooleanOptionItem(19711, "ImpostorsCanGuess", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);

        LoadingPercentage = 91;

        NeutralKillersCanGuess = new BooleanOptionItem(19712, "NeutralKillersCanGuess", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        PassiveNeutralsCanGuess = new BooleanOptionItem(19713, "PassiveNeutralsCanGuess", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        CanGuessAddons = new BooleanOptionItem(19714, "CanGuessAddons", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        CrewCanGuessCrew = new BooleanOptionItem(19715, "CrewCanGuessCrew", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        ImpCanGuessImp = new BooleanOptionItem(19716, "ImpCanGuessImp", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        HideGuesserCommands = new BooleanOptionItem(19717, "GuesserTryHideMsg", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode)
            .SetColor(Color.green);

        GuesserDoesntDieOnMisguess = new BooleanOptionItem(19718, "GuesserDoesntDieOnMisguess", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 92;
        MainLoadingText = "Building game settings";

        new TextOptionItem(100037, "MenuTitle.Meeting", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        SyncButtonMode = new BooleanOptionItem(23300, "SyncButtonMode", false, TabGroup.GameSettings)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue))
            .SetHeader(true)
            .SetGameMode(CustomGameMode.Standard);
        SyncedButtonCount = new IntegerOptionItem(23310, "SyncedButtonCount", new(0, 100, 1), 10, TabGroup.GameSettings)
            .SetParent(SyncButtonMode)
            .SetValueFormat(OptionFormat.Times)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 93;

        AllAliveMeeting = new BooleanOptionItem(23400, "AllAliveMeeting", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));
        AllAliveMeetingTime = new FloatOptionItem(23410, "AllAliveMeetingTime", new(1f, 300f, 1f), 10f, TabGroup.GameSettings)
            .SetParent(AllAliveMeeting)
            .SetValueFormat(OptionFormat.Seconds);

        EnableKillerLeftCommand = new BooleanOptionItem(44428, "EnableKillerLeftCommand", true, TabGroup.GameSettings)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        int i = 0;
        foreach (var s in Enum.GetValues<GameStateInfo>())
        {
            GameStateSettings[s] = new BooleanOptionItem(44429 + i, $"GameStateCommand.Show{s}", true, TabGroup.GameSettings)
                .SetParent(EnableKillerLeftCommand)
                .SetColor(new Color32(147, 241, 240, byte.MaxValue));
            i++;
        }

        MinPlayersForGameStateCommand = new IntegerOptionItem(44438, "MinPlayersForGameStateCommand", new(1, 15, 1), 1, TabGroup.GameSettings)
            .SetParent(EnableKillerLeftCommand)
            .SetValueFormat(OptionFormat.Players)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        SeeEjectedRolesInMeeting = new BooleanOptionItem(44439, "SeeEjectedRolesInMeeting", true, TabGroup.GameSettings)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        LoadingPercentage = 94;


        AdditionalEmergencyCooldown = new BooleanOptionItem(23500, "AdditionalEmergencyCooldown", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));
        AdditionalEmergencyCooldownThreshold = new IntegerOptionItem(23510, "AdditionalEmergencyCooldownThreshold", new(1, 15, 1), 1, TabGroup.GameSettings)
            .SetParent(AdditionalEmergencyCooldown)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);
        AdditionalEmergencyCooldownTime = new FloatOptionItem(23511, "AdditionalEmergencyCooldownTime", new(1f, 60f, 1f), 1f, TabGroup.GameSettings)
            .SetParent(AdditionalEmergencyCooldown)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Seconds);

        LoadingPercentage = 95;

        VoteMode = new BooleanOptionItem(23600, "VoteMode", false, TabGroup.GameSettings)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVote = new StringOptionItem(23610, "WhenSkipVote", VoteModes[..3], 0, TabGroup.GameSettings)
            .SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreFirstMeeting = new BooleanOptionItem(23611, "WhenSkipVoteIgnoreFirstMeeting", false, TabGroup.GameSettings)
            .SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreNoDeadBody = new BooleanOptionItem(23612, "WhenSkipVoteIgnoreNoDeadBody", false, TabGroup.GameSettings)
            .SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreEmergency = new BooleanOptionItem(23613, "WhenSkipVoteIgnoreEmergency", false, TabGroup.GameSettings)
            .SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenNonVote = new StringOptionItem(23700, "WhenNonVote", VoteModes, 0, TabGroup.GameSettings)
            .SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);
        WhenTie = new StringOptionItem(23750, "WhenTie", TieModes, 0, TabGroup.GameSettings)
            .SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 96;


        new TextOptionItem(100028, "MenuTitle.Other", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        LadderDeath = new BooleanOptionItem(23800, "LadderDeath", false, TabGroup.GameSettings)
            .SetHeader(true)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));
        LadderDeathChance = new StringOptionItem(23810, "LadderDeathChance", Rates[1..], 0, TabGroup.GameSettings)
            .SetParent(LadderDeath);

        LoadingPercentage = 97;


        FixFirstKillCooldown = new BooleanOptionItem(23900, "FixFirstKillCooldown", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        StartingKillCooldown = new FloatOptionItem(23950, "StartingKillCooldown", new(1, 60, 1), 18, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        ShieldPersonDiedFirst = new BooleanOptionItem(24000, "ShieldPersonDiedFirst", false, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        LoadingPercentage = 98;


        KillFlashDuration = new FloatOptionItem(24100, "KillFlashDuration", new(0.1f, 0.45f, 0.05f), 0.3f, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        UniqueNeutralRevealScreen = new BooleanOptionItem(24450, "UniqueNeutralRevealScreen", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));


        new TextOptionItem(100029, "MenuTitle.Ghost", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));

        LoadingPercentage = 99;


        GhostCanSeeOtherRoles = new BooleanOptionItem(24300, "GhostCanSeeOtherRoles", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeOtherVotes = new BooleanOptionItem(24400, "GhostCanSeeOtherVotes", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeDeathReason = new BooleanOptionItem(24500, "GhostCanSeeDeathReason", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));

        yield return null;


        AFKDetector.SetupCustomOption();


        new TextOptionItem(100027, "MenuTitle.CTA", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(215, 227, 84, byte.MaxValue));

        CustomTeamManager.LoadCustomTeams();

        LoadingPercentage = 100;

        #endregion

        yield return null;

        OptionSaver.Load();

        IsLoaded = true;

        PostLoadTasks();
    }

    public static void SetupRoleOptions(int id, TabGroup tab, CustomRoles role, CustomGameMode customGameMode = CustomGameMode.Standard, bool zeroOne = false)
    {
        var spawnOption = new StringOptionItem(id, role.ToString(), zeroOne ? RatesZeroOne : Rates, 0, tab).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        CustomRoleSpawnChances.Add(role, spawnOption);

        var countOption = new IntegerOptionItem(id + 1, "Maximum", new(1, 15, 1), 1, tab)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Players)
            .SetGameMode(customGameMode);

        CustomRoleCounts.Add(role, countOption);
    }

    public static void SetupAdtRoleOptions(int id, CustomRoles role, CustomGameMode customGameMode = CustomGameMode.Standard, bool canSetNum = false, TabGroup tab = TabGroup.Addons, bool canSetChance = true, bool teamSpawnOptions = false)
    {
        var spawnOption = new StringOptionItem(id, role.ToString(), RatesZeroOne, 0, tab).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        var countOption = new IntegerOptionItem(id + 1, "Maximum", new(1, canSetNum ? 15 : 1, 1), 1, tab)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Players)
            .SetHidden(!canSetNum)
            .SetGameMode(customGameMode);

        var spawnRateOption = new IntegerOptionItem(id + 2, "AdditionRolesSpawnRate", new(0, 100, 5), canSetChance ? 65 : 100, tab)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Percent)
            .SetHidden(!canSetChance)
            .SetGameMode(customGameMode) as IntegerOptionItem;

        if (teamSpawnOptions)
        {
            var impOption = new BooleanOptionItem(id + 3, "ImpCanBeRole", true, tab)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode)
                .AddReplacement(("{role}", role.ToColoredString()));

            var neutralOption = new BooleanOptionItem(id + 4, "NeutralCanBeRole", true, tab)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode)
                .AddReplacement(("{role}", role.ToColoredString()));

            var crewOption = new BooleanOptionItem(id + 5, "CrewCanBeRole", true, tab)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode)
                .AddReplacement(("{role}", role.ToColoredString()));

            AddonCanBeSettings.Add(role, (impOption, neutralOption, crewOption));
        }

        CustomAdtRoleSpawnRate.Add(role, spawnRateOption);
        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
    }

    public static void SetupSingleRoleOptions(int id, TabGroup tab, CustomRoles role, int count = 1, CustomGameMode customGameMode = CustomGameMode.Standard, bool zeroOne = false, bool hideMaxSetting = true)
    {
        var spawnOption = new StringOptionItem(id, role.ToString(), zeroOne ? RatesZeroOne : Rates, 0, tab).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        var countOption = new IntegerOptionItem(id + 1, "Maximum", new(count, count, count), count, tab)
            .SetParent(spawnOption)
            .SetHidden(hideMaxSetting)
            .SetGameMode(customGameMode);

        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
        SingleRoles.Add(role);
    }

    public static OptionItem CreateCDSetting(int id, TabGroup tab, CustomRoles role, bool isKCD = false) =>
        new FloatOptionItem(id, isKCD ? "KillCooldown" : "AbilityCooldown", new(0f, 180f, 2.5f), 30f, tab)
            .SetParent(CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Seconds);

    public static OptionItem CreatePetUseSetting(int id, CustomRoles role) =>
        new BooleanOptionItem(id, "UsePetInsteadOfKillButton", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[role])
            .SetColor(Color.magenta);

    public static OptionItem CreateVoteCancellingUseSetting(int id, CustomRoles role, TabGroup tab) =>
        new BooleanOptionItem(id, "UseVoteCancellingAfterVote", false, tab)
            .SetParent(CustomRoleSpawnChances[role])
            .SetColor(Color.yellow);

    public class OverrideTasksData
    {
        public static readonly Dictionary<CustomRoles, OverrideTasksData> AllData = [];
        public readonly OptionItem AssignCommonTasks;
        public readonly OptionItem DoOverride;
        public readonly OptionItem NumLongTasks;
        public readonly OptionItem NumShortTasks;

        private OverrideTasksData(int idStart, TabGroup tab, CustomRoles role)
        {
            Dictionary<string, string> replacementDic = new() { { "%role%", role.ToColoredString() } };
            DoOverride = new BooleanOptionItem(idStart++, "doOverride", false, tab)
                .SetParent(CustomRoleSpawnChances[role])
                .SetValueFormat(OptionFormat.None);
            DoOverride.ReplacementDictionary = replacementDic;
            AssignCommonTasks = new BooleanOptionItem(idStart++, "assignCommonTasks", true, tab)
                .SetParent(DoOverride)
                .SetValueFormat(OptionFormat.None);
            AssignCommonTasks.ReplacementDictionary = replacementDic;
            NumLongTasks = new IntegerOptionItem(idStart++, "roleLongTasksNum", new(0, 90, 1), 3, tab)
                .SetParent(DoOverride)
                .SetValueFormat(OptionFormat.Pieces);
            NumLongTasks.ReplacementDictionary = replacementDic;
            NumShortTasks = new IntegerOptionItem(idStart, "roleShortTasksNum", new(0, 90, 1), 3, tab)
                .SetParent(DoOverride)
                .SetValueFormat(OptionFormat.Pieces);
            NumShortTasks.ReplacementDictionary = replacementDic;

            if (!AllData.ContainsKey(role)) AllData.Add(role, this);
            else Logger.Warn("OverrideTasksData created for duplicate CustomRoles", "OverrideTasksData");
        }

        public static OverrideTasksData Create(int idStart, TabGroup tab, CustomRoles role) => new(idStart, tab, role);
    }

    // ReSharper disable NotAccessedField.Global

    // Ability Use Gain With Each Task Completed
    public static OptionItem TimeMasterAbilityUseGainWithEachTaskCompleted;
    public static OptionItem VeteranAbilityUseGainWithEachTaskCompleted;
    public static OptionItem GrenadierAbilityUseGainWithEachTaskCompleted;
    public static OptionItem LighterAbilityUseGainWithEachTaskCompleted;
    public static OptionItem SecurityGuardAbilityUseGainWithEachTaskCompleted;
    public static OptionItem DovesOfNeaceAbilityUseGainWithEachTaskCompleted;

    // Ability Use Gain every 5 seconds
    public static OptionItem GrenadierAbilityChargesWhenFinishedTasks;
    public static OptionItem LighterAbilityChargesWhenFinishedTasks;
    public static OptionItem SecurityGuardAbilityChargesWhenFinishedTasks;
    public static OptionItem DovesOfNeaceAbilityChargesWhenFinishedTasks;
    public static OptionItem TimeMasterAbilityChargesWhenFinishedTasks;
    public static OptionItem VeteranAbilityChargesWhenFinishedTasks;

    // ReSharper restore NotAccessedField.Global
}