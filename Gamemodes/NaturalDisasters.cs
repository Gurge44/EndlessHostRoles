using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EHR.Gamemodes;

[SuppressMessage("ReSharper", "UnusedType.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class NaturalDisasters
{
    private const float Range = 1.5f;
    private static Dictionary<string, Type> AllDisasters = [];
    private static readonly List<Disaster> ActiveDisasters = [];
    private static readonly List<NaturalDisaster> PreparingDisasters = [];
    public static readonly Dictionary<byte, int> SurvivalTimes = [];

    private static ((float Left, float Right) X, (float Bottom, float Top) Y) MapBounds;

    private static OptionItem DisasterFrequency;
    private static OptionItem DisasterWarningTime;
    private static OptionItem LimitMaximumDisastersAtOnce;
    private static OptionItem MaximumDisastersAtOnce;
    private static OptionItem WhenLimitIsReached;
    private static OptionItem PreferRemovingThunderstorm;
    private static OptionItem ChatDuringGame;
    private static OptionItem DisasterSpawnMode;
    private static readonly Dictionary<string, OptionItem> DisasterSpawnChances = [];

    private static readonly string[] LimitReachedOptions =
    [
        "ND_LimitReachedOptions.RemoveRandom",
        "ND_LimitReachedOptions.RemoveOldest"
    ];

    private static readonly string[] DisasterSpawnModes =
    [
        "ND_DisasterSpawnModes.AllOnPlayers",
        "ND_DisasterSpawnModes.AllOnRandomPlaces",
        "ND_DisasterSpawnModes.OnPlayersAndRandomPlaces"
    ];

    public static List<Disaster> GetActiveDisasters()
    {
        return ActiveDisasters;
    }

    public static IEnumerable<Type> GetAllDisasters()
    {
        return AllDisasters.Values;
    }

    public static int FrequencyOfDisasters => DisasterFrequency.GetInt();

    public static bool Chat => ChatDuringGame.GetBool();

    public static void SetupCustomOption()
    {
        var id = 69_216_001;
        Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);
        const CustomGameMode gameMode = CustomGameMode.NaturalDisasters;

        DisasterFrequency = new FloatOptionItem(id++, "ND_DisasterFrequency", new(0f, 20f, 0.1f), 1f, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Seconds);

        DisasterWarningTime = new IntegerOptionItem(id++, "ND_DisasterWarningTime", new(1, 30, 1), 5, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Seconds);

        LimitMaximumDisastersAtOnce = new BooleanOptionItem(id++, "ND_LimitMaximumDisastersAtOnce", false, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetColor(color);

        MaximumDisastersAtOnce = new IntegerOptionItem(id++, "ND_MaximumDisastersAtOnce", new(1, 120, 1), 20, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetParent(LimitMaximumDisastersAtOnce)
            .SetColor(color);

        WhenLimitIsReached = new StringOptionItem(id++, "ND_WhenLimitIsReached", LimitReachedOptions, 1, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetParent(LimitMaximumDisastersAtOnce)
            .SetColor(color);

        PreferRemovingThunderstorm = new BooleanOptionItem(id++, "ND_PreferRemovingThunderstorm", true, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetParent(WhenLimitIsReached)
            .SetColor(color);
        
        ChatDuringGame = new BooleanOptionItem(id++, "FFA_ChatDuringGame", false, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetColor(color);

        DisasterSpawnMode = new StringOptionItem(id++, "ND_DisasterSpawnMode", DisasterSpawnModes, 0, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetColor(color);

        LoadAllDisasters();

        foreach ((string name, Type type) in AllDisasters)
        {
            DisasterSpawnChances[name] = new IntegerOptionItem(id++, "ND_Disaster.SpawnChance", new(0, 100, 5), 50, TabGroup.GameSettings)
                .SetGameMode(gameMode)
                .SetColor(color)
                .SetHeader(true)
                .SetValueFormat(OptionFormat.Percent)
                .AddReplacement(("{disaster}", Translator.GetString($"ND_{name}")));

            type.GetMethod("SetupOwnCustomOption")?.Invoke(null, null);
        }
    }

    public static void LoadAllDisasters()
    {
        AllDisasters = Main.AllTypes
            .Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(Disaster)))
            .ToDictionary(x => x.Name, x => x);
    }

    public static void OnGameStart()
    {
        LoadAllDisasters();
        SurvivalTimes.Clear();
        ActiveDisasters.Clear();
        PreparingDisasters.Clear();
        Sinkhole.Sinkholes.Clear();
        BuildingCollapse.CollapsedRooms.Clear();
        BuildingCollapse.LastPosition.Clear();

        if (Options.CurrentGameMode != CustomGameMode.NaturalDisasters && !Options.IntegrateNaturalDisasters.GetBool()) return;

        FixedUpdatePatch.WaitTime = Chat ? 10 : 5;
        
        RebuildSuffixText();

        List<Vector2> rooms = Main.LIMap
            ? ShipStatus.Instance.AllRooms.Select(x => new Vector2(x.transform.position.x, x.transform.position.y)).ToList()
            : RandomSpawn.SpawnMap.GetSpawnMap().Positions?.Values.ToList();
        
        if (rooms == null) return;

        List<float> x = rooms.ConvertAll(r => r.x);
        List<float> y = rooms.ConvertAll(r => r.y);

        const float extend = 5f;
        MapBounds = ((x.Min() - extend, x.Max() + extend), (y.Min() - extend, y.Max() + extend));
    }

    public static void ApplyGameOptions(IGameOptions opt, byte id)
    {
        ActiveDisasters.ForEach(x => x.ApplyOwnGameOptions(opt, id));
    }

    public static string SuffixText;

    public static void RebuildSuffixText()
    {
        try
        {
            var allRooms = ShipStatus.Instance.AllRooms;
            var collapsedRooms = BuildingCollapse.CollapsedRooms;
            string cb;

            if (allRooms.Count / 2 <= collapsedRooms.Count)
            {
                SystemTypes[] remainingRooms = allRooms.Select(x => x.RoomId).Where(x => x is not (SystemTypes.Hallway or SystemTypes.Outside or SystemTypes.Decontamination2 or SystemTypes.Decontamination3)).Except(collapsedRooms.ConvertAll(x => x.RoomId)).ToArray();
                cb = string.Format(Translator.GetString("AvailableBuildings"), remainingRooms.Length > 0
                    ? remainingRooms.Select(x => Translator.GetString($"{x}")).Distinct().Join()
                    : $"<#ff0000>{Translator.GetString("None")}</color>");
            }
            else
            {
                cb = string.Format(Translator.GetString("CollapsedBuildings"), collapsedRooms.Count > 0
                    ? collapsedRooms.Select(x => Translator.GetString($"{x.RoomId}")).Distinct().Join()
                    : Translator.GetString("None"));
            }

            string ts = ActiveDisasters.Exists(x => x is Thunderstorm) ? $"\n{Translator.GetString("OngoingThunderstorm")}" : string.Empty;
            string rp = string.Format(Translator.GetString("ND_RemainingPlayers"), Utils.ColorString(Utils.GetRoleColor(CustomRoles.NDPlayer), Main.AllAlivePlayerControlsCount.ToString()));
            SuffixText = $"<size=70%>{cb}{ts}\n{rp}</size>";
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static int SurvivalTime(byte id)
    {
        return SurvivalTimes.GetValueOrDefault(id, 0);
    }

    private static readonly List<(byte id, PlayerState.DeathReason deathReason, long recordTimeStamp)> DeathMessageQueue = [];

    public static void RecordDeath(PlayerControl pc, PlayerState.DeathReason deathReason)
    {
        RebuildSuffixText();
        long now = Utils.TimeStamp;
        SurvivalTimes[pc.PlayerId] = (int)(now - IntroCutsceneDestroyPatch.IntroDestroyTS - FixedUpdatePatch.WaitTime);

        string message = Translator.GetString($"ND_DRLaughMessage-{IRandom.Instance.Next(4)}.{deathReason}");
        Color color = DeathReasonColor(deathReason);
        message = Utils.ColorString(color, message);
        LateTask.New(() => pc.Notify(message, 20f), 1f, $"{pc.GetRealName()} died with the reason {deathReason}, survived for {SurvivalTime(pc.PlayerId)} seconds");
        Utils.SendRPC(CustomRPC.NaturalDisastersSync, pc.PlayerId, SurvivalTimes[pc.PlayerId]);

        DeathMessageQueue.Add((pc.PlayerId, deathReason, now));
    }

    private static Color DeathReasonColor(PlayerState.DeathReason deathReason)
    {
        return deathReason switch
        {
            PlayerState.DeathReason.Meteor => Color.red,
            PlayerState.DeathReason.Lava => Palette.Orange,
            PlayerState.DeathReason.Tornado => Color.gray,
            PlayerState.DeathReason.Lightning => Palette.White_75Alpha,
            PlayerState.DeathReason.Drowned => Color.cyan,
            PlayerState.DeathReason.Sunken => Color.yellow,
            PlayerState.DeathReason.Collapsed => Palette.Brown,
            _ => Color.white
        };
    }

    private static string Sprite(string name)
    {
        return name switch
        {
            "Earthquake" => "<size=170%><line-height=97%><cspace=0.16em><mark=#000000>WW</mark><mark=#5e5e5e>W</mark><mark=#adadad>W</mark><#0000>WWWW</color>\n<mark=#5e5e5e>W</mark><mark=#000000>W</mark><mark=#5e5e5e>W</mark><mark=#adadad>WW</mark><#0000>WWW</color>\n<mark=#5e5e5e>W</mark><mark=#000000>WW</mark><mark=#5e5e5e>WW</mark><mark=#adadad>WW</mark><#0000>W</color>\n<mark=#adadad>W</mark><mark=#5e5e5e>W</mark><mark=#000000>WWW</mark><mark=#5e5e5e>WW</mark><mark=#adadad>W</mark>\n<#0000>W</color><mark=#adadad>W</mark><mark=#5e5e5e>WW</mark><mark=#000000>WWW</mark><mark=#5e5e5e>W</mark>\n<#0000>WW</color><mark=#adadad>WW</mark><mark=#5e5e5e>WW</mark><mark=#000000>WW</mark>\n<#0000>WWWW</color><mark=#adadad>WW</mark><mark=#5e5e5e>W</mark><mark=#000000>W</mark>\n<#0000>WWWWW</color><mark=#adadad>W</mark><mark=#5e5e5e>W</mark><mark=#000000>W",
            "Meteor" => "<size=170%><line-height=97%><cspace=0.16em><#0000>WWW</color><mark=#fff700>WW</mark><#0000>WWW\nWW</color><mark=#fff700>W</mark><mark=#ffae00>WW</mark><mark=#fff700>W</mark><#0000>WW\nW</color><mark=#fff700>W</mark><mark=#ffae00>W</mark><mark=#ff6f00>WW</mark><mark=#ffae00>W</mark><mark=#fff700>W</mark><#0000>W</color>\n<mark=#fff700>W</mark><mark=#ffae00>W</mark><mark=#ff6f00>W</mark><mark=#ff1100>WW</mark><mark=#ff6f00>W</mark><mark=#ffae00>W</mark><mark=#fff700>W</mark>\n<mark=#fff700>W</mark><mark=#ffae00>W</mark><mark=#ff6f00>W</mark><mark=#ff1100>WW</mark><mark=#ff6f00>W</mark><mark=#ffae00>W</mark><mark=#fff700>W</mark>\n<#0000>W</color><mark=#fff700>W</mark><mark=#ffae00>W</mark><mark=#ff6f00>WW</mark><mark=#ffae00>W</mark><mark=#fff700>W</mark><#0000>W\nWW</color><mark=#fff700>W</mark><mark=#ffae00>WW</mark><mark=#fff700>W</mark><#0000>WW\nWWW</color><mark=#fff700>WW</mark><#0000>WWW",
            "VolcanoEruption" => "<size=170%><line-height=97%><cspace=0.16em><mark=#ff6200>WW</mark>\n<mark=#ff6200>WW",
            "Tornado" => "<size=170%><line-height=97%><cspace=0.16em><#0000>WW</color><mark=#dbdbdb>WWWW</mark><#0000>WW\nW</color><mark=#dbdbdb>W</mark><mark=#b0b0b0>WWWW</mark><mark=#dbdbdb>W</mark><#0000>W</color>\n<mark=#dbdbdb>W</mark><mark=#b0b0b0>WW</mark><mark=#828282>WW</mark><mark=#b0b0b0>WW</mark><mark=#dbdbdb>W</mark>\n<mark=#dbdbdb>W</mark><mark=#b0b0b0>W</mark><mark=#828282>W</mark><mark=#474747>WW</mark><mark=#828282>W</mark><mark=#b0b0b0>W</mark><mark=#dbdbdb>W</mark>\n<mark=#dbdbdb>W</mark><mark=#b0b0b0>W</mark><mark=#828282>W</mark><mark=#474747>WW</mark><mark=#828282>W</mark><mark=#b0b0b0>W</mark><mark=#dbdbdb>W</mark>\n<mark=#dbdbdb>W</mark><mark=#b0b0b0>WW</mark><mark=#828282>WW</mark><mark=#b0b0b0>WW</mark><mark=#dbdbdb>W</mark>\n<#0000>W</color><mark=#dbdbdb>W</mark><mark=#b0b0b0>WWWW</mark><mark=#dbdbdb>W</mark><#0000>W\nWW</color><mark=#dbdbdb>WWWW</mark><#0000>WW",
            "SandStorm" => "<size=170%><line-height=97%><cspace=0.16em><mark=#ffff99>WWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW",
            "Sinkhole" => "<size=170%><line-height=97%><cspace=0.16em><mark=#7d7d7d>WWWWWWWW</mark>\n<mark=#545454>W</mark><mark=#424242>WWWWWW</mark><mark=#7d7d7d>W</mark>\n<mark=#7d7d7d>W</mark><mark=#424242>W</mark><mark=#000000>WWWW</mark><mark=#424242>W</mark><mark=#545454>W</mark>\n<mark=#545454>W</mark><mark=#424242>W</mark><mark=#000000>WWWW</mark><mark=#424242>W</mark><mark=#7d7d7d>W</mark>\n<mark=#7d7d7d>W</mark><mark=#424242>W</mark><mark=#000000>WWWW</mark><mark=#424242>W</mark><mark=#545454>W</mark>\n<mark=#545454>W</mark><mark=#424242>W</mark><mark=#000000>WWWW</mark><mark=#424242>W</mark><mark=#7d7d7d>W</mark>\n<mark=#7d7d7d>W</mark><mark=#424242>WWWWWW</mark><mark=#545454>W</mark>\n<mark=#7d7d7d>WWWWWWWW",
            _ => string.Empty
        };
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        public static readonly Stopwatch LastDisasterTimer = new();
        private static long LastSync = Utils.TimeStamp;
        public static int WaitTime;

        public static void Postfix( /*PlayerControl __instance*/)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || (Options.CurrentGameMode != CustomGameMode.NaturalDisasters && !Options.IntegrateNaturalDisasters.GetBool()) || !Main.IntroDestroyed || Main.HasJustStarted /* || __instance.PlayerId >= 254 || !__instance.IsHost()*/) return;
            
            long now = Utils.TimeStamp;
            if (IntroCutsceneDestroyPatch.IntroDestroyTS + WaitTime > now) return;

            if (Options.CurrentGameMode != CustomGameMode.NaturalDisasters)
            {
                (int minimumWaitTime, bool shouldWait) = Options.CurrentGameMode switch
                {
                    CustomGameMode.BedWars => (30, BedWars.IsGracePeriod),
                    CustomGameMode.HideAndSeek => (0, CustomHnS.IsBlindTime),
                    CustomGameMode.KingOfTheZones => (0, !KingOfTheZones.GameGoing),
                    CustomGameMode.RoomRush => (40, false),
                    CustomGameMode.Deathrace => (0, !Deathrace.GameGoing),
                    CustomGameMode.Mingle => (60, !Mingle.GameGoing),
                    _ => (0, false)
                };

                if (shouldWait || IntroCutsceneDestroyPatch.IntroDestroyTS + minimumWaitTime > now) return;
            }
            
            UpdatePreparingDisasters();

            for (int index = ActiveDisasters.Count - 1; index >= 0; index--)
                ActiveDisasters[index].Update();

            Sinkhole.OnFixedUpdate();
            BuildingCollapse.OnFixedUpdate();

            if (LimitMaximumDisastersAtOnce.GetBool())
            {
                int numDisasters = ActiveDisasters.Count + Sinkhole.Sinkholes.Count;

                if (numDisasters > MaximumDisastersAtOnce.GetInt())
                {
                    if (Sinkhole.Sinkholes.Count > 0)
                    {
                        Sinkhole.RemoveRandomSinkhole();
                    }
                    else
                    {
                        switch (WhenLimitIsReached.GetValue())
                        {
                            case 0:
                            {
                                Disaster remove = PreferRemovingThunderstorm.GetBool() && ActiveDisasters.FindFirst(x => x is Thunderstorm, out Disaster thunderstorm) ? thunderstorm : ActiveDisasters.RandomElement();
                                remove?.Duration = 0;
                                remove?.RemoveIfExpired();
                                break;
                            }
                            case 1:
                            {
                                Disaster oldest = ActiveDisasters.MinBy(x => x.StartTimeStamp);
                                oldest.Duration = 0;
                                oldest.RemoveIfExpired();
                                break;
                            }
                        }
                    }
                }
            }
            
            LastDisasterTimer.Start(); // Checks IsRunning internally

            float frequency = DisasterFrequency.GetFloat();
            
            if (frequency < 0.5f && GameStates.CurrentServerType is GameStates.ServerType.Local or GameStates.ServerType.Vanilla)
                frequency = 0.5f; // Due to rate limits on InnerSloth's servers

            if (LastDisasterTimer.Elapsed.TotalSeconds >= frequency)
            {
                LastDisasterTimer.Restart();
                
                Dictionary<string, Type> pool = AllDisasters.ToDictionary(x => x.Key, x => x.Value);

                if (ActiveDisasters.Exists(x => x is Thunderstorm))
                    pool.Remove("Thunderstorm");
                
                (SystemTypes CollapsingRoom, Vector2 WarningPosition) buildingCollapseInfo = (default(SystemTypes), default(Vector2));
                PlainShipRoom[] nonCollapsedRooms = ShipStatus.Instance.AllRooms.Where(x => x.RoomId is not (SystemTypes.Hallway or SystemTypes.Outside or SystemTypes.Decontamination2 or SystemTypes.Decontamination3)).Except(BuildingCollapse.CollapsedRooms).ToArray();

                if (nonCollapsedRooms.Length == 0)
                {
                    pool.Remove("BuildingCollapse");
                }
                else
                {
                    PlainShipRoom collapsingRoom = nonCollapsedRooms.RandomElement();
                    buildingCollapseInfo = (collapsingRoom.RoomId, Main.LIMap ? collapsingRoom.transform.position : RandomSpawn.SpawnMap.GetSpawnMap().Positions.TryGetValue(collapsingRoom.RoomId, out Vector2 spawnLocation) ? spawnLocation : collapsingRoom.transform.position);
                }

                Type disaster = pool.SelectMany(x => Enumerable.Repeat(x.Value, DisasterSpawnChances[x.Key].GetInt() / 5)).RandomElement();

                if (disaster.Name == "Thunderstorm")
                {
                    _ = new Thunderstorm(Vector2.zero, null);
                    return;
                }

                var aapc = Main.AllAlivePlayerControlsToList;
                bool bc = disaster.Name == "BuildingCollapse";
                bool spawnOnPlayer = !bc && aapc.Count > 0 && DisasterSpawnMode.GetValue() switch
                {
                    0 => true,
                    1 => false,
                    _ => IRandom.Instance.Next(2) == 0
                };
                
                Vector2 position = bc
                    ? buildingCollapseInfo.WarningPosition
                    :  spawnOnPlayer
                        ? aapc.RandomElement().Pos()
                        : new(Random.Range(MapBounds.X.Left, MapBounds.X.Right), Random.Range(MapBounds.Y.Bottom, MapBounds.Y.Top));

                SystemTypes? room = bc ? buildingCollapseInfo.CollapsingRoom : null;
                AddPreparingDisaster(position, disaster.Name, room);
            }

            if (now - LastSync >= 15)
            {
                if (Options.CurrentGameMode != CustomGameMode.NaturalDisasters)
                {
                    BuildingCollapse.CollapsedRooms.Clear();
                    Sinkhole.RemoveRandomSinkhole();
                    RebuildSuffixText();
                    Utils.NotifyRoles();
                }
                
                LastSync = now;
                Utils.MarkEveryoneDirtySettings();
            }

            if (DeathMessageQueue.Count > 0 && DeathMessageQueue.FindFirst(x => x.recordTimeStamp + 3 <= now, out var expired))
            {
                var names = string.Join(", ", DeathMessageQueue.Where(x => x.deathReason == expired.deathReason).Select(x => x.id.ColoredPlayerName()));
                string msgOthers = string.Format(Translator.GetString($"ND_DRLaughMessageOthers-{IRandom.Instance.Next(4)}.{expired.deathReason}"), names);
                Main.EnumerateAlivePlayerControls().NotifyPlayers($"<#ff0000>[╳]</color> {Utils.ColorString(DeathReasonColor(expired.deathReason), msgOthers)}");
                DeathMessageQueue.RemoveAll(x => x.deathReason == expired.deathReason);
            }
        }

        public static void UpdatePreparingDisasters()
        {
            for (var index = PreparingDisasters.Count - 1; index >= 0; index--)
            {
                NaturalDisaster naturalDisaster = PreparingDisasters[index];
                naturalDisaster.Update();

                if (!naturalDisaster.SpawnTimer.IsRunning)
                {
                    Type type = AllDisasters[naturalDisaster.DisasterName];
                    Activator.CreateInstance(type, naturalDisaster.Position, naturalDisaster);
                    PreparingDisasters.RemoveAt(index);
                }
            }
        }

        public static void AddPreparingDisaster(Vector2 position, string disasterName, SystemTypes? room)
        {
            try { PreparingDisasters.Add(new(position, DisasterWarningTime.GetInt(), Sprite(disasterName), disasterName, room)); }
            catch (Exception e) { Utils.ThrowException(e); }
        }
    }
    
    public static void DieToDisaster(this PlayerControl pc, PlayerState.DeathReason deathReason)
    {
        if (ExtendedPlayerControl.TempExiled.Contains(pc.PlayerId)) return;
        
        if (Main.GM.Value) PlayerControl.LocalPlayer.KillFlash();
        ChatCommands.Spectators.ToValidPlayers().Do(x => x.KillFlash());
        
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloPVP when !pc.inVent:
                SoloPVP.BackCountdown.TryAdd(pc.PlayerId, SoloPVP.SoloPVP_ResurrectionWaitingTime.GetInt());
                pc.ExileTemporarily();
                return;
            case CustomGameMode.KingOfTheZones:
                KingOfTheZones.RespawnTimes[pc.PlayerId] = Utils.TimeStamp + KingOfTheZones.RespawnTime.GetInt() + 1;
                pc.ExileTemporarily();
                return;
            case CustomGameMode.BedWars when !pc.inVent:
                BedWars.DisasterDeath(pc, deathReason);
                return;
            case CustomGameMode.Mingle:
                Mingle.HandleDisconnect();
                goto default;
            case CustomGameMode.HotPotato:
                HotPotato.RecordDeath(pc.PlayerId, (int)(Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS - FixedUpdatePatch.WaitTime));
                goto default;
            default:
                pc.Suicide(deathReason);
                return;
        }
    }

    public abstract class Disaster
    {
        protected Disaster(Vector2 position)
        {
            Position = position;
            ActiveDisasters.Add(this);
        }

        public long StartTimeStamp { get; } = Utils.TimeStamp;
        protected Vector2 Position { get; set; }
        protected NaturalDisaster NetObject { get; init; }
        public virtual int Duration { get; set; }

        public virtual bool RemoveIfExpired()
        {
            if (Duration == 0 || Utils.TimeStamp - StartTimeStamp >= Duration)
            {
                ActiveDisasters.Remove(this);
                NetObject.Despawn();
                return true;
            }

            return false;
        }

        public abstract bool Update();

        public virtual void ApplyOwnGameOptions(IGameOptions opt, byte id) { }

        protected void KillNearbyPlayers(PlayerState.DeathReason deathReason, float range = Range)
        {
            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                if (FastVector2.DistanceWithinRange(pc.Pos(), Position, range))
                    pc.DieToDisaster(deathReason);
            }
        }
    }

    public sealed class Earthquake : Disaster
    {
        private static OptionItem DurationOpt;
        private static OptionItem Speed;

        private readonly HashSet<byte> AffectedPlayers = [];

        public Earthquake(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
            Update();
        }

        public override int Duration { get; set; } = DurationOpt.GetInt();

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void SetupOwnCustomOption()
        {
            const int id = 69_216_100;
            Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

            DurationOpt = new IntegerOptionItem(id, "ND_Earthquake.DurationOpt", new(1, 120, 1), 30, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            Speed = new FloatOptionItem(id + 1, "ND_Earthquake.Speed", new(0.05f, 2f, 0.05f), 0.2f, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override bool Update()
        {
            if (RemoveIfExpired()) return true;

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                float speed = FastVector2.DistanceWithinRange(pc.Pos(), Position, Range) switch
                {
                    true when AffectedPlayers.Add(pc.PlayerId) => Speed.GetFloat(),
                    false when AffectedPlayers.Remove(pc.PlayerId) => Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod),
                    _ => float.NaN
                };

                if (!float.IsNaN(speed) && !Mathf.Approximately(Main.AllPlayerSpeed[pc.PlayerId], speed))
                {
                    Main.AllPlayerSpeed[pc.PlayerId] = speed;
                    pc.MarkDirtySettings();
                }
            }

            return false;
        }

        public override bool RemoveIfExpired()
        {
            if (base.RemoveIfExpired())
            {
                foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
                {
                    if (AffectedPlayers.Remove(pc.PlayerId))
                    {
                        Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                        pc.MarkDirtySettings();
                    }
                }

                return true;
            }

            return false;
        }
    }

    public sealed class Meteor : Disaster
    {
        public Meteor(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
            Update();

            KillNearbyPlayers(PlayerState.DeathReason.Meteor);
        }

        public override int Duration { get; set; } = 5;

        public override bool Update()
        {
            return RemoveIfExpired();
        }
    }

    public sealed class VolcanoEruption : Disaster
    {
        private const int Phases = 4;
        private static OptionItem FlowStepDelay;
        private static OptionItem DurationAfterFlowComplete;

        private int Phase = 1;
        private float Timer;

        public VolcanoEruption(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
            Update();
        }

        public override int Duration { get; set; } = (int)((Phases - 1) * FlowStepDelay.GetFloat()) + DurationAfterFlowComplete.GetInt();

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void SetupOwnCustomOption()
        {
            const int id = 69_216_200;
            Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

            FlowStepDelay = new FloatOptionItem(id, "ND_VolcanoEruption.FlowStepDelay", new(0.5f, 5f, 0.5f), 1f, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            DurationAfterFlowComplete = new IntegerOptionItem(id + 1, "ND_VolcanoEruption.DurationAfterFlowComplete", new(1, 120, 1), 5, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override bool Update()
        {
            if (RemoveIfExpired()) return true;

            Timer += Time.deltaTime;

            if (Timer >= FlowStepDelay.GetFloat())
            {
                Timer = 0f;
                if (Phase <= Phases) Phase++;
                if (Phase > Phases) return false;

                string newSprite = Phase switch
                {
                    2 => "<size=170%><line-height=97%><cspace=0.16em><mark=#ff6200>WWWW</mark>\n<mark=#ff6200>WWWW</mark>\n<mark=#ff6200>WWWW</mark>\n<mark=#ff6200>WWWW",
                    3 => "<size=170%><line-height=97%><cspace=0.16em><mark=#ff6200>WWWWWW</mark>\n<mark=#ff6200>WWWWWW</mark>\n<mark=#ff6200>WWWWWW</mark>\n<mark=#ff6200>WWWWWW</mark>\n<mark=#ff6200>WWWWWW</mark>\n<mark=#ff6200>WWWWWW",
                    4 => "<size=170%><line-height=97%><cspace=0.16em><mark=#ff6200>WWWWWWWW</mark>\n<mark=#ff6200>WWWWWWWW</mark>\n<mark=#ff6200>WWWWWWWW</mark>\n<mark=#ff6200>WWWWWWWW</mark>\n<mark=#ff6200>WWWWWWWW</mark>\n<mark=#ff6200>WWWWWWWW</mark>\n<mark=#ff6200>WWWWWWWW</mark>\n<mark=#ff6200>WWWWWWWW",
                    _ => Utils.EmptyMessage
                };

                NetObject.RpcChangeSprite(newSprite);
            }

            float range = Range - ((Phases - Math.Min(4, Phase)) * 0.4f);
            KillNearbyPlayers(PlayerState.DeathReason.Lava, range);
            return false;
        }
    }

    public sealed class Tornado : Disaster
    {
        private static OptionItem DurationOpt;
        private static OptionItem GoesThroughWalls;
        private static OptionItem MovingSpeed;
        private static OptionItem AngleChangeFrequency;

        private float Angle;
        private long LastAngleChange = Utils.TimeStamp;

        public Tornado(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
            Angle = RandomAngle();
            Update();
        }

        public override int Duration { get; set; } = DurationOpt.GetInt();

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void SetupOwnCustomOption()
        {
            const int id = 69_216_300;
            Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

            DurationOpt = new IntegerOptionItem(id, "ND_Tornado.DurationOpt", new(1, 120, 1), 20, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            GoesThroughWalls = new BooleanOptionItem(id + 1, "ND_Tornado.GoesThroughWalls", false, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color);

            MovingSpeed = new FloatOptionItem(id + 2, "ND_Tornado.MovingSpeed", new(0f, 10f, 0.25f), 2f, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Multiplier);

            AngleChangeFrequency = new IntegerOptionItem(id + 3, "ND_Tornado.AngleChangeFrequency", new(1, 30, 1), 5, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override bool Update()
        {
            if (RemoveIfExpired()) return true;

            const float eyeRange = Range / 4f;
            const float dragRange = Range * 1.5f;

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                Vector2 pos = pc.Pos();

                switch (Vector2.Distance(pos, Position))
                {
                    case <= eyeRange:
                        pc.DieToDisaster(PlayerState.DeathReason.Tornado);
                        continue;
                    case <= dragRange:
                        Vector2 direction = (Position - pos).normalized;
                        Vector2 newPosition = pos + (direction * 0.15f);
                        pc.TP(newPosition, true);
                        continue;
                }
            }

            float angle;
            long now = Utils.TimeStamp;

            if (LastAngleChange + AngleChangeFrequency.GetInt() <= now)
            {
                angle = RandomAngle();
                LastAngleChange = now;
                Angle = angle;
            }
            else
                angle = Angle;

            float speed = MovingSpeed.GetFloat() * Time.fixedDeltaTime;
            Vector2 addVector = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 newPos = Position + addVector * speed;

            if ((!GoesThroughWalls.GetBool() && PhysicsHelpers.AnythingBetween(NetObject.playerControl.Collider, Position, newPos + addVector * 2, Constants.ShipOnlyMask, false)) ||
                newPos.x < MapBounds.X.Left || newPos.x > MapBounds.X.Right || newPos.y < MapBounds.Y.Bottom || newPos.y > MapBounds.Y.Top)
            {
                Angle = RandomAngle();
                LastAngleChange = now;
                return false;
            }

            Position = newPos;
            NetObject.TP(Position);
            return false;
        }

        private float RandomAngle()
        {
            if (!FastVector2.TryGetClosest(Position, Main.EnumerateAlivePlayerControls().Select(x => x.Pos()), out Vector2 closest)) return Random.Range(0, 2 * Mathf.PI);
            Vector2 direction = (closest - Position).normalized;
            return Mathf.Atan2(direction.y, direction.x);
        }
    }

    public sealed class Thunderstorm : Disaster
    {
        private static OptionItem HitFrequency;
        private static OptionItem DurationOpt;

        private long LastHit;

        public Thunderstorm(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
            Update();

            LateTask.New(() =>
            {
                RebuildSuffixText();
                Utils.NotifyRoles();
            }, 0.2f, "Thunderstorm Notify");
        }

        public override int Duration { get; set; } = DurationOpt.GetInt();

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void SetupOwnCustomOption()
        {
            const int id = 69_216_400;
            Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

            DurationOpt = new IntegerOptionItem(id, "ND_Thunderstorm.DurationOpt", new(1, 120, 1), 20, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            HitFrequency = new IntegerOptionItem(id + 1, "ND_Thunderstorm.HitFrequency", new(1, 20, 1), 3, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override bool Update()
        {
            if (RemoveIfExpired()) return true;

            long now = Utils.TimeStamp;

            if (now - LastHit >= HitFrequency.GetInt())
            {
                LastHit = now;
                var hit = new Vector2(Random.Range(MapBounds.X.Left, MapBounds.X.Right), Random.Range(MapBounds.Y.Bottom, MapBounds.Y.Top));

                if (!hit.GetPlainShipRoom())
                {
                    if (GameStates.CurrentServerType != GameStates.ServerType.Vanilla)
                        _ = new Lightning(hit);
                    
                    foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
                    {
                        if (FastVector2.DistanceWithinRange(pc.Pos(), hit, Range / 2f))
                            pc.DieToDisaster(PlayerState.DeathReason.Lightning);
                    }
                }
            }

            return false;
        }

        public override bool RemoveIfExpired()
        {
            if (Duration == 0 || Utils.TimeStamp - StartTimeStamp >= Duration)
            {
                ActiveDisasters.Remove(this);
                RebuildSuffixText();
                Utils.NotifyRoles();
                return true;
            }

            return false;
        }
    }

    public sealed class SandStorm : Disaster
    {
        private static OptionItem DurationOpt;
        private static OptionItem Vision;

        private readonly HashSet<byte> AffectedPlayers = [];

        public SandStorm(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
            Update();
        }

        public override int Duration { get; set; } = DurationOpt.GetInt();

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void SetupOwnCustomOption()
        {
            const int id = 69_216_500;
            Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

            DurationOpt = new IntegerOptionItem(id, "ND_SandStorm.DurationOpt", new(1, 120, 1), 30, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            Vision = new FloatOptionItem(id + 1, "ND_SandStorm.Vision", new(0f, 1f, 0.05f), 0.2f, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override bool Update()
        {
            if (RemoveIfExpired()) return true;

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                bool inRange = FastVector2.DistanceWithinRange(pc.Pos(), Position, Range);
                
                if ((inRange && AffectedPlayers.Add(pc.PlayerId)) || (!inRange && AffectedPlayers.Remove(pc.PlayerId)))
                    pc.MarkDirtySettings();
            }

            return false;
        }

        public override bool RemoveIfExpired()
        {
            if (base.RemoveIfExpired())
            {
                foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
                {
                    if (AffectedPlayers.Remove(pc.PlayerId))
                        pc.MarkDirtySettings();
                }

                return true;
            }

            return false;
        }

        public override void ApplyOwnGameOptions(IGameOptions opt, byte id)
        {
            if (AffectedPlayers.Contains(id))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, Vision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision.GetFloat());
            }
        }
    }

    public sealed class Tsunami : Disaster
    {
        private static OptionItem MovingSpeed;

        private static readonly Dictionary<MovingDirection, string> Sprites = new()
        {
            [MovingDirection.LeftToRight] = "<size=170%><line-height=97%><cspace=0.16em><mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW",
            [MovingDirection.RightToLeft] = "<size=170%><line-height=97%><cspace=0.16em><mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W",
            [MovingDirection.TopToBottom] = "<size=170%><line-height=97%><cspace=0.16em><mark=#003cff>WWWWWWWW</mark>\n<mark=#006aff>WWWWWWWW</mark>\n<mark=#00bbff>WWWWWWWW</mark>\n<mark=#b8e1ff>WWWWWWWW</mark>\n<mark=#b8e1ff>WWWWWWWW</mark>\n<mark=#b8e1ff>WWWWWWWW</mark>\n<mark=#b8e1ff>WWWWWWWW</mark>\n<mark=#b8e1ff>WWWWWWWW",
            [MovingDirection.BottomToTop] = "<size=170%><line-height=97%><cspace=0.16em><mark=#c2e2ff>WWWWWWWW</mark>\n<mark=#c2e2ff>WWWWWWWW</mark>\n<mark=#c2e2ff>WWWWWWWW</mark>\n<mark=#c2e2ff>WWWWWWWW</mark>\n<mark=#c2e2ff>WWWWWWWW</mark>\n<mark=#00bfff>WWWWWWWW</mark>\n<mark=#007bff>WWWWWWWW</mark>\n<mark=#0033ff>WWWWWWWW"
        };

        private readonly MovingDirection Direction;

        public Tsunami(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
            Update();

            Direction = GetBestDirection();
            naturalDisaster.RpcChangeSprite(Sprites[Direction]);
            return;

            MovingDirection GetBestDirection()
            {
                int leftToRight = 0;
                int rightToLeft = 0;
                int topToBottom = 0;
                int bottomToTop = 0;

                const float rangeSq = Range * Range;

                foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
                {
                    Vector2 diff = pc.Pos() - Position;

                    if (diff.y * diff.y <= rangeSq)
                    {
                        switch (diff.x)
                        {
                            case > 0:
                                leftToRight++;
                                break;
                            case < 0:
                                rightToLeft++;
                                break;
                        }
                    }

                    if (diff.x * diff.x <= rangeSq)
                    {
                        switch (diff.y)
                        {
                            case < 0:
                                topToBottom++;
                                break;
                            case > 0:
                                bottomToTop++;
                                break;
                        }
                    }
                }

                int max = 0;
                MovingDirection best = MovingDirection.LeftToRight;

                if (leftToRight > max)
                {
                    max = leftToRight;
                    best = MovingDirection.LeftToRight;
                }

                if (rightToLeft > max)
                {
                    max = rightToLeft;
                    best = MovingDirection.RightToLeft;
                }

                if (topToBottom > max)
                {
                    max = topToBottom;
                    best = MovingDirection.TopToBottom;
                }

                if (bottomToTop > max)
                {
                    max = bottomToTop;
                    best = MovingDirection.BottomToTop;
                }
                
                return max == 0 ? Enum.GetValues<MovingDirection>().RandomElement() : best;
            }
        }

        public override int Duration { get; set; } = int.MaxValue;

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void SetupOwnCustomOption()
        {
            const int id = 69_216_600;
            Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

            MovingSpeed = new FloatOptionItem(id, "ND_Tsunami.MovingSpeed", new(0.25f, 10f, 0.25f), 2f, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override bool Update()
        {
            if (RemoveIfExpired()) return true;

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                Vector2 pos = pc.Pos();

                bool inWay = Direction switch
                {
                    MovingDirection.LeftToRight => pos.x >= Position.x,
                    MovingDirection.RightToLeft => pos.x <= Position.x,
                    MovingDirection.TopToBottom => pos.y <= Position.y,
                    MovingDirection.BottomToTop => pos.y >= Position.y,
                    _ => false
                };

                if (FastVector2.DistanceWithinRange(pos, Position, Range) && inWay)
                    pc.DieToDisaster(PlayerState.DeathReason.Drowned);
            }

            float speed = MovingSpeed.GetFloat() * Time.fixedDeltaTime;
            Vector2 newPos = Position;

            switch (Direction)
            {
                case MovingDirection.LeftToRight:
                    newPos.x += speed;
                    break;
                case MovingDirection.RightToLeft:
                    newPos.x -= speed;
                    break;
                case MovingDirection.TopToBottom:
                    newPos.y -= speed;
                    break;
                case MovingDirection.BottomToTop:
                    newPos.y += speed;
                    break;
            }

            if (newPos.x < MapBounds.X.Left || newPos.x > MapBounds.X.Right || newPos.y < MapBounds.Y.Bottom || newPos.y > MapBounds.Y.Top)
            {
                Duration = 0;
                return RemoveIfExpired();
            }

            Position = newPos;
            NetObject.TP(Position);
            return false;
        }

        private enum MovingDirection
        {
            LeftToRight,
            RightToLeft,
            TopToBottom,
            BottomToTop
        }
    }

    public sealed class Sinkhole : Disaster
    {
        public static readonly List<(Vector2 Position, NaturalDisaster NetObject)> Sinkholes = [];

        public Sinkhole(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
            Update();

            KillNearbyPlayers(PlayerState.DeathReason.Sunken);

            Sinkholes.Add((position, naturalDisaster));
        }

        public override int Duration { get; set; } = int.MaxValue;

        public override bool Update()
        {
            return ActiveDisasters.Remove(this);
        }

        public static void OnFixedUpdate()
        {
            if (Sinkholes.Count == 0) return;

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                Vector2 pos = pc.Pos();

                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < Sinkholes.Count; i++)
                {
                    if (FastVector2.DistanceWithinRange(pos, Sinkholes[i].Position, Range))
                        pc.DieToDisaster(PlayerState.DeathReason.Sunken);
                }
            }
        }

        public static void RemoveRandomSinkhole()
        {
            if (Sinkholes.Count == 0) return;
            (Vector2 Position, NaturalDisaster NetObject) remove = Sinkholes.RandomElement();
            remove.NetObject.Despawn();
            Sinkholes.Remove(remove);
        }
    }

    public sealed class BuildingCollapse : Disaster
    {
        private static int Count = 1;
        public static readonly List<PlainShipRoom> CollapsedRooms = [];
        public static readonly Dictionary<byte, Vector2> LastPosition = [];

        public BuildingCollapse(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
            Update();
            
            if (!naturalDisaster.Room.HasValue) return;
            PlainShipRoom room = naturalDisaster.Room.Value.GetRoomClass();
            if (!room) return;

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                if (pc.IsInRoom(room))
                    pc.DieToDisaster(PlayerState.DeathReason.Collapsed);
            }

            CollapsedRooms.Add(room);
            RebuildSuffixText();
            Utils.NotifyRoles();
        }

        public override bool Update()
        {
            Duration = 0;
            return RemoveIfExpired();
        }

        public static void OnFixedUpdate()
        {
            if (CollapsedRooms.Count == 0) return;

            if (Count++ < 10) return;
            Count = 0;

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                if (pc.onLadder || pc.inMovingPlat || pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) continue;
                
                if (CollapsedRooms.Exists(x => pc.IsInRoom(x)))
                {
                    if (LastPosition.TryGetValue(pc.PlayerId, out Vector2 lastPos))
                    {
                        RPC.PlaySoundRPC(pc.PlayerId, Sounds.ImpDiscovered);
                        pc.TP(lastPos);
                    }
                    else pc.DieToDisaster(PlayerState.DeathReason.Collapsed);
                }
                else LastPosition[pc.PlayerId] = pc.transform.position;
            }
        }
    }
}

/*
 * Earthquake: The ground shakes, making it hard to move.
 * Meteor: A meteor falls from the sky, killing players in a certain range.
 * Volcano Eruption: Lava flows from one spot, killing players.
 * Tornado: Players are sucked into the tornado and die.
 * Thunderstorm: Lightning strikes outside of rooms, killing players that it hits.
 * SandStorm: Sand blows, making it hard to see.
 * Tsunami: A giant wave comes moving in a specific direction, drowning players that are on the way.
 * Sinkhole: The ground collapses, killing players in range and any players that enter this range in the future.
 * Building Collapse: A building on the ship collapses, killing everyone inside it. Collapsed rooms cannot be entered in the future.
 */