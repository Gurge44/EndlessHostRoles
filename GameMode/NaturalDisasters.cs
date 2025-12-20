using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EHR;

[SuppressMessage("ReSharper", "UnusedType.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class NaturalDisasters
{
    private const float Range = 1.5f;
    private static List<Type> AllDisasters = [];
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
    private static readonly Dictionary<string, OptionItem> DisasterSpawnChances = [];

    private static readonly string[] LimitReachedOptions =
    [
        "ND_LimitReachedOptions.OnlySpawnInstantDisasters",
        "ND_LimitReachedOptions.RemoveRandom",
        "ND_LimitReachedOptions.RemoveOldest"
    ];

    public static List<Disaster> GetActiveDisasters()
    {
        return ActiveDisasters;
    }

    public static List<Type> GetAllDisasters()
    {
        return AllDisasters;
    }

    public static int FrequencyOfDisasters => DisasterFrequency.GetInt();

    public static void SetupCustomOption()
    {
        var id = 69_216_001;
        Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);
        const CustomGameMode gameMode = CustomGameMode.NaturalDisasters;

        DisasterFrequency = new IntegerOptionItem(id++, "ND_DisasterFrequency", new(1, 20, 1), 2, TabGroup.GameSettings)
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

        WhenLimitIsReached = new StringOptionItem(id++, "ND_WhenLimitIsReached", LimitReachedOptions, 2, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetParent(LimitMaximumDisastersAtOnce)
            .SetColor(color);

        PreferRemovingThunderstorm = new BooleanOptionItem(id++, "ND_PreferRemovingThunderstorm", true, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetParent(WhenLimitIsReached)
            .SetColor(color);

        LoadAllDisasters();

        AllDisasters.ConvertAll(x => x.Name).ForEach(x => DisasterSpawnChances[x] = new IntegerOptionItem(id++, "ND_Disaster.SpawnChance", new(0, 100, 5), 50, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Percent)
            .AddReplacement(("{disaster}", Translator.GetString($"ND_{x}"))));

        AllDisasters.ForEach(x => x.GetMethod("SetupOwnCustomOption")?.Invoke(null, null));
    }

    public static void LoadAllDisasters()
    {
        AllDisasters = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(Disaster)))
            .ToList();
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

        Dictionary<SystemTypes, Vector2>.ValueCollection rooms = RandomSpawn.SpawnMap.GetSpawnMap().Positions?.Values;
        if (rooms == null) return;

        float[] x = rooms.Select(r => r.x).ToArray();
        float[] y = rooms.Select(r => r.y).ToArray();

        const float extend = 5f;
        MapBounds = ((x.Min() - extend, x.Max() + extend), (y.Min() - extend, y.Max() + extend));

        LateTask.New(() =>
        {
            Main.AllPlayerSpeed = Main.PlayerStates.Keys.ToDictionary(k => k, _ => Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
            Utils.SyncAllSettings();
        }, 16f, log: false);
    }

    public static void ApplyGameOptions(IGameOptions opt, byte id)
    {
        ActiveDisasters.ForEach(x => x.ApplyOwnGameOptions(opt, id));
    }

    public static string SuffixText()
    {
        string cb = string.Format(Translator.GetString("CollapsedBuildings"), BuildingCollapse.CollapsedBuildingsString);
        string ts = ActiveDisasters.Exists(x => x is Thunderstorm) ? $"\n{Translator.GetString("OngoingThunderstorm")}" : string.Empty;
        return $"<size=70%>{cb}{ts}</size>";
    }

    public static int SurvivalTime(byte id)
    {
        return SurvivalTimes.GetValueOrDefault(id, 0);
    }

    public static void RecordDeath(PlayerControl pc, PlayerState.DeathReason deathReason)
    {
        SurvivalTimes[pc.PlayerId] = (int)(Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS);

        string message = Translator.GetString($"ND_DRLaughMessage.{deathReason}");
        message = Utils.ColorString(DeathReasonColor(deathReason), message);
        LateTask.New(() => pc.Notify(message, 20f), 1f, $"{pc.GetRealName()} died with the reason {deathReason}, survived for {SurvivalTime(pc.PlayerId)} seconds");
        Utils.SendRPC(CustomRPC.NaturalDisastersSync, pc.PlayerId, SurvivalTimes[pc.PlayerId]);
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
            "Earthquake" => "<size=170%><font=\"VCR SDF\"><line-height=67%><#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<#adadad>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#5e5e5e>\u2588<#000000>\u2588<#5e5e5e>\u2588<#adadad>\u2588<#adadad>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#adadad>\u2588<#adadad>\u2588<alpha=#00>\u2588<br><#adadad>\u2588<#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#adadad>\u2588<br><alpha=#00>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#adadad>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#adadad>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#000000>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#000000>\u2588<br></line-height></size>",
            "Meteor" => "<size=170%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<br><#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff1100>\u2588<#ff1100>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<br><#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff1100>\u2588<#ff1100>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<br><alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
            "VolcanoEruption" => "<size=170%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
            "Tornado" => "<size=170%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#828282>\u2588<#828282>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#828282>\u2588<#474747>\u2588<#474747>\u2588<#828282>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#828282>\u2588<#474747>\u2588<#474747>\u2588<#828282>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#828282>\u2588<#828282>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><alpha=#00>\u2588<#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
            "SandStorm" => "<size=170%><font=\"VCR SDF\"><line-height=67%><#ffdc7a>█<#dbcfba>█<#f5c387>█<#ffe8d6>█<#ffc18f>█<#ffe8d6>█<#ffe8d6>█<#ffdc7a>█<br><#dbcfba>█<#f5c6a2>█<#ffc18f>█<#ffe8d6>█<#e6a875>█<#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<br><#e6a875>█<#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<#ffe8d6>█<#ffdc7a>█<#ffe8d6>█<#f5c387>█<br><#ffe8d6>█<#ffdc7a>█<#dbcfba>█<#ffc18f>█<#ffe8d6>█<#ffe8d6>█<#f5c6a2>█<#ffc18f>█<br><#f5c6a2>█<#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<#f5c6a2>█<#e6a875>█<#dbcfba>█<#ffe8d6>█<br><#ffc18f>█<#ffe8d6>█<#ffc18f>█<#ffe8d6>█<#ffe8d6>█<#ffdc7a>█<#ffe8d6>█<#e6a875>█<br><#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<br><#ffdc7a>█<#ffe8d6>█<#e6a875>█<#ffe8d6>█<#ffc18f>█<#ffe8d6>█<#ffe8d6>█<#ffdc7a>█<br></line-height></size>",
            "Sinkhole" => "<size=170%><font=\"VCR SDF\"><line-height=67%><#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<br><#545454>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#7d7d7d>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#545454>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#7d7d7d>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#545454>\u2588<br><#545454>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#7d7d7d>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#545454>\u2588<#545454>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<br></line-height></size>",
            _ => Utils.EmptyMessage
        };
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        private static long LastDisaster = Utils.TimeStamp;
        private static long LastSync = Utils.TimeStamp;

        public static void Postfix( /*PlayerControl __instance*/)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || (Options.CurrentGameMode != CustomGameMode.NaturalDisasters && !Options.IntegrateNaturalDisasters.GetBool()) || !Main.IntroDestroyed || Main.HasJustStarted || IntroCutsceneDestroyPatch.IntroDestroyTS + 5 > Utils.TimeStamp /* || __instance.PlayerId >= 254 || !__instance.IsHost()*/) return;

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

                if (shouldWait || IntroCutsceneDestroyPatch.IntroDestroyTS + minimumWaitTime > Utils.TimeStamp) return;
            }
            
            UpdatePreparingDisasters();

            ActiveDisasters.ToArray().Do(x => x.Update());
            Sinkhole.OnFixedUpdate();
            BuildingCollapse.OnFixedUpdate();

            if (LimitMaximumDisastersAtOnce.GetBool())
            {
                int numDisasters = ActiveDisasters.Count + PreparingDisasters.Count + Sinkhole.Sinkholes.Count;

                if (numDisasters > MaximumDisastersAtOnce.GetInt())
                {
                    switch (WhenLimitIsReached.GetValue())
                    {
                        case 0:
                        {
                            AllDisasters.RemoveAll(x => x.Name is "Earthquake" or "VolcanoEruption" or "Tornado" or "Thunderstorm" or "SandStorm" or "Tsunami");
                            break;
                        }
                        case 1:
                        {
                            if (IRandom.Instance.Next(AllDisasters.Count) == 0)
                            {
                                Sinkhole.RemoveRandomSinkhole();
                                return;
                            }

                            Disaster remove = PreferRemovingThunderstorm.GetBool() ? ActiveDisasters.Find(x => x is Thunderstorm) : ActiveDisasters.RandomElement();
                            if (remove != null) remove.Duration = 0;

                            remove?.RemoveIfExpired();
                            break;
                        }
                        case 2:
                        {
                            Disaster oldest = ActiveDisasters.MinBy(x => x.StartTimeStamp);
                            oldest.Duration = 0;
                            oldest.RemoveIfExpired();
                            break;
                        }
                    }
                }
            }

            long now = Utils.TimeStamp;
            int frequency = DisasterFrequency.GetInt();

            if (now - LastDisaster >= frequency)
            {
                LastDisaster = now;
                List<Type> disasters = AllDisasters.ToList();
                if (ActiveDisasters.Exists(x => x is Thunderstorm)) disasters.RemoveAll(x => x.Name == "Thunderstorm");

                Type disaster = disasters.SelectMany(x => Enumerable.Repeat(x, DisasterSpawnChances[x.Name].GetInt() / 5)).RandomElement();
                KeyValuePair<SystemTypes, Vector2> roomKvp = RandomSpawn.SpawnMap.GetSpawnMap().Positions.RandomElement();

                Vector2 position = disaster.Name switch
                {
                    "BuildingCollapse" => roomKvp.Value,
                    "Thunderstorm" => Pelican.GetBlackRoomPS(),
                    _ => IRandom.Instance.Next(2) == 0 && Options.CurrentGameMode == CustomGameMode.NaturalDisasters
                        ? Main.AllAlivePlayerControls.RandomElement().Pos()
                        : new(Random.Range(MapBounds.X.Left, MapBounds.X.Right), Random.Range(MapBounds.Y.Top, MapBounds.Y.Bottom))
                };

                SystemTypes? room = disaster.Name == "BuildingCollapse" ? roomKvp.Key : null;
                AddPreparingDisaster(position, disaster.Name, room);
            }

            if (now - LastSync >= 15)
            {
                if (Options.CurrentGameMode != CustomGameMode.NaturalDisasters)
                {
                    BuildingCollapse.CollapsedRooms.Clear();
                    Sinkhole.RemoveRandomSinkhole();
                    Utils.NotifyRoles();
                }
                
                LastSync = now;
                Utils.MarkEveryoneDirtySettings();
            }
        }

        public static void UpdatePreparingDisasters()
        {
            foreach (NaturalDisaster naturalDisaster in PreparingDisasters.ToArray())
            {
                naturalDisaster.Update();

                if (float.IsNaN(naturalDisaster.SpawnTimer))
                {
                    Type type = AllDisasters.Find(d => d.Name == naturalDisaster.DisasterName);
                    LateTask.New(() => Activator.CreateInstance(type, naturalDisaster.Position, naturalDisaster), 1f, log: false);
                    PreparingDisasters.Remove(naturalDisaster);
                }
            }
        }

        public static void AddPreparingDisaster(Vector2 position, string disasterName, SystemTypes? room)
        {
            PreparingDisasters.Add(new(position, DisasterWarningTime.GetFloat(), Sprite(disasterName), disasterName, room));
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

        public abstract void Update();

        public virtual void ApplyOwnGameOptions(IGameOptions opt, byte id) { }

        protected void KillNearbyPlayers(PlayerState.DeathReason deathReason, float range = Range)
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (Vector2.Distance(pc.Pos(), Position) <= range)
                    pc.Suicide(deathReason);
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
                .SetHeader(true)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            Speed = new FloatOptionItem(id + 1, "ND_Earthquake.Speed", new(0.05f, 2f, 0.05f), 0.2f, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override void Update()
        {
            if (RemoveIfExpired()) return;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                float speed = (Vector2.Distance(pc.Pos(), Position) <= Range) switch
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
        }

        public override bool RemoveIfExpired()
        {
            if (base.RemoveIfExpired())
            {
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
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

        public override void Update()
        {
            RemoveIfExpired();
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
                .SetHeader(true)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            DurationAfterFlowComplete = new IntegerOptionItem(id + 1, "ND_VolcanoEruption.DurationAfterFlowComplete", new(1, 120, 1), 5, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Update()
        {
            if (RemoveIfExpired()) return;

            Timer += Time.deltaTime;

            if (Timer >= FlowStepDelay.GetFloat())
            {
                Timer = 0f;
                if (Phase <= Phases) Phase++;
                if (Phase > Phases) return;

                string newSprite = Phase switch
                {
                    2 => "<size=170%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
                    3 => "<size=170%><font=\"VCR SDF\"><line-height=67%><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br></line-height></size>",
                    4 => "<size=170%><font=\"VCR SDF\"><line-height=67%><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br></line-height></size>",
                    _ => Utils.EmptyMessage
                };

                NetObject.RpcChangeSprite(newSprite);
            }

            float range = Range - ((Phases - Math.Min(4, Phase)) * 0.4f);
            KillNearbyPlayers(PlayerState.DeathReason.Lava, range);
        }
    }

    public sealed class Tornado : Disaster
    {
        private static OptionItem DurationOpt;
        private static OptionItem GoesThroughWalls;
        private static OptionItem MovingSpeed;
        private static OptionItem AngleChangeFrequency;

        private float Angle = RandomAngle();
        private long LastAngleChange = Utils.TimeStamp;

        public Tornado(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
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
                .SetHeader(true)
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

        public override void Update()
        {
            if (RemoveIfExpired()) return;

            const float eyeRange = Range / 4f;
            const float dragRange = Range * 1.5f;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                Vector2 pos = pc.Pos();

                switch (Vector2.Distance(pos, Position))
                {
                    case <= eyeRange:
                        pc.Suicide(PlayerState.DeathReason.Tornado);
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
                return;
            }

            Position = newPos;
            NetObject.TP(Position);
        }

        private static float RandomAngle()
        {
            return Random.Range(0, 2 * Mathf.PI);
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

            naturalDisaster.Despawn();

            LateTask.New(() => Utils.NotifyRoles(), 0.2f, "Thunderstorm Notify");
        }

        public override int Duration { get; set; } = DurationOpt.GetInt();

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void SetupOwnCustomOption()
        {
            const int id = 69_216_400;
            Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

            DurationOpt = new IntegerOptionItem(id, "ND_Thunderstorm.DurationOpt", new(1, 120, 1), 20, TabGroup.GameSettings)
                .SetHeader(true)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            HitFrequency = new IntegerOptionItem(id + 1, "ND_Thunderstorm.HitFrequency", new(1, 20, 1), 3, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Update()
        {
            if (RemoveIfExpired()) return;

            long now = Utils.TimeStamp;

            if (now - LastHit >= HitFrequency.GetInt())
            {
                LastHit = now;
                var hit = new Vector2(Random.Range(MapBounds.X.Left, MapBounds.X.Right), Random.Range(MapBounds.Y.Bottom, MapBounds.Y.Top));
                var cno = new Lightning(hit);

                if (cno.playerControl.GetPlainShipRoom() != null)
                    cno.Despawn();
                else
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (Vector2.Distance(pc.Pos(), hit) <= Range / 2f)
                            pc.Suicide(PlayerState.DeathReason.Lightning);
                    }
                }
            }
        }

        public override bool RemoveIfExpired()
        {
            if (Duration == 0 || Utils.TimeStamp - StartTimeStamp >= Duration)
            {
                ActiveDisasters.Remove(this);
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
                .SetHeader(true)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            Vision = new FloatOptionItem(id + 1, "ND_SandStorm.Vision", new(0f, 1f, 0.05f), 0.2f, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override void Update()
        {
            if (RemoveIfExpired()) return;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                bool inRange = Vector2.Distance(pc.Pos(), Position) <= Range;
                if ((inRange && AffectedPlayers.Add(pc.PlayerId)) || (!inRange && AffectedPlayers.Remove(pc.PlayerId)))
                    pc.MarkDirtySettings();
            }
        }

        public override bool RemoveIfExpired()
        {
            if (base.RemoveIfExpired())
            {
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
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
            [MovingDirection.LeftToRight] = "<size=170%><font=\"VCR SDF\"><line-height=67%><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br></line-height></size>",
            [MovingDirection.RightToLeft] = "<size=170%><font=\"VCR SDF\"><line-height=67%><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br></line-height></size>",
            [MovingDirection.TopToBottom] = "<size=170%><font=\"VCR SDF\"><line-height=67%><#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<br><#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<br><#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<br><#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<br><#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<br><#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<br><#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<br><#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<br></line-height></size>",
            [MovingDirection.BottomToTop] = "<size=170%><font=\"VCR SDF\"><line-height=67%><#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<br><#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<br><#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<br><#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<br><#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<br><#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<br><#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<br><#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<br></line-height></size>"
        };

        private readonly MovingDirection Direction;

        public Tsunami(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
        {
            NetObject = naturalDisaster;
            Update();

            Direction = Enum.GetValues<MovingDirection>().RandomElement();
            naturalDisaster.RpcChangeSprite(Sprites[Direction]);
        }

        public override int Duration { get; set; } = int.MaxValue;

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void SetupOwnCustomOption()
        {
            const int id = 69_216_600;
            Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

            MovingSpeed = new FloatOptionItem(id, "ND_Tsunami.MovingSpeed", new(0.25f, 10f, 0.25f), 2f, TabGroup.GameSettings)
                .SetHeader(true)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override void Update()
        {
            if (RemoveIfExpired()) return;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
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

                if (Vector2.Distance(pos, Position) <= Range && inWay)
                    pc.Suicide(PlayerState.DeathReason.Drowned);
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
                RemoveIfExpired();
                return;
            }

            Position = newPos;
            NetObject.TP(Position);
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

        public override void Update()
        {
            ActiveDisasters.Remove(this);
        }

        public static void OnFixedUpdate()
        {
            if (Sinkholes.Count == 0) return;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                Vector2 pos = pc.Pos();

                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < Sinkholes.Count; i++)
                {
                    if (Vector2.Distance(pos, Sinkholes[i].Position) <= Range)
                        pc.Suicide(PlayerState.DeathReason.Sunken);
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

            PlainShipRoom room = ShipStatus.Instance.AllRooms.FirstOrDefault(x => x.RoomId == naturalDisaster.Room);
            if (room == null) return;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (pc.IsInRoom(room))
                    pc.Suicide(PlayerState.DeathReason.Collapsed);
            }

            CollapsedRooms.Add(room);
            Utils.NotifyRoles();
        }

        public static string CollapsedBuildingsString => CollapsedRooms.Count > 0
            ? CollapsedRooms.Select(x => Translator.GetString($"{x.RoomId}")).Distinct().Join()
            : Translator.GetString("None");

        public override void Update()
        {
            Duration = 0;
            RemoveIfExpired();
        }

        public static void OnFixedUpdate()
        {
            if (CollapsedRooms.Count == 0) return;

            if (Count++ < 10) return;
            Count = 0;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (pc.onLadder || pc.inMovingPlat || pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) continue;
                
                PlainShipRoom room = pc.GetPlainShipRoom();

                if (room != null && CollapsedRooms.Exists(x => x == room))
                {
                    if (LastPosition.TryGetValue(pc.PlayerId, out Vector2 lastPos))
                    {
                        RPC.PlaySoundRPC(pc.PlayerId, Sounds.ImpDiscovered);
                        pc.TP(lastPos);
                    }
                    else pc.Suicide(PlayerState.DeathReason.Collapsed);
                }
                else LastPosition[pc.PlayerId] = pc.Pos();
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
