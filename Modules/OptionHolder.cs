using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using EHR.Gamemodes;
using EHR.Modules;
using EHR.Roles;
using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace EHR;

[Flags]
public enum CustomGameMode
{
    Standard = 0x01,
    SoloPVP = 0x02,
    FFA = 0x03,
    StopAndGo = 0x04,
    HotPotato = 0x05,
    HideAndSeek = 0x06,
    Speedrun = 0x07,
    CaptureTheFlag = 0x08,
    NaturalDisasters = 0x09,
    RoomRush = 0x0A,
    KingOfTheZones = 0x0B,
    Quiz = 0x0C,
    TheMindGame = 0x0D,
    BedWars = 0x0E,
    Deathrace = 0x0F,
    Mingle = 0x10,
    Snowdown = 0x11,
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
        CovenCount,
        CrewCount,
        RomanticState,
        LoversState,
        Tasks
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    public enum ModLanguages
    {
        UseGameLanguage,
        Hungarian,
        Polish,
        Indonesian,
        Persian
    }

    public static Dictionary<TabGroup, OptionItem[]> GroupedOptions = [];
    public static Dictionary<AddonTypes, List<CustomRoles>> GroupedAddons = [];

    public static OptionItem Preset;
    public static OptionItem GameMode;

    private static readonly string[] GameModes =
    [
        "Standard",
        "SoloPVP",
        "FFA",
        "StopAndGo",
        "HotPotato",
        "HideAndSeek",
        "Speedrun",
        "CaptureTheFlag",
        "NaturalDisasters",
        "RoomRush",
        "KingOfTheZones",
        "Quiz",
        "TheMindGame",
        "BedWars",
        "Deathrace",
        "Mingle",
        "Snowdown"
    ];

    private static Dictionary<CustomRoles, int> roleCounts;
    private static Dictionary<CustomRoles, float> roleSpawnChances;
    public static Dictionary<CustomRoles, OptionItem> CustomRoleCounts;
    public static Dictionary<CustomRoles, StringOptionItem> CustomRoleSpawnChances;
    public static Dictionary<CustomRoles, IntegerOptionItem> CustomAdtRoleSpawnRate;
    
    public static Dictionary<CustomGameMode, OptionItem> GMPollGameModesSettings;
    public static Dictionary<MapNames, OptionItem> MPollMapsSettings;
    public static Dictionary<CustomRoles, OptionItem> CrewAdvancedGameEndCheckingSettings;
    public static OptionItem GuessersKeepTheGameGoing;

    public static OptionItem EnableAutoFactionMinMaxSettings;
    public static readonly List<(OptionItem MinPlayersToActivate, Dictionary<Team, (OptionItem MinSetting, OptionItem MaxSetting)> TeamSettings, OptionItem MinNNKs, OptionItem MaxNNKs)> AutoFactionMinMaxSettings = [];
    public static readonly Dictionary<Team, (OptionItem MinSetting, OptionItem MaxSetting)> FactionMinMaxSettings = [];
    public static readonly Dictionary<RoleOptionType, OptionItem[]> RoleSubCategoryLimits = [];


    public static OptionItem EnableAutoGMRotation;
    public static readonly Dictionary<int, Dictionary<CustomGameMode, OptionItem>> AutoGMRotationRandomGroups = [];
    public static readonly List<(OptionItem Slot, OptionItem Count, OptionItem ExplicitChoice, OptionItem RandomGroupChoice)> AutoGMRotationSlots = [];
    public const int MaxAutoGMRotationRandomGroups = 5;
    public static List<CustomGameMode> AutoGMRotationCompiled = [];
    public static bool AutoGMRotationRecompileOnClose;
    public static int AutoGMRotationIndex;

    public static bool AutoGMRotationEnabled => EnableAutoGMRotation.GetBool() && AutoGMRotationCompiled.Count >= 2;

