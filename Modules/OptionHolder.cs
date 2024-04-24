using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EHR.Modules;
using EHR.Roles.AddOns;
using HarmonyLib;
using UnityEngine;

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
    All = int.MaxValue
}

[HarmonyPatch]
public static class Options
{
    static Task taskOptionsLoad;

    public static Dictionary<TabGroup, OptionItem[]> GroupedOptions = [];

    public static OptionItem GameMode;

    public static readonly string[] GameModes =
    [
        "Standard",
        "SoloKombat",
        "FFA",
        "MoveAndStop",
        "HotPotato",
        "HideAndSeek"
    ];

    public static Dictionary<CustomRoles, int> roleCounts;
    public static Dictionary<CustomRoles, float> roleSpawnChances;
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
        "Rate100",
    ];

    public static readonly string[] RatesZeroOne =
    [
        "RoleOff", /*"Rate10", "Rate20", "Rate30", "Rate40", "Rate50",
        "Rate60", "Rate70", "Rate80", "Rate90", */
        "RoleRate",
    ];

    public static readonly string[] CheatResponsesName =
    [
        "Ban",
        "Kick",
        "NoticeMe",
        "NoticeEveryone",
        "OnlyCancel"
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

    public static float DefaultKillCooldown = Main.NormalOptions?.KillCooldown ?? 25;

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
    public static OptionItem ZombieKillCooldown;
    public static OptionItem ZombieSpeedReduce;
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
    public static OptionItem OppoImmuneToAttacksWhenTasksDone;
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

    public static OptionItem NimbleCD;
    public static OptionItem NimbleInVentTime;
    public static OptionItem PhysicistCD;
    public static OptionItem PhysicistViewDuration;

    public static OptionItem CleanerKillCooldown;
    public static OptionItem KillCooldownAfterCleaning;
    public static OptionItem GuardSpellTimes;
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
    public static OptionItem ImpCanBeOnbound;
    public static OptionItem CrewCanBeOnbound;
    public static OptionItem NeutralCanBeOnbound;
    public static OptionItem ImpCanBeInLove;
    public static OptionItem CrewCanBeInLove;
    public static OptionItem NeutralCanBeInLove;
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
    public static OptionItem AsthmaticMinRedTime;
    public static OptionItem AsthmaticMaxRedTime;
    public static OptionItem AsthmaticMinGreenTime;
    public static OptionItem AsthmaticMaxGreenTime;
    public static OptionItem DiscoChangeInterval;

    public static OptionItem TruantWaitingTime;

    // RASCAL //
    public static OptionItem RascalAppearAsMadmate;

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

    public static OptionItem InhibitorCD;
    public static OptionItem InhibitorCDAfterMeetings;
    public static OptionItem SaboteurCD;
    public static OptionItem SaboteurCDAfterMeetings;
    public static OptionItem PhantomCanVent;

    public static OptionItem PhantomSnatchesWin;

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
    public static OptionItem ChangeDecontaminationTime;
    public static OptionItem DecontaminationTimeOnMiraHQ;
    public static OptionItem DecontaminationTimeOnPolus;

    public static OptionItem ShapeshiftCD;
    public static OptionItem ShapeshiftDur;

    public static OptionItem MafiaShapeshiftCD;
    public static OptionItem MafiaShapeshiftDur;

    public static OptionItem ScientistDur;
    public static OptionItem ScientistCD;

    public static OptionItem GCanGuessImp;
    public static OptionItem GCanGuessCrew;
    public static OptionItem GCanGuessAdt;
    public static OptionItem GTryHideMsg;

    // Masochist
    //public static OptionItem MasochistKillMax;

    public static OptionItem DisableTaskWinIfAllCrewsAreDead;

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

    public static OptionItem BusyLongTasks;
    public static OptionItem BusyShortTasks;

    // Disable Devices
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

    // Ability Use Gain every 5 seconds
    public static OptionItem VentguardAbilityChargesWhenFinishedTasks;
    public static OptionItem GrenadierAbilityChargesWhenFinishedTasks;
    public static OptionItem LighterAbilityChargesWhenFinishedTasks;
    public static OptionItem SecurityGuardAbilityChargesWhenFinishedTasks;
    public static OptionItem DovesOfNeaceAbilityChargesWhenFinishedTasks;
    public static OptionItem TimeMasterAbilityChargesWhenFinishedTasks;
    public static OptionItem VeteranAbilityChargesWhenFinishedTasks;

    // Maps
    public static OptionItem RandomMapsMode;
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
    public static OptionItem WhenSkipVote;
    public static OptionItem WhenSkipVoteIgnoreFirstMeeting;
    public static OptionItem WhenSkipVoteIgnoreNoDeadBody;
    public static OptionItem WhenSkipVoteIgnoreEmergency;
    public static OptionItem WhenNonVote;
    public static OptionItem WhenTie;

    public static readonly string[] VoteModes =
    [
        "Default",
        "Suicide",
        "SelfVote",
        "Skip"
    ];

    public static readonly string[] TieModes =
    [
        "TieMode.Default",
        "TieMode.All",
        "TieMode.Random"
    ];

    public static readonly string[] MadmateSpawnModeStrings =
    [
        "MadmateSpawnMode.Assign",
        "MadmateSpawnMode.FirstKill",
        "MadmateSpawnMode.SelfVote",
    ];

    public static readonly string[] MadmateCountModeStrings =
    [
        "MadmateCountMode.None",
        "MadmateCountMode.Imp",
        "MadmateCountMode.Crew",
    ];

    public static readonly string[] SidekickCountMode =
    [
        "SidekickCountMode.Jackal",
        "SidekickCountMode.None",
        "SidekickCountMode.Original",
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
    public static OptionItem SuffixMode;
    public static OptionItem HideGameSettings;
    public static OptionItem FormatNameMode;
    public static OptionItem DisableEmojiName;
    public static OptionItem ChangeNameToRoleInfo;
    public static OptionItem SendRoleDescriptionFirstMeeting;
    public static OptionItem RoleAssigningAlgorithm;
    public static OptionItem EndWhenPlayerBug;
    public static OptionItem RemovePetsAtDeadPlayers;

    public static OptionItem CTAPlayersCanWinWithOriginalTeam;
    public static OptionItem CTAPlayersCanSeeEachOthersRoles;

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
    public static OptionItem ImpCanBeAvanger;
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
    public static OptionItem LoverSpawnChances;
    public static OptionItem LoverKnowRoles;
    public static OptionItem LoverSuicide;
    public static OptionItem ImpEgoistVisibalToAllies;
    public static OptionItem TicketsPerKill;
    public static OptionItem ImpCanBeDualPersonality;
    public static OptionItem CrewCanBeDualPersonality;
    public static OptionItem DualVotes;
    public static OptionItem ImpCanBeLoyal;
    public static OptionItem CrewCanBeLoyal;
    public static OptionItem MinWaitAutoStart;
    public static OptionItem MaxWaitAutoStart;
    public static OptionItem PlayerAutoStart;

    public static OptionItem DumpLogAfterGameEnd;

    public static readonly string[] SuffixModes =
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

    public static readonly string[] RoleAssigningAlgorithms =
    [
        "RoleAssigningAlgorithm.Default",
        "RoleAssigningAlgorithm.NetRandom",
        "RoleAssigningAlgorithm.HashRandom",
        "RoleAssigningAlgorithm.Xorshift",
        "RoleAssigningAlgorithm.MersenneTwister",
    ];

    public static readonly string[] FormatNameModes =
    [
        "FormatNameModes.None",
        "FormatNameModes.Color",
        "FormatNameModes.Snacks",
    ];

    public static bool IsLoaded;

    public static int LoadingPercentage;
    public static string MainLoadingText = string.Empty;
    public static string RoleLoadingText = string.Empty;

    public static Dictionary<CustomRoles, (OptionItem Imp, OptionItem Neutral, OptionItem Crew)> AddonCanBeSettings = [];

    public static HashSet<CustomRoles> SingleRoles = [];

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
            _ => CustomGameMode.Standard
        };

    [HarmonyPatch(typeof(TranslationController), nameof(TranslationController.Initialize)), HarmonyPostfix]
    public static void OptionsLoadStart()
    {
        Logger.Info("Options.Load Start", "Options");
        Main.LoadRoleClasses();
        taskOptionsLoad = Task.Run(Load);
        taskOptionsLoad.ContinueWith(_ =>
        {
            Logger.Info("Options.Load End", "Options");
            GroupOptions();
        });
    }

    private static void GroupOptions()
    {
        GroupedOptions = OptionItem.AllOptions
            .GroupBy(x => x.Tab)
            .OrderBy(x => (int)x.Key)
            .ToDictionary(x => x.Key, x => x.ToArray());
    }

    public static VoteMode GetWhenSkipVote() => (VoteMode)WhenSkipVote.GetValue();
    public static VoteMode GetWhenNonVote() => (VoteMode)WhenNonVote.GetValue();

    public static SuffixModes GetSuffixMode() => (SuffixModes)SuffixMode.GetValue();

    public static void ResetRoleCounts()
    {
        roleCounts = [];
        roleSpawnChances = [];

        foreach (var role in EnumHelper.GetAllValues<CustomRoles>())
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

#pragma warning disable IDE0079 // Remove unnecessary suppression
    [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
    public static void Load()
    {
        LoadingPercentage = 0;
        MainLoadingText = "Building system settings";

        if (IsLoaded) return;

        OptionSaver.Initialize();

        int defaultPresetNumber = OptionSaver.GetDefaultPresetNumber();
        _ = PresetOptionItem.Create(defaultPresetNumber, TabGroup.SystemSettings)
            .SetColor(new Color32(255, 235, 4, byte.MaxValue))
            .SetHeader(true);

        GameMode = StringOptionItem.Create(1, "GameMode", GameModes, 0, TabGroup.GameSettings)
            .SetHeader(true);

        #region Settings

        CustomRoleCounts = [];
        CustomRoleSpawnChances = [];
        CustomAdtRoleSpawnRate = [];

        MainLoadingText = "Building general settings";


        ImpKnowAlliesRole = BooleanOptionItem.Create(150, "ImpKnowAlliesRole", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);
        ImpKnowWhosMadmate = BooleanOptionItem.Create(151, "ImpKnowWhosMadmate", false, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);
        ImpCanKillMadmate = BooleanOptionItem.Create(152, "ImpCanKillMadmate", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);

        MadmateKnowWhosMadmate = BooleanOptionItem.Create(153, "MadmateKnowWhosMadmate", false, TabGroup.ImpostorRoles)
            .SetHeader(true)
            .SetGameMode(CustomGameMode.Standard);
        MadmateKnowWhosImp = BooleanOptionItem.Create(154, "MadmateKnowWhosImp", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);
        MadmateCanKillImp = BooleanOptionItem.Create(155, "MadmateCanKillImp", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);
        MadmateHasImpostorVision = BooleanOptionItem.Create(156, "MadmateHasImpostorVision", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);

        DefaultShapeshiftCooldown = FloatOptionItem.Create(200, "DefaultShapeshiftCooldown", new(5f, 180f, 5f), 15f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Seconds);
        DeadImpCantSabotage = BooleanOptionItem.Create(201, "DeadImpCantSabotage", false, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.Standard);

        NonNeutralKillingRolesMinPlayer = IntegerOptionItem.Create(202, "NonNeutralKillingRolesMinPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Players);
        NonNeutralKillingRolesMaxPlayer = IntegerOptionItem.Create(203, "NonNeutralKillingRolesMaxPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);

        NeutralKillingRolesMinPlayer = IntegerOptionItem.Create(204, "NeutralKillingRolesMinPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Players);
        NeutralKillingRolesMaxPlayer = IntegerOptionItem.Create(205, "NeutralKillingRolesMaxPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);

        NeutralRoleWinTogether = BooleanOptionItem.Create(208, "NeutralRoleWinTogether", false, TabGroup.NeutralRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);
        NeutralWinTogether = BooleanOptionItem.Create(209, "NeutralWinTogether", false, TabGroup.NeutralRoles)
            .SetParent(NeutralRoleWinTogether)
            .SetGameMode(CustomGameMode.Standard);

        NameDisplayAddons = BooleanOptionItem.Create(210, "NameDisplayAddons", true, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);
        NoLimitAddonsNumMax = IntegerOptionItem.Create(211, "NoLimitAddonsNumMax", new(1, 90, 1), 1, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard);


        RoleLoadingText = "Add-ons\n.";


        AddBracketsToAddons = BooleanOptionItem.Create(13500, "BracketAddons", false, TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);

        #region Roles/AddOns_Settings

        try
        {
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
                TextOptionItem.Create(titleId, $"ROT.AddonType.{addonType.Key}", TabGroup.Addons)
                    .SetGameMode(CustomGameMode.Standard)
                    .SetColor(addonType.Key.GetAddonTypeColor())
                    .SetHeader(true);
                titleId += 10;

                foreach (var addon in addonType.Value)
                {
                    addon.SetupCustomOption();
                }
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
                    TextOptionItem.Create(titleId, "ROT.Vanilla", x.Tab)
                        .SetGameMode(CustomGameMode.Standard)
                        .SetColor(Color.white)
                        .SetHeader(true);
                    titleId += 10;

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


            TextOptionItem.Create(titleId, "ROT.MadMates", TabGroup.ImpostorRoles)
                .SetHeader(true)
                .SetGameMode(CustomGameMode.Standard)
                .SetColor(Palette.ImpostorRed);
            titleId += 10;

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
                    TextOptionItem.Create(titleId, $"ROT.Basic{categorySuffix}", tab)
                        .SetGameMode(CustomGameMode.Standard)
                        .SetColor(Color.gray)
                        .SetHeader(true);
                    titleId += 10;

                    foreach (ISettingHolder holder in value)
                    {
                        holder.SetupCustomOption();
                    }

                    simpleRoleClasses.Remove(key);
                }

                TextOptionItem.Create(titleId, $"ROT.{roleClasses.Key}", tab)
                    .SetHeader(true)
                    .SetGameMode(CustomGameMode.Standard)
                    .SetColor(roleClasses.Key.GetRoleOptionTypeColor());
                titleId += 10;

                foreach (var roleClass in roleClasses.Value)
                {
                    index++;
                    var type = roleClass.GetType();
                    RoleLoadingText = $"{index}/{allRoles} ({type.Name})";
                    try
                    {
                        type.GetMethod("SetupCustomOption")?.Invoke(roleClass, null);
                    }
                    catch (Exception e)
                    {
                        Logger.Exception(e, $"{MainLoadingText} - {RoleLoadingText}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Options");
        }

        #endregion


        LoadingPercentage = 60;

        RoleLoadingText = string.Empty;

        #endregion

        #region EHRSettings

        MainLoadingText = "Building EHR settings";

        KickLowLevelPlayer = IntegerOptionItem.Create(19300, "KickLowLevelPlayer", new(0, 100, 1), 0, TabGroup.SystemSettings)
            .SetValueFormat(OptionFormat.Level)
            .SetHeader(true);
        KickAndroidPlayer = BooleanOptionItem.Create(19301, "KickAndroidPlayer", false, TabGroup.SystemSettings);
        KickPlayerFriendCodeNotExist = BooleanOptionItem.Create(19302, "KickPlayerFriendCodeNotExist", false, TabGroup.SystemSettings, true);
        ApplyDenyNameList = BooleanOptionItem.Create(19303, "ApplyDenyNameList", true, TabGroup.SystemSettings, true);
        ApplyBanList = BooleanOptionItem.Create(19304, "ApplyBanList", true, TabGroup.SystemSettings, true);
        ApplyModeratorList = BooleanOptionItem.Create(19305, "ApplyModeratorList", false, TabGroup.SystemSettings);

        LoadingPercentage = 61;

        AutoKickStart = BooleanOptionItem.Create(19310, "AutoKickStart", false, TabGroup.SystemSettings);
        AutoKickStartTimes = IntegerOptionItem.Create(19311, "AutoKickStartTimes", new(0, 90, 1), 1, TabGroup.SystemSettings)
            .SetParent(AutoKickStart)
            .SetValueFormat(OptionFormat.Times);
        AutoKickStartAsBan = BooleanOptionItem.Create(19312, "AutoKickStartAsBan", false, TabGroup.SystemSettings)
            .SetParent(AutoKickStart);
        AutoKickStopWords = BooleanOptionItem.Create(19313, "AutoKickStopWords", false, TabGroup.SystemSettings);
        AutoKickStopWordsTimes = IntegerOptionItem.Create(19314, "AutoKickStopWordsTimes", new(0, 90, 1), 3, TabGroup.SystemSettings)
            .SetParent(AutoKickStopWords)
            .SetValueFormat(OptionFormat.Times);
        AutoKickStopWordsAsBan = BooleanOptionItem.Create(19315, "AutoKickStopWordsAsBan", false, TabGroup.SystemSettings)
            .SetParent(AutoKickStopWords);

        LoadingPercentage = 62;

        AutoWarnStopWords = BooleanOptionItem.Create(19316, "AutoWarnStopWords", false, TabGroup.SystemSettings);
        MinWaitAutoStart = FloatOptionItem.Create(44420, "MinWaitAutoStart", new(0f, 10f, 0.5f), 1.5f, TabGroup.SystemSettings);
        MaxWaitAutoStart = FloatOptionItem.Create(44421, "MaxWaitAutoStart", new(0f, 10f, 0.5f), 1.5f, TabGroup.SystemSettings);
        PlayerAutoStart = IntegerOptionItem.Create(44422, "PlayerAutoStart", new(1, 15, 1), 14, TabGroup.SystemSettings);
        AutoStartTimer = IntegerOptionItem.Create(44423, "AutoStartTimer", new(10, 600, 1), 20, TabGroup.SystemSettings)
            .SetValueFormat(OptionFormat.Seconds);
        AutoPlayAgain = BooleanOptionItem.Create(44424, "AutoPlayAgain", false, TabGroup.SystemSettings);
        AutoPlayAgainCountdown = IntegerOptionItem.Create(44425, "AutoPlayAgainCountdown", new(1, 90, 1), 10, TabGroup.SystemSettings)
            .SetParent(AutoPlayAgain);

        LowLoadMode = BooleanOptionItem.Create(19317, "LowLoadMode", true, TabGroup.SystemSettings)
            .SetHeader(true)
            .SetColor(Color.green);

        DeepLowLoad = BooleanOptionItem.Create(19325, "DeepLowLoad", false, TabGroup.SystemSettings)
            .SetColor(Color.red);

        DontUpdateDeadPlayers = BooleanOptionItem.Create(19326, "DontUpdateDeadPlayers", true, TabGroup.SystemSettings)
            .SetColor(Color.red);

        DumpLogAfterGameEnd = BooleanOptionItem.Create(19327, "DumpLogAfterGameEnd", true, TabGroup.SystemSettings)
            .SetColor(Color.yellow);

        EndWhenPlayerBug = BooleanOptionItem.Create(19318, "EndWhenPlayerBug", true, TabGroup.SystemSettings)
            .SetHeader(true)
            .SetColor(Color.blue);

        RemovePetsAtDeadPlayers = BooleanOptionItem.Create(60294, "RemovePetsAtDeadPlayers", false, TabGroup.SystemSettings)
            .SetColor(Color.magenta);

        CheatResponses = StringOptionItem.Create(19319, "CheatResponses", CheatResponsesName, 2, TabGroup.SystemSettings)
            .SetHeader(true);

        DisableVoteBan = BooleanOptionItem.Create(19320, "DisableVoteBan", true, TabGroup.SystemSettings, true);


        LoadingPercentage = 63;


        AutoDisplayKillLog = BooleanOptionItem.Create(19321, "AutoDisplayKillLog", true, TabGroup.SystemSettings)
            .SetHeader(true);
        AutoDisplayLastRoles = BooleanOptionItem.Create(19322, "AutoDisplayLastRoles", true, TabGroup.SystemSettings);
        AutoDisplayLastAddOns = BooleanOptionItem.Create(19328, "AutoDisplayLastAddOns", true, TabGroup.SystemSettings);
        AutoDisplayLastResult = BooleanOptionItem.Create(19323, "AutoDisplayLastResult", true, TabGroup.SystemSettings);

        SuffixMode = StringOptionItem.Create(19324, "SuffixMode", SuffixModes, 0, TabGroup.SystemSettings, true)
            .SetHeader(true);
        HideGameSettings = BooleanOptionItem.Create(19400, "HideGameSettings", false, TabGroup.SystemSettings);
        DIYGameSettings = BooleanOptionItem.Create(19401, "DIYGameSettings", false, TabGroup.SystemSettings);
        PlayerCanSetColor = BooleanOptionItem.Create(19402, "PlayerCanSetColor", false, TabGroup.SystemSettings);
        PlayerCanSetName = BooleanOptionItem.Create(19410, "PlayerCanSetName", false, TabGroup.SystemSettings);
        PlayerCanTPInAndOut = BooleanOptionItem.Create(19411, "PlayerCanTPInAndOut", false, TabGroup.SystemSettings);
        FormatNameMode = StringOptionItem.Create(19403, "FormatNameMode", FormatNameModes, 0, TabGroup.SystemSettings);
        DisableEmojiName = BooleanOptionItem.Create(19404, "DisableEmojiName", true, TabGroup.SystemSettings);
        ChangeNameToRoleInfo = BooleanOptionItem.Create(19405, "ChangeNameToRoleInfo", true, TabGroup.SystemSettings);
        SendRoleDescriptionFirstMeeting = BooleanOptionItem.Create(19406, "SendRoleDescriptionFirstMeeting", true, TabGroup.SystemSettings);
        NoGameEnd = BooleanOptionItem.Create(19407, "NoGameEnd", false, TabGroup.SystemSettings)
            .SetColor(Color.red);
        AllowConsole = BooleanOptionItem.Create(19408, "AllowConsole", false, TabGroup.SystemSettings)
            .SetColor(Color.red);
        RoleAssigningAlgorithm = StringOptionItem.Create(19409, "RoleAssigningAlgorithm", RoleAssigningAlgorithms, 4, TabGroup.SystemSettings, true)
            .RegisterUpdateValueEvent((_, args) => IRandom.SetInstanceById(args.CurrentValue));
        KPDCamouflageMode = StringOptionItem.Create(19500, "KPDCamouflageMode", CamouflageMode, 0, TabGroup.SystemSettings)
            .SetHeader(true)
            .SetColor(new Color32(255, 192, 203, byte.MaxValue));

        LoadingPercentage = 64;


        EnableUpMode = BooleanOptionItem.Create(19600, "EnableYTPlan", false, TabGroup.SystemSettings)
            .SetColor(Color.cyan)
            .SetHeader(true);

        #endregion

        #region Gamemodes

        MainLoadingText = "Building Settings for Other Gamemodes";

        //SoloKombat
        SoloKombatManager.SetupCustomOption();
        //FFA
        FFAManager.SetupCustomOption();
        //Move And Stop
        MoveAndStopManager.SetupCustomOption();
        //Hot Potato
        HotPotatoManager.SetupCustomOption();
        //Hide And Seek
        CustomHideAndSeekManager.SetupCustomOption();


        LoadingPercentage = 65;
        MainLoadingText = "Building game settings";

        TextOptionItem.Create(100023, "MenuTitle.Ejections", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        CEMode = StringOptionItem.Create(19800, "ConfirmEjectionsMode", ConfirmEjectionsMode, 2, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ShowImpRemainOnEject = BooleanOptionItem.Create(19810, "ShowImpRemainOnEject", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ShowNKRemainOnEject = BooleanOptionItem.Create(19811, "ShowNKRemainOnEject", true, TabGroup.GameSettings)
            .SetParent(ShowImpRemainOnEject)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        LoadingPercentage = 66;

        ShowTeamNextToRoleNameOnEject = BooleanOptionItem.Create(19812, "ShowTeamNextToRoleNameOnEject", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ConfirmEgoistOnEject = BooleanOptionItem.Create(19813, "ConfirmEgoistOnEject", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue))
            .SetHeader(true);
        ConfirmLoversOnEject = BooleanOptionItem.Create(19815, "ConfirmLoversOnEject", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        LoadingPercentage = 67;


        //Maps Settings
        TextOptionItem.Create(100024, "MenuTitle.MapsSettings", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Random Maps Mode
        RandomMapsMode = BooleanOptionItem.Create(19900, "RandomMapsMode", false, TabGroup.GameSettings)
            .SetHeader(true)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        LoadingPercentage = 68;

        SkeldChance = IntegerOptionItem.Create(19910, "SkeldChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        MiraChance = IntegerOptionItem.Create(19911, "MiraChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        PolusChance = IntegerOptionItem.Create(19912, "PolusChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        DleksChance = IntegerOptionItem.Create(19914, "DleksChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        AirshipChance = IntegerOptionItem.Create(19913, "AirshipChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);
        FungleChance = IntegerOptionItem.Create(19922, "FungleChance", new(0, 100, 5), 0, TabGroup.GameSettings)
            .SetParent(RandomMapsMode)
            .SetValueFormat(OptionFormat.Percent);

        LoadingPercentage = 69;


        // Random Spawn
        RandomSpawn = BooleanOptionItem.Create(22000, "RandomSpawn", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        AirshipAdditionalSpawn = BooleanOptionItem.Create(22010, "AirshipAdditionalSpawn", false, TabGroup.GameSettings)
            .SetParent(RandomSpawn)
            .SetGameMode(CustomGameMode.Standard);

        // Airship Variable Electrical
        AirshipVariableElectrical = BooleanOptionItem.Create(22100, "AirshipVariableElectrical", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Disable Airship Moving Platform
        DisableAirshipMovingPlatform = BooleanOptionItem.Create(22110, "DisableAirshipMovingPlatform", false, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Disable Spore Triggers on Fungle
        DisableSporeTriggerOnFungle = BooleanOptionItem.Create(22130, "DisableSporeTriggerOnFungle", false, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Disable Zipline On Fungle
        DisableZiplineOnFungle = BooleanOptionItem.Create(22305, "DisableZiplineOnFungle", false, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Disable Zipline From Top
        DisableZiplineFromTop = BooleanOptionItem.Create(22308, "DisableZiplineFromTop", false, TabGroup.GameSettings)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Disable Zipline From Under
        DisableZiplineFromUnder = BooleanOptionItem.Create(22310, "DisableZiplineFromUnder", false, TabGroup.GameSettings)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        DisableZiplineForCrew = BooleanOptionItem.Create(22316, "DisableZiplineForCrew", false, TabGroup.GameSettings)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        DisableZiplineForImps = BooleanOptionItem.Create(22318, "DisableZiplineForImps", false, TabGroup.GameSettings)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        DisableZiplineForNeutrals = BooleanOptionItem.Create(22320, "DisableZiplineForNeutrals", false, TabGroup.GameSettings)
            .SetParent(DisableZiplineOnFungle)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        ZiplineTravelTimeFromBottom = FloatOptionItem.Create(22312, "ZiplineTravelTimeFromBottom", new(0.5f, 10f, 0.5f), 4f, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        ZiplineTravelTimeFromTop = FloatOptionItem.Create(22314, "ZiplineTravelTimeFromTop", new(0.5f, 10f, 0.5f), 2f, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));

        // Reset Doors After Meeting
        ResetDoorsEveryTurns = BooleanOptionItem.Create(22120, "ResetDoorsEveryTurns", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Reset Doors Mode
        DoorsResetMode = StringOptionItem.Create(22122, "DoorsResetMode", EnumHelper.GetAllNames<DoorsReset.ResetMode>(), 2, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(ResetDoorsEveryTurns);

        // Change decontamination time on MiraHQ/Polus
        ChangeDecontaminationTime = BooleanOptionItem.Create(60503, "ChangeDecontaminationTime", false, TabGroup.GameSettings)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Decontamination time on MiraHQ
        DecontaminationTimeOnMiraHQ = FloatOptionItem.Create(60504, "DecontaminationTimeOnMiraHQ", new(0.5f, 10f, 0.25f), 3f, TabGroup.GameSettings)
            .SetParent(ChangeDecontaminationTime)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));
        // Decontamination time on Polus
        DecontaminationTimeOnPolus = FloatOptionItem.Create(60505, "DecontaminationTimeOnPolus", new(0.5f, 10f, 0.25f), 3f, TabGroup.GameSettings)
            .SetParent(ChangeDecontaminationTime)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new Color32(19, 188, 233, byte.MaxValue));


        LoadingPercentage = 70;

        // Sabotage
        TextOptionItem.Create(100025, "MenuTitle.Sabotage", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetHeader(true);

        // CommsCamouflage
        CommsCamouflage = BooleanOptionItem.Create(22200, "CommsCamouflage", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));
        CommsCamouflageDisableOnFungle = BooleanOptionItem.Create(22202, "CommsCamouflageDisableOnFungle", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(CommsCamouflage)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));
        DisableReportWhenCC = BooleanOptionItem.Create(22300, "DisableReportWhenCC", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue));

        LoadingPercentage = 71;


        // Sabotage Cooldown Control
        SabotageCooldownControl = BooleanOptionItem.Create(22400, "SabotageCooldownControl", false, TabGroup.GameSettings)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        SabotageCooldown = FloatOptionItem.Create(22405, "SabotageCooldown", new(1f, 60f, 1f), 30f, TabGroup.GameSettings)
            .SetParent(SabotageCooldownControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        // Sabotage Duration Control
        SabotageTimeControl = BooleanOptionItem.Create(22410, "SabotageTimeControl", false, TabGroup.GameSettings)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);

        // The Skeld
        SkeldReactorTimeLimit = FloatOptionItem.Create(22418, "SkeldReactorTimeLimit", new(5f, 90f, 1f), 30f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        SkeldO2TimeLimit = FloatOptionItem.Create(22419, "SkeldO2TimeLimit", new(5f, 90f, 1f), 30f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // Mira HQ
        MiraReactorTimeLimit = FloatOptionItem.Create(22422, "MiraReactorTimeLimit", new(5f, 90f, 1f), 45f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        MiraO2TimeLimit = FloatOptionItem.Create(22423, "MiraO2TimeLimit", new(5f, 90f, 1f), 45f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // Polus
        PolusReactorTimeLimit = FloatOptionItem.Create(22424, "PolusReactorTimeLimit", new(5f, 90f, 1f), 60f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // The Airship
        AirshipReactorTimeLimit = FloatOptionItem.Create(22425, "AirshipReactorTimeLimit", new(5f, 90f, 1f), 90f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        // The Fungle
        FungleReactorTimeLimit = FloatOptionItem.Create(22426, "FungleReactorTimeLimit", new(5f, 90f, 1f), 60f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        FungleMushroomMixupDuration = FloatOptionItem.Create(22427, "FungleMushroomMixupDuration", new(5f, 90f, 1f), 10f, TabGroup.GameSettings)
            .SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 72;

        // LightsOutSpecialSettings
        LightsOutSpecialSettings = BooleanOptionItem.Create(22500, "LightsOutSpecialSettings", false, TabGroup.GameSettings)
            .SetColor(new Color32(243, 96, 96, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipViewingDeckLightsPanel = BooleanOptionItem.Create(22510, "DisableAirshipViewingDeckLightsPanel", false, TabGroup.GameSettings)
            .SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipGapRoomLightsPanel = BooleanOptionItem.Create(22511, "DisableAirshipGapRoomLightsPanel", false, TabGroup.GameSettings)
            .SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipCargoLightsPanel = BooleanOptionItem.Create(22512, "DisableAirshipCargoLightsPanel", false, TabGroup.GameSettings)
            .SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 73;


        TextOptionItem.Create(100026, "MenuTitle.Disable", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableShieldAnimations = BooleanOptionItem.Create(22601, "DisableShieldAnimations", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableKillAnimationOnGuess = BooleanOptionItem.Create(22602, "DisableKillAnimationOnGuess", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableShapeshiftAnimations = BooleanOptionItem.Create(22604, "DisableShapeshiftAnimations", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableAllShapeshiftAnimations = BooleanOptionItem.Create(22605, "DisableAllShapeshiftAnimations", false, TabGroup.GameSettings)
            .SetParent(DisableShapeshiftAnimations)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableVanillaRoles = BooleanOptionItem.Create(22600, "DisableVanillaRoles", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableTaskWin = BooleanOptionItem.Create(22650, "DisableTaskWin", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableTaskWinIfAllCrewsAreDead = BooleanOptionItem.Create(22651, "DisableTaskWinIfAllCrewsAreDead", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 74;


        DisableMeeting = BooleanOptionItem.Create(22700, "DisableMeeting", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableSabotage = BooleanOptionItem.Create(22800, "DisableSabotage", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableCloseDoor = BooleanOptionItem.Create(22810, "DisableCloseDoor", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(DisableSabotage)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        LoadingPercentage = 75;

        DisableDevices = BooleanOptionItem.Create(22900, "DisableDevices", false, TabGroup.GameSettings)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableSkeldDevices = BooleanOptionItem.Create(22905, "DisableSkeldDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices);
        DisableSkeldAdmin = BooleanOptionItem.Create(22906, "DisableSkeldAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableSkeldDevices);
        DisableSkeldCamera = BooleanOptionItem.Create(22907, "DisableSkeldCamera", false, TabGroup.GameSettings)
            .SetParent(DisableSkeldDevices);

        LoadingPercentage = 76;

        DisableMiraHQDevices = BooleanOptionItem.Create(22908, "DisableMiraHQDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices);
        DisableMiraHQAdmin = BooleanOptionItem.Create(22909, "DisableMiraHQAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableMiraHQDevices);
        DisableMiraHQDoorLog = BooleanOptionItem.Create(22910, "DisableMiraHQDoorLog", false, TabGroup.GameSettings)
            .SetParent(DisableMiraHQDevices);
        DisablePolusDevices = BooleanOptionItem.Create(22911, "DisablePolusDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices);
        DisablePolusAdmin = BooleanOptionItem.Create(22912, "DisablePolusAdmin", false, TabGroup.GameSettings)
            .SetParent(DisablePolusDevices);
        DisablePolusCamera = BooleanOptionItem.Create(22913, "DisablePolusCamera", false, TabGroup.GameSettings)
            .SetParent(DisablePolusDevices);
        DisablePolusVital = BooleanOptionItem.Create(22914, "DisablePolusVital", false, TabGroup.GameSettings)
            .SetParent(DisablePolusDevices);
        DisableAirshipDevices = BooleanOptionItem.Create(22915, "DisableAirshipDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices);
        DisableAirshipCockpitAdmin = BooleanOptionItem.Create(22916, "DisableAirshipCockpitAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices);

        LoadingPercentage = 77;

        DisableAirshipRecordsAdmin = BooleanOptionItem.Create(22917, "DisableAirshipRecordsAdmin", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices);
        DisableAirshipCamera = BooleanOptionItem.Create(22918, "DisableAirshipCamera", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices);
        DisableAirshipVital = BooleanOptionItem.Create(22919, "DisableAirshipVital", false, TabGroup.GameSettings)
            .SetParent(DisableAirshipDevices);
        DisableFungleDevices = BooleanOptionItem.Create(22925, "DisableFungleDevices", false, TabGroup.GameSettings)
            .SetParent(DisableDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableFungleCamera = BooleanOptionItem.Create(22926, "DisableFungleCamera", false, TabGroup.GameSettings)
            .SetParent(DisableFungleDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableFungleVital = BooleanOptionItem.Create(22927, "DisableFungleVital", false, TabGroup.GameSettings)
            .SetParent(DisableFungleDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevicesIgnoreConditions = BooleanOptionItem.Create(22920, "IgnoreConditions", false, TabGroup.GameSettings)
            .SetParent(DisableDevices);
        DisableDevicesIgnoreImpostors = BooleanOptionItem.Create(22921, "IgnoreImpostors", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions);
        DisableDevicesIgnoreNeutrals = BooleanOptionItem.Create(22922, "IgnoreNeutrals", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions);
        DisableDevicesIgnoreCrewmates = BooleanOptionItem.Create(22923, "IgnoreCrewmates", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions);
        DisableDevicesIgnoreAfterAnyoneDied = BooleanOptionItem.Create(22924, "IgnoreAfterAnyoneDied", false, TabGroup.GameSettings)
            .SetParent(DisableDevicesIgnoreConditions)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 78;


        UsePets = BooleanOptionItem.Create(23850, "UsePets", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(60, 0, 255, byte.MaxValue));
        PetToAssignToEveryone = StringOptionItem.Create(23854, "PetToAssign", PetToAssign, 24, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(UsePets)
            .SetColor(new Color32(60, 0, 255, byte.MaxValue));

        UseVoteCancelling = BooleanOptionItem.Create(23852, "UseVoteCancelling", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(0, 65, 196, byte.MaxValue));

        EveryoneCanVent = BooleanOptionItem.Create(23853, "EveryoneCanVent", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(Color.green);
        OverrideScientistBasedRoles = BooleanOptionItem.Create(23855, "OverrideScientistBasedRoles", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(EveryoneCanVent);
        WhackAMole = BooleanOptionItem.Create(23856, "WhackAMole", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetParent(EveryoneCanVent);

        SpawnAdditionalRefugeeOnImpsDead = BooleanOptionItem.Create(23857, "SpawnAdditionalRefugeeOnImpsDead", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.magenta)
            .SetHeader(true);
        SpawnAdditionalRefugeeWhenNKAlive = BooleanOptionItem.Create(23858, "SpawnAdditionalRefugeeWhenNKAlive", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.magenta)
            .SetParent(SpawnAdditionalRefugeeOnImpsDead);
        SpawnAdditionalRefugeeMinAlivePlayers = IntegerOptionItem.Create(23859, "SpawnAdditionalRefugeeMinAlivePlayers", new(1, 14, 1), 7, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.magenta)
            .SetParent(SpawnAdditionalRefugeeOnImpsDead);

        AprilFoolsMode = BooleanOptionItem.Create(23860, "AprilFoolsMode", Main.IsAprilFools, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));


        // Disable Short Tasks
        DisableShortTasks = BooleanOptionItem.Create(23000, "DisableShortTasks", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableCleanVent = BooleanOptionItem.Create(23001, "DisableCleanVent", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCalibrateDistributor = BooleanOptionItem.Create(23002, "DisableCalibrateDistributor", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableChartCourse = BooleanOptionItem.Create(23003, "DisableChartCourse", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 79;

        DisableStabilizeSteering = BooleanOptionItem.Create(23004, "DisableStabilizeSteering", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCleanO2Filter = BooleanOptionItem.Create(23005, "DisableCleanO2Filter", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableUnlockManifolds = BooleanOptionItem.Create(23006, "DisableUnlockManifolds", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePrimeShields = BooleanOptionItem.Create(23007, "DisablePrimeShields", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMeasureWeather = BooleanOptionItem.Create(23008, "DisableMeasureWeather", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 80;

        DisableBuyBeverage = BooleanOptionItem.Create(23009, "DisableBuyBeverage", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableAssembleArtifact = BooleanOptionItem.Create(23010, "DisableAssembleArtifact", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableSortSamples = BooleanOptionItem.Create(23011, "DisableSortSamples", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableProcessData = BooleanOptionItem.Create(23012, "DisableProcessData", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRunDiagnostics = BooleanOptionItem.Create(23013, "DisableRunDiagnostics", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 81;

        DisableRepairDrill = BooleanOptionItem.Create(23014, "DisableRepairDrill", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableAlignTelescope = BooleanOptionItem.Create(23015, "DisableAlignTelescope", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRecordTemperature = BooleanOptionItem.Create(23016, "DisableRecordTemperature", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFillCanisters = BooleanOptionItem.Create(23017, "DisableFillCanisters", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 82;

        DisableMonitorTree = BooleanOptionItem.Create(23018, "DisableMonitorTree", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableStoreArtifacts = BooleanOptionItem.Create(23019, "DisableStoreArtifacts", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePutAwayPistols = BooleanOptionItem.Create(23020, "DisablePutAwayPistols", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePutAwayRifles = BooleanOptionItem.Create(23021, "DisablePutAwayRifles", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMakeBurger = BooleanOptionItem.Create(23022, "DisableMakeBurger", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 83;

        DisableCleanToilet = BooleanOptionItem.Create(23023, "DisableCleanToilet", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDecontaminate = BooleanOptionItem.Create(23024, "DisableDecontaminate", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableSortRecords = BooleanOptionItem.Create(23025, "DisableSortRecords", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFixShower = BooleanOptionItem.Create(23026, "DisableFixShower", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePickUpTowels = BooleanOptionItem.Create(23027, "DisablePickUpTowels", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePolishRuby = BooleanOptionItem.Create(23028, "DisablePolishRuby", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDressMannequin = BooleanOptionItem.Create(23029, "DisableDressMannequin", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRoastMarshmallow = BooleanOptionItem.Create(23030, "DisableRoastMarshmallow", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCollectSamples = BooleanOptionItem.Create(23031, "DisableCollectSamples", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableReplaceParts = BooleanOptionItem.Create(23032, "DisableReplaceParts", false, TabGroup.TaskSettings)
            .SetParent(DisableShortTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 84;


        // Disable Common Tasks
        DisableCommonTasks = BooleanOptionItem.Create(23100, "DisableCommonTasks", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableSwipeCard = BooleanOptionItem.Create(23101, "DisableSwipeCardTask", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFixWiring = BooleanOptionItem.Create(23102, "DisableFixWiring", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableEnterIdCode = BooleanOptionItem.Create(23103, "DisableEnterIdCode", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableInsertKeys = BooleanOptionItem.Create(23104, "DisableInsertKeys", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableScanBoardingPass = BooleanOptionItem.Create(23105, "DisableScanBoardingPass", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCollectVegetables = BooleanOptionItem.Create(23106, "DisableCollectVegetables", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMineOres = BooleanOptionItem.Create(23107, "DisableMineOres", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableExtractFuel = BooleanOptionItem.Create(23108, "DisableExtractFuel", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCatchFish = BooleanOptionItem.Create(23109, "DisableCatchFish", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePolishGem = BooleanOptionItem.Create(23110, "DisablePolishGem", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableHelpCritter = BooleanOptionItem.Create(23111, "DisableHelpCritter", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableHoistSupplies = BooleanOptionItem.Create(23112, "DisableHoistSupplies", false, TabGroup.TaskSettings)
            .SetParent(DisableCommonTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 85;

        // Disable Long Tasks
        DisableLongTasks = BooleanOptionItem.Create(23150, "DisableLongTasks", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableSubmitScan = BooleanOptionItem.Create(23151, "DisableSubmitScanTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableUnlockSafe = BooleanOptionItem.Create(23152, "DisableUnlockSafeTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableStartReactor = BooleanOptionItem.Create(23153, "DisableStartReactorTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableResetBreaker = BooleanOptionItem.Create(23154, "DisableResetBreakerTask", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 86;

        DisableAlignEngineOutput = BooleanOptionItem.Create(23155, "DisableAlignEngineOutput", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableInspectSample = BooleanOptionItem.Create(23156, "DisableInspectSample", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableEmptyChute = BooleanOptionItem.Create(23157, "DisableEmptyChute", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableClearAsteroids = BooleanOptionItem.Create(23158, "DisableClearAsteroids", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableWaterPlants = BooleanOptionItem.Create(23159, "DisableWaterPlants", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableOpenWaterways = BooleanOptionItem.Create(23160, "DisableOpenWaterways", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 87;

        DisableReplaceWaterJug = BooleanOptionItem.Create(23161, "DisableReplaceWaterJug", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRebootWifi = BooleanOptionItem.Create(23162, "DisableRebootWifi", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevelopPhotos = BooleanOptionItem.Create(23163, "DisableDevelopPhotos", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableRewindTapes = BooleanOptionItem.Create(23164, "DisableRewindTapes", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableStartFans = BooleanOptionItem.Create(23165, "DisableStartFans", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFixAntenna = BooleanOptionItem.Create(23166, "DisableFixAntenna", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableBuildSandcastle = BooleanOptionItem.Create(23167, "DisableBuildSandcastle", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 88;

        DisableCrankGenerator = BooleanOptionItem.Create(23168, "DisableCrankGenerator", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableMonitorMushroom = BooleanOptionItem.Create(23169, "DisableMonitorMushroom", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisablePlayVideoGame = BooleanOptionItem.Create(23170, "DisablePlayVideoGame", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFindSignal = BooleanOptionItem.Create(23171, "DisableFindSignal", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableThrowFisbee = BooleanOptionItem.Create(23172, "DisableThrowFisbee", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableLiftWeights = BooleanOptionItem.Create(23173, "DisableLiftWeights", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableCollectShells = BooleanOptionItem.Create(23174, "DisableCollectShells", false, TabGroup.TaskSettings)
            .SetParent(DisableLongTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 89;


        // Disable Divert Power, Weather Nodes etc. situational Tasks
        DisableOtherTasks = BooleanOptionItem.Create(23200, "DisableOtherTasks", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(239, 89, 175, byte.MaxValue));
        DisableUploadData = BooleanOptionItem.Create(23205, "DisableUploadDataTask", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableEmptyGarbage = BooleanOptionItem.Create(23206, "DisableEmptyGarbage", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableFuelEngines = BooleanOptionItem.Create(23207, "DisableFuelEngines", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableDivertPower = BooleanOptionItem.Create(23208, "DisableDivertPower", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableActivateWeatherNodes = BooleanOptionItem.Create(23209, "DisableActivateWeatherNodes", false, TabGroup.TaskSettings)
            .SetParent(DisableOtherTasks)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 90;
        MainLoadingText = "Building Guesser Mode settings";

        TextOptionItem.Create(100022, "MenuTitle.Guessers", TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.yellow)
            .SetHeader(true);
        GuesserMode = BooleanOptionItem.Create(19700, "GuesserMode", false, TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.yellow)
            .SetHeader(true);
        CrewmatesCanGuess = BooleanOptionItem.Create(19710, "CrewmatesCanGuess", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        ImpostorsCanGuess = BooleanOptionItem.Create(19711, "ImpostorsCanGuess", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);

        LoadingPercentage = 91;

        NeutralKillersCanGuess = BooleanOptionItem.Create(19712, "NeutralKillersCanGuess", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        PassiveNeutralsCanGuess = BooleanOptionItem.Create(19713, "PassiveNeutralsCanGuess", false, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        CanGuessAddons = BooleanOptionItem.Create(19714, "CanGuessAddons", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        CrewCanGuessCrew = BooleanOptionItem.Create(19715, "CrewCanGuessCrew", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        ImpCanGuessImp = BooleanOptionItem.Create(19716, "ImpCanGuessImp", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode);
        HideGuesserCommands = BooleanOptionItem.Create(19717, "GuesserTryHideMsg", true, TabGroup.TaskSettings)
            .SetParent(GuesserMode)
            .SetColor(Color.green);

        LoadingPercentage = 92;


        TextOptionItem.Create(100050, "MenuTitle.GuesserModeRoles", TabGroup.TaskSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Color.yellow)
            .SetHeader(true);
        SetupAdtRoleOptions(14500, CustomRoles.Onbound, canSetNum: true, tab: TabGroup.TaskSettings);
        ImpCanBeOnbound = BooleanOptionItem.Create(14510, "ImpCanBeOnbound", true, TabGroup.TaskSettings)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Onbound]);
        CrewCanBeOnbound = BooleanOptionItem.Create(14511, "CrewCanBeOnbound", true, TabGroup.TaskSettings)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Onbound]);
        NeutralCanBeOnbound = BooleanOptionItem.Create(14512, "NeutralCanBeOnbound", true, TabGroup.TaskSettings)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Onbound]);


        LoadingPercentage = 93;
        MainLoadingText = "Building game settings";


        TextOptionItem.Create(100027, "MenuTitle.CTA", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(215, 227, 84, byte.MaxValue));

        CTAPlayersCanWinWithOriginalTeam = BooleanOptionItem.Create(23550, "CTA.PlayersCanWinWithOriginalTeam", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(215, 227, 84, byte.MaxValue));
        CTAPlayersCanSeeEachOthersRoles = BooleanOptionItem.Create(23551, "CTA.PlayersCanSeeEachOthersRoles", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(215, 227, 84, byte.MaxValue));


        TextOptionItem.Create(100037, "MenuTitle.Meeting", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        SyncButtonMode = BooleanOptionItem.Create(23300, "SyncButtonMode", false, TabGroup.GameSettings)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue))
            .SetHeader(true)
            .SetGameMode(CustomGameMode.Standard);
        SyncedButtonCount = IntegerOptionItem.Create(23310, "SyncedButtonCount", new(0, 100, 1), 10, TabGroup.GameSettings)
            .SetParent(SyncButtonMode)
            .SetValueFormat(OptionFormat.Times)
            .SetGameMode(CustomGameMode.Standard);

        AllAliveMeeting = BooleanOptionItem.Create(23400, "AllAliveMeeting", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));
        AllAliveMeetingTime = FloatOptionItem.Create(23410, "AllAliveMeetingTime", new(1f, 300f, 1f), 10f, TabGroup.GameSettings)
            .SetParent(AllAliveMeeting)
            .SetValueFormat(OptionFormat.Seconds);

        EnableKillerLeftCommand = BooleanOptionItem.Create(44428, "EnableKillerLeftCommand", true, TabGroup.GameSettings)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        SeeEjectedRolesInMeeting = BooleanOptionItem.Create(44429, "SeeEjectedRolesInMeeting", true, TabGroup.GameSettings)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        LoadingPercentage = 94;


        AdditionalEmergencyCooldown = BooleanOptionItem.Create(23500, "AdditionalEmergencyCooldown", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));
        AdditionalEmergencyCooldownThreshold = IntegerOptionItem.Create(23510, "AdditionalEmergencyCooldownThreshold", new(1, 15, 1), 1, TabGroup.GameSettings)
            .SetParent(AdditionalEmergencyCooldown)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);
        AdditionalEmergencyCooldownTime = FloatOptionItem.Create(23511, "AdditionalEmergencyCooldownTime", new(1f, 60f, 1f), 1f, TabGroup.GameSettings)
            .SetParent(AdditionalEmergencyCooldown)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Seconds);

        LoadingPercentage = 95;

        VoteMode = BooleanOptionItem.Create(23600, "VoteMode", false, TabGroup.GameSettings)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVote = StringOptionItem.Create(23610, "WhenSkipVote", VoteModes[..3], 0, TabGroup.GameSettings)
            .SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreFirstMeeting = BooleanOptionItem.Create(23611, "WhenSkipVoteIgnoreFirstMeeting", false, TabGroup.GameSettings)
            .SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreNoDeadBody = BooleanOptionItem.Create(23612, "WhenSkipVoteIgnoreNoDeadBody", false, TabGroup.GameSettings)
            .SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreEmergency = BooleanOptionItem.Create(23613, "WhenSkipVoteIgnoreEmergency", false, TabGroup.GameSettings)
            .SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenNonVote = StringOptionItem.Create(23700, "WhenNonVote", VoteModes, 0, TabGroup.GameSettings)
            .SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);
        WhenTie = StringOptionItem.Create(23750, "WhenTie", TieModes, 0, TabGroup.GameSettings)
            .SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);

        LoadingPercentage = 96;


        TextOptionItem.Create(100028, "MenuTitle.Other", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        LadderDeath = BooleanOptionItem.Create(23800, "LadderDeath", false, TabGroup.GameSettings)
            .SetHeader(true)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));
        LadderDeathChance = StringOptionItem.Create(23810, "LadderDeathChance", Rates[1..], 0, TabGroup.GameSettings)
            .SetParent(LadderDeath);

        LoadingPercentage = 97;


        FixFirstKillCooldown = BooleanOptionItem.Create(23900, "FixFirstKillCooldown", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        StartingKillCooldown = FloatOptionItem.Create(23950, "StartingKillCooldown", new(1, 60, 1), 18, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        ShieldPersonDiedFirst = BooleanOptionItem.Create(24000, "ShieldPersonDiedFirst", false, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        LoadingPercentage = 98;


        KillFlashDuration = FloatOptionItem.Create(24100, "KillFlashDuration", new(0.1f, 0.45f, 0.05f), 0.3f, TabGroup.GameSettings)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        UniqueNeutralRevealScreen = BooleanOptionItem.Create(24450, "UniqueNeutralRevealScreen", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));


        TextOptionItem.Create(100029, "MenuTitle.Ghost", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));

        LoadingPercentage = 99;


        GhostCanSeeOtherRoles = BooleanOptionItem.Create(24300, "GhostCanSeeOtherRoles", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeOtherVotes = BooleanOptionItem.Create(24400, "GhostCanSeeOtherVotes", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeDeathReason = BooleanOptionItem.Create(24500, "GhostCanSeeDeathReason", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));

        LoadingPercentage = 100;

        #endregion

        OptionSaver.Load();

        IsLoaded = true;
    }

    public static void SetupRoleOptions(int id, TabGroup tab, CustomRoles role, CustomGameMode customGameMode = CustomGameMode.Standard, bool zeroOne = false)
    {
        var spawnOption = StringOptionItem.Create(id, role.ToString(), zeroOne ? RatesZeroOne : Rates, 0, tab).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        CustomRoleSpawnChances.Add(role, spawnOption);

        var countOption = IntegerOptionItem.Create(id + 1, "Maximum", new(1, 15, 1), 1, tab)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Players)
            .SetGameMode(customGameMode);

        CustomRoleCounts.Add(role, countOption);
    }

    public static void SetupAdtRoleOptions(int id, CustomRoles role, CustomGameMode customGameMode = CustomGameMode.Standard, bool canSetNum = false, TabGroup tab = TabGroup.Addons, bool canSetChance = true, bool teamSpawnOptions = false)
    {
        var spawnOption = StringOptionItem.Create(id, role.ToString(), RatesZeroOne, 0, tab).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        var countOption = IntegerOptionItem.Create(id + 1, "Maximum", new(1, canSetNum ? 15 : 1, 1), 1, tab)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Players)
            .SetHidden(!canSetNum)
            .SetGameMode(customGameMode);

        var spawnRateOption = IntegerOptionItem.Create(id + 2, "AdditionRolesSpawnRate", new(0, 100, 5), canSetChance ? 65 : 100, tab)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Percent)
            .SetHidden(!canSetChance)
            .SetGameMode(customGameMode) as IntegerOptionItem;

        if (teamSpawnOptions)
        {
            var impOption = BooleanOptionItem.Create(id + 3, "ImpCanBeRole", true, tab)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode)
                .AddReplacement(("{role}", role.ToColoredString()));

            var neutralOption = BooleanOptionItem.Create(id + 4, "NeutralCanBeRole", true, tab)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode)
                .AddReplacement(("{role}", role.ToColoredString()));

            var crewOption = BooleanOptionItem.Create(id + 5, "CrewCanBeRole", true, tab)
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
        var spawnOption = StringOptionItem.Create(id, role.ToString(), zeroOne ? RatesZeroOne : Rates, 0, tab).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        var countOption = IntegerOptionItem.Create(id + 1, "Maximum", new(count, count, count), count, tab)
            .SetParent(spawnOption)
            .SetHidden(hideMaxSetting)
            .SetGameMode(customGameMode);

        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
        SingleRoles.Add(role);
    }

    public static OptionItem CreateCDSetting(int id, TabGroup tab, CustomRoles role, bool isKCD = false) =>
        FloatOptionItem.Create(id, isKCD ? "KillCooldown" : "AbilityCooldown", new(0f, 180f, 2.5f), 30f, tab)
            .SetParent(CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Seconds);

    public static OptionItem CreatePetUseSetting(int id, CustomRoles role) =>
        BooleanOptionItem.Create(id, "UsePetInsteadOfKillButton", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[role])
            .SetColor(Color.magenta);

    public static OptionItem CreateVoteCancellingUseSetting(int id, CustomRoles role, TabGroup tab) =>
        BooleanOptionItem.Create(id, "UseVoteCancellingAfterVote", false, tab)
            .SetParent(CustomRoleSpawnChances[role])
            .SetColor(Color.yellow);

    public class OverrideTasksData
    {
        public static Dictionary<CustomRoles, OverrideTasksData> AllData = [];
        public OptionItem AssignCommonTasks;
        public OptionItem DoOverride;
        public OptionItem NumLongTasks;
        public OptionItem NumShortTasks;

        public OverrideTasksData(int idStart, TabGroup tab, CustomRoles role)
        {
            IdStart = idStart;
            Role = role;
            Dictionary<string, string> replacementDic = new() { { "%role%", Utils.ColorString(Utils.GetRoleColor(role), Utils.GetRoleName(role)) } };
            DoOverride = BooleanOptionItem.Create(idStart++, "doOverride", false, tab)
                .SetParent(CustomRoleSpawnChances[role])
                .SetValueFormat(OptionFormat.None);
            DoOverride.ReplacementDictionary = replacementDic;
            AssignCommonTasks = BooleanOptionItem.Create(idStart++, "assignCommonTasks", true, tab)
                .SetParent(DoOverride)
                .SetValueFormat(OptionFormat.None);
            AssignCommonTasks.ReplacementDictionary = replacementDic;
            NumLongTasks = IntegerOptionItem.Create(idStart++, "roleLongTasksNum", new(0, 90, 1), 3, tab)
                .SetParent(DoOverride)
                .SetValueFormat(OptionFormat.Pieces);
            NumLongTasks.ReplacementDictionary = replacementDic;
            NumShortTasks = IntegerOptionItem.Create(idStart, "roleShortTasksNum", new(0, 90, 1), 3, tab)
                .SetParent(DoOverride)
                .SetValueFormat(OptionFormat.Pieces);
            NumShortTasks.ReplacementDictionary = replacementDic;

            if (!AllData.ContainsKey(role)) AllData.Add(role, this);
            else Logger.Warn("OverrideTasksData created for duplicate CustomRoles", "OverrideTasksData");
        }

        public CustomRoles Role { get; private set; }
        public int IdStart { get; private set; }

        public static OverrideTasksData Create(int idStart, TabGroup tab, CustomRoles role) => new(idStart, tab, role);
    }
}