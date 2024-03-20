using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TOHE;
using TOHE.Roles.Neutral;
using UnityEngine;

[assembly: AssemblyFileVersion(Main.PluginVersion)]
[assembly: AssemblyInformationalVersion(Main.PluginVersion)]
[assembly: AssemblyVersion(Main.PluginVersion)]

namespace TOHE;

[BepInPlugin(PluginGuid, "TOHE+", PluginVersion)]
[BepInIncompatibility("jp.ykundesu.supernewroles")]
[BepInProcess("Among Us.exe")]
public class Main : BasePlugin
{
    // == プログラム設定 / Program Config ==
    public static readonly string ModName = "TOHE+";
    public static readonly string ModColor = "#00ffff";
    public static readonly bool AllowPublicRoom = true;
    public static readonly string ForkId = "TOHE+";
    public static HashAuth DebugKeyAuth { get; private set; }
    public const string DebugKeyHash = "c0fd562955ba56af3ae20d7ec9e64c664f0facecef4b3e366e109306adeae29d";
    public const string DebugKeySalt = "59687b";
    public static ConfigEntry<string> DebugKeyInput { get; private set; }
    public const string PluginGuid = "com.gurge44.toheplus";
    public const string PluginVersion = "3.0.0";
    public const string PluginDisplayVersion = "3.0.0";
    public static readonly string SupportedAUVersion = "2024.3.5";

    public Harmony Harmony { get; } = new(PluginGuid);
    public static Version version = Version.Parse(PluginVersion);
    public static ManualLogSource Logger;
    public static bool hasArgumentException;
    public static string ExceptionMessage;
    public static bool ExceptionMessageIsShown;
    public static string credentialsText;

    public static NormalGameOptionsV07 NormalOptions => GameOptionsManager.Instance.currentNormalGameOptions;

    //Client Options
    public static ConfigEntry<string> HideName { get; private set; }
    public static ConfigEntry<string> HideColor { get; private set; }
    public static ConfigEntry<int> MessageWait { get; private set; }
    public static ConfigEntry<bool> GM { get; private set; }
    public static ConfigEntry<bool> UnlockFPS { get; private set; }
    public static ConfigEntry<bool> AutoStart { get; private set; }
    public static ConfigEntry<bool> ForceOwnLanguage { get; private set; }
    public static ConfigEntry<bool> ForceOwnLanguageRoleName { get; private set; }
    public static ConfigEntry<bool> EnableCustomButton { get; private set; }
    public static ConfigEntry<bool> EnableCustomSoundEffect { get; private set; }
    public static ConfigEntry<bool> SwitchVanilla { get; private set; }
    public static ConfigEntry<bool> VersionCheat { get; private set; }
    public static ConfigEntry<bool> GodMode { get; private set; }
    public static ConfigEntry<bool> DarkTheme { get; private set; }

    public static Dictionary<byte, PlayerVersion> playerVersion = [];

    //Preset Name Options
    public static ConfigEntry<string> Preset1 { get; private set; }
    public static ConfigEntry<string> Preset2 { get; private set; }
    public static ConfigEntry<string> Preset3 { get; private set; }
    public static ConfigEntry<string> Preset4 { get; private set; }

    public static ConfigEntry<string> Preset5 { get; private set; }

    //Other Configs
    public static ConfigEntry<string> WebhookURL { get; private set; }
    public static ConfigEntry<string> BetaBuildURL { get; private set; }
    public static ConfigEntry<float> LastKillCooldown { get; private set; }
    public static ConfigEntry<float> LastShapeshifterCooldown { get; private set; }
    public const string NeutralColor = "#ffab1b";
    public const string ImpostorColor = "#ff1919";
    public const string CrewmateColor = "#8cffff";
    public static bool IsFixedCooldown => CustomRoles.Vampire.IsEnable() || CustomRoles.Poisoner.IsEnable();
    public static bool ChangedRole = false;
    public static OptionBackupData RealOptionsData;
    public static Dictionary<byte, float> KillTimers = [];
    public static Dictionary<byte, PlayerState> PlayerStates = [];
    public static Dictionary<byte, string> AllPlayerNames = [];
    public static Dictionary<(byte, byte), string> LastNotifyNames;
    public static Dictionary<byte, Color32> PlayerColors = [];
    public static Dictionary<byte, PlayerState.DeathReason> AfterMeetingDeathPlayers = [];
    public static Dictionary<CustomRoles, string> roleColors;
    public static Dictionary<byte, CustomRoles> SetRoles = [];
    public static Dictionary<byte, List<CustomRoles>> SetAddOns = [];
    public static Dictionary<CustomRoles, CustomRoles> AlwaysSpawnTogetherCombos = [];
    public static Dictionary<CustomRoles, CustomRoles> NeverSpawnTogetherCombos = [];
    public static Dictionary<byte, string> LastAddOns = [];
    public static List<RoleBase> AllRoleClasses;
    public static float RefixCooldownDelay;
    public static bool ProcessShapeshifts = true;
    public static Dictionary<byte, (long START_TIMESTAMP, int TOTALCD)> AbilityCD = [];
    public static Dictionary<byte, float> AbilityUseLimit = [];
    public static List<byte> DontCancelVoteList = [];
    public static string LastVotedPlayer;
    public static byte NimblePlayer = byte.MaxValue;
    public static byte PhysicistPlayer = byte.MaxValue;
    public static byte BloodlustPlayer = byte.MaxValue;
    public static List<byte> ResetCamPlayerList = [];
    public static List<byte> winnerList = [];
    public static List<CustomRoles> winnerRolesList = [];
    public static List<string> winnerNameList = [];
    public static List<int> clientIdList = [];
    public static Dictionary<byte, float> AllPlayerKillCooldown = [];
    public static Dictionary<byte, Vent> LastEnteredVent = [];
    public static Dictionary<byte, Vector2> LastEnteredVentLocation = [];
    public static List<(string MESSAGE, byte RECEIVER_ID, string TITLE)> MessagesToSend = [];
    public static bool isChatCommand;
    public static bool DoBlockNameChange;
    public static int updateTime;
    public static bool newLobby;
    public static Dictionary<int, int> SayStartTimes = [];
    public static Dictionary<int, int> SayBanwordsTimes = [];
    public static Dictionary<byte, float> AllPlayerSpeed = [];
    public const float MinSpeed = 0.0001f;
    public static Dictionary<byte, int> GuesserGuessed = [];
    public static bool HasJustStarted;
    public static int AliveImpostorCount;
    public static Dictionary<byte, bool> CheckShapeshift = [];
    public static Dictionary<byte, byte> ShapeshiftTarget = [];
    public static bool VisibleTasksCount;
    public static string nickName = "";
    public static bool introDestroyed;
    public static float DefaultCrewmateVision;
    public static float DefaultImpostorVision;
    public static bool IsAprilFools = DateTime.Now.Month == 4 && DateTime.Now.Day is 1;
    public static bool ResetOptions = true;
    public static byte FirstDied = byte.MaxValue;
    public static byte ShieldPlayer = byte.MaxValue;