    public enum AutoGMRoationSlotOptions
    {
        Unused,
        Explicit,
        Random,
        Poll
    }
    

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
        "OnlyCancel",
        "TempBan"
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
        "CamouflageMode.Gurge44",
        "CamouflageMode.TommyXL"
    ];

    public static readonly string[] PetToAssign =
    [
        "pet_Goose",
        "pet_Bedcrab",
        "pet_DancingSkeletonPet",
        "pet_BredPet",
        "pet_YuleGoatPet",
        "pet_Bush",
        "pet_Charles",
        "pet_ChewiePet",
        "pet_clank",
        "pet_coaltonpet",
        "pet_Creb",
        "pet_Cube",
        "pet_lny_dragon",
        "pet_Doggy",
        "pet_Ellie",
        "pet_Strawb",
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
        "pet_Crow",
        "pet_Lava",
        "pet_Crewmate",
        "pet_Mister",
        "pet_nancy",
        "pet_napstamate",
        "pet_Pip",
        "pet_pocketCircuitCar",
        "pet_D2PoukaPet",
        "pet_Pusheen",
        "pet_Pate",
        "pet_Rammy",
        "pet_Robot",
        "pet_Snow",
        "pet_spaceCat",
        "pet_Squig",
        "pet_Stormy",
        "pet_nuggetPet",
        "pet_Charles_Red",
        "pet_UFO",
        "pet_D2WormPet",
        "pet_RANDOM_FOR_EVERYONE"
    ];

    public static OptionItem ModLanguage;

    public static readonly Dictionary<GameStateInfo, OptionItem> GameStateSettings = [];
    public static OptionItem MinPlayersForGameStateCommand;
    public static OptionItem AnonymousKillerCount;

    public static OptionItem DisableMeeting;
    public static OptionItem DisableCloseDoor;
    public static OptionItem DisableSabotage;

    public static OptionItem DisableWhisperCommand;
    public static OptionItem DisableSpectateCommand;
    public static OptionItem Disable8ballCommand;
    public static OptionItem DisableVoteStartCommand;
    public static OptionItem DisableVentingOn1v1;
    public static OptionItem DisableSabotagingOn1v1;

    public static OptionItem DisableReactorOnSkeldAndMira;
    public static OptionItem DisableReactorOnPolus;
    public static OptionItem DisableReactorOnAirship;
    public static OptionItem DisableO2;
    public static OptionItem DisableComms;
    public static OptionItem DisableLights;
    public static OptionItem DisableMushroomMixup;

    public static OptionItem DisableTaskWin;

    public static OptionItem KillFlashDuration;
    public static OptionItem EnableKillerLeftCommand;

    public static OptionItem SeeEjectedRolesInMeeting;
    public static OptionItem EveryoneSeesDeathReasons;
    public static OptionItem HostSeesCommandsEnteredByOthers;

    public static OptionItem DisableShieldAnimations;
    public static OptionItem DisableShapeshiftAnimations;
    public static OptionItem DisableAllShapeshiftAnimations;
    public static OptionItem DisableKillAnimationOnGuess;
    public static OptionItem SabotageCooldownControl;
    public static OptionItem SabotageCooldown;
    public static OptionItem CEMode;
    public static OptionItem ShowImpRemainOnEject;
    public static OptionItem ShowNKRemainOnEject;
    public static OptionItem ShowCovenRemainOnEject;
    public static OptionItem ShowTeamNextToRoleNameOnEject;
    public static OptionItem CheatResponses;
    public static OptionItem EnableMovementChecking;
    public static OptionItem EnableEHRRateLimit;
    public static OptionItem KickOnInvalidRPC;
    public static OptionItem LowLoadMode;
    public static OptionItem DeepLowLoad;

    public static OptionItem MinNNKs;
    public static OptionItem MaxNNKs;

    public static OptionItem CovenReceiveNecronomiconAfterNumMeetings;
    public static OptionItem CovenLeaderSpawns;
    public static OptionItem CovenLeaderKillCooldown;

    public static OptionItem ConfirmEgoistOnEject;
    public static OptionItem ConfirmLoversOnEject;

    public static OptionItem UniqueNeutralRevealScreen;

    public static OptionItem NeutralRoleWinTogether;
    public static OptionItem NeutralWinTogether;
    public static OptionItem NeutralsKnowEachOther;

    public static OptionItem DefaultShapeshiftCooldown;
    public static OptionItem DeadImpCantSabotage;
    public static OptionItem ImpKnowAlliesRole;
    public static OptionItem ImpKnowWhosMadmate;
    public static OptionItem MadmateKnowWhosImp;
    public static OptionItem MadmateKnowWhosMadmate;

    public static OptionItem MinMadmateRoles;
    public static OptionItem MaxMadmateRoles;

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
    public static OptionItem BeartrapBlockMoveTime;
    public static OptionItem InnocentCanWinByImp;
    public static OptionItem BaitNotification;
    public static OptionItem DoctorVisibleToEveryone;
    public static OptionItem LegacyNemesis;
    public static OptionItem BodyguardProtectRadius;
    public static OptionItem BodyguardKillsKiller;
    public static OptionItem WitnessCD;
    public static OptionItem WitnessTime;
    public static OptionItem WitnessUsePet;
    public static OptionItem DQNumOfKillsNeeded;
    public static OptionItem ParanoidNumOfUseButton;
    public static OptionItem ParanoidVentCooldown;
    public static OptionItem ImpKnowSuperStarDead;
    public static OptionItem NeutralKnowSuperStarDead;
    public static OptionItem CovenKnowSuperStarDead;
    public static OptionItem DemolitionistVentTime;
    public static OptionItem DemolitionistKillerDiesOnMeetingCall;
    public static OptionItem ExpressSpeed;
    public static OptionItem ExpressSpeedDur;
    public static OptionItem EveryOneKnowSuperStar;
    public static OptionItem NemesisCanKillNum;
    public static OptionItem ReportBaitAtAllCost;

    public static OptionItem GuesserDoesntDieOnMisguess;
    public static OptionItem CanGuessDuringDiscussionTime;
    public static OptionItem MisguessDeathReason;

    public static OptionItem GuesserMaxKillsPerMeeting;
    public static OptionItem GuesserMaxKillsPerGame;
    public static OptionItem GuesserNumRestrictions;
    public static Dictionary<Team, (OptionItem MinSetting, OptionItem MaxSetting)> NumGuessersOnEachTeam = [];

    public static OptionItem RenegadeKillCD;

    public static OptionItem SkeldChance;
    public static OptionItem MiraChance;
    public static OptionItem PolusChance;
    public static OptionItem DleksChance;
    public static OptionItem AirshipChance;
    public static OptionItem FungleChance;
    public static OptionItem MinPlayersForAirship;
    public static OptionItem MinPlayersForFungle;
    public static OptionItem SpeedForSkeld;
    public static OptionItem SpeedForMira;
    public static OptionItem SpeedForPolus;
    public static OptionItem SpeedForDlesk;
    public static OptionItem SpeedForAirship;
    public static OptionItem SpeedForFungle;

    public static OptionItem GodfatherCancelVote;

    public static OptionItem GuardSpellTimes;
    public static OptionItem CapitalistSkillCooldown;
    public static OptionItem CapitalistKillCooldown;
    public static OptionItem GrenadierSkillCooldown;
    public static OptionItem GrenadierSkillDuration;
    public static OptionItem GrenadierCauseVision;
    public static OptionItem GrenadierCanAffectNeutral;
    public static OptionItem GrenadierSkillMaxOfUsage;
    public static OptionItem LighterVisionNormal;
    public static OptionItem LighterVisionOnLightsOut;
    public static OptionItem LighterSkillCooldown;
    public static OptionItem LighterSkillDuration;
    public static OptionItem LighterSkillMaxOfUsage;
    public static OptionItem SecurityGuardSkillCooldown;
    public static OptionItem SecurityGuardSkillDuration;
    public static OptionItem SecurityGuardSkillMaxOfUsage;
    public static OptionItem ShapeSoulCatcherShapeshiftDuration;
    public static OptionItem SoulCatcherShapeshiftCooldown;
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
    
    public static OptionItem EnterKeyToStartGame;

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
    public static OptionItem PacifistCooldown;
    public static OptionItem PacifistMaxOfUsage;
    public static OptionItem killAttacker;
    public static OptionItem MimicCanSeeDeadRoles;
    public static OptionItem ResetDoorsEveryTurns;
    public static OptionItem DoorsResetMode;
    public static OptionItem ChangeDecontaminationTime;
    public static OptionItem DecontaminationTimeOnMiraHQ;
    public static OptionItem DecontaminationDoorOpenTimeOnMiraHQ;
    public static OptionItem DecontaminationTimeOnPolus;
    public static OptionItem DecontaminationDoorOpenTimeOnPolus;
    public static OptionItem ExtraKillCooldownOnPolus;
    public static OptionItem ExtraKillCooldownOnAirship;
    public static OptionItem ExtraKillCooldownOnFungle;

    public static OptionItem NemesisShapeshiftCD;
    public static OptionItem NemesisShapeshiftDur;

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
    public static OptionItem DisableZiplineForCoven;
    public static OptionItem ZiplineTravelTimeFromBottom;
    public static OptionItem ZiplineTravelTimeFromTop;

    // Sabotage
    public static OptionItem CommsCamouflage;
    public static OptionItem CommsCamouflageDisableOnFungle;
    public static OptionItem CommsCamouflageDisableOnMira;
    public static OptionItem CommsCamouflageLimit;
    public static OptionItem CommsCamouflageLimitSetChance;
    public static OptionItem CommsCamouflageLimitChance;
    public static OptionItem CommsCamouflageLimitSetFrequency;
    public static OptionItem CommsCamouflageLimitFrequency;
    public static OptionItem CommsCamouflageLimitSetMaxTimes;
    public static OptionItem CommsCamouflageLimitMaxTimesPerGame;
    public static OptionItem CommsCamouflageLimitMaxTimesPerRound;
    public static OptionItem CommsCamouflageSetSameSpeed;
    public static OptionItem CommsCamouflagePreventRound1;
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
    public static OptionItem BlockDisturbancesToSwitches;
    public static OptionItem DisableAirshipViewingDeckLightsPanel;
    public static OptionItem DisableAirshipGapRoomLightsPanel;
    public static OptionItem DisableAirshipCargoLightsPanel;
    public static OptionItem WhoWinsBySabotageIfNoImpAlive;
    public static OptionItem IfSelectedTeamIsDead;
    public static OptionItem EnableCustomSabotages;
    public static OptionItem EnableGrabOxygenMaskCustomSabotage;

    // Guesser Mode
    public static OptionItem GuesserMode;
    public static OptionItem CrewmatesCanGuess;
    public static OptionItem ImpostorsCanGuess;
    public static OptionItem NeutralKillersCanGuess;
    public static OptionItem PassiveNeutralsCanGuess;
    public static OptionItem CovenCanGuess;
    public static OptionItem BetrayalAddonsCanGuess;
    public static OptionItem HideGuesserCommands;
    public static OptionItem CanGuessAddons;
    public static OptionItem ImpCanGuessImp;
    public static OptionItem CrewCanGuessCrew;

    public static OptionItem EveryoneCanVent;
    public static OptionItem OverrideOtherCrewBasedRoles;
    public static OptionItem WhackAMole;

    public static OptionItem SpawnAdditionalRenegadeOnImpsDead;
    public static OptionItem SpawnAdditionalRenegadeWhenNKAlive;
    public static OptionItem SpawnAdditionalRenegadeMinAlivePlayers;

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

    public static OptionItem DisablePlayerVotedMessage;

    public static OptionItem LadderDeath;
    public static OptionItem LadderDeathChance;

    public static OptionItem FixFirstKillCooldown;
    public static OptionItem StartingKillCooldown;
    public static OptionItem FallBackKillCooldownValue;
    public static OptionItem ShieldPersonDiedFirst;
    public static OptionItem GhostCanSeeOtherRoles;
    public static OptionItem GhostCanSeeOtherVotes;

    public static OptionItem GhostCanSeeDeathReason;

    public static OptionItem KPDCamouflageMode;

    // Guess Restrictions //
    public static OptionItem PhantomCanGuess;

    public static OptionItem PostLobbyCodeToEHRWebsite;
    public static OptionItem SendHashedPuidToUseLinkedAccount;
    public static OptionItem LobbyUpdateInterval;
    public static OptionItem StoreCompletedAchievementsOnEHRDatabase;
    public static OptionItem AllCrewRolesHaveVanillaColor;
    public static OptionItem MessageRpcSizeLimit;
    public static OptionItem KickSlowJoiningPlayers;
    public static OptionItem EnableAutoMessage;
    public static OptionItem AutoMessageSendInterval;
    public static OptionItem DraftMaxRolesPerPlayer;
    public static OptionItem DraftAffectedByRoleSpawnChances;
    public static OptionItem LargerRoleTextSize;
    public static OptionItem DynamicTaskCountColor;
    public static OptionItem ShowTaskCountWhenAlive;
    public static OptionItem ShowTaskCountWhenDead;
    public static OptionItem IntegrateNaturalDisasters;
    public static OptionItem EnableGameTimeLimit;
    public static OptionItem GameTimeLimit;
    public static OptionItem ShowDifferentEjectionMessageForSomeRoles;
    public static OptionItem ShowAntiBlackoutWarning;
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
    public static OptionItem ShowLongInfo;
    public static OptionItem SendRoleDescriptionFirstMeeting;
    public static OptionItem RoleAssigningAlgorithm;
    public static OptionItem EndWhenPlayerBug;
    public static OptionItem RemovePetsAtDeadPlayers;
    public static OptionItem KickNotJoinedPlayersRegularly;

    public static OptionItem UsePets;
    public static OptionItem PetToAssignToEveryone;
    public static OptionItem AnonymousBodies;
    public static OptionItem EveryoneSeesDeadPlayersRoles;
    public static OptionItem UsePhantomBasis;
    public static OptionItem UsePhantomBasisForNKs;
    public static OptionItem UseMeetingShapeshift;
    public static OptionItem UseMeetingShapeshiftForGuessing;
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
    public static OptionItem ApplyVIPList;
    public static OptionItem ApplyAdminList;
    public static OptionItem AutoWarnStopWords;

    public static OptionItem DIYGameSettings;
    public static OptionItem PlayerCanSetColor;
    public static OptionItem PlayerCanSetName;
    public static OptionItem PlayerCanTPInAndOut;

    // Add-Ons
    public static OptionItem NameDisplayAddons;
    public static OptionItem NameDisplayAddonsOnlyInMeetings;
    public static OptionItem AddBracketsToAddons;
    public static OptionItem NoLimitAddonsNumMax;

    public static OptionItem CharmedCanBeGuessed;
    public static OptionItem ContagiousCanBeGuessed;
    public static OptionItem UndeadCanBeGuessed;
    public static OptionItem EgoistCanBeGuessed;
    public static OptionItem EntrancedCanBeGuessed;

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
    public static OptionItem InvestigatorCanBeMadmate;
    public static OptionItem PresidentCanBeMadmate;

    public static OptionItem MadSnitchTasks;
    public static OptionItem FlashSpeed;
    public static OptionItem GiantSpeed;
    public static OptionItem ImpEgoistVisibalToAllies;
    public static OptionItem VotesPerKill;
    public static OptionItem DualVotes;
    public static OptionItem ImpCanBeLoyal;
    public static OptionItem CrewCanBeLoyal;
    public static OptionItem MinWaitAutoStart;
    public static OptionItem MaxWaitAutoStart;
    public static OptionItem PlayerAutoStart;
    public static OptionItem AutoGMPollCommandAfterJoin;
    public static OptionItem AutoGMPollCommandCooldown;
    public static OptionItem AutoMPollCommandAfterJoin;
    public static OptionItem AutoMPollCommandCooldown;
    public static OptionItem AutoDraftStartCommandAfterJoin;
    public static OptionItem AutoDraftStartCommandCooldown;
    public static OptionItem AutoReadyCheckCommandAfterJoin;
    public static OptionItem AutoReadyCheckCommandCooldown;
    
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

    public static readonly Dictionary<CustomRoles, (OptionItem Imp, OptionItem Neutral, OptionItem Crew, OptionItem Coven)> AddonCanBeSettings = [];
    public static readonly Dictionary<CustomRoles, OptionItem> AddonGuessSettings = [];

    public static readonly HashSet<CustomRoles> SingleRoles = [];

    private static readonly string[] AddonGuessOptions =
    [
        "RoleOn",
        "RoleOff",
        "Untouched"
    ];

    static Options()
    {
        ResetRoleCounts();
        CustomRolesHelper.CanCheck = true;
    }

    public static CustomGameMode CurrentGameMode => GameMode.GetInt() switch
    {
        1 => CustomGameMode.SoloPVP,
        2 => CustomGameMode.FFA,
        3 => CustomGameMode.StopAndGo,
        4 => CustomGameMode.HotPotato,
        5 => CustomGameMode.HideAndSeek,
        6 => CustomGameMode.Speedrun,
        7 => CustomGameMode.CaptureTheFlag,
        8 => CustomGameMode.NaturalDisasters,
        9 => CustomGameMode.RoomRush,
        10 => CustomGameMode.KingOfTheZones,
        11 => CustomGameMode.Quiz,
        12 => CustomGameMode.TheMindGame,
        13 => CustomGameMode.BedWars,
        14 => CustomGameMode.Deathrace,
        15 => CustomGameMode.Mingle,
        16 => CustomGameMode.Snowdown,
        _ => CustomGameMode.Standard
    };

    public static float DefaultKillCooldown = Main.NormalOptions == null ? FallBackKillCooldownValue?.GetFloat() ?? 25f : Main.NormalOptions.KillCooldown;

    public static float AdjustedDefaultKillCooldown => !GameStates.InGame
        ? DefaultKillCooldown
        : DefaultKillCooldown + Main.CurrentMap switch
        {
            MapNames.Polus => ExtraKillCooldownOnPolus?.GetFloat() ?? 0f,
            MapNames.Airship => ExtraKillCooldownOnAirship?.GetFloat() ?? 0f,
            MapNames.Fungle => ExtraKillCooldownOnFungle?.GetFloat() ?? 0f,
            _ => 0f
        };

    [HarmonyPatch(typeof(TranslationController), nameof(TranslationController.Initialize))]
    [HarmonyPostfix]
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
        LoadUserData();
        Achievements.LoadAllData();
        OptionShower.LastText = Translator.GetString("Loading");

        if (AllCrewRolesHaveVanillaColor.GetBool())
        {
            List<CustomRoles> toChange = Main.RoleColors.Keys.Where(x => !x.IsAdditionRole() && x.IsCrewmate() && !x.IsForOtherGameMode()).ToList();
            toChange.ForEach(x => Main.RoleColors[x] = "#8cffff");
        }

        CompileAutoGMRotationSettings();

        foreach (OptionItem optionItem in OptionItem.AllOptions)
        {
            if (optionItem.UpdateValueEventRunsOnLoad)
            {
                int value = optionItem.CurrentValue;
                optionItem.CallUpdateValueEvent(value, value);
            }
        }

