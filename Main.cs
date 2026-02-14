using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using EHR;
using EHR.Modules;
using EHR.Patches;
using EHR.Roles;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.Networking;

[assembly: AssemblyFileVersion(Main.PluginVersion)]
[assembly: AssemblyInformationalVersion(Main.PluginVersion)]
[assembly: AssemblyVersion(Main.PluginVersion)]

namespace EHR;

[BepInPlugin(PluginGuid, "EHR", PluginVersion)]
[BepInIncompatibility("jp.ykundesu.supernewroles")]
[BepInIncompatibility("MalumMenu")]
[BepInIncompatibility("com.ten.thebetterroles")]
[BepInIncompatibility("xyz.crowdedmods.crowdedmod")]
[BepInDependency(SubmergedCompatibility.SubmergedGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInProcess("Among Us.exe")]
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class Main : BasePlugin
{
    private const string DebugKeyHash = "c0fd562955ba56af3ae20d7ec9e64c664f0facecef4b3e366e109306adeae29d";
    private const string DebugKeySalt = "59687b";
    private const string PluginGuid = "com.gurge44.endlesshostroles";
    public const string PluginVersion = "7.3.0";
    public const string PluginDisplayVersion = "7.3.0";
    public const bool TestBuild = false;

    public const string NeutralColor = "#ffab1b";
    public const string ImpostorColor = "#ff1919";
    public const string CrewmateColor = "#8cffff";
    public const string CovenColor = "#7b3fbb";

    public const float MinSpeed = 0.0001f;

    // == プログラム設定 / Program Config ==
    public const string ModName = "EHR";
    public const string ModColor = "#00ffff";
    public const bool AllowPublicRoom = true;
    public const string ForkId = "EHR";
    public const string SupportedAUVersion = "2025.9.9";

    public static readonly string DataPath = OperatingSystem.IsAndroid() ? Application.persistentDataPath : ".";

    public static readonly Version Version = Version.Parse(PluginVersion);

    //public static ManualLogSource Logger;
    public static bool HasArgumentException;
    public static string CredentialsText;

    // Cache
    public static readonly Type[] AllTypes = Assembly.GetExecutingAssembly().GetTypes();
    public static readonly CustomRoles[] CustomRoleValues = Enum.GetValues<CustomRoles>();

    public static IntPtr? OriginalAffinity;
    public static Dictionary<byte, PlayerVersion> PlayerVersion = [];
    public static OptionBackupData RealOptionsData;
    public static Dictionary<byte, float> KillTimers = [];
    public static Dictionary<byte, PlayerState> PlayerStates = [];
    public static Dictionary<byte, string> AllPlayerNames = [];
    public static Dictionary<int, string> AllClientRealNames = [];
    public static Dictionary<(byte, byte), string> LastNotifyNames = [];
    public static Dictionary<byte, Color32> PlayerColors = [];
    public static Dictionary<byte, PlayerState.DeathReason> AfterMeetingDeathPlayers = [];
    public static Dictionary<CustomRoles, string> RoleColors;
    public static Dictionary<byte, CustomRoles> SetRoles = [];
    public static Dictionary<byte, List<CustomRoles>> SetAddOns = [];
    public static readonly Dictionary<int, Dictionary<CustomRoles, List<CustomRoles>>> AlwaysSpawnTogetherCombos = [];
    public static readonly Dictionary<int, Dictionary<CustomRoles, List<CustomRoles>>> NeverSpawnTogetherCombos = [];
    public static readonly List<(CustomRoles, CustomRoles)> XORRoles = [];
    public static Dictionary<byte, string> LastAddOns = [];
    public static List<RoleBase> AllRoleClasses;
    public static float RefixCooldownDelay;
    public static bool ProcessShapeshifts = true;
    public static readonly Dictionary<byte, (long StartTimeStamp, int TotalCooldown)> AbilityCD = [];
    public static Dictionary<byte, float> AbilityUseLimit = [];
    public static List<byte> DontCancelVoteList = [];
    public static HashSet<byte> ResetCamPlayerList = [];
    public static List<byte> WinnerList = [];
    public static List<string> WinnerNameList = [];
    public static List<int> ClientIdList = [];
    public static Dictionary<byte, float> AllPlayerKillCooldown = [];
    public static Dictionary<byte, Vent> LastEnteredVent = [];
    public static Dictionary<byte, Vector2> LastEnteredVentLocation = [];
    public static bool IsChatCommand;
    public static bool DoBlockNameChange;
    public static int UpdateTime;
    public static readonly Dictionary<int, int> SayStartTimes = [];
    public static readonly Dictionary<int, int> SayBanwordsTimes = [];
    public static Dictionary<byte, float> AllPlayerSpeed = [];
    public static Dictionary<byte, int> GuesserGuessed = [];
    public static Dictionary<byte, int> GuesserGuessedMeeting = [];
    public static bool HasJustStarted;
    public static Dictionary<byte, bool> CheckShapeshift = [];
    public static Dictionary<byte, byte> ShapeshiftTarget = [];
    public static bool VisibleTasksCount;
    public static string NickName = "";
    public static bool IntroDestroyed = true;
    public static float DefaultCrewmateVision;
    public static float DefaultImpostorVision;
    public static readonly bool IsAprilFools = DateTime.Now.Month == 4 && DateTime.Now.Day == 1;
    public static bool ResetOptions = true;
    public static string FirstDied = string.Empty;
    public static string ShieldPlayer = string.Empty;
    public static readonly Dictionary<string, int> GamesPlayed = [];
    public static readonly HashSet<byte> GotShieldAnimationInfoThisGame = [];
    public static readonly HashSet<byte> Invisible = [];
    public static readonly Dictionary<string, Options.UserData> UserData = [];

    public static readonly Dictionary<CustomGameMode, HashSet<string>> HasPlayedGM = new()
    {
        [CustomGameMode.SoloPVP] = [],
        [CustomGameMode.FFA] = [],
        [CustomGameMode.HotPotato] = [],
        [CustomGameMode.HideAndSeek] = [],
        [CustomGameMode.Speedrun] = [],
        [CustomGameMode.CaptureTheFlag] = [],
        [CustomGameMode.NaturalDisasters] = [],
        [CustomGameMode.Snowdown] = []
    };

    public static Dictionary<CustomGameMode, Color> GameModeColors = [];
    public static readonly Dictionary<CustomGameMode, Dictionary<string, int>> NumWinsPerGM = [];
    public static HashSet<byte> DiedThisRound = [];
    public static List<PlayerControl> LoversPlayers = [];
    public static bool IsLoversDead = true;
    public static List<byte> SuperStarDead = [];
    public static List<byte> BaitAlive = [];
    public static Dictionary<byte, int> KilledDiseased = [];
    public static Dictionary<byte, int> KilledAntidote = [];
    public static List<byte> TiebreakerVoteFor = [];
    public static Dictionary<byte, string> SleuthMsgs = [];
    public static Dictionary<byte, int> NumEmergencyMeetingsUsed = [];
    public static int MadmateNum;
    public static uint LobbyBehaviourNetId;
    
    public static Stopwatch GameTimer = new();
    public static bool GameEndDueToTimer;

    public static bool ShowResult = true;

    public static Main Instance;


    public static string OverrideWelcomeMsg = string.Empty;

    public static readonly Dictionary<byte, List<int>> GuessNumber = [];

    private static readonly List<string> NameSnacksCn = ["冰激凌", "奶茶", "巧克力", "蛋糕", "甜甜圈", "可乐", "柠檬水", "冰糖葫芦", "果冻", "糖果", "牛奶", "抹茶", "烧仙草", "菠萝包", "布丁", "椰子冻", "曲奇", "红豆土司", "三彩团子", "艾草团子", "泡芙", "可丽饼", "桃酥", "麻薯", "鸡蛋仔", "马卡龙", "雪梅娘", "炒酸奶", "蛋挞", "松饼", "西米露", "奶冻", "奶酥", "可颂", "奶糖"];

    // ReSharper disable once StringLiteralTypo
    private static readonly List<string> NameSnacksEn = ["Ice cream", "Milk tea", "Chocolate", "Cake", "Donut", "Coke", "Lemonade", "Candied haws", "Jelly", "Candy", "Milk", "Matcha", "Burning Grass Jelly", "Pineapple Bun", "Pudding", "Coconut Jelly", "Cookies", "Red Bean Toast", "Three Color Dumplings", "Wormwood Dumplings", "Puffs", "Can be Crepe", "Peach Crisp", "Mochi", "Egg Waffle", "Macaron", "Snow Plum Niang", "Fried Yogurt", "Egg Tart", "Muffin", "Sago Dew", "panna cotta", "soufflé", "croissant", "toffee"];
    private Coroutines coroutines;

    private static HashAuth DebugKeyAuth { get; set; }
    private static ConfigEntry<string> DebugKeyInput { get; set; }

    public Harmony Harmony { get; } = new(PluginGuid);

    public static NormalGameOptionsV10 NormalOptions => GameOptionsManager.Instance != null ? GameOptionsManager.Instance.currentNormalGameOptions : null;

    // Client Options
    public static ConfigEntry<string> HideName { get; private set; }
    public static ConfigEntry<string> HideColor { get; private set; }
    public static ConfigEntry<int> MessageWait { get; private set; }
    public static ConfigEntry<bool> GM { get; private set; }
    public static ConfigEntry<bool> UnlockFps { get; private set; }
    public static ConfigEntry<bool> ShowFps { get; private set; }
    public static ConfigEntry<bool> AutoStart { get; private set; }
    public static ConfigEntry<bool> ForceOwnLanguage { get; private set; }
    public static ConfigEntry<bool> ForceOwnLanguageRoleName { get; private set; }
    public static ConfigEntry<bool> EnableCustomButton { get; private set; }
    public static ConfigEntry<bool> EnableCustomSoundEffect { get; private set; }
    public static ConfigEntry<bool> SwitchVanilla { get; private set; }
    public static ConfigEntry<bool> GodMode { get; private set; }
    public static ConfigEntry<bool> DarkTheme { get; private set; }
    public static ConfigEntry<bool> DarkThemeForMeetingUI { get; private set; }
    public static ConfigEntry<bool> HorseMode { get; private set; }
    public static ConfigEntry<bool> LongMode { get; private set; }
    public static ConfigEntry<bool> ShowPlayerInfoInLobby { get; private set; }
    public static ConfigEntry<bool> LobbyMusic { get; private set; }
    public static ConfigEntry<bool> EnableCommandHelper { get; private set; }
    public static ConfigEntry<bool> ShowModdedClientText { get; private set; }
    public static ConfigEntry<bool> AutoHaunt { get; private set; }
    public static ConfigEntry<bool> ButtonCooldownInDecimalUnder10s { get; private set; }
    public static ConfigEntry<bool> CancelPetAnimation { get; private set; }
    public static ConfigEntry<bool> TryFixStuttering { get; private set; }
    public static ConfigEntry<float> UIScaleFactor { get; private set; }

    // Preset Name Options
    public static ConfigEntry<string> Preset1 { get; private set; }
    public static ConfigEntry<string> Preset2 { get; private set; }
    public static ConfigEntry<string> Preset3 { get; private set; }
    public static ConfigEntry<string> Preset4 { get; private set; }
    public static ConfigEntry<string> Preset5 { get; private set; }
    public static ConfigEntry<string> Preset6 { get; private set; }
    public static ConfigEntry<string> Preset7 { get; private set; }
    public static ConfigEntry<string> Preset8 { get; private set; }
    public static ConfigEntry<string> Preset9 { get; private set; }
    public static ConfigEntry<string> Preset10 { get; private set; }

    // Other Configs
    public static ConfigEntry<string> WebhookUrl { get; private set; }
    public static ConfigEntry<string> BetaBuildUrl { get; private set; }
    public static ConfigEntry<float> LastKillCooldown { get; private set; }
    public static ConfigEntry<float> LastShapeshifterCooldown { get; private set; }

    public static IReadOnlyList<PlayerControl> AllPlayerControls => EnumeratePlayerControls().ToArray();
    public static IReadOnlyList<PlayerControl> AllAlivePlayerControls => EnumerateAlivePlayerControls().ToArray();

    public static IEnumerable<PlayerControl> EnumeratePlayerControls()
    {
        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            if (!pc || pc.PlayerId >= 254) continue;
            yield return pc;
        }
    }

    public static IEnumerable<PlayerControl> EnumerateAlivePlayerControls()
    {
        return EnumeratePlayerControls()
            .Where(pc => pc.IsAlive()
                         && pc.Data
                         && (!pc.Data.Disconnected || !IntroDestroyed)
                         && !Pelican.IsEaten(pc.PlayerId));
    }

    // ReSharper disable once InconsistentNaming
    public static string Get_TName_Snacks => TranslationController.Instance.currentLanguage.languageID is SupportedLangs.SChinese or SupportedLangs.TChinese ? NameSnacksCn.RandomElement() : NameSnacksEn.RandomElement();

    public static NetworkedPlayerInfo LastVotedPlayerInfo { get; set; }

    public static MapNames CurrentMap => (MapNames)NormalOptions.MapId;

    public static bool LIMap => NormalOptions is { MapId: 7 };

    public override void Load()
    {
        // EmbeddedDeps.Install();
        Instance = this;

        //Client Options
        HideName = Config.Bind("Client Options", "Hide Game Code Name", "EHR");
        HideColor = Config.Bind("Client Options", "Hide Game Code Color", $"{ModColor}");
        DebugKeyInput = Config.Bind("Authentication", "Debug Key", string.Empty);
        AutoStart = Config.Bind("Client Options", "AutoStart", false);
        GM = Config.Bind("Client Options", "GM", false);
        UnlockFps = Config.Bind("Client Options", "UnlockFPS", false);
        ShowFps = Config.Bind("Client Options", "ShowFPS", false);
        AutoStart = Config.Bind("Client Options", "AutoStart", false);
        ForceOwnLanguage = Config.Bind("Client Options", "ForceOwnLanguage", false);
        ForceOwnLanguageRoleName = Config.Bind("Client Options", "ForceOwnLanguageRoleName", false);
        EnableCustomButton = Config.Bind("Client Options", "EnableCustomButton", true);
        EnableCustomSoundEffect = Config.Bind("Client Options", "EnableCustomSoundEffect", true);
        SwitchVanilla = Config.Bind("Client Options", "SwitchVanilla", false);
        GodMode = Config.Bind("Client Options", "GodMode", false);
        DarkTheme = Config.Bind("Client Options", "DarkTheme", true);
        DarkThemeForMeetingUI = Config.Bind("Client Options", "DarkThemeForMeetingUI", false);
        HorseMode = Config.Bind("Client Options", "HorseMode", false);
        LongMode = Config.Bind("Client Options", "LongMode", false);
        ShowPlayerInfoInLobby = Config.Bind("Client Options", "ShowPlayerInfoInLobby", false);
        LobbyMusic = Config.Bind("Client Options", "LobbyMusic", true);
        EnableCommandHelper = Config.Bind("Client Options", "EnableCommandHelper", true);
        ShowModdedClientText = Config.Bind("Client Options", "ShowModdedClientText", true);
        AutoHaunt = Config.Bind("Client Options", "AutoHaunt", false);
        ButtonCooldownInDecimalUnder10s = Config.Bind("Client Options", "ButtonCooldownInDecimalUnder10s", false);
        CancelPetAnimation = Config.Bind("Client Options", "CancelPetAnimation", true);
        TryFixStuttering = Config.Bind("Client Options", "TryFixStuttering", true);
        UIScaleFactor = Config.Bind("Client Options", "UIScaleFactor", 1f);

        //Logger = BepInEx.Logging.Logger.CreateLogSource("EHR");
        coroutines = AddComponent<Coroutines>();
        Logger.Enable();
        Logger.Disable("NotifyRoles");
        Logger.Disable("SwitchSystem");
        Logger.Disable("ModNews");
        //Logger.Disable("CustomRpcSender");

        if (!DebugModeManager.AmDebugger)
        {
            Logger.Disable("2018k");
            Logger.Disable("Github");
            // Logger.Disable("SendRPC");
            Logger.Disable("SetRole");
            Logger.Disable("Info.Role");
            //Logger.Disable("TaskState.Init");
            Logger.Disable("RpcSetNamePrivate");
            Logger.Disable("SetName");
            Logger.Disable("PlayerControl.RpcSetRole");
        }
        //EHR.Logger.isDetail = true;

        // Authentication related - Initialization
        DebugKeyAuth = new(DebugKeyHash, DebugKeySalt);

        DebugModeManager.Auth(DebugKeyAuth, DebugKeyInput.Value);

        Preset1 = Config.Bind("Preset Name Options", "Preset1", "Preset_1");
        Preset2 = Config.Bind("Preset Name Options", "Preset2", "Preset_2");
        Preset3 = Config.Bind("Preset Name Options", "Preset3", "Preset_3");
        Preset4 = Config.Bind("Preset Name Options", "Preset4", "Preset_4");
        Preset5 = Config.Bind("Preset Name Options", "Preset5", "Preset_5");
        Preset6 = Config.Bind("Preset Name Options", "Preset6", "Preset_6");
        Preset7 = Config.Bind("Preset Name Options", "Preset7", "Preset_7");
        Preset8 = Config.Bind("Preset Name Options", "Preset8", "Preset_8");
        Preset9 = Config.Bind("Preset Name Options", "Preset9", "Preset_9");
        Preset10 = Config.Bind("Preset Name Options", "Preset10", "Preset_10");
        WebhookUrl = Config.Bind("Other", "WebhookURL", "none");
        BetaBuildUrl = Config.Bind("Other", "BetaBuildURL", string.Empty);
        MessageWait = Config.Bind("Other", "MessageWait", 0);
        LastKillCooldown = Config.Bind("Other", "LastKillCooldown", (float)30);
        LastShapeshifterCooldown = Config.Bind("Other", "LastShapeshifterCooldown", (float)30);

        HasArgumentException = false;

        try
        {
            RoleColors = new()
            {
                // Vanilla
                { CustomRoles.Crewmate, "#8cffff" },
                { CustomRoles.Engineer, "#FF6A00" },
                { CustomRoles.Scientist, "#8ee98e" },
                { CustomRoles.GuardianAngel, "#77e6d1" },
                { CustomRoles.Tracker, "#34ad50" },
                { CustomRoles.Noisemaker, "#ff4a62" },
                { CustomRoles.Detective, "#625EEE" },
                // Vanilla Remakes
                { CustomRoles.CrewmateEHR, "#8cffff" },
                { CustomRoles.EngineerEHR, "#FF6A00" },
                { CustomRoles.ScientistEHR, "#8ee98e" },
                { CustomRoles.GuardianAngelEHR, "#77e6d1" },
                { CustomRoles.TrackerEHR, "#34ad50" },
                { CustomRoles.NoisemakerEHR, "#ff4a62" },
                { CustomRoles.DetectiveEHR, "#625EEE" },
                // Crewmates
                { CustomRoles.DoubleAgent, "#ff1919" },
                { CustomRoles.Luckey, "#b8d7a3" },
                { CustomRoles.LazyGuy, "#a4dffe" },
                { CustomRoles.Mechanic, "#3333ff" },
                { CustomRoles.Snitch, "#b8fb4f" },
                { CustomRoles.Marshall, "#5573aa" },
                { CustomRoles.Mayor, "#204d42" },
                { CustomRoles.Paranoid, "#c993f5" },
                { CustomRoles.Psychic, "#6F698C" },
                { CustomRoles.Sheriff, "#ffb347" },
                { CustomRoles.CopyCat, "#ffb2ab" },
                { CustomRoles.SuperStar, "#f6f657" },
                { CustomRoles.Ventguard, "#ffa5ff" },
                { CustomRoles.Demolitionist, "#5e2801" },
                { CustomRoles.Express, "#00ffff" },
                { CustomRoles.NiceEraser, "#00a5ff" },
                { CustomRoles.TaskManager, "#00ffa5" },
                { CustomRoles.Adventurer, "#fbbb04" },
                { CustomRoles.Randomizer, "#fcba03" },
                { CustomRoles.Beacon, "#a3fdff" },
                { CustomRoles.Rabbit, "#88d2ff" },
                { CustomRoles.Shiftguard, "#eb34cc" },
                { CustomRoles.Mole, "#00ff80" },
                { CustomRoles.Markseeker, "#f2a0f1" },
                { CustomRoles.Sentinel, "#4bc8d6" },
                { CustomRoles.Electric, "#fbff00" },
                { CustomRoles.Tornado, "#303030" },
                { CustomRoles.Dad, "#037bfc" },
                { CustomRoles.Insight, "#26ff38" },
                { CustomRoles.Tunneler, "#543232" },
                { CustomRoles.Detour, "#ffd35c" },
                { CustomRoles.Gaulois, "#42d1f5" },
                { CustomRoles.Druid, "#ffb694" },
                { CustomRoles.Catcher, "#aaf2cf" },
                { CustomRoles.Autocrat, "#e2ed64" },
                { CustomRoles.LovingCrewmate, "#ff9ace" },
                { CustomRoles.LovingImpostor, "#ff9ace" },
                { CustomRoles.ToiletMaster, "#4281f5" },
                { CustomRoles.Goose, "#f9ffb8" },
                { CustomRoles.Sentry, "#db55f2" },
                { CustomRoles.Perceiver, "#ebeb34" },
                { CustomRoles.Convener, "#34eb7a" },
                { CustomRoles.Mathematician, "#eb3474" },
                { CustomRoles.Helper, "#fcf1bd" },
                { CustomRoles.Astral, "#b329d6" },
                { CustomRoles.Gardener, "#00ff00" },
                { CustomRoles.Farmer, "#FFDE59" },
                { CustomRoles.Vacuum, "#E44CD6" },
                { CustomRoles.Carrier, "#5DE2E7" },
                { CustomRoles.Transmitter, "#c9a11e" },
                { CustomRoles.Sensor, "#a3f7ff" },
                { CustomRoles.Doorjammer, "#FFECA1" },
                { CustomRoles.Captain, "#53B3EF" },
                { CustomRoles.Tree, "#00ff00" },
                { CustomRoles.Inquisitor, "#7726B6" },
                { CustomRoles.Imitator, "#c99e28" },
                { CustomRoles.PortalMaker, "#700078" },
                { CustomRoles.Ankylosaurus, "#7FE44C" },
                { CustomRoles.Leery, "#32a852" },
                { CustomRoles.Wizard, "#FD05CC" },
                { CustomRoles.Negotiator, "#00c3ff" },
                { CustomRoles.Grappler, "#befc03" },
                { CustomRoles.Journalist, "#fcba03" },
                { CustomRoles.Whisperer, "#82919e" },
                { CustomRoles.Car, "#6e6e6e" },
                { CustomRoles.President, "#30916f" },
                { CustomRoles.Oxyman, "#ffa58c" },
                { CustomRoles.Rhapsode, "#f5ad42" },
                { CustomRoles.Chef, "#d6d6ff" },
                { CustomRoles.Decryptor, "#14ba7d" },
                { CustomRoles.Socialite, "#32a8a8" },
                { CustomRoles.Adrenaline, "#ffff00" },
                { CustomRoles.Safeguard, "#4949e3" },
                { CustomRoles.Clairvoyant, "#d4ffdd" },
                { CustomRoles.Inquirer, "#7c55f2" },
                { CustomRoles.Soothsayer, "#4e529c" },
                { CustomRoles.Telekinetic, "#d6c618" },
                { CustomRoles.Doppelganger, "#f6f4a3" },
                { CustomRoles.Nightmare, "#1e1247" },
                { CustomRoles.Battery, "#00ffff" },
                { CustomRoles.Altruist, "#300000" },
                { CustomRoles.Bane, "#745da3" },
                { CustomRoles.Benefactor, "#4aeaff" },
                { CustomRoles.MeetingManager, "#d4ff00" },
                { CustomRoles.Drainer, "#149627" },
                { CustomRoles.Hacker, "#75fa4c" },
                { CustomRoles.Aid, "#D7BDE2" },
                { CustomRoles.DonutDelivery, "#a46efa" },
                { CustomRoles.Analyst, "#33ddff" },
                { CustomRoles.Dealer, "#f57242" },
                { CustomRoles.Escort, "#ff94e6" },
                { CustomRoles.Spy, "#34495E" },
                { CustomRoles.Tether, "#138D75" },
                { CustomRoles.Ricochet, "#EDBB99" },
                { CustomRoles.SpeedBooster, "#00ffff" },
                { CustomRoles.Doctor, "#80ffdd" },
                { CustomRoles.Dictator, "#df9b00" },
                { CustomRoles.Forensic, "#7160e8" },
                { CustomRoles.NiceGuesser, "#f0e68c" },
                { CustomRoles.Vigilante, "#7a7a7a" },
                { CustomRoles.Transporter, "#42D1FF" },
                { CustomRoles.TimeManager, "#6495ed" },
                { CustomRoles.Veteran, "#a77738" },
                { CustomRoles.Bodyguard, "#185abd" },
                { CustomRoles.Witness, "#e70052" },
                { CustomRoles.Lookout, "#2a52be" },
                { CustomRoles.Grenadier, "#3c4a16" },
                { CustomRoles.Lighter, "#eee5be" },
                { CustomRoles.SecurityGuard, "#c3b25f" },
                { CustomRoles.Medic, "#00ff97" },
                { CustomRoles.FortuneTeller, "#882c83" },
                { CustomRoles.Glitch, "#39FF14" },
                { CustomRoles.Judge, "#f8d85a" },
                { CustomRoles.Mortician, "#333c49" },
                { CustomRoles.Medium, "#a200ff" },
                { CustomRoles.Observer, "#a8e0fa" },
                { CustomRoles.Pacifist, "#ffffff" },
                { CustomRoles.Jailor, "#aa900d" },
                { CustomRoles.Monarch, "#FFA500" },
                { CustomRoles.Coroner, "#8B0000" },
                { CustomRoles.Enigma, "#676798" },
                { CustomRoles.Scout, "#3CB371" },
                { CustomRoles.CameraMan, "#000930" },
                { CustomRoles.Merchant, "#D27D2D" },
                { CustomRoles.Telecommunication, "#7223DA" },
                { CustomRoles.Deputy, "#df9026" },
                { CustomRoles.Bestower, "#4C4FE4" },
                { CustomRoles.Retributionist, "#cfc999" },
                { CustomRoles.Cleanser, "#98FF98" },
                { CustomRoles.Swapper, "#922348" },
                { CustomRoles.Ignitor, "#ffffa5" },
                { CustomRoles.Guardian, "#2E8B57" },
                { CustomRoles.Addict, "#008000" },
                { CustomRoles.Alchemist, "#e6d798" },
                { CustomRoles.Tracefinder, "#0066CC" },
                { CustomRoles.Oracle, "#6666FF" },
                { CustomRoles.Spiritualist, "#669999" },
                { CustomRoles.Chameleon, "#01C834" },
                { CustomRoles.Inspector, "#0D57AF" },
                { CustomRoles.TimeMaster, "#44baff" },
                { CustomRoles.Crusader, "#C65C39" },
                { CustomRoles.Speedrunner, "#800080" },
                { CustomRoles.Unshifter, "#00b4eb" },
                { CustomRoles.Scanner, "#42f5a7" },
                // Neutrals
                { CustomRoles.Arsonist, "#ff6633" },
                { CustomRoles.Pyromaniac, "#ff6633" },
                { CustomRoles.PlagueBearer, "#e5f6b4" },
                { CustomRoles.Pestilence, "#343136" },
                { CustomRoles.Jester, "#ec62a5" },
                { CustomRoles.Terrorist, "#00e600" },
                { CustomRoles.Executioner, "#611c3a" },
                { CustomRoles.Lawyer, "#008080" },
                { CustomRoles.God, "#f96464" },
                { CustomRoles.Opportunist, "#4dff4d" },
                { CustomRoles.Vector, "#ff6201" },
                { CustomRoles.Jackal, "#00b4eb" },
                { CustomRoles.Sidekick, "#00b4eb" },
                { CustomRoles.Innocent, "#8f815e" },
                { CustomRoles.Pelican, "#34c84b" },
                { CustomRoles.Revolutionist, "#ba4d06" },
                { CustomRoles.Hater, "#414b66" },
                { CustomRoles.Demon, "#68bc71" },
                { CustomRoles.Stalker, "#483d8b" },
                { CustomRoles.Workaholic, "#008b8b" },
                { CustomRoles.Collector, "#9d8892" },
                { CustomRoles.NecroGuesser, "#F6FE03" },
                { CustomRoles.Provocateur, "#74ba43" },
                { CustomRoles.Sunnyboy, "#ff9902" },
                { CustomRoles.Poisoner, "#e70052" },
                { CustomRoles.Follower, "#ff9409" },
                { CustomRoles.Romantic, "#FF1493" },
                { CustomRoles.VengefulRomantic, "#ba2749" },
                { CustomRoles.RuthlessRomantic, "#D2691E" },
                { CustomRoles.Cultist, "#cf6acd" },
                { CustomRoles.Necromancer, "#f7adcf" },
                { CustomRoles.Deathknight, "#361d12" },
                { CustomRoles.HexMaster, "#ff00ff" },
                { CustomRoles.Wraith, "#4B0082" },
                { CustomRoles.Duality, "#8D6F64" },
                { CustomRoles.Thanos, "#F9D401" },
                { CustomRoles.Berserker, "#50538F" },
                { CustomRoles.SerialKiller, "#233fcc" },
                { CustomRoles.Quarry, "#c1fb2b" },
                { CustomRoles.Accumulator, "#2bfbae" },
                { CustomRoles.Spider, "#C9E44C" },
                { CustomRoles.SoulCollector, "#6021A0" },
                { CustomRoles.Sharpshooter, "#5901D4" },
                { CustomRoles.Explosivist, "#ff5900" },
                { CustomRoles.Slenderman, "#2c2e00" },
                { CustomRoles.Amogus, "#ff0000" },
                { CustomRoles.Weatherman, "#347deb" },
                { CustomRoles.NoteKiller, "#4CA8E4" },
                { CustomRoles.Vortex, "#a83293" },
                { CustomRoles.Beehive, "#ffff00" },
                { CustomRoles.RouleteGrandeur, "#a88332" },
                { CustomRoles.Nonplus, "#09632f" },
                { CustomRoles.Tremor, "#e942f5" },
                { CustomRoles.Evolver, "#f2c444" },
                { CustomRoles.Rogue, "#7a629c" },
                { CustomRoles.Patroller, "#c1cc27" },
                { CustomRoles.Simon, "#c4b8ff" },
                { CustomRoles.Chemist, "#4287f5" },
                { CustomRoles.Samurai, "#73495c" },
                { CustomRoles.QuizMaster, "#CF2472" },
                { CustomRoles.Bargainer, "#4f2f36" },
                { CustomRoles.Tiger, "#fcba03" },
                { CustomRoles.SoulHunter, "#3f2c61" },
                { CustomRoles.Enderman, "#3c008a" },
                { CustomRoles.Mycologist, "#0043de" },
                { CustomRoles.Bubble, "#ff38c3" },
                { CustomRoles.Hookshot, "#32a852" },
                { CustomRoles.Sprayer, "#ffc038" },
                { CustomRoles.Infection, "#ff6633" },
                { CustomRoles.Curser, "#510c91" },
                { CustomRoles.Postman, "#00b893" },
                { CustomRoles.Thief, "#44AF84" },
                { CustomRoles.Auditor, "#6BB626" },
                { CustomRoles.Magistrate, "#E44CB8" },
                { CustomRoles.Seamstress, "#BFE44C" },
                { CustomRoles.Spirit, "#26B652" },
                { CustomRoles.Starspawn, "#4CE4B4" },
                { CustomRoles.Clerk, "#26B6A9" },
                { CustomRoles.RoomRusher, "#ffab1b" },
                { CustomRoles.SchrodingersCat, "#616161" },
                { CustomRoles.Shifter, "#777777" },
                { CustomRoles.Impartial, "#4287f5" },
                { CustomRoles.Gaslighter, "#b6aa82" },
                { CustomRoles.Investor, "#4ccf73" },
                { CustomRoles.Tank, "#176320" },
                { CustomRoles.Technician, "#4e96f5" },
                { CustomRoles.Backstabber, "#fcba03" },
                { CustomRoles.Predator, "#c73906" },
                { CustomRoles.Reckless, "#6e000d" },
                { CustomRoles.Magician, "#BF5FFF" },
                { CustomRoles.WeaponMaster, "#6f02bd" },
                { CustomRoles.Eclipse, "#0E6655" },
                { CustomRoles.Vengeance, "#33cccc" },
                { CustomRoles.HeadHunter, "#ffcc66" },
                { CustomRoles.Pulse, "#ff00a5" },
                { CustomRoles.Werewolf, "#964B00" },
                { CustomRoles.Bandit, "#8B008B" },
                { CustomRoles.Agitator, "#F4A460" },
                { CustomRoles.BloodKnight, "#630000" },
                { CustomRoles.Juggernaut, "#A41342" },
                { CustomRoles.Cherokious, "#de4b9e" },
                { CustomRoles.Pawn, "#78C48F" },
                { CustomRoles.Parasite, "#ff1919" },
                { CustomRoles.Crewpostor, "#ff1919" },
                { CustomRoles.Hypocrite, "#ff1919" },
                { CustomRoles.Renegade, "#ff1919" },
                { CustomRoles.Virus, "#2E8B57" },
                { CustomRoles.Investigator, "#BA55D3" },
                { CustomRoles.Pursuer, "#617218" },
                { CustomRoles.Specter, "#662962" },
                { CustomRoles.Jinx, "#ed2f91" },
                { CustomRoles.Maverick, "#781717" },
                { CustomRoles.Ritualist, "#663399" },
                { CustomRoles.Pickpocket, "#47008B" },
                { CustomRoles.Traitor, "#BA2E05" },
                { CustomRoles.Vulture, "#556B2F" },
                { CustomRoles.Medusa, "#9900CC" },
                { CustomRoles.Spiritcaller, "#003366" },
                { CustomRoles.EvilSpirit, "#003366" },
                { CustomRoles.Convict, "#ff1919" },
                { CustomRoles.Amnesiac, "#7FBFFF" },
                { CustomRoles.Doomsayer, "#14f786" },
                // Ghost roles
                { CustomRoles.Warden, "#32a852" },
                { CustomRoles.Minion, "#ff1919" },
                { CustomRoles.Phantasm, "#b446e3" },
                { CustomRoles.Haunter, "#d1b1de" },
                { CustomRoles.Bloodmoon, "#ff1313" },
                { CustomRoles.GA, "#8cffff" },
                { CustomRoles.Facilitator, CovenColor },
                { CustomRoles.Shade, "#060270" },
                // GM
                { CustomRoles.GM, "#ff5b70" },
                // Add-ons
                { CustomRoles.NotAssigned, "#ffffff" },
                { CustomRoles.LastImpostor, "#ff1919" },
                { CustomRoles.Lovers, "#ff9ace" },
                { CustomRoles.Bloodlust, "#630000" },
                { CustomRoles.Madmate, "#ff1919" },
                { CustomRoles.Watcher, "#800080" },
                { CustomRoles.Sleuth, "#30221c" },
                { CustomRoles.Energetic, "#ffff00" },
                { CustomRoles.Messenger, "#28b573" },
                { CustomRoles.Dynamo, "#ebe534" },
                { CustomRoles.Listener, "#060270" },
                { CustomRoles.Unbound, "#DFC57B" },
                { CustomRoles.AntiTP, "#fcba03" },
                { CustomRoles.Blessed, "#7bfbff" },
                { CustomRoles.Hidden, "#E2EAF4" },
                { CustomRoles.Looter, "#F5D866" },
                { CustomRoles.Tired, "#ff1919" },
                { CustomRoles.Concealer, "#ff1919" },
                { CustomRoles.Composter, "#8D6F64" },
                { CustomRoles.TaskMaster, "#00ffa5" },
                { CustomRoles.Compelled, "#D2E44C" },
                { CustomRoles.Commited, "#f5c542" },
                { CustomRoles.BananaMan, "#ffe135" },
                { CustomRoles.Blind, "#666666" },
                { CustomRoles.Shy, "#9582f5" },
                { CustomRoles.Blocked, "#B7A627" },
                { CustomRoles.Aide, "#ff1919" },
                { CustomRoles.Anchor, "#6B4CE4" },
                { CustomRoles.Fragile, "#debe66" },
                { CustomRoles.Allergic, "#e3bd56" },
                { CustomRoles.Introvert, "#6293e3" },
                { CustomRoles.Deadlined, "#ffa500" },
                { CustomRoles.Rookie, "#bf671f" },
                { CustomRoles.Trainee, "#4287f5" },
                { CustomRoles.Taskcounter, "#ff1919" },
                { CustomRoles.Stained, "#e6bf91" },
                { CustomRoles.Clumsy, "#b8b8b8" },
                { CustomRoles.Mischievous, "#30221c" },
                { CustomRoles.Flash, "#ff8400" },
                { CustomRoles.Haste, "#f0ec22" },
                { CustomRoles.Busy, "#32a852" },
                { CustomRoles.Sleep, "#000000" },
                { CustomRoles.Truant, "#eb3467" },
                { CustomRoles.Disco, "#eb34e8" },
                { CustomRoles.Sonar, "#b8fffe" },
                { CustomRoles.Asthmatic, "#8feb34" },
                { CustomRoles.Giant, "#32a852" },
                { CustomRoles.Nimble, "#feffc7" },
                { CustomRoles.Physicist, "#87e9ff" },
                { CustomRoles.Finder, "#32a879" },
                { CustomRoles.Noisy, "#e34fb2" },
                { CustomRoles.Examiner, "#326ac9" },
                { CustomRoles.Venom, "#ff1919" },
                { CustomRoles.Torch, "#eee5be" },
                { CustomRoles.Seer, "#61b26c" },
                { CustomRoles.Tiebreaker, "#1447af" },
                { CustomRoles.Oblivious, "#424242" },
                { CustomRoles.Bewilder, "#c894f5" },
                { CustomRoles.Spurt, "#c9e8f5" },
                { CustomRoles.Sunglasses, "#E7C12B" },
                { CustomRoles.Workhorse, "#00ffff" },
                { CustomRoles.Undead, "#ed9abd" },
                { CustomRoles.Cleansed, "#98FF98" },
                { CustomRoles.Fool, "#e6e7ff" },
                { CustomRoles.Avenger, "#ffab1c" },
                { CustomRoles.Youtuber, "#fb749b" },
                { CustomRoles.Egoist, "#5600ff" },
                { CustomRoles.Stealer, "#ff1919" },
                { CustomRoles.Schizophrenic, "#3a648f" },
                { CustomRoles.Mimic, "#ff1919" },
                { CustomRoles.Guesser, "#f8cd46" },
                { CustomRoles.Necroview, "#663399" },
                { CustomRoles.Reach, "#74ba43" },
                { CustomRoles.Magnet, "#eb3477" },
                { CustomRoles.DeadlyQuota, "#ff1919" },
                { CustomRoles.Circumvent, "#ff1919" },
                { CustomRoles.Damocles, "#ff1919" },
                { CustomRoles.Stressed, "#9403fc" },
                { CustomRoles.Charmed, "#cf6acd" },
                { CustomRoles.Bait, "#00f7ff" },
                { CustomRoles.Beartrap, "#5a8fd0" },
                { CustomRoles.Onbound, "#BAAAE9" },
                { CustomRoles.Knighted, "#FFA500" },
                { CustomRoles.Contagious, "#2E8B57" },
                { CustomRoles.Disregarded, "#FF6347" },
                { CustomRoles.Lucky, "#b8d7a3" },
                { CustomRoles.Unlucky, "#d7a3a3" },
                { CustomRoles.DoubleShot, "#19fa8d" },
                { CustomRoles.Rascal, "#990000" },
                { CustomRoles.Gravestone, "#2EA8E7" },
                { CustomRoles.Lazy, "#a4dffe" },
                { CustomRoles.Autopsy, "#80ffdd" },
                { CustomRoles.Loyal, "#B71556" },
                { CustomRoles.Visionary, "#ff1919" },
                { CustomRoles.Glow, "#E2F147" },
                { CustomRoles.Diseased, "#AAAAAA" },
                { CustomRoles.Antidote, "#FF9876" },
                { CustomRoles.Swift, "#ff1919" },
                { CustomRoles.Mare, "#ff1919" },
                { CustomRoles.Underdog, "#ff1919" },

                // Solo PVP
                { CustomRoles.Challenger, "#f55252" },
                // FFA
                { CustomRoles.Killer, "#00ffff" },
                // Stop And Go
                { CustomRoles.Tasker, "#00ffa5" },
                // Hot Potato
                { CustomRoles.Potato, "#e8cd46" },
                // Speedrun
                { CustomRoles.Runner, "#800080" },
                // Capture The Flag
                { CustomRoles.CTFPlayer, "#1313c2" },
                // Natural Disaster
                { CustomRoles.NDPlayer, "#03fc4a" },
                // Room Rush
                { CustomRoles.RRPlayer, "#ffab1b" },
                // King of the Zones
                { CustomRoles.KOTZPlayer, "#ff0000" },
                // Quiz
                { CustomRoles.QuizPlayer, "#CF2472" },
                // The Mind Game
                { CustomRoles.TMGPlayer, "#ffff00" },
                // Bed Wars
                { CustomRoles.BedWarsPlayer, "#fc03f8" },
                // Deathrace
                { CustomRoles.Racer, "#AFAFAF" },
                // Mingle
                { CustomRoles.MinglePlayer, "#FE9900" },
                // Snowdown
                { CustomRoles.SnowdownPlayer, "#e4fdff" },
                // Hide And Seek
                { CustomRoles.Seeker, "#ff1919" },
                { CustomRoles.Hider, "#345eeb" },
                { CustomRoles.Fox, "#00ff00" },
                { CustomRoles.Troll, "#ff00ff" },
                { CustomRoles.Jumper, "#ddf542" },
                { CustomRoles.Detector, "#42ddf5" },
                { CustomRoles.Jet, "#42f54b" },
                { CustomRoles.Dasher, "#f542b0" },
                { CustomRoles.Locator, "#f59e42" },
                { CustomRoles.Venter, "#694141" },
                { CustomRoles.Agent, "#ff8f8f" },
                { CustomRoles.Taskinator, "#561dd1" }
            };

            CustomRoleValues.Where(x => x.GetCustomRoleTypes() == CustomRoleTypes.Impostor).Do(x => RoleColors.TryAdd(x, ImpostorColor));
            CustomRoleValues.Where(x => x.IsCoven() || x == CustomRoles.Entranced).Do(x => RoleColors.TryAdd(x, CovenColor));
        }
        catch (ArgumentException ex)
        {
            Logger.Error("Error: Duplicate keys", "LoadDictionary");
            Logger.Exception(ex, "LoadDictionary");
            HasArgumentException = true;
        }
        catch (Exception ex) { Logger.Fatal(ex.ToString(), "Main"); }

        CustomWinnerHolder.Reset();
        Translator.Init();
        BanManager.Init();
        TemplateManager.Init();
        SpamManager.Init();

        IRandom.SetInstance(new NetRandomWrapper());

        Logger.Info($"{Application.version}", "AmongUs Version");

        LogHandler handler = Logger.Handler("GitVersion");
        handler.Info($"{nameof(ThisAssembly.Git.BaseTag)}: {ThisAssembly.Git.BaseTag}");
        handler.Info($"{nameof(ThisAssembly.Git.Commit)}: {ThisAssembly.Git.Commit}");
        handler.Info($"{nameof(ThisAssembly.Git.Commits)}: {ThisAssembly.Git.Commits}");
        handler.Info($"{nameof(ThisAssembly.Git.IsDirty)}: {ThisAssembly.Git.IsDirty}");
        handler.Info($"{nameof(ThisAssembly.Git.Sha)}: {ThisAssembly.Git.Sha}");
        handler.Info($"{nameof(ThisAssembly.Git.Tag)}: {ThisAssembly.Git.Tag}");

        ClassInjector.RegisterTypeInIl2Cpp<ErrorText>();
        ClassInjector.RegisterTypeInIl2Cpp<MeetingHudPagingBehaviour>();
        ClassInjector.RegisterTypeInIl2Cpp<ShapeShifterPagingBehaviour>();
        ClassInjector.RegisterTypeInIl2Cpp<VitalsPagingBehaviour>();

        NormalGameOptionsV10.RecommendedImpostors = NormalGameOptionsV10.MaxImpostors = Enumerable.Repeat(128, 128).ToArray();
        NormalGameOptionsV10.MinPlayers = Enumerable.Repeat(4, 128).ToArray();
        HideNSeekGameOptionsV10.MinPlayers = Enumerable.Repeat(4, 128).ToArray();

        PrivateTagManager.LoadTagsFromFile();

        Harmony.PatchAll(Assembly.GetExecutingAssembly());

        if (!OperatingSystem.IsAndroid())
        {
            // there are some issues with TextBoxPatch on Android
            Harmony.PatchAll(typeof(TextBoxPatch));
        }

        if (!DebugModeManager.AmDebugger)
            ConsoleManager.DetachConsole();
        else
            ConsoleManager.CreateConsole();

        GameModeColors = new()
        {
            [CustomGameMode.Standard] = Color.white,
            [CustomGameMode.SoloPVP] = ColorUtility.TryParseHtmlString("#f55252", out Color c) ? c : Color.white,
            [CustomGameMode.FFA] = Color.cyan,
            [CustomGameMode.StopAndGo] = ColorUtility.TryParseHtmlString("#00ffa5", out c) ? c : Color.white,
            [CustomGameMode.HotPotato] = ColorUtility.TryParseHtmlString("#e8cd46", out c) ? c : Color.white,
            [CustomGameMode.HideAndSeek] = ColorUtility.TryParseHtmlString("#345eeb", out c) ? c : Color.white,
            [CustomGameMode.Speedrun] = Utils.GetRoleColor(CustomRoles.Speedrunner),
            [CustomGameMode.CaptureTheFlag] = ColorUtility.TryParseHtmlString("#1313c2", out c) ? c : Color.white,
            [CustomGameMode.NaturalDisasters] = ColorUtility.TryParseHtmlString("#03fc4a", out c) ? c : Color.white,
            [CustomGameMode.RoomRush] = Team.Neutral.GetColor(),
            [CustomGameMode.KingOfTheZones] = Color.red,
            [CustomGameMode.Quiz] = Utils.GetRoleColor(CustomRoles.QuizMaster),
            [CustomGameMode.TheMindGame] = Color.yellow,
            [CustomGameMode.BedWars] = Utils.GetRoleColor(CustomRoles.BedWarsPlayer),
            [CustomGameMode.Deathrace] = Utils.GetRoleColor(CustomRoles.Racer),
            [CustomGameMode.Mingle] = Utils.GetRoleColor(CustomRoles.MinglePlayer),
            [CustomGameMode.Snowdown] = Utils.GetRoleColor(CustomRoles.SnowdownPlayer)
        };

        IL2CPPChainloader.Instance.Finished += () =>
        {
            CustomLogger.ClearLog();

            StartCoroutine(ModNewsFetcher.FetchNews());

            try { DevManager.StartFetchingTags(); }
            catch (Exception e) { Utils.ThrowException(e); }

            try { SubmergedCompatibility.Initialize(); }
            catch (Exception e) { Utils.ThrowException(e); }

            try { HandleRoleColorFiles(); }
            catch (Exception e) { Utils.ThrowException(e); }

            if (AutoHaunt.Value)
                Modules.AutoHaunt.Start();

            Logger.Msg("========= EHR loaded! =========", "Plugin Load");
            Logger.Msg($"EHR Version: {PluginVersion}, Test Build: {TestBuild}", "Plugin Load");
        };

        try
        {
            if (TryFixStuttering.Value && OperatingSystem.IsWindows() && Environment.ProcessorCount >= 4)
            {
                var process = Process.GetCurrentProcess();
                OriginalAffinity = process.ProcessorAffinity;
                process.ProcessorAffinity = (IntPtr)((1 << 2) | (1 << 3));
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    private static void HandleRoleColorFiles()
    {
        string serialized = JsonSerializer.Serialize(RoleColors, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText($"{DataPath}/OriginalRoleColors.json", serialized);

        if (!Directory.Exists($"{DataPath}/EHR_DATA"))
            Directory.CreateDirectory($"{DataPath}/EHR_DATA");

        var path = $"{DataPath}/EHR_DATA/RoleColors.json";

        if (!File.Exists(path)) File.WriteAllText(path, serialized);
        else
        {
            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json) || json == serialized || json.Length < serialized.Length) return;
                var deserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                RoleColors = deserialized.ToDictionary(x => Enum.Parse<CustomRoles>(x.Key), x => x.Value);
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }
    }

    public static void LoadRoleClasses()
    {
        AllRoleClasses = [];

        try
        {
            AllRoleClasses.AddRange(AllTypes
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(RoleBase)))
                .Select(t => (RoleBase)Activator.CreateInstance(t, null)));

            AllRoleClasses.Sort();
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public Coroutine StartCoroutine(IEnumerator coroutine)
    {
        if (coroutine == null) return null;
        return coroutines.StartCoroutine(coroutine.WrapToIl2Cpp());
    }

    public void StopCoroutine(IEnumerator coroutine)
    {
        if (coroutine == null) return;
        coroutines.StopCoroutine(coroutine.WrapToIl2Cpp());
    }

    public void StopCoroutine(Coroutine coroutine)
    {
        if (coroutine == null) return;
        coroutines.StopCoroutine(coroutine);
    }

    public void StopAllCoroutines()
    {
        coroutines.StopAllCoroutines();
    }

    public static IEnumerator GetRandomWord(Action<string> onComplete)
    {
        var api = "https://random-word.ryanrk.com/api/en/word/random";
        UnityWebRequest request = UnityWebRequest.Get(api);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            api = "https://random-word-api.herokuapp.com/word";
            request = UnityWebRequest.Get(api);
            yield return request.SendWebRequest();
        }

        if (request.result != UnityWebRequest.Result.Success)
            yield break;

        string response = request.downloadHandler.text;
        int firstQuote = response.IndexOf("\"", StringComparison.Ordinal);
        int lastQuote = response.LastIndexOf("\"", StringComparison.Ordinal);
        string word = response.Substring(firstQuote + 1, lastQuote - firstQuote - 1);

        onComplete?.Invoke(word);
    }
}

[Flags]
public enum Team
{
    None = 0,
    Impostor = 1,
    Neutral = 2,
    Crewmate = 4,
    Coven = 8

    /*
     * Impostor | Neutral = 3
     * Impostor | Crewmate = 5
     * Neutral | Crewmate = 6
     * Impostor | Neutral | Crewmate = 7
     * Impostor | Coven = 9
     * Neutral | Coven = 10
     * Impostor | Neutral | Coven = 11
     * Crewmate | Coven = 12
     * Impostor | Crewmate | Coven = 13
     * Neutral | Crewmate | Coven = 14
     * Impostor | Neutral | Crewmate | Coven = 15
     */
}

#pragma warning disable IDE0079 // Remove unnecessary suppression
[SuppressMessage("ReSharper", "UnusedMember.Global")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
public enum CustomWinner
{
    Draw = -1,
    Default = -2,
    None = -3,
    Error = -4,
    Neutrals = -5,

    // CTA
    CustomTeam = -6,

    // Hide And Seek
    Hider = CustomRoles.Hider,
    Seeker = CustomRoles.Seeker,
    Troll = CustomRoles.Troll,
    Taskinator = CustomRoles.Taskinator,

    // Standard
    Impostor = CustomRoles.Impostor,
    Crewmate = CustomRoles.Crewmate,
    Jester = CustomRoles.Jester,
    Terrorist = CustomRoles.Terrorist,
    Lovers = CustomRoles.Lovers,
    Executioner = CustomRoles.Executioner,
    Arsonist = CustomRoles.Arsonist,
    Revolutionist = CustomRoles.Revolutionist,
    Technician = CustomRoles.Technician,
    Jackal = CustomRoles.Jackal,
    God = CustomRoles.God,
    Vector = CustomRoles.Vector,
    Innocent = CustomRoles.Innocent,
    Pelican = CustomRoles.Pelican,
    Youtuber = CustomRoles.Youtuber,
    Egoist = CustomRoles.Egoist,
    Demon = CustomRoles.Demon,
    Stalker = CustomRoles.Stalker,
    Workaholic = CustomRoles.Workaholic,
    Collector = CustomRoles.Collector,
    NecroGuesser = CustomRoles.NecroGuesser,
    BloodKnight = CustomRoles.BloodKnight,
    Poisoner = CustomRoles.Poisoner,
    HexMaster = CustomRoles.HexMaster,
    Cultist = CustomRoles.Cultist,
    Necromancer = CustomRoles.Necromancer,
    Wraith = CustomRoles.Wraith,
    SerialKiller = CustomRoles.SerialKiller,
    Quarry = CustomRoles.Quarry,
    Accumulator = CustomRoles.Accumulator,
    Spider = CustomRoles.Spider,
    SoulCollector = CustomRoles.SoulCollector,
    Berserker = CustomRoles.Berserker,
    Sharpshooter = CustomRoles.Sharpshooter,
    Explosivist = CustomRoles.Explosivist,
    Thanos = CustomRoles.Thanos,
    Duality = CustomRoles.Duality,
    Slenderman = CustomRoles.Slenderman,
    Amogus = CustomRoles.Amogus,
    Weatherman = CustomRoles.Weatherman,
    NoteKiller = CustomRoles.NoteKiller,
    Vortex = CustomRoles.Vortex,
    Beehive = CustomRoles.Beehive,
    RouleteGrandeur = CustomRoles.RouleteGrandeur,
    Nonplus = CustomRoles.Nonplus,
    Tremor = CustomRoles.Tremor,
    Evolver = CustomRoles.Evolver,
    Rogue = CustomRoles.Rogue,
    Patroller = CustomRoles.Patroller,
    Simon = CustomRoles.Simon,
    Chemist = CustomRoles.Chemist,
    Samurai = CustomRoles.Samurai,
    QuizMaster = CustomRoles.QuizMaster,
    Bargainer = CustomRoles.Bargainer,
    Tiger = CustomRoles.Tiger,
    Enderman = CustomRoles.Enderman,
    Mycologist = CustomRoles.Mycologist,
    Bubble = CustomRoles.Bubble,
    Hookshot = CustomRoles.Hookshot,
    Sprayer = CustomRoles.Sprayer,
    Infection = CustomRoles.Infection,
    Reckless = CustomRoles.Reckless,
    Magician = CustomRoles.Magician,
    WeaponMaster = CustomRoles.WeaponMaster,
    Pyromaniac = CustomRoles.Pyromaniac,
    Eclipse = CustomRoles.Eclipse,
    HeadHunter = CustomRoles.HeadHunter,
    Agitator = CustomRoles.Agitator,
    Vengeance = CustomRoles.Vengeance,
    Werewolf = CustomRoles.Werewolf,
    Juggernaut = CustomRoles.Juggernaut,
    Bandit = CustomRoles.Bandit,
    Virus = CustomRoles.Virus,
    Specter = CustomRoles.Specter,
    Jinx = CustomRoles.Jinx,
    Ritualist = CustomRoles.Ritualist,
    Pickpocket = CustomRoles.Pickpocket,
    Traitor = CustomRoles.Traitor,
    Vulture = CustomRoles.Vulture,
    Pestilence = CustomRoles.Pestilence,
    Medusa = CustomRoles.Medusa,
    Spiritcaller = CustomRoles.Spiritcaller,
    Glitch = CustomRoles.Glitch,
    Plaguebearer = CustomRoles.PlagueBearer,
    Doomsayer = CustomRoles.Doomsayer,
    RuthlessRomantic = CustomRoles.RuthlessRomantic,
    Doppelganger = CustomRoles.Doppelganger,
    Pulse = CustomRoles.Pulse,
    Cherokious = CustomRoles.Cherokious,
    Phantasm = CustomRoles.Phantasm,

    Coven = CustomRoles.CovenLeader,

    Bloodlust = CustomRoles.Bloodlust
}

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum AdditionalWinners
{
    None = -1,

    AliveNeutrals = -2,

    // Hide And Seek
    Fox = CustomRoles.Fox,

    // -------------
    Phantasm = CustomRoles.Phantasm,
    Shade = CustomRoles.Shade,
    Lovers = CustomRoles.Lovers,
    Executioner = CustomRoles.Executioner,
    Opportunist = CustomRoles.Opportunist,
    Lawyer = CustomRoles.Lawyer,
    Hater = CustomRoles.Hater,
    Provocateur = CustomRoles.Provocateur,
    Sunnyboy = CustomRoles.Sunnyboy,
    Follower = CustomRoles.Follower,
    Romantic = CustomRoles.Romantic,
    VengefulRomantic = CustomRoles.VengefulRomantic,
    Pursuer = CustomRoles.Pursuer,
    Specter = CustomRoles.Specter,
    Sidekick = CustomRoles.Sidekick,
    Maverick = CustomRoles.Maverick,
    Curser = CustomRoles.Curser,
    Postman = CustomRoles.Postman,
    Auditor = CustomRoles.Auditor,
    Magistrate = CustomRoles.Magistrate,
    Seamstress = CustomRoles.Seamstress,
    Spirit = CustomRoles.Spirit,
    Starspawn = CustomRoles.Starspawn,
    Clerk = CustomRoles.Clerk,
    RoomRusher = CustomRoles.RoomRusher,
    Impartial = CustomRoles.Impartial,
    Gaslighter = CustomRoles.Gaslighter,
    Tank = CustomRoles.Tank,
    Investor = CustomRoles.Investor,
    Technician = CustomRoles.Technician,
    Backstabber = CustomRoles.Backstabber,
    Predator = CustomRoles.Predator,
    SoulHunter = CustomRoles.SoulHunter,
    SchrodingersCat = CustomRoles.SchrodingersCat,
    Innocent = CustomRoles.Innocent,
    NoteKiller = CustomRoles.NoteKiller,
    NecroGuesser = CustomRoles.NecroGuesser
}

public enum SuffixModes
{
    None = 0,
    EHR,
    Streaming,
    Recording,
    RoomHost,
    OriginalName,
    DoNotKillMe,
    NoAndroidPlz,
    AutoHost
}

public enum VoteMode
{
    Default,
    Suicide,
    SelfVote,
    Skip
}

public enum TieMode
{
    Default,
    All,
    Random
}

public class Coroutines : MonoBehaviour { }