    public static List<PlayerControl> LoversPlayers = [];
    public static bool isLoversDead = true;
    public static List<byte> CyberStarDead = [];
    public static List<byte> BaitAlive = [];
    public static Dictionary<byte, int> KilledDiseased = [];
    public static Dictionary<byte, int> KilledAntidote = [];
    public static List<byte> BrakarVoteFor = [];
    public static Dictionary<byte, string> SleuthMsgs = [];
    public static int MadmateNum;


    public static PlayerControl[] AllPlayerControls
    {
        get
        {
            List<PlayerControl> result = [];
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null) continue;
                result.Add(pc);
            }

            return [.. result];
        }
    }

    public static PlayerControl[] AllAlivePlayerControls
    {
        get
        {
            List<PlayerControl> result = [];
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || !pc.IsAlive() || pc.Data.Disconnected || Pelican.IsEaten(pc.PlayerId)) continue;
                result.Add(pc);
            }

            return [.. result];
        }
    }

    public static Main Instance;


    public static string OverrideWelcomeMsg = string.Empty;
    public static int HostClientId;

    public static Dictionary<byte, List<int>> GuessNumber = [];

    public static List<string> TName_Snacks_CN = ["冰激凌", "奶茶", "巧克力", "蛋糕", "甜甜圈", "可乐", "柠檬水", "冰糖葫芦", "果冻", "糖果", "牛奶", "抹茶", "烧仙草", "菠萝包", "布丁", "椰子冻", "曲奇", "红豆土司", "三彩团子", "艾草团子", "泡芙", "可丽饼", "桃酥", "麻薯", "鸡蛋仔", "马卡龙", "雪梅娘", "炒酸奶", "蛋挞", "松饼", "西米露", "奶冻", "奶酥", "可颂", "奶糖"];

    // ReSharper disable once StringLiteralTypo
    public static List<string> TName_Snacks_EN = ["Ice cream", "Milk tea", "Chocolate", "Cake", "Donut", "Coke", "Lemonade", "Candied haws", "Jelly", "Candy", "Milk", "Matcha", "Burning Grass Jelly", "Pineapple Bun", "Pudding", "Coconut Jelly", "Cookies", "Red Bean Toast", "Three Color Dumplings", "Wormwood Dumplings", "Puffs", "Can be Crepe", "Peach Crisp", "Mochi", "Egg Waffle", "Macaron", "Snow Plum Niang", "Fried Yogurt", "Egg Tart", "Muffin", "Sago Dew", "panna cotta", "soufflé", "croissant", "toffee"];

    // ReSharper disable once InconsistentNaming
    public static string Get_TName_Snacks => TranslationController.Instance.currentLanguage.languageID is SupportedLangs.SChinese or SupportedLangs.TChinese ? TName_Snacks_CN[IRandom.Instance.Next(0, TName_Snacks_CN.Count)] : TName_Snacks_EN[IRandom.Instance.Next(0, TName_Snacks_EN.Count)];

    public static GameData.PlayerInfo LastVotedPlayerInfo { get; set; }

    public static MapNames CurrentMap => (MapNames)NormalOptions.MapId;

    public override void Load()
    {
        Instance = this;

        //Client Options
        HideName = Config.Bind("Client Options", "Hide Game Code Name", "TOHE");
        HideColor = Config.Bind("Client Options", "Hide Game Code Color", $"{ModColor}");
        DebugKeyInput = Config.Bind("Authentication", "Debug Key", string.Empty);
        AutoStart = Config.Bind("Client Options", "AutoStart", false);
        GM = Config.Bind("Client Options", "GM", false);
        UnlockFPS = Config.Bind("Client Options", "UnlockFPS", false);
        AutoStart = Config.Bind("Client Options", "AutoStart", false);
        ForceOwnLanguage = Config.Bind("Client Options", "ForceOwnLanguage", false);
        ForceOwnLanguageRoleName = Config.Bind("Client Options", "ForceOwnLanguageRoleName", false);
        EnableCustomButton = Config.Bind("Client Options", "EnableCustomButton", true);
        EnableCustomSoundEffect = Config.Bind("Client Options", "EnableCustomSoundEffect", true);
        SwitchVanilla = Config.Bind("Client Options", "SwitchVanilla", false);
        VersionCheat = Config.Bind("Client Options", "VersionCheat", false);
        GodMode = Config.Bind("Client Options", "GodMode", false);
        DarkTheme = Config.Bind("Client Options", "DarkTheme", false);

        Logger = BepInEx.Logging.Logger.CreateLogSource("TOHE");
        TOHE.Logger.Enable();
        TOHE.Logger.Disable("NotifyRoles");
        TOHE.Logger.Disable("SwitchSystem");
        TOHE.Logger.Disable("ModNews");
        if (!DebugModeManager.AmDebugger)
        {
            TOHE.Logger.Disable("2018k");
            TOHE.Logger.Disable("Github");
            TOHE.Logger.Disable("CustomRpcSender");
            //TOHE.Logger.Disable("ReceiveRPC");
            TOHE.Logger.Disable("SendRPC");
            TOHE.Logger.Disable("SetRole");
            TOHE.Logger.Disable("Info.Role");
            TOHE.Logger.Disable("TaskState.Init");
            //TOHE.Logger.Disable("Vote");
            TOHE.Logger.Disable("RpcSetNamePrivate");
            //TOHE.Logger.Disable("SendChat");
            TOHE.Logger.Disable("SetName");
            //TOHE.Logger.Disable("AssignRoles");
            //TOHE.Logger.Disable("RepairSystem");
            //TOHE.Logger.Disable("MurderPlayer");
            //TOHE.Logger.Disable("CheckMurder");
            TOHE.Logger.Disable("PlayerControl.RpcSetRole");
            TOHE.Logger.Disable("SyncCustomSettings");
        }
        //TOHE.Logger.isDetail = true;

        // 認証関連-初期化
        DebugKeyAuth = new(DebugKeyHash, DebugKeySalt);

        // 認証関連-認証
        DebugModeManager.Auth(DebugKeyAuth, DebugKeyInput.Value);

        Preset1 = Config.Bind("Preset Name Options", "Preset1", "Preset_1");
        Preset2 = Config.Bind("Preset Name Options", "Preset2", "Preset_2");
        Preset3 = Config.Bind("Preset Name Options", "Preset3", "Preset_3");
        Preset4 = Config.Bind("Preset Name Options", "Preset4", "Preset_4");
        Preset5 = Config.Bind("Preset Name Options", "Preset5", "Preset_5");
        WebhookURL = Config.Bind("Other", "WebhookURL", "none");
        BetaBuildURL = Config.Bind("Other", "BetaBuildURL", string.Empty);
        MessageWait = Config.Bind("Other", "MessageWait", 0);
        LastKillCooldown = Config.Bind("Other", "LastKillCooldown", (float)30);
        LastShapeshifterCooldown = Config.Bind("Other", "LastShapeshifterCooldown", (float)30);

        hasArgumentException = false;
        ExceptionMessage = string.Empty;
        try
        {
            roleColors = new()
            {
                // Vanilla
                { CustomRoles.Crewmate, "#8cffff" },
                { CustomRoles.Engineer, "#8cffff" },
                { CustomRoles.Scientist, "#8cffff" },
                { CustomRoles.GuardianAngel, "#ffffff" },
                // Vanilla Remakes
                { CustomRoles.CrewmateTOHE, "#8cffff" },
                { CustomRoles.EngineerTOHE, "#FF6A00" },
                { CustomRoles.ScientistTOHE, "#8ee98e" },
                { CustomRoles.GuardianAngelTOHE, "#77e6d1" },
                // Crewmates
                { CustomRoles.Luckey, "#b8d7a3" },
                { CustomRoles.Needy, "#a4dffe" },
                { CustomRoles.SabotageMaster, "#3333ff" },
                { CustomRoles.Snitch, "#b8fb4f" },
                { CustomRoles.Marshall, "#5573aa" },
                { CustomRoles.Mayor, "#204d42" },
                { CustomRoles.Paranoia, "#c993f5" },
                { CustomRoles.Psychic, "#6F698C" },
                { CustomRoles.Sheriff, "#ffb347" },
                { CustomRoles.CopyCat, "#ffb2ab" },
                { CustomRoles.SuperStar, "#f6f657" },
                { CustomRoles.CyberStar, "#ee4a55" },
                { CustomRoles.Ventguard, "#ffa5ff" },
                { CustomRoles.Demolitionist, "#5e2801" },
                { CustomRoles.Express, "#00ffff" },
                { CustomRoles.NiceEraser, "#00a5ff" },
                { CustomRoles.TaskManager, "#00ffa5" },
                { CustomRoles.Randomizer, "#fcba03" },
                { CustomRoles.Beacon, "#a3fdff" },
                { CustomRoles.Rabbit, "#88d2ff" },
                { CustomRoles.Shiftguard, "#eb34cc" },
                { CustomRoles.Mole, "#00ff80" },
                { CustomRoles.Markseeker, "#f2a0f1" },
                { CustomRoles.Sentinel, "#4bc8d6" },
                { CustomRoles.Electric, "#fbff00" },
                { CustomRoles.Philantropist, "#e3b384" },
                { CustomRoles.Tornado, "#303030" },
                { CustomRoles.Insight, "#26ff38" },
                { CustomRoles.Tunneler, "#543232" },
                { CustomRoles.Detour, "#ffd35c" },
                { CustomRoles.Gaulois, "#42d1f5" },
                { CustomRoles.Druid, "#ffb694" },
                { CustomRoles.Autocrat, "#e2ed64" },
                { CustomRoles.Perceiver, "#ebeb34" },
                { CustomRoles.Convener, "#34eb7a" },
                { CustomRoles.Mathematician, "#eb3474" },
                { CustomRoles.Transmitter, "#c9a11e" },
                { CustomRoles.Doppelganger, "#f6f4a3" },
                { CustomRoles.Nightmare, "#1e1247" },
                { CustomRoles.Altruist, "#300000" },
                { CustomRoles.Benefactor, "#4aeaff" },
                { CustomRoles.GuessManagerRole, "#d4ff00" },
                { CustomRoles.Drainer, "#149627" },
                { CustomRoles.NiceHacker, "#75fa4c" },
                { CustomRoles.Aid, "#D7BDE2" },
                { CustomRoles.DonutDelivery, "#a46efa" },
                { CustomRoles.Analyzer, "#33ddff" },
                { CustomRoles.Escort, "#ff94e6" },
                { CustomRoles.Spy, "#34495E" },
                { CustomRoles.Doormaster, "#7FB3D5" },
                { CustomRoles.Tether, "#138D75" },
                { CustomRoles.Ricochet, "#EDBB99" },
                { CustomRoles.SpeedBooster, "#00ffff" },
                { CustomRoles.Doctor, "#80ffdd" },
                { CustomRoles.Dictator, "#df9b00" },
                { CustomRoles.Detective, "#7160e8" },
                { CustomRoles.NiceGuesser, "#f0e68c" },
                { CustomRoles.SwordsMan, "#7a7a7a" },
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
                { CustomRoles.Divinator, "#882c83" },
                { CustomRoles.Glitch, "#39FF14" },
                { CustomRoles.Judge, "#f8d85a" },
                { CustomRoles.Mortician, "#333c49" },
                { CustomRoles.Mediumshiper, "#a200ff" },
                { CustomRoles.Observer, "#a8e0fa" },
                { CustomRoles.DovesOfNeace, "#ffffff" },
                { CustomRoles.Jailor, "#aa900d" },
                { CustomRoles.Monarch, "#FFA500" },
                { CustomRoles.Bloodhound, "#8B0000" },
                { CustomRoles.Enigma, "#676798" },
                { CustomRoles.Tracker, "#3CB371" },
                { CustomRoles.CameraMan, "#000930" },
                { CustomRoles.Merchant, "#D27D2D" },
                { CustomRoles.Monitor, "#7223DA" },
                { CustomRoles.Deputy, "#df9026" },
                { CustomRoles.Cleanser, "#98FF98" },
                { CustomRoles.NiceSwapper, "#922348" },
                { CustomRoles.Ignitor, "#ffffa5" },
                { CustomRoles.Guardian, "#2E8B57" },
                { CustomRoles.Addict, "#008000" },
                { CustomRoles.Alchemist, "#e6d798" },
                { CustomRoles.Tracefinder, "#0066CC" },
                { CustomRoles.Oracle, "#6666FF" },
                { CustomRoles.Spiritualist, "#669999" },
                { CustomRoles.Chameleon, "#01C834" },
                { CustomRoles.ParityCop, "#0D57AF" },
                { CustomRoles.TimeMaster, "#44baff" },
                { CustomRoles.Crusader, "#C65C39" },
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
                { CustomRoles.Mario, "#ff6201" },
                { CustomRoles.Jackal, "#00b4eb" },
                { CustomRoles.Sidekick, "#00b4eb" },
                { CustomRoles.Innocent, "#8f815e" },
                { CustomRoles.Pelican, "#34c84b" },
                { CustomRoles.Revolutionist, "#ba4d06" },
                { CustomRoles.FFF, "#414b66" },
                { CustomRoles.Konan, "#4d4dff" },
                { CustomRoles.Gamer, "#68bc71" },
                { CustomRoles.DarkHide, "#483d8b" },
                { CustomRoles.Workaholic, "#008b8b" },
                { CustomRoles.Speedrunner, "#800080" },
                { CustomRoles.Collector, "#9d8892" },
                { CustomRoles.Provocateur, "#74ba43" },
                { CustomRoles.Sunnyboy, "#ff9902" },
                { CustomRoles.Poisoner, "#e70052" },
                { CustomRoles.Totocalcio, "#ff9409" },
                { CustomRoles.Romantic, "#FF1493" },
                { CustomRoles.VengefulRomantic, "#ba2749" },
                { CustomRoles.RuthlessRomantic, "#D2691E" },
                { CustomRoles.Succubus, "#cf6acd" },
                { CustomRoles.Necromancer, "#f7adcf" },
                { CustomRoles.Deathknight, "#361d12" },
                { CustomRoles.HexMaster, "#ff00ff" },
                { CustomRoles.Wraith, "#4B0082" },
                { CustomRoles.NSerialKiller, "#233fcc" },
                { CustomRoles.Tiger, "#fcba03" },
                { CustomRoles.SoulHunter, "#3f2c61" },
                { CustomRoles.Enderman, "#3c008a" },
                { CustomRoles.Mycologist, "#0043de" },
                { CustomRoles.Bubble, "#ff38c3" },
                { CustomRoles.Hookshot, "#32a852" },
                { CustomRoles.Sprayer, "#ffc038" },
                { CustomRoles.PlagueDoctor, "#ff6633" },
                { CustomRoles.Postman, "#00b893" },
                { CustomRoles.Impartial, "#4287f5" },
                { CustomRoles.Predator, "#c73906" },
                { CustomRoles.Reckless, "#6e000d" },
                { CustomRoles.Magician, "#BF5FFF" },
                { CustomRoles.WeaponMaster, "#6f02bd" },
                { CustomRoles.Eclipse, "#0E6655" },
                { CustomRoles.Vengeance, "#33cccc" },
                { CustomRoles.HeadHunter, "#ffcc66" },
                { CustomRoles.Imitator, "#ff00a5" },
                { CustomRoles.Werewolf, "#964B00" },
                { CustomRoles.Bandit, "#8B008B" },
                { CustomRoles.Agitater, "#F4A460" },
                { CustomRoles.BloodKnight, "#630000" },
                { CustomRoles.Juggernaut, "#A41342" },
                { CustomRoles.Parasite, "#ff1919" },
                { CustomRoles.Crewpostor, "#ff1919" },
                { CustomRoles.Refugee, "#ff1919" },
                { CustomRoles.Virus, "#2E8B57" },
                { CustomRoles.Farseer, "#BA55D3" },
                { CustomRoles.Pursuer, "#617218" },
                { CustomRoles.Phantom, "#662962" },
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
                { CustomRoles.Specter, "#b446e3" },
                { CustomRoles.Haunter, "#d1b1de" },
                // GM
                { CustomRoles.GM, "#ff5b70" },
                // Add-ons
                { CustomRoles.NotAssigned, "#ffffff" },
                { CustomRoles.LastImpostor, "#ff1919" },
                { CustomRoles.Lovers, "#ff9ace" },
                { CustomRoles.Bloodlust, "#630000" },
                { CustomRoles.Ntr, "#00a4ff" },
                { CustomRoles.Madmate, "#ff1919" },
                { CustomRoles.Watcher, "#800080" },
                { CustomRoles.Sleuth, "#30221c" },
                { CustomRoles.Taskcounter, "#ff1919" },
                { CustomRoles.Stained, "#e6bf91" },
                { CustomRoles.Clumsy, "#b8b8b8" },
                { CustomRoles.Mischievous, "#30221c" },
                { CustomRoles.Flashman, "#ff8400" },
                { CustomRoles.Haste, "#f0ec22" },
                { CustomRoles.Busy, "#32a852" },
                { CustomRoles.Truant, "#eb3467" },
                { CustomRoles.Disco, "#eb34e8" },
                { CustomRoles.Asthmatic, "#8feb34" },
                { CustomRoles.Giant, "#32a852" },
                { CustomRoles.Nimble, "#feffc7" },
                { CustomRoles.Physicist, "#87e9ff" },
                { CustomRoles.Torch, "#eee5be" },
                { CustomRoles.Seer, "#61b26c" },
                { CustomRoles.Brakar, "#1447af" },
                { CustomRoles.Oblivious, "#424242" },
                { CustomRoles.Bewilder, "#c894f5" },
                { CustomRoles.Sunglasses, "#E7C12B" },
                { CustomRoles.Workhorse, "#00ffff" },
                { CustomRoles.Undead, "#ed9abd" },
                { CustomRoles.Cleansed, "#98FF98" },
                { CustomRoles.Fool, "#e6e7ff" },
                { CustomRoles.Avanger, "#ffab1c" },
                { CustomRoles.Youtuber, "#fb749b" },
                { CustomRoles.Egoist, "#5600ff" },
                { CustomRoles.TicketsStealer, "#ff1919" },
                { CustomRoles.DualPersonality, "#3a648f" },
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
                { CustomRoles.Trapper, "#5a8fd0" },
                { CustomRoles.Onbound, "#BAAAE9" },
                { CustomRoles.Knighted, "#FFA500" },
                { CustomRoles.Contagious, "#2E8B57" },
                { CustomRoles.Unreportable, "#FF6347" },
                { CustomRoles.Rogue, "#696969" },
                { CustomRoles.Lucky, "#b8d7a3" },
                { CustomRoles.Unlucky, "#d7a3a3" },
                { CustomRoles.DoubleShot, "#19fa8d" },
                { CustomRoles.Rascal, "#990000" },
                { CustomRoles.Gravestone, "#2EA8E7" },
                { CustomRoles.Lazy, "#a4dffe" },
                { CustomRoles.Autopsy, "#80ffdd" },
                { CustomRoles.Loyal, "#B71556" },
                { CustomRoles.Visionary, "#ff1919" },
                { CustomRoles.Recruit, "#00b4eb" },
                { CustomRoles.Glow, "#E2F147" },
                { CustomRoles.Diseased, "#AAAAAA" },
                { CustomRoles.Antidote, "#FF9876" },

                { CustomRoles.Swift, "#ff1919" },
                { CustomRoles.Mare, "#ff1919" },


                //SoloKombat
                { CustomRoles.KB_Normal, "#f55252" },
                //FFA
                { CustomRoles.Killer, "#00ffff" },
                //Move And Stop
                { CustomRoles.Tasker, "#00ffa5" },
                //Hot Potato
                { CustomRoles.Potato, "#e8cd46" },
                //Hide And Seek
                { CustomRoles.Seeker, "#ff1919" },
                { CustomRoles.Hider, "#345eeb" },
                { CustomRoles.Fox, "#00ff00" },
                { CustomRoles.Troll, "#ff00ff" }
            };
            Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x.GetCustomRoleTypes() == CustomRoleTypes.Impostor).Do(x => roleColors.TryAdd(x, "#ff1919"));
        }
        catch (ArgumentException ex)
        {
            TOHE.Logger.Error("错误：字典出现重复项", "LoadDictionary");
            TOHE.Logger.Exception(ex, "LoadDictionary");
            hasArgumentException = true;
            ExceptionMessage = ex.Message;
            ExceptionMessageIsShown = false;
        }
        catch (Exception ex)
        {
            TOHE.Logger.Fatal(ex.ToString(), "Main");
        }

        CustomWinnerHolder.Reset();
        ServerAddManager.Init();
        Translator.Init();
        BanManager.Init();
        TemplateManager.Init();
        SpamManager.Init();
        DevManager.Init();
        Cloud.Init();

        IRandom.SetInstance(new NetRandomWrapper());

        TOHE.Logger.Info($"{Application.version}", "AmongUs Version");

        var handler = TOHE.Logger.Handler("GitVersion");
        handler.Info($"{nameof(ThisAssembly.Git.BaseTag)}: {ThisAssembly.Git.BaseTag}");
        handler.Info($"{nameof(ThisAssembly.Git.Commit)}: {ThisAssembly.Git.Commit}");
        handler.Info($"{nameof(ThisAssembly.Git.Commits)}: {ThisAssembly.Git.Commits}");
        handler.Info($"{nameof(ThisAssembly.Git.IsDirty)}: {ThisAssembly.Git.IsDirty}");
        handler.Info($"{nameof(ThisAssembly.Git.Sha)}: {ThisAssembly.Git.Sha}");
        handler.Info($"{nameof(ThisAssembly.Git.Tag)}: {ThisAssembly.Git.Tag}");

        ClassInjector.RegisterTypeInIl2Cpp<ErrorText>();

        Harmony.PatchAll();

        if (!DebugModeManager.AmDebugger) ConsoleManager.DetachConsole();
        else ConsoleManager.CreateConsole();

        TOHE.Logger.Msg("========= TOHE+ loaded! =========", "Plugin Load");
    }

    public static void LoadRoleClasses()
    {
        AllRoleClasses = [];
        try
        {
            AllRoleClasses.AddRange(Assembly.GetAssembly(typeof(RoleBase))!
                .GetTypes()
                .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(RoleBase)))
                .Select(type => (RoleBase)Activator.CreateInstance(type, null)));
            AllRoleClasses.Sort();
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
        }
    }
}