#if DEBUG
        // Used for generating the table of roles for the README
        try
        {
            var sb = new StringBuilder();

            var grouped = Enum.GetValues<CustomRoles>().GroupBy(x =>
            {
                if (x is CustomRoles.GM or CustomRoles.NotAssigned or CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor or CustomRoles.Convict or CustomRoles.Hider or CustomRoles.Seeker or CustomRoles.Fox or CustomRoles.Troll or CustomRoles.Jumper or CustomRoles.Detector or CustomRoles.Jet or CustomRoles.Dasher or CustomRoles.Locator or CustomRoles.Agent or CustomRoles.Venter or CustomRoles.Taskinator || x.IsForOtherGameMode() || x.IsVanilla() || x.ToString().Contains("EHR")) return 4;
                if (x == CustomRoles.DoubleAgent) return 2;
                if (x.IsAdditionRole()) return 3;
                if (x.IsImpostor() || x.IsMadmate()) return 0;
                if (x.IsNeutral()) return 1;
                if (x.IsCrewmate()) return 2;
                if (x.IsCoven()) return 5;
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
                var cor = grouped[5].ElementAtOrDefault(i);
                var coven = Translator.GetString(cor.ToString());
                if (cor == default) coven = string.Empty;
                var a = grouped[3].ElementAtOrDefault(i);
                var add = Translator.GetString(a.ToString());
                if (a == default) add = string.Empty;
                sb.AppendLine($"| {crew} | {imp} | {neu} | {coven} | {add} |");
            }

            sb.AppendLine("| | | | | |");
            sb.Append($"| {grouped[2].Length} | {grouped[0].Length} | {grouped[1].Length} | {grouped[5].Length} | {grouped[3].Length} |");

            const string path = "./roles.txt";
            if (!File.Exists(path)) File.Create(path).Close();
            File.WriteAllText(path, sb.ToString());
        }
        catch (Exception e) { Utils.ThrowException(e); }

        // Used for generating the chat command table for the README
        try
        {
            var sb = new StringBuilder();

            sb.AppendLine("| Command | Description | Arguments | Usage Level | Usage Time | Hidden |");
            sb.AppendLine("|---------|-------------|-----------|-------------|------------|--------|");

            foreach ((String key, Command command) in Command.AllCommands)
            {
                string forms = command.CommandForms.TakeWhile(x => x.All(char.IsAscii)).Join(x => $"/{x}", "<br>");
                string description = command.Description;

                string argumentsMarkdown = "";

                if (!string.IsNullOrWhiteSpace(command.Arguments) && command.Arguments.Length > 0)
                {
                    string[] args = command.Arguments.Split(' ');

                    for (int i = 0; i < args.Length; i++)
                    {
                        string arg = args[i];
                        string argName = arg.Trim('{', '}').Trim('[', ']');
                        bool required = arg.StartsWith("{") && arg.EndsWith("}");
                        string argDesc = command.ArgsDescriptions[i];
                        string type = required ? "&#x1F538;" : "&#x1F539;";
                        argumentsMarkdown += $"{type} **{argName}** – {argDesc}<br>";
                    }
                }
                else
                    argumentsMarkdown = "–";

                string usageLevel = command.UsageLevel switch
                {
                    Command.UsageLevels.Everyone => ":purple_circle: Everyone",
                    Command.UsageLevels.Modded => ":green_circle: Modded Clients",
                    Command.UsageLevels.Host => ":yellow_circle: Host",
                    Command.UsageLevels.HostOrModerator => ":red_circle: Host, Moderators, And Admins",
                    Command.UsageLevels.HostOrAdmin => ":white_circle: Host And Admins",
                    _ => string.Empty
                };

                string usageTime = command.UsageTime switch
                {
                    Command.UsageTimes.Always => ":purple_square: Always",
                    Command.UsageTimes.InLobby => ":green_square: In Lobby",
                    Command.UsageTimes.InGame => ":white_large_square: In Game",
                    Command.UsageTimes.InMeeting => ":yellow_square: In Meetings",
                    Command.UsageTimes.AfterDeath => ":red_square: After Death",
                    Command.UsageTimes.AfterDeathOrLobby => ":brown_square: After Death And In Lobby",
                    _ => string.Empty
                };

                string hidden = command.AlwaysHidden ? ":heavy_check_mark:" : ":x:";

                sb.AppendLine($"| {forms} | {description} | {argumentsMarkdown} | {usageLevel} | {usageTime} | {hidden} |");
            }

            sb.AppendLine("| | | | | | |");
            sb.Append($"| {Command.AllCommands.Count} | | | | | |");

            const string path = "./commands.txt";
            if (!File.Exists(path)) File.Create(path).Close();
            File.WriteAllText(path, sb.ToString());
        }
        catch (Exception e) { Utils.ThrowException(e); }


        try
        {
            var sb = new StringBuilder();

            foreach ((TabGroup tab, OptionItem[] options) in GroupedOptions)
            {
                sb.AppendLine($"## {Translator.GetString($"TabGroup.{tab}")}");
                
                sb.AppendLine("| Setting Name | Possible Values | Default Value |");
                sb.AppendLine("|--------------|-----------------|---------------|");

                foreach (OptionItem option in options)
                {
                    if (IsRoleOption() || option.GameMode is not CustomGameMode.Standard and not CustomGameMode.All) continue;

                    bool IsRoleOption()
                    {
                        OptionItem o = option;
                        while (true)
                        {
                            if (o == null) return false;
                            if (Enum.TryParse<CustomRoles>(o.Name, out _)) return true;
                            o = o.Parent;
                        }
                    }
                    
                    string name = option.GetName().RemoveHtmlTags();

                    IList<string> values = option switch
                    {
                        BooleanOptionItem => ["✔️", "❌"],
                        IntegerOptionItem ioi => [$"{ioi.Rule.MinValue} - {ioi.Rule.MaxValue}", $"± {ioi.Rule.Step}"],
                        FloatOptionItem foi => [$"{foi.Rule.MinValue} - {foi.Rule.MaxValue}", $"± {foi.Rule.Step}"],
                        StringOptionItem soi => soi.noTranslation ? soi.Selections : soi.Selections.Select(x => Translator.GetString(x)).ToArray(),
                        _ => []
                    };
                    
                    if (values.Count == 0) continue;
                    
                    string possibleValues = string.Join("<br>", values.Select(x => $"`{x}`"));
                    
                    string defaultValue = option switch
                    {
                        BooleanOptionItem b => b.GetBool() ? "✔️" : "❌",
                        IntegerOptionItem i => i.GetInt().ToString(),
                        FloatOptionItem f => f.GetFloat().ToString("F2"),
                        StringOptionItem s => s.noTranslation ? s.Selections[s.DefaultValue] : Translator.GetString(s.Selections[s.DefaultValue]),
                        _ => string.Empty
                    };

                    sb.AppendLine($"| {name} | {possibleValues} | `{defaultValue}` |");
                }
            }
            
            const string path = "./settings.txt";
            if (!File.Exists(path)) File.Create(path).Close();
            File.WriteAllText(path, sb.ToString());
        }
        catch (Exception e) { Utils.ThrowException(e); }
#endif
    }

    private static void GroupOptions()
    {
        GroupedOptions = OptionItem.AllOptions
            .GroupBy(x => x.Tab)
            .OrderBy(x => (int)x.Key)
            .ToDictionary(x => x.Key, x => x.ToArray());

        CustomHnS.AllHnSRoles = CustomHnS.GetAllHnsRoles(CustomHnS.GetAllHnsRoleTypes());
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

    public static void LoadUserData()
    {
        try
        {
            Main.UserData.Clear();

            var path = $"{Main.DataPath}/EHR_DATA/UserData";

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(path + "/friendcode#1234.txt", JsonSerializer.Serialize(new UserData(), new JsonSerializerOptions { WriteIndented = true }));
            }

            List<string> errors = [];

            foreach (string file in Directory.GetFiles(path, "*.txt"))
            {
                try
                {
                    string content = File.ReadAllText(file);
                    var userData = JsonSerializer.Deserialize<UserData>(content);
                    if (userData == null) throw new FormatException($"The data in {file} was not in the correct format.");
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    Main.UserData[fileName] = userData;
                }
                catch (Exception e)
                {
                    errors.Add($"{file}: {e.Message}");
                }
            }
            
            if (errors.Count > 0)
            {
                errors.Insert(0, "The following errors occurred while loading user data files:");
                Logger.Error(string.Join('\n', errors), "Options", multiLine: true);
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public class UserData
    {
        public bool Vip { get; init; }
        public bool Moderator { get; init; }
        public bool Admin { get; init; }
        public string Tag { get; init; }
    }

    public static VoteMode GetWhenSkipVote()
    {
        return (VoteMode)WhenSkipVote.GetValue();
    }

    public static VoteMode GetWhenNonVote()
    {
        return (VoteMode)WhenNonVote.GetValue();
    }

    public static SuffixModes GetSuffixMode()
    {
        return (SuffixModes)SuffixMode.GetValue();
    }

    private static void ResetRoleCounts()
    {
        roleCounts = [];
        roleSpawnChances = [];

        foreach (CustomRoles role in Enum.GetValues<CustomRoles>())
        {
            roleCounts.Add(role, 0);
            roleSpawnChances.Add(role, 0);
        }
    }

    public static void SetRoleCount(CustomRoles role, int count)
    {
        roleCounts[role] = count;

        if (CustomRoleCounts.TryGetValue(role, out OptionItem option)) option.SetValue(count - 1);
    }

    public static int GetRoleSpawnMode(CustomRoles role)
    {
        return CustomRoleSpawnChances.TryGetValue(role, out StringOptionItem sc) ? sc.GetChance() : 0;
    }

    public static int GetRoleCount(CustomRoles role)
    {
        int mode = GetRoleSpawnMode(role);
        return mode is 0 ? 0 : CustomRoleCounts.TryGetValue(role, out OptionItem option) ? option.GetInt() : roleCounts[role];
    }

    public static float GetRoleChance(CustomRoles role)
    {
        return CustomRoleSpawnChances.TryGetValue(role, out StringOptionItem option) ? option.GetValue() /* / 10f */ : roleSpawnChances[role];
    }

    private static IEnumerator Load()
    {
        LoadingPercentage = 0;
        MainLoadingText = "Building system settings";

        if (IsLoaded) yield break;

        OptionSaver.Initialize();

        yield return null;

        int defaultPresetNumber = OptionSaver.GetDefaultPresetNumber();

        Preset = new PresetOptionItem(defaultPresetNumber, TabGroup.SystemSettings)
            .SetColor(new Color32(255, 235, 4, byte.MaxValue))
            .SetHidden(true);

        GameMode = new StringOptionItem(1, "GameMode", GameModes, 0, TabGroup.GameSettings)
            .SetHidden(true);

        CustomRoleCounts = [];
        CustomRoleSpawnChances = [];
        CustomAdtRoleSpawnRate = [];


        #region RoleListMaker

        var id = 19820;

        foreach (Team team in new[] { Team.Impostor, Team.Neutral, Team.Coven })
        {
            (int Min, int Max) defaultNum = team switch
            {
                Team.Impostor => (1, 1),
                Team.Neutral => (0, 4),
                Team.Coven => (0, 0),
                _ => (0, 15)
            };

            TabGroup tab = team switch
            {
                Team.Impostor => TabGroup.ImpostorRoles,
                Team.Neutral => TabGroup.NeutralRoles,
                Team.Coven => TabGroup.CovenRoles,
                _ => TabGroup.OtherRoles
            };

            OptionItem minSetting = new IntegerOptionItem(id++, $"FactionLimits.{team}.Min", new(0, 15, 1), defaultNum.Min, tab)
                .SetGameMode(CustomGameMode.Standard)
                .SetHeader(true)
                .SetColor(team.GetColor());

            OptionItem maxSetting = new IntegerOptionItem(id++, $"FactionLimits.{team}.Max", new(0, 15, 1), defaultNum.Max, tab)
                .SetGameMode(CustomGameMode.Standard)
                .SetColor(team.GetColor());

            FactionMinMaxSettings[team] = (minSetting, maxSetting);
        }

        MinNNKs = new IntegerOptionItem(id++, "MinNNKs", new(0, 15, 1), 0, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);

        MaxNNKs = new IntegerOptionItem(id++, "MaxNNKs", new(0, 15, 1), 2, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard);

        HashSet<TabGroup> doneTabs = [];

        foreach (RoleOptionType roleOptionType in Enum.GetValues<RoleOptionType>())
        {
            if (roleOptionType is RoleOptionType.Coven_Miscellaneous or RoleOptionType.Impostor_Madmate) continue;

            TabGroup tab = roleOptionType.GetTabFromOptionType();
            Color roleOptionTypeColor = roleOptionType.GetRoleOptionTypeColor();
            var options = new OptionItem[3];

            options[0] = new BooleanOptionItem(id++, $"RoleSubCategoryLimitOptions.{roleOptionType}.EnableLimit", false, tab)
                .SetGameMode(CustomGameMode.Standard)
                .SetHeader(doneTabs.Add(tab))
                .SetColor(roleOptionTypeColor)
                .SetHidden(tab == TabGroup.NeutralRoles);

            options[1] = new IntegerOptionItem(id++, $"RoleSubCategoryLimitOptions.{roleOptionType}.Min", new(0, 15, 1), 0, tab)
                .SetGameMode(CustomGameMode.Standard)
                .SetValueFormat(OptionFormat.Players)
                .SetColor(roleOptionTypeColor);

            options[2] = new IntegerOptionItem(id++, $"RoleSubCategoryLimitOptions.{roleOptionType}.Max", new(0, 15, 1), 1, tab)
                .SetGameMode(CustomGameMode.Standard)
                .SetValueFormat(OptionFormat.Players)
                .SetColor(roleOptionTypeColor);

            if (tab != TabGroup.NeutralRoles) options[1..].Do(x => x.SetParent(options[0]));
            else options[1].SetHeader(true);

            RoleSubCategoryLimits[roleOptionType] = options;
        }

        #endregion

        #region Settings

        MainLoadingText = "Building general settings";


        CovenReceiveNecronomiconAfterNumMeetings = new IntegerOptionItem(650001, "CovenReceiveNecronomiconAfterNumMeetings", new(1, 10, 1), 3, TabGroup.CovenRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);

        CovenLeaderSpawns = new BooleanOptionItem(650002, "CovenLeader", true, TabGroup.CovenRoles)
            .SetGameMode(CustomGameMode.Standard);

        CovenLeaderKillCooldown = new FloatOptionItem(650000, "CovenLeaderKillCooldown", new(0f, 120f, 0.5f), 30f, TabGroup.CovenRoles)
            .SetParent(CovenLeaderSpawns)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Seconds);

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

        RenegadeKillCD = new FloatOptionItem(157, "RenegadeKillCD", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Seconds);

        DefaultShapeshiftCooldown = new FloatOptionItem(200, "DefaultShapeshiftCooldown", new(5f, 180f, 5f), 15f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Seconds);

        DeadImpCantSabotage = new BooleanOptionItem(201, "DeadImpCantSabotage", false, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);

        MinMadmateRoles = new IntegerOptionItem(202, "MinMadmateRoles", new(0, 15, 1), 0, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);

        MaxMadmateRoles = new IntegerOptionItem(203, "MaxMadmateRoles", new(0, 15, 1), 1, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);

        NeutralRoleWinTogether = new BooleanOptionItem(208, "NeutralRoleWinTogether", false, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);

        NeutralWinTogether = new BooleanOptionItem(209, "NeutralWinTogether", false, TabGroup.NeutralRoles)
            .SetParent(NeutralRoleWinTogether)
            .SetGameMode(CustomGameMode.Standard);

        NeutralsKnowEachOther = new BooleanOptionItem(212, "NeutralsKnowEachOther", false, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard);

        NameDisplayAddons = new BooleanOptionItem(210, "NameDisplayAddons", true, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);
        
        NameDisplayAddonsOnlyInMeetings = new BooleanOptionItem(219, "NameDisplayAddonsOnlyInMeetings", false, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard);

        NoLimitAddonsNumMax = new IntegerOptionItem(211, "NoLimitAddonsNumMax", new(1, 90, 1), 1, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard);

        CharmedCanBeGuessed = new StringOptionItem(213, "ConvertedAddonCanBeGuessed", AddonGuessOptions, 2, TabGroup.Addons)
            .SetHeader(true)
            .SetGameMode(CustomGameMode.Standard)
            .AddReplacement(("{role}", CustomRoles.Charmed.ToColoredString()));

        ContagiousCanBeGuessed = new StringOptionItem(215, "ConvertedAddonCanBeGuessed", AddonGuessOptions, 2, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .AddReplacement(("{role}", CustomRoles.Contagious.ToColoredString()));

        UndeadCanBeGuessed = new StringOptionItem(216, "ConvertedAddonCanBeGuessed", AddonGuessOptions, 2, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .AddReplacement(("{role}", CustomRoles.Undead.ToColoredString()));

        EgoistCanBeGuessed = new StringOptionItem(217, "ConvertedAddonCanBeGuessed", AddonGuessOptions, 2, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .AddReplacement(("{role}", CustomRoles.Egoist.ToColoredString()));

        EntrancedCanBeGuessed = new StringOptionItem(218, "ConvertedAddonCanBeGuessed", AddonGuessOptions, 2, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .AddReplacement(("{role}", CustomRoles.Entranced.ToColoredString()));


        RoleLoadingText = "Add-ons\n.";

        yield return null;


        AddBracketsToAddons = new BooleanOptionItem(13500, "BracketAddons", false, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);

        #endregion

        #region Roles/AddOns_Settings

        var titleId = 100100;

        LoadingPercentage = 5;
        MainLoadingText = "Building Add-on Settings";

        Type IAddonType = typeof(IAddon);

        Type[] assemblyTypes = Assembly
            .GetExecutingAssembly()
            .GetTypes();

        Dictionary<AddonTypes, IAddon[]> addonTypes = assemblyTypes
            .Where(t => IAddonType.IsAssignableFrom(t) && !t.IsInterface)
            .OrderBy(t => Translator.GetString(t.Name))
            .Select(type => (IAddon)Activator.CreateInstance(type))
            .Where(x => x != null)
            .GroupBy(x => x.Type)
            .ToDictionary(x => x.Key, x => x.ToArray());

        foreach (KeyValuePair<AddonTypes, IAddon[]> addonType in addonTypes)
        {
            MainLoadingText = $"Building Add-on Settings ({addonType.Key})";
            var index = 0;

            new TextOptionItem(titleId, $"ROT.AddonType.{addonType.Key}", TabGroup.Addons)
                .SetGameMode(CustomGameMode.Standard)
                .SetColor(addonType.Key.GetAddonTypeColor())
                .SetHeader(true);

            titleId += 10;

            foreach (IAddon addon in addonType.Value)
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

        Type IVanillaType = typeof(IVanillaSettingHolder);

        assemblyTypes
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

        Type IType = typeof(IGhostRole);

        assemblyTypes
            .Where(t => IType.IsAssignableFrom(t) && !t.IsInterface)
            .OrderBy(t => Translator.GetString(t.Name))
            .Select(type => (IGhostRole)Activator.CreateInstance(type))
            .Do(x => x.SetupCustomOption());

        Dictionary<RoleOptionType, RoleBase[]> roleClassesDict = Main.AllRoleClasses
            .Where(x => x.GetType().Name != "VanillaRole")
            .GroupBy(x => ((CustomRoles)Enum.Parse(typeof(CustomRoles), ignoreCase: true, value: x.GetType().Name)).GetRoleOptionType())
            .OrderBy(x => (int)x.Key)
            .ToDictionary(x => x.Key, x => x.ToArray());

        foreach (KeyValuePair<RoleOptionType, RoleBase[]> roleClasses in roleClassesDict)
        {
            MainLoadingText = $"Building Role Settings: {roleClasses.Key} Roles";
            int allRoles = roleClasses.Value.Length;
            var index = 0;

            TabGroup tab = roleClasses.Key.GetTabFromOptionType();

            new TextOptionItem(titleId, $"ROT.{roleClasses.Key}", tab)
                .SetHeader(true)
                .SetGameMode(CustomGameMode.Standard)
                .SetColor(roleClasses.Key.GetRoleOptionTypeColor());

            titleId += 10;

            foreach (RoleBase roleClass in roleClasses.Value)
            {
                index++;
                RoleLoadingText = $"{index}/{allRoles} ({roleClass.GetType().Name})";
                Log();

                try { roleClass.SetupCustomOption(); }
                catch (Exception e) { Logger.Exception(e, $"{MainLoadingText} - {RoleLoadingText}"); }

                yield return null;
            }

            yield return null;
        }

        void Log() => Logger.Info(" " + RoleLoadingText, MainLoadingText);


        LoadingPercentage = 60;

        RoleLoadingText = string.Empty;

        #endregion

        #region EHRSettings

        MainLoadingText = "Building EHR settings";

        ModLanguage = new StringOptionItem(19308, "ModLanguage", Enum.GetNames<ModLanguages>(), 0, TabGroup.SystemSettings)
            .SetHeader(true);

        KickLowLevelPlayer = new IntegerOptionItem(19300, "KickLowLevelPlayer", new(0, 100, 1), 0, TabGroup.SystemSettings)
            .SetValueFormat(OptionFormat.Level)
            .SetHeader(true);

        KickAndroidPlayer = new BooleanOptionItem(19301, "KickAndroidPlayer", false, TabGroup.SystemSettings);
        KickPlayerFriendCodeNotExist = new BooleanOptionItem(19302, "KickPlayerFriendCodeNotExist", false, TabGroup.SystemSettings, true);
        ApplyDenyNameList = new BooleanOptionItem(19303, "ApplyDenyNameList", true, TabGroup.SystemSettings, true);
        ApplyBanList = new BooleanOptionItem(19304, "ApplyBanList", true, TabGroup.SystemSettings, true);
        ApplyModeratorList = new BooleanOptionItem(19305, "ApplyModeratorList", true, TabGroup.SystemSettings);
        ApplyVIPList = new BooleanOptionItem(19306, "ApplyVIPList", true, TabGroup.SystemSettings);
        ApplyAdminList = new BooleanOptionItem(19330, "ApplyAdminList", true, TabGroup.SystemSettings);

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
        MinWaitAutoStart = new FloatOptionItem(44420, "MinWaitAutoStart", new(0f, 10f, 0.5f), 2f, TabGroup.SystemSettings);
        MaxWaitAutoStart = new FloatOptionItem(44421, "MaxWaitAutoStart", new(0f, 10f, 0.5f), 6f, TabGroup.SystemSettings);
        PlayerAutoStart = new IntegerOptionItem(44422, "PlayerAutoStart", new(1, 15, 1), 5, TabGroup.SystemSettings);

        AutoStartTimer = new IntegerOptionItem(44423, "AutoStartTimer", new(10, 600, 1), 20, TabGroup.SystemSettings)
            .SetValueFormat(OptionFormat.Seconds);

        AutoPlayAgain = new BooleanOptionItem(44424, "AutoPlayAgain", false, TabGroup.SystemSettings);

        AutoPlayAgainCountdown = new IntegerOptionItem(44425, "AutoPlayAgainCountdown", new(1, 90, 1), 10, TabGroup.SystemSettings)
            .SetParent(AutoPlayAgain);

        AutoGMPollCommandAfterJoin = new BooleanOptionItem(19309, "AutoGMPollCommandAfterJoin", false, TabGroup.SystemSettings)
            .SetHeader(true);

        AutoGMPollCommandCooldown = new IntegerOptionItem(19307, "AutoGMPollCommandCooldown", new(10, 600, 5), 15, TabGroup.SystemSettings)
            .SetParent(AutoGMPollCommandAfterJoin)
            .SetValueFormat(OptionFormat.Seconds);
        
        AutoMPollCommandAfterJoin = new BooleanOptionItem(19335, "AutoMPollCommandAfterJoin", false, TabGroup.SystemSettings);

        AutoMPollCommandCooldown = new IntegerOptionItem(19336, "AutoMPollCommandCooldown", new(10, 600, 5), 90, TabGroup.SystemSettings)
            .SetParent(AutoMPollCommandAfterJoin)
            .SetValueFormat(OptionFormat.Seconds);

        AutoDraftStartCommandAfterJoin = new BooleanOptionItem(19426, "AutoDraftStartCommandAfterJoin", false, TabGroup.SystemSettings);

        AutoDraftStartCommandCooldown = new IntegerOptionItem(19427, "AutoDraftStartCommandCooldown", new(10, 600, 5), 150, TabGroup.SystemSettings)
            .SetParent(AutoDraftStartCommandAfterJoin)
            .SetValueFormat(OptionFormat.Seconds);

        AutoReadyCheckCommandAfterJoin = new BooleanOptionItem(19433, "AutoReadyCheckCommandAfterJoin", false, TabGroup.SystemSettings);

        AutoReadyCheckCommandCooldown = new IntegerOptionItem(19434, "AutoReadyCheckCommandCooldown", new(10, 600, 5), 325, TabGroup.SystemSettings)
            .SetParent(AutoReadyCheckCommandAfterJoin)
            .SetValueFormat(OptionFormat.Seconds);
        
        EnterKeyToStartGame = new BooleanOptionItem(19432, "EnterKeyToStartGame", false, TabGroup.SystemSettings);

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

        KickNotJoinedPlayersRegularly = new BooleanOptionItem(60295, "KickNotJoinedPlayersRegularly", true, TabGroup.SystemSettings)
            .SetColor(Color.yellow);

        CheatResponses = new StringOptionItem(19319, "CheatResponses", CheatResponsesName, 4, TabGroup.SystemSettings)
            .SetHeader(true);

        EnableMovementChecking = new BooleanOptionItem(19329, "EnableMovementChecking", false, TabGroup.SystemSettings)
            .SetHeader(true);

        EnableEHRRateLimit = new BooleanOptionItem(19333, "EnableEHRRateLimit", true, TabGroup.SystemSettings)
            .SetHeader(true);

        KickOnInvalidRPC = new BooleanOptionItem(19334, "KickOnInvalidRPC", true, TabGroup.SystemSettings)
            .SetHeader(true);


        LoadingPercentage = 63;


        AutoDisplayKillLog = new BooleanOptionItem(19321, "AutoDisplayKillLog", true, TabGroup.SystemSettings)
            .SetHeader(true);

        AutoDisplayLastRoles = new BooleanOptionItem(19322, "AutoDisplayLastRoles", true, TabGroup.SystemSettings);
        AutoDisplayLastAddOns = new BooleanOptionItem(19328, "AutoDisplayLastAddOns", true, TabGroup.SystemSettings);
        AutoDisplayLastResult = new BooleanOptionItem(19323, "AutoDisplayLastResult", true, TabGroup.SystemSettings);

        SuffixMode = new StringOptionItem(19324, "SuffixMode", SuffixModes, 0, TabGroup.SystemSettings, true)
            .SetHeader(true);

        HideGameSettings = new BooleanOptionItem(19450, "HideGameSettings", false, TabGroup.SystemSettings);
        DIYGameSettings = new BooleanOptionItem(19471, "DIYGameSettings", false, TabGroup.SystemSettings);
        PlayerCanSetColor = new BooleanOptionItem(19402, "PlayerCanSetColor", false, TabGroup.SystemSettings);
        PlayerCanSetName = new BooleanOptionItem(19410, "PlayerCanSetName", false, TabGroup.SystemSettings);
        PlayerCanTPInAndOut = new BooleanOptionItem(19411, "PlayerCanTPInAndOut", false, TabGroup.SystemSettings);
        FormatNameMode = new StringOptionItem(19403, "FormatNameMode", FormatNameModes, 0, TabGroup.SystemSettings);
        DisableEmojiName = new BooleanOptionItem(19404, "DisableEmojiName", true, TabGroup.SystemSettings);
        ChangeNameToRoleInfo = new BooleanOptionItem(19405, "ChangeNameToRoleInfo", true, TabGroup.SystemSettings);

        ShowLongInfo = new BooleanOptionItem(19420, "ShowLongInfo", true, TabGroup.SystemSettings)
            .SetParent(ChangeNameToRoleInfo);

        SendRoleDescriptionFirstMeeting = new BooleanOptionItem(19406, "SendRoleDescriptionFirstMeeting", true, TabGroup.SystemSettings);

        NoGameEnd = new BooleanOptionItem(19407, "NoGameEnd", false, TabGroup.SystemSettings)
            .SetColor(Color.red);

        AllowConsole = new BooleanOptionItem(19408, "AllowConsole", false, TabGroup.SystemSettings)
            .SetColor(Color.red);

        ShowAntiBlackoutWarning = new BooleanOptionItem(19421, "ShowAntiBlackoutWarning", true, TabGroup.SystemSettings);

        PostLobbyCodeToEHRWebsite = new BooleanOptionItem(19422, "PostLobbyCodeToEHRDiscordServer", true, TabGroup.SystemSettings)
            .SetHeader(true);

        SendHashedPuidToUseLinkedAccount = new BooleanOptionItem(19501, "SendHashedPuidToUseLinkedAccount", true, TabGroup.SystemSettings)
            .SetParent(PostLobbyCodeToEHRWebsite);
        
        LobbyUpdateInterval = new IntegerOptionItem(19502, "LobbyUpdateInterval", new(10, 600, 5), 30, TabGroup.SystemSettings)
            .SetParent(PostLobbyCodeToEHRWebsite)
            .SetValueFormat(OptionFormat.Seconds);

        StoreCompletedAchievementsOnEHRDatabase = new BooleanOptionItem(19423, "StoreCompletedAchievementsOnEHRDatabase", true, TabGroup.SystemSettings);

        AllCrewRolesHaveVanillaColor = new BooleanOptionItem(19424, "AllCrewRolesHaveVanillaColor", false, TabGroup.SystemSettings)
            .SetHeader(true);

        MessageRpcSizeLimit = new IntegerOptionItem(19425, "MessageRpcSizeLimit", new(500, 100000, 100), 1400, TabGroup.SystemSettings)
            .SetHeader(true);

        KickSlowJoiningPlayers = new BooleanOptionItem(19428, "KickSlowJoiningPlayers", false, TabGroup.SystemSettings)
            .SetHeader(true);
        
        EnableAutoMessage = new BooleanOptionItem(19429, "EnableAutoMessage", false, TabGroup.SystemSettings)
            .SetHeader(true);
        
        AutoMessageSendInterval = new IntegerOptionItem(19430, "AutoMessageSendInterval", new(10, 300, 5), 60, TabGroup.SystemSettings)
            .SetParent(EnableAutoMessage)
            .SetValueFormat(OptionFormat.Seconds);

        RoleAssigningAlgorithm = new StringOptionItem(19409, "RoleAssigningAlgorithm", RoleAssigningAlgorithms, 4, TabGroup.SystemSettings, true)
            .SetHeader(true)
            .RegisterUpdateValueEvent((_, _, currentValue) => IRandom.SetInstanceById(currentValue));

        KPDCamouflageMode = new StringOptionItem(19500, "KPDCamouflageMode", CamouflageMode, 0, TabGroup.SystemSettings)
            .SetHeader(true)
            .SetColor(new Color32(255, 192, 203, byte.MaxValue));

        LoadingPercentage = 64;

        #endregion

        yield return null;

        #region Gamemodes

        MainLoadingText = "Building Settings for Other Gamemodes";

        // SoloPVP
        SoloPVP.SetupCustomOption();
        // FFA
        FreeForAll.SetupCustomOption();
        // Move And Stop
        StopAndGo.SetupCustomOption();
        // Hot Potato
        HotPotato.SetupCustomOption();
        // Speedrun
        Speedrun.SetupCustomOption();
        // Hide And Seek
        CustomHnS.SetupCustomOption();
        // Capture The Flag
        CaptureTheFlag.SetupCustomOption();
        // Natural Disasters
        NaturalDisasters.SetupCustomOption();
        // Room Rush
        RoomRush.SetupCustomOption();
        // King Of The Zones
        KingOfTheZones.SetupCustomOption();
        // Quiz
        Quiz.SetupCustomOption();
        // The Mind Game
        TheMindGame.SetupCustomOption();
        // Bed Wars
        BedWars.SetupCustomOption();
        // Deathrace
        Deathrace.SetupCustomOption();
        // Mingle
        Mingle.SetupCustomOption();
        // Snowdown
        Snowdown.SetupCustomOption();

        yield return null;

        #endregion

        #region Game Settings

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
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        ShowCovenRemainOnEject = new BooleanOptionItem(19816, "ShowCovenRemainOnEject", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        
        AnonymousKillerCount = new BooleanOptionItem(44443, "AnonymousKillerCount", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        LoadingPercentage = 66;

        ShowTeamNextToRoleNameOnEject = new BooleanOptionItem(19812, "ShowTeamNextToRoleNameOnEject", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        ConfirmEgoistOnEject = new BooleanOptionItem(19813, "ConfirmEgoistOnEject", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue))
            .SetHeader(true);

        ConfirmLoversOnEject = new BooleanOptionItem(19815, "ConfirmLoversOnEject", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        ShowDifferentEjectionMessageForSomeRoles = new BooleanOptionItem(19817, "ShowDifferentEjectionMessageForSomeRoles", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        LoadingPercentage = 67;


        // Map Settings
        new TextOptionItem(100024, "MenuTitle.MapsSettings", TabGroup.GameSettings)
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
        
        MinPlayersForAirship = new IntegerOptionItem(19915, "MinPlayersForAirship", new(1, 15, 1), 10, TabGroup.GameSettings)
            .SetParent(AirshipChance)
            .SetValueFormat(OptionFormat.Players);
        
        MinPlayersForFungle = new IntegerOptionItem(19923, "MinPlayersForFungle", new(1, 15, 1), 8, TabGroup.GameSettings)
            .SetParent(FungleChance)
            .SetValueFormat(OptionFormat.Players);

        SpeedForSkeld = new FloatOptionItem(20782, "SpeedForSkeld", new(0.05f, 3f, 0.05f), 1.25f, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Multiplier);

        SpeedForMira = new FloatOptionItem(20783, "SpeedForMira", new(0.05f, 3f, 0.05f), 1.25f, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Multiplier);

        SpeedForPolus = new FloatOptionItem(20784, "SpeedForPolus", new(0.05f, 3f, 0.05f), 1.25f, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Multiplier);

        SpeedForDlesk = new FloatOptionItem(20785, "SpeedForDlesk", new(0.05f, 3f, 0.05f), 1.25f, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Multiplier);

        SpeedForAirship = new FloatOptionItem(20786, "SpeedForAirship", new(0.05f, 3f, 0.05f), 1.25f, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Multiplier);

        SpeedForFungle = new FloatOptionItem(20787, "SpeedForFungle", new(0.05f, 3f, 0.05f), 1.25f, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Multiplier);

        LoadingPercentage = 69;


        // Random Spawn
        RandomSpawn = new BooleanOptionItem(22000, "RandomSpawn", false, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        AirshipAdditionalSpawn = new BooleanOptionItem(22010, "AirshipAdditionalSpawn", false, TabGroup.GameSettings)
            .SetParent(RandomSpawn);

        // Airship Variable Electrical
        AirshipVariableElectrical = new BooleanOptionItem(22100, "AirshipVariableElectrical", false, TabGroup.GameSettings)
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

        DisableZiplineForCoven = new BooleanOptionItem(22322, "DisableZiplineForCoven", false, TabGroup.GameSettings)
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
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Decontamination door open time on MiraHQ
        DecontaminationDoorOpenTimeOnMiraHQ = new FloatOptionItem(60509, "DecontaminationDoorOpenTimeOnMiraHQ", new(0.5f, 10f, 0.25f), 3f, TabGroup.GameSettings)
            .SetParent(ChangeDecontaminationTime)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Decontamination time on Polus
        DecontaminationTimeOnPolus = new FloatOptionItem(60505, "DecontaminationTimeOnPolus", new(0.5f, 10f, 0.25f), 3f, TabGroup.GameSettings)
            .SetParent(ChangeDecontaminationTime)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Decontamination door open time on Polus
        DecontaminationDoorOpenTimeOnPolus = new FloatOptionItem(60510, "DecontaminationDoorOpenTimeOnPolus", new(0.5f, 10f, 0.25f), 3f, TabGroup.GameSettings)
            .SetParent(ChangeDecontaminationTime)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        ExtraKillCooldownOnPolus = new FloatOptionItem(60506, "ExtraKillCooldownOnPolus", new(0f, 60f, 0.5f), 0f, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        ExtraKillCooldownOnAirship = new FloatOptionItem(60507, "ExtraKillCooldownOnAirship", new(0f, 60f, 0.5f), 0f, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        ExtraKillCooldownOnFungle = new FloatOptionItem(60508, "ExtraKillCooldownOnFungle", new(0f, 60f, 0.5f), 0f, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Seconds)
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

        CommsCamouflageDisableOnMira = new BooleanOptionItem(22201, "CommsCamouflageDisableOnMira", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflage)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        CommsCamouflageLimit = new BooleanOptionItem(22203, "CommsCamouflageLimit", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflage)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        CommsCamouflageLimitSetChance = new BooleanOptionItem(22204, "CommsCamouflageLimitSetChance", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflageLimit)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        CommsCamouflageLimitChance = new IntegerOptionItem(22205, "CommsCamouflageLimitChance", new(0, 100, 5), 50, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflageLimitSetChance)
            .SetValueFormat(OptionFormat.Percent)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        CommsCamouflageLimitSetFrequency = new BooleanOptionItem(22206, "CommsCamouflageLimitSetFrequency", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflageLimit)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        CommsCamouflageLimitFrequency = new IntegerOptionItem(22207, "CommsCamouflageLimitFrequency", new(1, 10, 1), 2, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflageLimitSetFrequency)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        CommsCamouflageLimitSetMaxTimes = new BooleanOptionItem(22208, "CommsCamouflageLimitSetMaxTimes", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflageLimit)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        CommsCamouflageLimitMaxTimesPerGame = new IntegerOptionItem(22209, "CommsCamouflageLimitMaxTimesPerGame", new(1, 30, 1), 3, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflageLimitSetMaxTimes)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        CommsCamouflageLimitMaxTimesPerRound = new IntegerOptionItem(22210, "CommsCamouflageLimitMaxTimesPerRound", new(1, 10, 1), 1, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflageLimitSetMaxTimes)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));
        
        CommsCamouflageSetSameSpeed = new BooleanOptionItem(22211, "CommsCamouflageSetSameSpeed", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));
        
        CommsCamouflagePreventRound1 = new BooleanOptionItem(22212, "CommsCamouflagePreventRound1", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
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

        LightsOutSpecialSettings = new BooleanOptionItem(22500, "LightsOutSpecialSettings", false, TabGroup.GameSettings)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);

        BlockDisturbancesToSwitches = new BooleanOptionItem(60551, "BlockDisturbancesToSwitches", false, TabGroup.GameSettings)
            .SetParent(LightsOutSpecialSettings)
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

        IList<string> selections = ["SabotageTimeLimitWinners.Imps", "SabotageTimeLimitWinners.NKs", "SabotageTimeLimitWinners.Coven"];

        WhoWinsBySabotageIfNoImpAlive = new StringOptionItem(22520, "NKWinsBySabotageIfNoImpAlive", selections, 0, TabGroup.GameSettings)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);

        IfSelectedTeamIsDead = new StringOptionItem(22521, "IfSelectedTeamIsDead", selections, 0, TabGroup.GameSettings)
            .SetParent(WhoWinsBySabotageIfNoImpAlive)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);

        EnableCustomSabotages = new BooleanOptionItem(22530, "EnableCustomSabotages", false, TabGroup.GameSettings)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);

        EnableGrabOxygenMaskCustomSabotage = new BooleanOptionItem(22531, "EnableGrabOxygenMaskCustomSabotage", false, TabGroup.GameSettings)
            .SetParent(EnableCustomSabotages)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 73;


        new TextOptionItem(100026, "MenuTitle.Disable", TabGroup.GameSettings)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableShieldAnimations = new BooleanOptionItem(22601, "DisableShieldAnimations", true, TabGroup.GameSettings)
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

        DisableTaskWin = new BooleanOptionItem(22650, "DisableTaskWin", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableTaskWinIfAllCrewsAreDead = new BooleanOptionItem(22651, "DisableTaskWinIfAllCrewsAreDead", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableTaskWinIfAllCrewsAreConverted = new BooleanOptionItem(22652, "DisableTaskWinIfAllCrewsAreConverted", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 74;
        
        DisableMeeting = new BooleanOptionItem(22700, "DisableMeeting", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableSabotage = new BooleanOptionItem(22800, "DisableSabotage", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableReactorOnSkeldAndMira = new BooleanOptionItem(22801, "DisableReactorOnSkeldAndMira", false, TabGroup.GameSettings)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableReactorOnPolus = new BooleanOptionItem(22802, "DisableReactorOnPolus", false, TabGroup.GameSettings)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableReactorOnAirship = new BooleanOptionItem(22803, "DisableReactorOnAirship", false, TabGroup.GameSettings)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableO2 = new BooleanOptionItem(22804, "DisableO2", false, TabGroup.GameSettings)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableComms = new BooleanOptionItem(22805, "DisableComms", false, TabGroup.GameSettings)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableLights = new BooleanOptionItem(22806, "DisableLights", false, TabGroup.GameSettings)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableMushroomMixup = new BooleanOptionItem(22807, "DisableMushroomMixup", false, TabGroup.GameSettings)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableCloseDoor = new BooleanOptionItem(22810, "DisableCloseDoor", false, TabGroup.GameSettings)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableWhisperCommand = new BooleanOptionItem(22811, "DisableWhisperCommand", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableSpectateCommand = new BooleanOptionItem(22812, "DisableSpectateCommand", false, TabGroup.GameSettings)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        Disable8ballCommand = new BooleanOptionItem(22813, "Disable8ballCommand", false, TabGroup.GameSettings)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableVoteStartCommand = new BooleanOptionItem(22814, "DisableVoteStartCommand", false, TabGroup.GameSettings)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        
        DisableVentingOn1v1 = new BooleanOptionItem(22815, "DisableVentingOn1v1", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        
        DisableSabotagingOn1v1 = new BooleanOptionItem(22816, "DisableSabotagingOn1v1", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 75;

        DisableDevices = new BooleanOptionItem(22900, "DisableDevices", false, TabGroup.GameSettings)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableSkeldDevices = new BooleanOptionItem(22905, "DisableSkeldDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableSkeldAdmin = new BooleanOptionItem(22906, "DisableSkeldAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableSkeldDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableSkeldCamera = new BooleanOptionItem(22907, "DisableSkeldCamera", false, TabGroup.GameSettings)
            .SetParent(DisableSkeldDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 76;

        DisableMiraHQDevices = new BooleanOptionItem(22908, "DisableMiraHQDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableMiraHQAdmin = new BooleanOptionItem(22909, "DisableMiraHQAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableMiraHQDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableMiraHQDoorLog = new BooleanOptionItem(22910, "DisableMiraHQDoorLog", false, TabGroup.GameSettings)
            .SetParent(DisableMiraHQDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisablePolusDevices = new BooleanOptionItem(22911, "DisablePolusDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisablePolusAdmin = new BooleanOptionItem(22912, "DisablePolusAdmin", false, TabGroup.GameSettings)
            .SetParent(DisablePolusDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisablePolusCamera = new BooleanOptionItem(22913, "DisablePolusCamera", false, TabGroup.GameSettings)
            .SetParent(DisablePolusDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisablePolusVital = new BooleanOptionItem(22914, "DisablePolusVital", false, TabGroup.GameSettings)
            .SetParent(DisablePolusDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableAirshipDevices = new BooleanOptionItem(22915, "DisableAirshipDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableAirshipCockpitAdmin = new BooleanOptionItem(22916, "DisableAirshipCockpitAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 77;

        DisableAirshipRecordsAdmin = new BooleanOptionItem(22917, "DisableAirshipRecordsAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableAirshipCamera = new BooleanOptionItem(22918, "DisableAirshipCamera", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableAirshipVital = new BooleanOptionItem(22919, "DisableAirshipVital", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableFungleDevices = new BooleanOptionItem(22925, "DisableFungleDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableFungleCamera = new BooleanOptionItem(22926, "DisableFungleCamera", false, TabGroup.GameSettings)
            .SetParent(DisableFungleDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableFungleVital = new BooleanOptionItem(22927, "DisableFungleVital", false, TabGroup.GameSettings)
            .SetParent(DisableFungleDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableDevicesIgnoreConditions = new BooleanOptionItem(22920, "IgnoreConditions", false, TabGroup.GameSettings)
            .SetParent(DisableDevices)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableDevicesIgnoreImpostors = new BooleanOptionItem(22921, "IgnoreImpostors", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableDevicesIgnoreNeutrals = new BooleanOptionItem(22922, "IgnoreNeutrals", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableDevicesIgnoreCrewmates = new BooleanOptionItem(22923, "IgnoreCrewmates", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableDevicesIgnoreAfterAnyoneDied = new BooleanOptionItem(22924, "IgnoreAfterAnyoneDied", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 78;

        #endregion

        #region Task Settings

        UsePets = new BooleanOptionItem(23850, "UsePets", false, TabGroup.TaskSettings)
            .SetHeader(true)
            .SetColor(new Color32(60, 0, 255, byte.MaxValue));

        PetToAssignToEveryone = new StringOptionItem(23854, "PetToAssign", PetToAssign, 18, TabGroup.TaskSettings)
            .SetParent(UsePets)
            .SetColor(new Color32(60, 0, 255, byte.MaxValue));

        AnonymousBodies = new BooleanOptionItem(23852, "AnonymousBodies", false, TabGroup.TaskSettings)
            .SetHeader(true)
            .SetColor(new Color32(0, 165, 255, byte.MaxValue));

        EveryoneSeesDeadPlayersRoles = new BooleanOptionItem(23861, "EveryoneSeesDeadPlayersRoles", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));

        UsePhantomBasis = new BooleanOptionItem(23851, "UsePhantomBasis", true, TabGroup.TaskSettings)
            .SetHeader(true)
            .SetColor(new Color32(255, 255, 44, byte.MaxValue));

        UsePhantomBasisForNKs = new BooleanOptionItem(23864, "UsePhantomBasisForNKs", true, TabGroup.TaskSettings)
            .SetParent(UsePhantomBasis)
            .SetColor(new Color32(255, 255, 44, byte.MaxValue));

        UseMeetingShapeshift = new BooleanOptionItem(23865, "UseMeetingShapeshift", true, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(Palette.Orange);

        UseMeetingShapeshiftForGuessing = new BooleanOptionItem(23866, "UseMeetingShapeshiftForGuessing", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(UseMeetingShapeshift)
            .SetColor(Palette.Orange);

        EveryoneCanVent = new BooleanOptionItem(23853, "EveryoneCanVent", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(Color.green);

        OverrideOtherCrewBasedRoles = new BooleanOptionItem(23855, "OverrideOtherCrewBasedRoles", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(EveryoneCanVent);

        WhackAMole = new BooleanOptionItem(23856, "WhackAMole", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(EveryoneCanVent);

        SpawnAdditionalRenegadeOnImpsDead = new BooleanOptionItem(23857, "SpawnAdditionalRenegadeOnImpsDead", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.magenta)
            .SetHeader(true);

        SpawnAdditionalRenegadeWhenNKAlive = new BooleanOptionItem(23858, "SpawnAdditionalRenegadeWhenNKAlive", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.magenta)
            .SetParent(SpawnAdditionalRenegadeOnImpsDead);

        SpawnAdditionalRenegadeMinAlivePlayers = new IntegerOptionItem(23859, "SpawnAdditionalRenegadeMinAlivePlayers", new(1, 14, 1), 7, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.magenta)
            .SetParent(SpawnAdditionalRenegadeOnImpsDead);

        AprilFoolsMode = new BooleanOptionItem(23860, "AprilFoolsMode", Main.IsAprilFools, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHidden(true)
            .SetHeader(true)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));


        // Disable Short Tasks
        DisableShortTasks = new BooleanOptionItem(23000, "DisableShortTasks", false, TabGroup.TaskSettings)
            .SetHeader(true)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));

        DisableCleanVent = new BooleanOptionItem(23001, "DisableCleanVent", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableCalibrateDistributor = new BooleanOptionItem(23002, "DisableCalibrateDistributor", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableChartCourse = new BooleanOptionItem(23003, "DisableChartCourse", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        LoadingPercentage = 79;

        DisableStabilizeSteering = new BooleanOptionItem(23004, "DisableStabilizeSteering", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableCleanO2Filter = new BooleanOptionItem(23005, "DisableCleanO2Filter", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableUnlockManifolds = new BooleanOptionItem(23006, "DisableUnlockManifolds", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisablePrimeShields = new BooleanOptionItem(23007, "DisablePrimeShields", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableMeasureWeather = new BooleanOptionItem(23008, "DisableMeasureWeather", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        LoadingPercentage = 80;

        DisableBuyBeverage = new BooleanOptionItem(23009, "DisableBuyBeverage", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableAssembleArtifact = new BooleanOptionItem(23010, "DisableAssembleArtifact", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableSortSamples = new BooleanOptionItem(23011, "DisableSortSamples", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableProcessData = new BooleanOptionItem(23012, "DisableProcessData", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableRunDiagnostics = new BooleanOptionItem(23013, "DisableRunDiagnostics", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        LoadingPercentage = 81;

        DisableRepairDrill = new BooleanOptionItem(23014, "DisableRepairDrill", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableAlignTelescope = new BooleanOptionItem(23015, "DisableAlignTelescope", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableRecordTemperature = new BooleanOptionItem(23016, "DisableRecordTemperature", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableFillCanisters = new BooleanOptionItem(23017, "DisableFillCanisters", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        LoadingPercentage = 82;

        DisableMonitorTree = new BooleanOptionItem(23018, "DisableMonitorTree", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableStoreArtifacts = new BooleanOptionItem(23019, "DisableStoreArtifacts", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisablePutAwayPistols = new BooleanOptionItem(23020, "DisablePutAwayPistols", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisablePutAwayRifles = new BooleanOptionItem(23021, "DisablePutAwayRifles", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableMakeBurger = new BooleanOptionItem(23022, "DisableMakeBurger", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        LoadingPercentage = 83;

        DisableCleanToilet = new BooleanOptionItem(23023, "DisableCleanToilet", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableDecontaminate = new BooleanOptionItem(23024, "DisableDecontaminate", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableSortRecords = new BooleanOptionItem(23025, "DisableSortRecords", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableFixShower = new BooleanOptionItem(23026, "DisableFixShower", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisablePickUpTowels = new BooleanOptionItem(23027, "DisablePickUpTowels", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisablePolishRuby = new BooleanOptionItem(23028, "DisablePolishRuby", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableDressMannequin = new BooleanOptionItem(23029, "DisableDressMannequin", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableRoastMarshmallow = new BooleanOptionItem(23030, "DisableRoastMarshmallow", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableCollectSamples = new BooleanOptionItem(23031, "DisableCollectSamples", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        DisableReplaceParts = new BooleanOptionItem(23032, "DisableReplaceParts", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks);

        LoadingPercentage = 84;


        // Disable Common Tasks
        DisableCommonTasks = new BooleanOptionItem(23100, "DisableCommonTasks", false, TabGroup.TaskSettings)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));

        DisableSwipeCard = new BooleanOptionItem(23101, "DisableSwipeCardTask", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisableFixWiring = new BooleanOptionItem(23102, "DisableFixWiring", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisableEnterIdCode = new BooleanOptionItem(23103, "DisableEnterIdCode", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisableInsertKeys = new BooleanOptionItem(23104, "DisableInsertKeys", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisableScanBoardingPass = new BooleanOptionItem(23105, "DisableScanBoardingPass", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisableCollectVegetables = new BooleanOptionItem(23106, "DisableCollectVegetables", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisableMineOres = new BooleanOptionItem(23107, "DisableMineOres", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisableExtractFuel = new BooleanOptionItem(23108, "DisableExtractFuel", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisableCatchFish = new BooleanOptionItem(23109, "DisableCatchFish", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisablePolishGem = new BooleanOptionItem(23110, "DisablePolishGem", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisableHelpCritter = new BooleanOptionItem(23111, "DisableHelpCritter", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        DisableHoistSupplies = new BooleanOptionItem(23112, "DisableHoistSupplies", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks);

        LoadingPercentage = 85;

        // Disable Long Tasks
        DisableLongTasks = new BooleanOptionItem(23150, "DisableLongTasks", false, TabGroup.TaskSettings)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));

        DisableSubmitScan = new BooleanOptionItem(23151, "DisableSubmitScanTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableUnlockSafe = new BooleanOptionItem(23152, "DisableUnlockSafeTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableStartReactor = new BooleanOptionItem(23153, "DisableStartReactorTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableResetBreaker = new BooleanOptionItem(23154, "DisableResetBreakerTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        LoadingPercentage = 86;

        DisableAlignEngineOutput = new BooleanOptionItem(23155, "DisableAlignEngineOutput", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableInspectSample = new BooleanOptionItem(23156, "DisableInspectSample", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableEmptyChute = new BooleanOptionItem(23157, "DisableEmptyChute", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableClearAsteroids = new BooleanOptionItem(23158, "DisableClearAsteroids", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableWaterPlants = new BooleanOptionItem(23159, "DisableWaterPlants", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableOpenWaterways = new BooleanOptionItem(23160, "DisableOpenWaterways", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        LoadingPercentage = 87;

        DisableReplaceWaterJug = new BooleanOptionItem(23161, "DisableReplaceWaterJug", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableRebootWifi = new BooleanOptionItem(23162, "DisableRebootWifi", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableDevelopPhotos = new BooleanOptionItem(23163, "DisableDevelopPhotos", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableRewindTapes = new BooleanOptionItem(23164, "DisableRewindTapes", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableStartFans = new BooleanOptionItem(23165, "DisableStartFans", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableFixAntenna = new BooleanOptionItem(23166, "DisableFixAntenna", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableBuildSandcastle = new BooleanOptionItem(23167, "DisableBuildSandcastle", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        LoadingPercentage = 88;

        DisableCrankGenerator = new BooleanOptionItem(23168, "DisableCrankGenerator", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableMonitorMushroom = new BooleanOptionItem(23169, "DisableMonitorMushroom", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisablePlayVideoGame = new BooleanOptionItem(23170, "DisablePlayVideoGame", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableFindSignal = new BooleanOptionItem(23171, "DisableFindSignal", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableThrowFisbee = new BooleanOptionItem(23172, "DisableThrowFisbee", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableLiftWeights = new BooleanOptionItem(23173, "DisableLiftWeights", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        DisableCollectShells = new BooleanOptionItem(23174, "DisableCollectShells", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks);

        LoadingPercentage = 89;


        // Disable Divert Power, Weather Nodes etc. situational Tasks
        DisableOtherTasks = new BooleanOptionItem(23200, "DisableOtherTasks", false, TabGroup.TaskSettings)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));

        DisableUploadData = new BooleanOptionItem(23205, "DisableUploadDataTask", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks);

        DisableEmptyGarbage = new BooleanOptionItem(23206, "DisableEmptyGarbage", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks);

        DisableFuelEngines = new BooleanOptionItem(23207, "DisableFuelEngines", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks);

        DisableDivertPower = new BooleanOptionItem(23208, "DisableDivertPower", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks);

        DisableActivateWeatherNodes = new BooleanOptionItem(23209, "DisableActivateWeatherNodes", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks);

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

        CovenCanGuess = new BooleanOptionItem(19735, "CovenCanGuess", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);

        BetrayalAddonsCanGuess = new BooleanOptionItem(19719, "BetrayalAddonsCanGuess", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);

        CanGuessAddons = new BooleanOptionItem(19714, "CanGuessAddons", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode);

        CrewCanGuessCrew = new BooleanOptionItem(19715, "CrewCanGuessCrew", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode);

        ImpCanGuessImp = new BooleanOptionItem(19716, "ImpCanGuessImp", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode);

        GuesserMaxKillsPerMeeting = new IntegerOptionItem(19720, "GuesserMaxKillsPerMeeting", new(1, 15, 1), 15, TabGroup.TaskSettings)
            .SetParent(GuesserMode)
            .SetValueFormat(OptionFormat.Players);

        GuesserMaxKillsPerGame = new IntegerOptionItem(19721, "GuesserMaxKillsPerGame", new(1, 15, 1), 15, TabGroup.TaskSettings)
            .SetParent(GuesserMode)
            .SetValueFormat(OptionFormat.Players);

        GuesserNumRestrictions = new BooleanOptionItem(19722, "GuesserNumRestrictions", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);

        var goId = 19723;

        NumGuessersOnEachTeam = Enum.GetValues<Team>()[1..].ToDictionary(x => x, x =>
        {
            Color teamColor = x.GetColor();

            OptionItem min = new IntegerOptionItem(goId++, $"NumGuessersOn.{x}.Min", new(1, 15, 1), 15, TabGroup.TaskSettings)
                .SetParent(GuesserNumRestrictions)
                .SetValueFormat(OptionFormat.Players)
                .SetColor(teamColor);

            OptionItem max = new IntegerOptionItem(goId++, $"NumGuessersOn.{x}.Max", new(1, 15, 1), 15, TabGroup.TaskSettings)
                .SetParent(GuesserNumRestrictions)
                .SetValueFormat(OptionFormat.Players)
                .SetColor(teamColor);

            return (min, max);
        });

        HideGuesserCommands = new BooleanOptionItem(19717, "GuesserTryHideMsg", true, TabGroup.TaskSettings)
            .SetColor(Color.green)
            .SetParent(GuesserMode);

        GuesserDoesntDieOnMisguess = new BooleanOptionItem(19718, "GuesserDoesntDieOnMisguess", false, TabGroup.TaskSettings)
            .SetColor(Color.yellow)
            .SetGameMode(CustomGameMode.Standard);

        CanGuessDuringDiscussionTime = new BooleanOptionItem(19799, "CanGuessDuringDiscussionTime", true, TabGroup.TaskSettings)
            .SetColor(Color.yellow)
            .SetGameMode(CustomGameMode.Standard);
        
        MisguessDeathReason = new BooleanOptionItem(44444, "MisguessDeathReason", false, TabGroup.TaskSettings)
            .SetColor(Color.yellow)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 92;
        MainLoadingText = "Building game settings";

        #endregion

        #region More Game Settings

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
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        var i = 0;

        foreach (GameStateInfo s in Enum.GetValues<GameStateInfo>())
        {
            GameStateSettings[s] = new BooleanOptionItem(44429 + i, $"GameStateCommand.Show{s}", true, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.Standard)
                .SetParent(EnableKillerLeftCommand)
                .SetColor(new Color32(147, 241, 240, byte.MaxValue));

            i++;
        }

        MinPlayersForGameStateCommand = new IntegerOptionItem(44442, "MinPlayersForGameStateCommand", new(1, 15, 1), 1, TabGroup.GameSettings)
            .SetParent(EnableKillerLeftCommand)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        SeeEjectedRolesInMeeting = new BooleanOptionItem(44439, "SeeEjectedRolesInMeeting", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        EveryoneSeesDeathReasons = new BooleanOptionItem(44440, "EveryoneSeesDeathReasons", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        HostSeesCommandsEnteredByOthers = new BooleanOptionItem(44441, "HostSeesCommandsEnteredByOthers", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
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

        DisablePlayerVotedMessage = new BooleanOptionItem(23512, "DisablePlayerVotedMessage", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

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
            .SetHeader(true)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        LadderDeath = new BooleanOptionItem(23800, "LadderDeath", false, TabGroup.GameSettings)
            .SetHeader(true)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        LadderDeathChance = new StringOptionItem(23810, "LadderDeathChance", Rates[1..], 0, TabGroup.GameSettings)
            .SetParent(LadderDeath);

        LoadingPercentage = 97;


        FixFirstKillCooldown = new BooleanOptionItem(23900, "FixFirstKillCooldown", false, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        StartingKillCooldown = new FloatOptionItem(23950, "StartingKillCooldown", new(1, 60, 1), 10, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        FallBackKillCooldownValue = new FloatOptionItem(23951, "KillCooldown", new(0.5f, 60f, 0.5f), 25f, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        ShieldPersonDiedFirst = new BooleanOptionItem(24000, "ShieldPersonDiedFirst", false, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        LoadingPercentage = 98;


        KillFlashDuration = new FloatOptionItem(24100, "KillFlashDuration", new(0.1f, 0.45f, 0.05f), 0.3f, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        UniqueNeutralRevealScreen = new BooleanOptionItem(24450, "UniqueNeutralRevealScreen", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        DraftMaxRolesPerPlayer = new IntegerOptionItem(19431, "DraftMaxRolesPerPlayer", new(1, 30, 1), 5, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));
        
        DraftAffectedByRoleSpawnChances = new BooleanOptionItem(19435, "DraftAffectedByRoleSpawnChances", false, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        LargerRoleTextSize = new BooleanOptionItem(24451, "LargerRoleTextSize", false, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));
        
        DynamicTaskCountColor = new BooleanOptionItem(24557, "DynamicTaskCountColor", false, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        ShowTaskCountWhenAlive = new BooleanOptionItem(24452, "ShowTaskCountWhenAlive", true, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        ShowTaskCountWhenDead = new BooleanOptionItem(24453, "ShowTaskCountWhenDead", true, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        IntegrateNaturalDisasters = new BooleanOptionItem(24454, "IntegrateNaturalDisasters", false, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .RegisterUpdateValueEvent((_, _, _) => GameOptionsMenuPatch.ReloadUI());

        EnableGameTimeLimit = new BooleanOptionItem(24455, "EnableGameTimeLimit", false, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetHeader(true);
        
        GameTimeLimit = new FloatOptionItem(24456, "GameTimeLimit", new(20f, 3600f, 20f), 900f, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetParent(EnableGameTimeLimit)
            .SetValueFormat(OptionFormat.Seconds);


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

        #endregion

        #region CTA

        new TextOptionItem(100027, "MenuTitle.CTA", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(215, 227, 84, byte.MaxValue));

        CustomTeamManager.LoadCustomTeams();

        LoadingPercentage = 100;

        #endregion

        yield return null;
        
        id = 68000;
        
        new TextOptionItem(110040, "MenuTitle.AutoFactionMinMaxSettings", TabGroup.SystemSettings)
            .SetHeader(true);

        EnableAutoFactionMinMaxSettings = new BooleanOptionItem(id++, "EnableAutoFactionMinMaxSettings", false, TabGroup.SystemSettings)
            .SetHeader(true)
            .SetColor(Color.cyan)
            .SetGameMode(CustomGameMode.Standard);

        for (int index = 0; index < 10; index++)
        {
            OptionItem minPlayers = new IntegerOptionItem(id++, "AutoFactionMinMaxSettings.PlayerCap", new(0, 30, 1), 0, TabGroup.SystemSettings)
                .SetParent(EnableAutoFactionMinMaxSettings);
            
            Dictionary<Team, (OptionItem MinSetting, OptionItem MaxSetting)> settings = new();
            
            foreach (Team team in new[] { Team.Impostor, Team.Coven, Team.Neutral })
            {
                OptionItem minSetting = new IntegerOptionItem(id++, $"FactionLimits.{team}.Min", new(0, 15, 1), 0, TabGroup.SystemSettings)
                    .SetParent(minPlayers)
                    .SetColor(team.GetColor());

                OptionItem maxSetting = new IntegerOptionItem(id++, $"FactionLimits.{team}.Max", new(0, 15, 1), 0, TabGroup.SystemSettings)
                    .SetParent(minPlayers)
                    .SetColor(team.GetColor());

                settings[team] = (minSetting, maxSetting);
            }

            OptionItem minNNKs = new IntegerOptionItem(id++, "MinNNKs", new(0, 15, 1), 0, TabGroup.SystemSettings)
                .SetParent(minPlayers);
            
            OptionItem maxNNKs = new IntegerOptionItem(id++, "MaxNNKs", new(0, 15, 1), 0, TabGroup.SystemSettings)
                .SetParent(minPlayers);
            
            AutoFactionMinMaxSettings.Add((minPlayers, settings, minNNKs, maxNNKs));
        }

        id = 69900;

        new TextOptionItem(110020, "MenuTitle.GMPoll", TabGroup.SystemSettings)
            .SetHeader(true);
        
        GMPollGameModesSettings = Enum.GetValues<CustomGameMode>()[..^1].ToDictionary(x => x, x => new BooleanOptionItem(id++, "GMPoll.Allow", true, TabGroup.SystemSettings)
            .SetColor(Main.GameModeColors[x])
            .SetHeader(x == CustomGameMode.Standard)
            .AddReplacement(("{gm}", Translator.GetString($"{x}"))));

        id = 69920;

        new TextOptionItem(110030, "MenuTitle.MPoll", TabGroup.SystemSettings)
            .SetHeader(true);

        MPollMapsSettings = Enum.GetValues<MapNames>().ToDictionary(x => x, x => new BooleanOptionItem(id++, "MPoll.Allow", true, TabGroup.SystemSettings)
            .SetHeader(x == MapNames.Skeld)
            .AddReplacement(("{m}", Translator.GetString($"{x}"))));
        
        id = 69935;

        CrewAdvancedGameEndCheckingSettings = [];
        bool first = true;

        new TextOptionItem(110000, "MenuTitle.CrewAdvancedGameEndChecking", TabGroup.SystemSettings)
            .SetHeader(true);

        foreach (RoleBase roleBase in Main.AllRoleClasses)
        {
            Type type = roleBase.GetType();
            if (type.GetMethod(nameof(roleBase.ManipulateGameEndCheckCrew))?.DeclaringType != type) continue;
            CustomRoles role = Enum.Parse<CustomRoles>(type.Name, true);
            
            CrewAdvancedGameEndCheckingSettings[role] = new BooleanOptionItem(id++, "CrewAdvancedGameEndChecking", true, TabGroup.SystemSettings)
                .SetHeader(first)
                .AddReplacement(("{role}", role.ToColoredString()));
            
            first = false;
        }
        
        GuessersKeepTheGameGoing = new BooleanOptionItem(id, "GuessersKeepTheGameGoing", false, TabGroup.SystemSettings)
            .SetHeader(true)
            .SetColor(Color.yellow);

        id = 69000;

        new TextOptionItem(110010, "MenuTitle.AutoGMRotation", TabGroup.SystemSettings)
            .SetHeader(true);

        EnableAutoGMRotation = new BooleanOptionItem(id++, "EnableAutoGMRotation", false, TabGroup.SystemSettings)
            .SetColor(Color.magenta)
            .SetHeader(true);

        Action<OptionItem, int, int> setRecompileNeeded = (_, _, _) => AutoGMRotationRecompileOnClose = true;

        for (var index = 0; index < 20; index++)
        {
            OptionItem slot = new StringOptionItem(id++, "AutoGMRotationSlot", Enum.GetNames<AutoGMRoationSlotOptions>().Select(x => $"AGMR.{x}").ToArray(), 0, TabGroup.SystemSettings)
                .SetParent(EnableAutoGMRotation)
                .SetHeader(index == 0)
                .AddReplacement(("{index}", (index + 1).ToString()))
                .RegisterUpdateValueEvent(setRecompileNeeded)
                .SetRunEventOnLoad(true);

            OptionItem explicitGM = new StringOptionItem(id++, "GameMode", GameModes, 0, TabGroup.SystemSettings)
                .SetParent(slot)
                .RegisterUpdateValueEvent(setRecompileNeeded)
                .SetRunEventOnLoad(true);

            OptionItem randomGroup = new IntegerOptionItem(id++, "AGMR.Slot.RandomGroupId", new(1, MaxAutoGMRotationRandomGroups, 1), 1, TabGroup.SystemSettings)
                .SetParent(slot)
                .RegisterUpdateValueEvent(setRecompileNeeded)
                .SetRunEventOnLoad(true);

            OptionItem count = new IntegerOptionItem(id++, "AGMR.Slot.Count", new(1, 15, 1), 1, TabGroup.SystemSettings)
                .SetParent(slot)
                .SetValueFormat(OptionFormat.Multiplier)
                .RegisterUpdateValueEvent(setRecompileNeeded)
                .SetRunEventOnLoad(true);

            slot.RegisterUpdateValueEvent((_, _, _) =>
            {
                explicitGM.SetHidden(slot.GetValue() != 1);
                randomGroup.SetHidden(slot.GetValue() != 2);
            });

            AutoGMRotationSlots.Add((slot, count, explicitGM, randomGroup));
        }

        for (var index = 1; index <= MaxAutoGMRotationRandomGroups; index++)
        {
            new TextOptionItem(100030 + index, "MenuTitle.AGMR.RandomGroup", TabGroup.SystemSettings)
                .SetHeader(true)
                .SetParent(EnableAutoGMRotation)
                .AddReplacement(("{index}", index.ToString()));

            Dictionary<CustomGameMode, OptionItem> dict = [];

            foreach (CustomGameMode customGameMode in Enum.GetValues<CustomGameMode>()[..^1])
            {
                OptionItem chanceToSelectGMInGroup = new IntegerOptionItem(id++, $"AGMR.RandomGroup.GMChance", new(0, 100, 5), 50, TabGroup.SystemSettings)
                    .SetParent(EnableAutoGMRotation)
                    .SetValueFormat(OptionFormat.Percent)
                    .SetColor(Main.GameModeColors[customGameMode])
                    .AddReplacement(("{gm}", Translator.GetString($"{customGameMode}")));

                dict[customGameMode] = chanceToSelectGMInGroup;
            }

            AutoGMRotationRandomGroups[index] = dict;
        }


        yield return null;

        OptionSaver.Load();

        IsLoaded = true;

        PostLoadTasks();
    }

    public static void AutoSetFactionMinMaxSettings()
    {
        try
        {
            if (!AmongUsClient.Instance.AmHost || !EnableAutoFactionMinMaxSettings.GetBool()) return;

            int playerCount = PlayerControl.AllPlayerControls.Count;
            var filtered = AutoFactionMinMaxSettings.FindAll(x => x.MinPlayersToActivate.GetInt() > 0 && x.MinPlayersToActivate.GetInt() <= playerCount);
            if (filtered.Count == 0) return;
            var usedValues = filtered.MaxBy(x => x.MinPlayersToActivate.GetInt());

            foreach ((Team team, (OptionItem minSetting, OptionItem maxSetting)) in FactionMinMaxSettings)
            {
                var teamSettings = usedValues.TeamSettings[team];
                minSetting.SetValue(teamSettings.MinSetting.GetInt(), false, false);
                maxSetting.SetValue(teamSettings.MaxSetting.GetInt(), false, false);
            }
        
            MinNNKs.SetValue(usedValues.MinNNKs.GetInt(), false, false);
            MaxNNKs.SetValue(usedValues.MaxNNKs.GetInt(), false, false);
        
            OptionSaver.Save();
            OptionItem.SyncAllOptions();
            
            Logger.SendInGame(string.Format(Translator.GetString("AutoFactionMinMaxSettings.Applied"), playerCount, usedValues.MinPlayersToActivate.GetInt(), string.Join(", ", new[] { Team.Impostor, Team.Coven, Team.Neutral }.Select(x => $"{Utils.ColorString(x.GetColor(), Translator.GetString($"ShortTeamName.{x}").ToUpper())}: <#ffffff>{usedValues.TeamSettings[x].MinSetting.GetInt()}-{usedValues.TeamSettings[x].MaxSetting.GetInt()}</color>")), usedValues.MinNNKs.GetInt(), usedValues.MaxNNKs.GetInt()));
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static void CompileAutoGMRotationSettings()
    {
        if (!EnableAutoGMRotation.GetBool())
        {
            AutoGMRotationCompiled = [];
            return;
        }

        AutoGMRotationCompiled = [];

        foreach ((OptionItem slot, OptionItem count, OptionItem explicitChoice, OptionItem randomGroupChoice) in AutoGMRotationSlots)
        {
            int times = count.GetInt();

            switch ((AutoGMRoationSlotOptions)slot.GetValue())
            {
                case AutoGMRoationSlotOptions.Unused:
                    continue;
                case AutoGMRoationSlotOptions.Explicit:
                    CustomGameMode gm = explicitChoice.GetInt() switch
                    {
                        1 => CustomGameMode.SoloPVP,
                        2 => CustomGameMode.FFA,
                        3 => CustomGameMode.StopAndGo,
                        4 => CustomGameMode.HotPotato,
                        5 => CustomGameMode.HideAndSeek,
                        6 => CustomGameMode.Speedrun,
                        7 => CustomGameMode.CaptureTheFlag,
                        8 => CustomGameMode.NaturalDisasters,
                        9 => CustomGameMode.RoomRush,
                        10 => CustomGameMode.KingOfTheZones,
                        11 => CustomGameMode.Quiz,
                        12 => CustomGameMode.TheMindGame,
                        13 => CustomGameMode.BedWars,
                        14 => CustomGameMode.Deathrace,
                        15 => CustomGameMode.Mingle,
                        16 => CustomGameMode.Snowdown,
                        _ => CustomGameMode.Standard
                    };
                    AutoGMRotationCompiled.AddRange(Enumerable.Repeat(gm, times));
                    break;
                case AutoGMRoationSlotOptions.Random:
                    int groupId = randomGroupChoice.GetInt();
                    Dictionary<CustomGameMode, OptionItem> options = AutoGMRotationRandomGroups[groupId];
                    List<CustomGameMode> pool = options.Where(x => IRandom.Instance.Next(100) < x.Value.GetInt()).Select(x => x.Key).ToList();
                    if (pool.Count == 0) pool = options.Where(x => x.Value.GetInt() > 0).Select(x => x.Key).ToList();
                    if (pool.Count == 0) pool = options.Keys.ToList();
                    for (var i = 0; i < times; i++) AutoGMRotationCompiled.Add(pool.RandomElement());
                    break;
                case AutoGMRoationSlotOptions.Poll:
                    AutoGMRotationCompiled.AddRange(Enumerable.Repeat(CustomGameMode.All, times));
                    break;
            }
        }

        AutoGMRotationIndex = 0;
        AutoGMRotationRecompileOnClose = false;

        Logger.Info($"Auto GM Rotation compilation result: {string.Join(", ", AutoGMRotationCompiled)}", "OptionHolder");
    }

    public static void SetupRoleOptions(int id, TabGroup tab, CustomRoles role, CustomGameMode customGameMode = CustomGameMode.Standard, bool zeroOne = false)
    {
        var spawnOption = new StringOptionItem(id, role.ToString(), zeroOne ? RatesZeroOne : Rates, 0, tab).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        CustomRoleSpawnChances.Add(role, spawnOption);

        OptionItem countOption = new IntegerOptionItem(id + 1, "Maximum", new(1, 15, 1), 1, tab)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Players)
            .SetGameMode(customGameMode);

        CustomRoleCounts.Add(role, countOption);
    }

    public static void SetupAdtRoleOptions(int id, CustomRoles role, CustomGameMode customGameMode = CustomGameMode.Standard, bool canSetNum = false, TabGroup tab = TabGroup.Addons, bool canSetChance = true, bool teamSpawnOptions = false, bool allowZeroCount = false)
    {
        var spawnOption = new StringOptionItem(id, role.ToString(), RatesZeroOne, 0, tab).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        OptionItem countOption = new IntegerOptionItem(id + 1, "Maximum", new(allowZeroCount ? 0 : 1, canSetNum ? 15 : 1, 1), 1, tab)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Players)
            .SetHidden(!canSetNum)
            .SetGameMode(customGameMode);

        var spawnRateOption = new IntegerOptionItem(id + 2, "AdditionRolesSpawnRate", new(0, 100, 5), canSetChance ? 65 : 100, tab)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Percent)
            .SetHidden(!canSetChance)
            .SetGameMode(customGameMode) as IntegerOptionItem;

        OptionItem guessSetting = new StringOptionItem(id + 3, "AddonCanBeGuessed", AddonGuessOptions, 2, tab)
            .SetParent(spawnOption)
            .SetGameMode(customGameMode);

        if (teamSpawnOptions)
        {
            OptionItem impOption = new BooleanOptionItem(id + 4, "ImpCanBeRole", true, tab)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode)
                .AddReplacement(("{role}", role.ToColoredString()));

            OptionItem neutralOption = new BooleanOptionItem(id + 5, "NeutralCanBeRole", true, tab)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode)
                .AddReplacement(("{role}", role.ToColoredString()));

            OptionItem crewOption = new BooleanOptionItem(id + 6, "CrewCanBeRole", true, tab)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode)
                .AddReplacement(("{role}", role.ToColoredString()));

            OptionItem covenOption = new BooleanOptionItem(id + 7, "CovenCanBeRole", true, tab)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode)
                .AddReplacement(("{role}", role.ToColoredString()));

            AddonCanBeSettings.Add(role, (impOption, neutralOption, crewOption, covenOption));
        }

        AddonGuessSettings.Add(role, guessSetting);

        CustomAdtRoleSpawnRate.Add(role, spawnRateOption);
        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
    }

    public static void SetupSingleRoleOptions(int id, TabGroup tab, CustomRoles role, int count = 1, CustomGameMode customGameMode = CustomGameMode.Standard, bool zeroOne = false, bool hideMaxSetting = true)
    {
        var spawnOption = new StringOptionItem(id, role.ToString(), zeroOne ? RatesZeroOne : Rates, 0, tab).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        OptionItem countOption = new IntegerOptionItem(id + 1, "Maximum", new(count, count, count), count, tab)
            .SetParent(spawnOption)
            .SetHidden(hideMaxSetting)
            .SetGameMode(customGameMode);

        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
        SingleRoles.Add(role);
    }

    public static OptionItem CreateCDSetting(int id, TabGroup tab, CustomRoles role, bool isKCD = false)
    {
        return new FloatOptionItem(id, isKCD ? "KillCooldown" : "AbilityCooldown", new(0f, 180f, 0.5f), 30f, tab)
            .SetParent(CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public static OptionItem CreatePetUseSetting(int id, CustomRoles role)
    {
        return new BooleanOptionItem(id, "UsePetInsteadOfKillButton", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[role])
            .SetColor(Color.magenta);
    }

    public static OptionItem CreateVoteCancellingUseSetting(int id, CustomRoles role, TabGroup tab)
    {
        return new BooleanOptionItem(id, "UseVoteCancellingAfterVote", true, tab)
            .SetParent(CustomRoleSpawnChances[role])
            .SetColor(Color.yellow);
    }

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

            if (!AllData.ContainsKey(role))
                AllData.Add(role, this);
            else
                Logger.Warn("OverrideTasksData created for duplicate CustomRoles", "OverrideTasksData");
        }

        public static OverrideTasksData Create(int idStart, TabGroup tab, CustomRoles role)
        {
            return new(idStart, tab, role);
        }
    }

    // ReSharper disable NotAccessedField.Global

    // Ability Use Gain With Each Task Completed
    public static OptionItem GrenadierAbilityUseGainWithEachTaskCompleted;
    public static OptionItem LighterAbilityUseGainWithEachTaskCompleted;
    public static OptionItem SecurityGuardAbilityUseGainWithEachTaskCompleted;
    public static OptionItem PacifistAbilityUseGainWithEachTaskCompleted;

    // Ability Use Gain every 5 seconds
    public static OptionItem GrenadierAbilityChargesWhenFinishedTasks;
    public static OptionItem LighterAbilityChargesWhenFinishedTasks;
    public static OptionItem SecurityGuardAbilityChargesWhenFinishedTasks;
    public static OptionItem PacifistAbilityChargesWhenFinishedTasks;

    // ReSharper restore NotAccessedField.Global
}
