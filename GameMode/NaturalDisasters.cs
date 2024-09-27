using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.Neutral;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EHR
{
    [SuppressMessage("ReSharper", "UnusedType.Local")]
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    public static class NaturalDisasters
    {
        private const float Range = 1.5f;
        private static List<Type> AllDisasters = [];
        private static readonly List<Disaster> ActiveDisasters = [];
        private static readonly List<NaturalDisaster> PreparingDisasters = [];
        private static readonly Dictionary<byte, int> SurvivalTimes = [];

        private static ((float Left, float Right) X, (float Bottom, float Top) Y) MapBounds;
        private static long GameStartTimeStamp;

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

        public static void SetupCustomOption()
        {
            int id = 69_216_001;
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

        private static void LoadAllDisasters()
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

            if (Options.CurrentGameMode != CustomGameMode.NaturalDisasters) return;

            var rooms = RoomLocations()?.Values;
            if (rooms == null) return;

            var x = rooms.Select(r => r.x).ToArray();
            var y = rooms.Select(r => r.y).ToArray();

            const float extend = 3.5f;
            MapBounds = ((x.Min() - extend, x.Max() + extend), (y.Min() - extend, y.Max() + extend));

            GameStartTimeStamp = Utils.TimeStamp + 8;

            LateTask.New(() =>
            {
                Main.AllPlayerSpeed = Main.PlayerStates.Keys.ToDictionary(k => k, _ => Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
                Utils.SyncAllSettings();
            }, 11f, log: false);
        }

        public static void ApplyGameOptions(IGameOptions opt, byte id)
        {
            ActiveDisasters.ForEach(x => x.ApplyOwnGameOptions(opt, id));
        }

        public static string SuffixText()
        {
            var cb = string.Format(Translator.GetString("CollapsedBuildings"), BuildingCollapse.CollapsedBuildingsString);
            var ts = ActiveDisasters.Exists(x => x is Thunderstorm) ? $"\n{Translator.GetString("OngoingThunderstorm")}" : string.Empty;
            return $"<size=80%>{cb}{ts}</size>";
        }

        public static int SurvivalTime(byte id)
        {
            return SurvivalTimes.GetValueOrDefault(id, 0);
        }

        public static void RecordDeath(PlayerControl pc, PlayerState.DeathReason deathReason)
        {
            SurvivalTimes[pc.PlayerId] = (int)(Utils.TimeStamp - GameStartTimeStamp);

            var message = Translator.GetString($"ND_DRLaughMessage.{deathReason}");
            message = Utils.ColorString(DeathReasonColor(deathReason), message);
            LateTask.New(() => pc.Notify(message, 20f), 1f, $"{pc.GetRealName()} died with the reason {deathReason}, survived for {SurvivalTime(pc.PlayerId)} seconds");
        }

        private static Color DeathReasonColor(PlayerState.DeathReason deathReason) => deathReason switch
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

        private static string Sprite(string name) => name switch
        {
            "Earthquake" => "<size=170%><font=\"VCR SDF\"><line-height=67%><#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<#adadad>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#5e5e5e>\u2588<#000000>\u2588<#5e5e5e>\u2588<#adadad>\u2588<#adadad>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#adadad>\u2588<#adadad>\u2588<alpha=#00>\u2588<br><#adadad>\u2588<#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#adadad>\u2588<br><alpha=#00>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#adadad>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#adadad>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#000000>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#000000>\u2588<br></line-height></size>",
            "Meteor" => "<size=170%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<br><#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff1100>\u2588<#ff1100>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<br><#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff1100>\u2588<#ff1100>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<br><alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
            "VolcanoEruption" => "<size=170%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
            "Tornado" => "<size=170%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#828282>\u2588<#828282>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#828282>\u2588<#474747>\u2588<#474747>\u2588<#828282>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#828282>\u2588<#474747>\u2588<#474747>\u2588<#828282>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#828282>\u2588<#828282>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><alpha=#00>\u2588<#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
            "SandStorm" => "<size=170%><font=\"VCR SDF\"><line-height=67%><#ffdc7a>█<#dbcfba>█<#f5c387>█<#ffe8d6>█<#ffc18f>█<#ffe8d6>█<#ffe8d6>█<#ffdc7a>█<br><#dbcfba>█<#f5c6a2>█<#ffc18f>█<#ffe8d6>█<#e6a875>█<#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<br><#e6a875>█<#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<#ffe8d6>█<#ffdc7a>█<#ffe8d6>█<#f5c387>█<br><#ffe8d6>█<#ffdc7a>█<#dbcfba>█<#ffc18f>█<#ffe8d6>█<#ffe8d6>█<#f5c6a2>█<#ffc18f>█<br><#f5c6a2>█<#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<#f5c6a2>█<#e6a875>█<#dbcfba>█<#ffe8d6>█<br><#ffc18f>█<#ffe8d6>█<#ffc18f>█<#ffe8d6>█<#ffe8d6>█<#ffdc7a>█<#ffe8d6>█<#e6a875>█<br><#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<#ffe8d6>█<#f5c6a2>█<#ffe8d6>█<br><#ffdc7a>█<#ffe8d6>█<#e6a875>█<#ffe8d6>█<#ffc18f>█<#ffe8d6>█<#ffe8d6>█<#ffdc7a>█<br></line-height></size>",
            "Sinkhole" => "<size=170%><font=\"VCR SDF\"><line-height=67%><#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<br><#545454>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#7d7d7d>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#545454>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#7d7d7d>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#545454>\u2588<br><#545454>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#7d7d7d>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#545454>\u2588<#545454>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<br></line-height></size>",
            _ => Utils.EmptyMessage
        };

        private static Dictionary<SystemTypes, Vector2> RoomLocations()
        {
            return Main.CurrentMap switch
            {
                MapNames.Skeld => new RandomSpawn.SkeldSpawnMap().positions,
                MapNames.Mira => new RandomSpawn.MiraHQSpawnMap().positions,
                MapNames.Polus => new RandomSpawn.PolusSpawnMap().positions,
                MapNames.Dleks => new RandomSpawn.DleksSpawnMap().positions,
                MapNames.Airship => new RandomSpawn.AirshipSpawnMap().positions,
                MapNames.Fungle => new RandomSpawn.FungleSpawnMap().positions,
                _ => null
            };
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        static class FixedUpdatePatch
        {
            private static long LastDisaster = Utils.TimeStamp;
            private static long LastSync = Utils.TimeStamp;

            [SuppressMessage("ReSharper", "UnusedMember.Local")]
            public static void Postfix(PlayerControl __instance)
            {
                if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.NaturalDisasters || Main.HasJustStarted || GameStartTimeStamp + 5 > Utils.TimeStamp || !__instance.IsHost()) return;

                foreach (NaturalDisaster naturalDisaster in PreparingDisasters.ToArray())
                {
                    naturalDisaster.Update();
                    if (float.IsNaN(naturalDisaster.SpawnTimer))
                    {
                        var type = AllDisasters.Find(d => d.Name == naturalDisaster.DisasterName);
                        LateTask.New(() => Activator.CreateInstance(type, naturalDisaster.Position, naturalDisaster), 1f, log: false);
                        PreparingDisasters.Remove(naturalDisaster);
                    }
                }

                ActiveDisasters.ToArray().Do(x => x.Update());
                Sinkhole.OnFixedUpdate();
                BuildingCollapse.OnFixedUpdate();

                if (LimitMaximumDisastersAtOnce.GetBool())
                {
                    var numDisasters = ActiveDisasters.Count + PreparingDisasters.Count;
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

                                var remove = PreferRemovingThunderstorm.GetBool() ? ActiveDisasters.Find(x => x is Thunderstorm) : ActiveDisasters.RandomElement();
                                if (remove != null) remove.Duration = 0;
                                remove?.RemoveIfExpired();
                                break;
                            }
                            case 2:
                            {
                                var oldest = ActiveDisasters.MinBy(x => x.StartTimeStamp);
                                oldest.Duration = 0;
                                oldest.RemoveIfExpired();
                                break;
                            }
                        }
                    }
                }

                var now = Utils.TimeStamp;
                if (now - LastDisaster >= DisasterFrequency.GetInt())
                {
                    LastDisaster = now;
                    var disasters = AllDisasters.ToList();
                    if (ActiveDisasters.Exists(x => x is Thunderstorm)) disasters.RemoveAll(x => x.Name == "Thunderstorm");
                    var disaster = disasters.SelectMany(x => Enumerable.Repeat(x, DisasterSpawnChances[x.Name].GetInt() / 5)).RandomElement();
                    var roomKvp = RoomLocations().RandomElement();
                    var position = disaster.Name switch
                    {
                        "BuildingCollapse" => roomKvp.Value,
                        "Thunderstorm" => Pelican.GetBlackRoomPS(),
                        _ => IRandom.Instance.Next(2) == 0
                            ? Main.AllAlivePlayerControls.RandomElement().Pos()
                            : new(Random.Range(MapBounds.X.Left, MapBounds.X.Right), Random.Range(MapBounds.Y.Top, MapBounds.Y.Bottom))
                    };
                    SystemTypes? room = disaster.Name == "BuildingCollapse" ? roomKvp.Key : null;
                    PreparingDisasters.Add(new(position, DisasterWarningTime.GetFloat(), Sprite(disaster.Name), disaster.Name, room));
                }

                if (now - LastSync >= 10)
                {
                    LastSync = now;
                    Utils.MarkEveryoneDirtySettings();
                }
            }
        }

        abstract class Disaster
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

            public virtual void ApplyOwnGameOptions(IGameOptions opt, byte id)
            {
            }

            protected void KillNearbyPlayers(PlayerState.DeathReason deathReason, float range = Range)
            {
                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    if (Vector2.Distance(pc.Pos(), this.Position) <= range)
                    {
                        pc.Suicide(deathReason);
                    }
                }
            }
        }

        sealed class Earthquake : Disaster
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

                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    float speed = (Vector2.Distance(pc.Pos(), this.Position) <= Range) switch
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
                    foreach (var pc in Main.AllAlivePlayerControls)
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

        sealed class Meteor : Disaster
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

        sealed class VolcanoEruption : Disaster
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
            public static void SetupOwnCustomOption()
            {
                const int id = 69_216_200;
                Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

                FlowStepDelay = new FloatOptionItem(id, "ND_VolcanoEruption.FlowStepDelay", new(0.5f, 5f, 0.5f), 1f, TabGroup.GameSettings)
                    .SetHeader(true)
                    .SetGameMode(CustomGameMode.NaturalDisasters)
                    .SetColor(color)
                    .SetValueFormat(OptionFormat.Seconds);

                DurationAfterFlowComplete = new IntegerOptionItem(id + 1, "ND_VolcanoEruption.DurationAfterFlowComplete", new(1, 120, 1), 10, TabGroup.GameSettings)
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
                    Phase++;
                    if (Phase > Phases) return;

                    var newSprite = Phase switch
                    {
                        2 => "<size=170%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
                        3 => "<size=170%><font=\"VCR SDF\"><line-height=67%><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br></line-height></size>",
                        4 => "<size=170%><font=\"VCR SDF\"><line-height=67%><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br></line-height></size>",
                        _ => Utils.EmptyMessage
                    };

                    this.NetObject.RpcChangeSprite(newSprite);
                }

                var range = Range - ((Phases - Phase) * 0.05f);
                KillNearbyPlayers(PlayerState.DeathReason.Lava, range);
            }
        }

        sealed class Tornado : Disaster
        {
            private static OptionItem DurationOpt;
            private static OptionItem GoesThroughWalls;
            private static OptionItem MovingSpeed;

            private float Angle = RandomAngle();
            private int Count = 2;

            private long LastAngleChange = Utils.TimeStamp;

            public Tornado(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
            {
                NetObject = naturalDisaster;
                Update();
            }

            public override int Duration { get; set; } = DurationOpt.GetInt();

            [SuppressMessage("ReSharper", "UnusedMember.Local")]
            public static void SetupOwnCustomOption()
            {
                const int id = 69_216_300;
                Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

                DurationOpt = new IntegerOptionItem(id, "ND_Tornado.DurationOpt", new(1, 120, 1), 40, TabGroup.GameSettings)
                    .SetHeader(true)
                    .SetGameMode(CustomGameMode.NaturalDisasters)
                    .SetColor(color)
                    .SetValueFormat(OptionFormat.Seconds);

                GoesThroughWalls = new BooleanOptionItem(id + 1, "ND_Tornado.GoesThroughWalls", false, TabGroup.GameSettings)
                    .SetGameMode(CustomGameMode.NaturalDisasters)
                    .SetColor(color);

                MovingSpeed = new FloatOptionItem(id + 2, "ND_Tornado.MovingSpeed", new(0f, 0.5f, 0.05f), 0.1f, TabGroup.GameSettings)
                    .SetGameMode(CustomGameMode.NaturalDisasters)
                    .SetColor(color)
                    .SetValueFormat(OptionFormat.Multiplier);
            }

            public override void Update()
            {
                if (RemoveIfExpired()) return;

                const float eyeRange = Range / 4f;
                const float dragRange = Range * 1.25f;

                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    var pos = pc.Pos();
                    switch (Vector2.Distance(pos, this.Position))
                    {
                        case <= eyeRange:
                            pc.Suicide(PlayerState.DeathReason.Tornado);
                            continue;
                        case <= dragRange:
                            Vector2 direction = (this.Position - pos).normalized;
                            Vector2 newPosition = pos + direction * 0.1f;
                            pc.TP(newPosition);
                            continue;
                    }
                }

                if (Count++ < 3) return;
                Count = 0;

                float angle;
                long now = Utils.TimeStamp;
                if (LastAngleChange + 7 <= now)
                {
                    angle = RandomAngle();
                    LastAngleChange = now;
                    Angle = angle;
                }
                else angle = Angle;

                Vector2 newPos = this.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * MovingSpeed.GetFloat();

                if (!GoesThroughWalls.GetBool() && PhysicsHelpers.AnythingBetween(this.NetObject.playerControl.Collider, this.Position, newPos, Constants.ShipOnlyMask, false) ||
                    newPos.x < MapBounds.X.Left || newPos.x > MapBounds.X.Right || newPos.y < MapBounds.Y.Bottom || newPos.y > MapBounds.Y.Top)
                {
                    Angle = RandomAngle();
                    LastAngleChange = now;
                    Count = 2;
                    return;
                }

                this.Position = newPos;
                this.NetObject.TP(this.Position);
            }

            private static float RandomAngle() => Random.Range(0, 2 * Mathf.PI);
        }

        sealed class Thunderstorm : Disaster
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
                    if (cno.playerControl.GetPlainShipRoom() != default(PlainShipRoom)) cno.Despawn();
                    foreach (var pc in Main.AllAlivePlayerControls)
                    {
                        if (pc.GetPlainShipRoom() != default(PlainShipRoom)) continue;
                        if (Vector2.Distance(pc.Pos(), hit) <= Range)
                        {
                            pc.Suicide(PlayerState.DeathReason.Lightning);
                        }
                    }
                }
            }

            public override bool RemoveIfExpired()
            {
                if (base.RemoveIfExpired())
                {
                    Utils.NotifyRoles();
                    return true;
                }

                return false;
            }
        }

        sealed class SandStorm : Disaster
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

                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    var inRange = Vector2.Distance(pc.Pos(), this.Position) <= Range;
                    if ((inRange && AffectedPlayers.Add(pc.PlayerId)) || (!inRange && AffectedPlayers.Remove(pc.PlayerId)))
                    {
                        pc.MarkDirtySettings();
                    }
                }
            }

            public override bool RemoveIfExpired()
            {
                if (base.RemoveIfExpired())
                {
                    foreach (var pc in Main.AllAlivePlayerControls)
                    {
                        if (AffectedPlayers.Remove(pc.PlayerId))
                        {
                            pc.MarkDirtySettings();
                        }
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

        sealed class Tsunami : Disaster
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

            private int Count = 1;

            public Tsunami(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
            {
                NetObject = naturalDisaster;
                Update();

                Direction = Enum.GetValues<MovingDirection>().RandomElement();
                naturalDisaster.RpcChangeSprite(Sprites[Direction]);
            }

            public override int Duration { get; set; } = int.MaxValue;

            [SuppressMessage("ReSharper", "UnusedMember.Local")]
            public static void SetupOwnCustomOption()
            {
                const int id = 69_216_600;
                Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

                MovingSpeed = new FloatOptionItem(id, "ND_Tsunami.MovingSpeed", new(0.05f, 0.5f, 0.05f), 0.1f, TabGroup.GameSettings)
                    .SetHeader(true)
                    .SetGameMode(CustomGameMode.NaturalDisasters)
                    .SetColor(color)
                    .SetValueFormat(OptionFormat.Multiplier);
            }

            public override void Update()
            {
                if (RemoveIfExpired()) return;

                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    var pos = pc.Pos();
                    var inWay = Direction switch
                    {
                        MovingDirection.LeftToRight => pos.x >= this.Position.x,
                        MovingDirection.RightToLeft => pos.x <= this.Position.x,
                        MovingDirection.TopToBottom => pos.y <= this.Position.y,
                        MovingDirection.BottomToTop => pos.y >= this.Position.y,
                        _ => false
                    };

                    if (Vector2.Distance(pos, this.Position) <= Range && inWay)
                    {
                        pc.Suicide(PlayerState.DeathReason.Drowned);
                    }
                }

                if (Count++ < 2) return;
                Count = 0;

                float speed = MovingSpeed.GetFloat();
                Vector2 newPos = this.Position;
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

                this.Position = newPos;
                this.NetObject.TP(this.Position);
            }

            private enum MovingDirection
            {
                LeftToRight,
                RightToLeft,
                TopToBottom,
                BottomToTop
            }
        }

        sealed class Sinkhole : Disaster
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

                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    var pos = pc.Pos();
                    // ReSharper disable once ForCanBeConvertedToForeach
                    for (int i = 0; i < Sinkholes.Count; i++)
                    {
                        if (Vector2.Distance(pos, Sinkholes[i].Position) <= Range)
                        {
                            pc.Suicide(PlayerState.DeathReason.Sunken);
                        }
                    }
                }
            }

            public static void RemoveRandomSinkhole()
            {
                var remove = Sinkholes.RandomElement();
                remove.NetObject.Despawn();
                Sinkholes.Remove(remove);
            }
        }

        sealed class BuildingCollapse : Disaster
        {
            private static int Count = 1;
            public static readonly List<PlainShipRoom> CollapsedRooms = [];
            public static readonly Dictionary<byte, Vector2> LastPosition = [];

            public BuildingCollapse(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
            {
                NetObject = naturalDisaster;
                Update();

                var room = ShipStatus.Instance.AllRooms.FirstOrDefault(x => x.RoomId == naturalDisaster.Room);
                if (room == default(PlainShipRoom)) return;

                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    if (pc.GetPlainShipRoom() == room)
                    {
                        pc.Suicide(PlayerState.DeathReason.Collapsed);
                    }
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

                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    var room = pc.GetPlainShipRoom();
                    if (room != default(PlainShipRoom) && CollapsedRooms.Exists(x => x == room))
                    {
                        if (LastPosition.TryGetValue(pc.PlayerId, out var lastPos)) pc.TP(lastPos);
                        else pc.Suicide(PlayerState.DeathReason.Collapsed);
                    }
                    else LastPosition[pc.PlayerId] = pc.Pos();
                }
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