public enum Team
{
    None,
    Impostor,
    Neutral,
    Crewmate
}

public enum CustomRoles
{
    //Default
    Crewmate = 0,

    //Impostors (Vanilla)
    Impostor,
    Shapeshifter,

    // Vanilla Remakes
    ImpostorTOHE,
    ShapeshifterTOHE,

    //Impostors

    Hacker, // Anonymous
    AntiAdminer,
    Sans, // Arrogance
    Bard,
    Blackmailer,
    Bomber,
    BountyHunter,
    OverKiller, // Butcher
    Camouflager,
    Capitalism,
    Cantankerous,
    Changeling,
    Chronomancer,
    Cleaner,
    Commander,
    EvilDiviner, // Consigliere
    Consort,
    Councillor,
    Crewpostor,
    CursedWolf,
    Deathpact,
    Devourer,
    Disperser,
    Duellist,
    Dazzler,
    Escapee,
    Eraser,
    EvilGuesser,
    EvilTracker,
    FireWorks,
    Freezer,
    Gambler,
    Gangster,
    Godfather,
    Greedier,
    Hangman,
    Hitman,
    Inhibitor,
    Kamikaze,
    Kidnapper,
    Minimalism, // Killing Machine
    BallLightning, // Lightning
    Librarian,
    Lurker,
    Mafioso,
    Mastermind,
    Mafia, // Nemesis
    SerialKiller, // Mercenary
    Miner,
    Morphling,
    Assassin, // Ninja
    Nuker,
    Nullifier,
    Parasite,
    Penguin,
    Puppeteer,
    QuickShooter,
    Refugee,
    RiftMaker,
    Saboteur,
    Sapper,
    Scavenger,
    Sniper,
    ImperiusCurse, // Soul Catcher
    Swapster,
    Swiftclaw,
    Swooper,
    Stealth,
    TimeThief,
    BoobyTrap, // Trapster
    Trickster,
    Twister,
    Underdog,
    Undertaker,
    Vampire,
    Vindicator,
    Visionary,
    Warlock,
    Wildling,
    Witch,
    YinYanger,
    Zombie,

    //Crewmates (Vanilla)
    Engineer,
    GuardianAngel,
    Scientist,

    // Vanilla Remakes
    CrewmateTOHE,
    EngineerTOHE,
    GuardianAngelTOHE,
    ScientistTOHE,

    // Crewmate

    Addict,
    Aid,
    Alchemist,
    Altruist,
    Analyzer,
    Autocrat,
    Beacon,
    Benefactor,
    Bodyguard,
    CameraMan,
    CyberStar, // Celebrity
    Chameleon,
    Cleanser,
    Convener,
    CopyCat,
    Bloodhound, // Coroner
    Crusader,
    Demolitionist,
    Deputy,
    Detective,
    Detour,
    Dictator,
    Doctor,
    DonutDelivery,
    Doormaster,
    DovesOfNeace,
    Drainer,
    Druid,
    Electric,
    Enigma,
    Escort,
    Express,
    Farseer,
    Divinator, // Fortune Teller
    Gaulois,
    Grenadier,
    GuessManagerRole,
    Guardian,
    Ignitor,
    Insight,
    ParityCop, // Inspector
    Jailor,
    Judge,
    Needy, // Lazy Guy
    Lighter,
    Lookout,
    Luckey,
    Markseeker,
    Marshall,
    Mathematician,
    Mayor,
    SabotageMaster, // Mechanic
    Medic,
    Mediumshiper,
    Merchant,
    Monitor,
    Mole,
    Monarch,
    Mortician,
    NiceEraser,
    NiceGuesser,
    NiceHacker,
    NiceSwapper,
    Nightmare,
    Observer,
    Oracle,
    Paranoia,
    Perceiver,
    Philantropist,
    Psychic,
    Rabbit,
    Randomizer,
    Ricochet,
    Sentinel,
    SecurityGuard,
    Sheriff,
    Shiftguard,
    Snitch,
    Spiritualist,
    Speedrunner,
    SpeedBooster,
    Spy,
    SuperStar,
    TaskManager,
    Tether,
    TimeManager,
    TimeMaster,
    Tornado,
    Tracker,
    Transmitter,
    Transporter,
    Tracefinder,
    Tunneler,
    Ventguard,
    Veteran,
    SwordsMan, // Vigilante
    Witness,

    //Neutrals

    Agitater,
    Amnesiac,
    Arsonist,
    Bandit,
    BloodKnight,
    Bubble,
    Collector,
    Deathknight,
    Gamer, // Demon
    Doppelganger,
    Doomsayer,
    Eclipse,
    Enderman,
    Executioner,
    Totocalcio, // Follower
    Glitch,
    God,
    FFF, // Hater
    HeadHunter,
    HexMaster,
    Hookshot,
    Imitator,
    Impartial,
    Innocent,
    Jackal,
    Jester,
    Jinx,
    Juggernaut,
    Konan,
    Lawyer,
    Magician,
    Mario,
    Maverick,
    Medusa,
    Mycologist,
    Necromancer,
    Opportunist,
    Pelican,
    Pestilence,
    Phantom,
    Pickpocket,
    PlagueBearer,
    PlagueDoctor,
    Poisoner,
    Postman,
    Predator,
    Provocateur,
    Pursuer,
    Pyromaniac,
    Reckless,
    Revolutionist,
    Ritualist,
    Romantic,
    RuthlessRomantic,
    NSerialKiller, // Serial Killer
    Sidekick,
    SoulHunter,
    Spiritcaller,
    Sprayer,
    DarkHide, // Stalker
    Succubus,
    Sunnyboy,
    Terrorist,
    Tiger,
    Traitor,
    Vengeance,
    VengefulRomantic,
    Virus,
    Vulture,
    Wraith,
    Werewolf,
    WeaponMaster,
    Workaholic,

    //SoloKombat
    KB_Normal,

    //FFA
    Killer,

    //MoveAndStop
    Tasker,

    //HotPotato
    Potato,

    //H&S
    Hider,
    Seeker,
    Fox,
    Troll,

    //GM
    GM,

    // ????
    Convict,


    // Sub-role after 500
    NotAssigned = 500,
    Antidote,
    Asthmatic,
    Autopsy,
    Avanger,
    Bait,
    Busy,
    Trapper, // Beartrap
    Bewilder,
    Bloodlust,
    Charmed,
    Circumvent,
    Cleansed,
    Clumsy,
    Contagious,
    Damocles,
    DeadlyQuota,
    Disco,
    Diseased,
    Unreportable, // Disregarded
    DoubleShot,
    Egoist,
    EvilSpirit,
    Flashman,
    Fool,
    Giant,
    Glow,
    Gravestone,
    Guesser,
    Haste,
    Haunter, // Ghost role
    Knighted,
    LastImpostor,
    Lazy,
    Lovers,
    Loyal,
    Lucky,
    Madmate,
    Magnet,
    Mare,
    Mimic,
    Minion, // Ghost role
    Mischievous,
    Necroview,
    Ntr, // Neptune
    Nimble,
    Oblivious,
    Onbound,
    Specter, // Ghost role
    Physicist,
    Rascal,
    Reach,
    Recruit,
    Rogue,
    DualPersonality, // Schizophrenic
    Seer,
    Sleuth,
    Stained,
    Taskcounter,
    TicketsStealer, // Stealer
    Stressed,
    Swift,
    Sunglasses,
    Brakar, // Tiebreaker
    Torch,
    Truant,
    Undead,
    Unlucky,
    Warden, // Ghost role
    Watcher,
    Workhorse,
    Youtuber
}

public enum CustomWinner
{
    Draw = -1,
    Default = -2,
    None = -3,
    Error = -4,
    Neutrals = -5,

    // Hide And Seek
    Hider = -6,
    Seeker = -7,
    Troll = -8,
    Specter = -9,

    // Standard
    Impostor = CustomRoles.Impostor,
    Crewmate = CustomRoles.Crewmate,
    Jester = CustomRoles.Jester,
    Terrorist = CustomRoles.Terrorist,
    Lovers = CustomRoles.Lovers,
    Executioner = CustomRoles.Executioner,
    Arsonist = CustomRoles.Arsonist,
    Revolutionist = CustomRoles.Revolutionist,
    Jackal = CustomRoles.Jackal,
    God = CustomRoles.God,
    Mario = CustomRoles.Mario,
    Innocent = CustomRoles.Innocent,
    Pelican = CustomRoles.Pelican,
    Youtuber = CustomRoles.Youtuber,
    Egoist = CustomRoles.Egoist,
    Gamer = CustomRoles.Gamer,
    DarkHide = CustomRoles.DarkHide,
    Workaholic = CustomRoles.Workaholic,
    Collector = CustomRoles.Collector,
    BloodKnight = CustomRoles.BloodKnight,
    Poisoner = CustomRoles.Poisoner,
    HexMaster = CustomRoles.HexMaster,
    Succubus = CustomRoles.Succubus,
    Necromancer = CustomRoles.Necromancer,
    Wraith = CustomRoles.Wraith,
    SerialKiller = CustomRoles.NSerialKiller,
    Tiger = CustomRoles.Tiger,
    Enderman = CustomRoles.Enderman,
    Mycologist = CustomRoles.Mycologist,
    Bubble = CustomRoles.Bubble,
    Hookshot = CustomRoles.Hookshot,
    Sprayer = CustomRoles.Sprayer,
    PlagueDoctor = CustomRoles.PlagueDoctor,
    Reckless = CustomRoles.Reckless,
    Magician = CustomRoles.Magician,
    WeaponMaster = CustomRoles.WeaponMaster,
    Pyromaniac = CustomRoles.Pyromaniac,
    Eclipse = CustomRoles.Eclipse,
    HeadHunter = CustomRoles.HeadHunter,
    Agitater = CustomRoles.Agitater,
    Vengeance = CustomRoles.Vengeance,
    Werewolf = CustomRoles.Werewolf,
    Juggernaut = CustomRoles.Juggernaut,
    Bandit = CustomRoles.Bandit,
    Virus = CustomRoles.Virus,
    Rogue = CustomRoles.Rogue,
    Phantom = CustomRoles.Phantom,
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
    Imitator = CustomRoles.Imitator
}

public enum AdditionalWinners
{
    None = -1,

    // Hide And Seek
    Fox = -2,
    Specter = -3,

    // -------------
    Lovers = CustomRoles.Lovers,
    Opportunist = CustomRoles.Opportunist,
    Lawyer = CustomRoles.Lawyer,
    FFF = CustomRoles.FFF,
    Provocateur = CustomRoles.Provocateur,
    Sunnyboy = CustomRoles.Sunnyboy,
    Totocalcio = CustomRoles.Totocalcio,
    Romantic = CustomRoles.Romantic,
    VengefulRomantic = CustomRoles.VengefulRomantic,
    Pursuer = CustomRoles.Pursuer,
    Phantom = CustomRoles.Phantom,
    Maverick = CustomRoles.Maverick,
    Postman = CustomRoles.Postman,
    Impartial = CustomRoles.Impartial,
    Predator = CustomRoles.Predator,
    SoulHunter = CustomRoles.SoulHunter
}

public enum SuffixModes
{
    None = 0,
    TOHE,
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
