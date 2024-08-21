using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

/*
 * Earthquake: <size=150%><font="VCR SDF"><line-height=67%><#000000>█<#000000>█<#5e5e5e>█<#adadad>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><#5e5e5e>█<#000000>█<#5e5e5e>█<#adadad>█<#adadad>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><#5e5e5e>█<#000000>█<#000000>█<#5e5e5e>█<#5e5e5e>█<#adadad>█<#adadad>█<alpha=#00>█<br><#adadad>█<#5e5e5e>█<#000000>█<#000000>█<#000000>█<#5e5e5e>█<#5e5e5e>█<#adadad>█<br><alpha=#00>█<#adadad>█<#5e5e5e>█<#5e5e5e>█<#000000>█<#000000>█<#000000>█<#5e5e5e>█<br><alpha=#00>█<alpha=#00>█<#adadad>█<#adadad>█<#5e5e5e>█<#5e5e5e>█<#000000>█<#000000>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#adadad>█<#adadad>█<#5e5e5e>█<#000000>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#adadad>█<#5e5e5e>█<#000000>█<br></line-height></size>
 * Meteor: <size=150%><font="VCR SDF"><line-height=67%><alpha=#00>█<alpha=#00>█<alpha=#00>█<#fff700>█<#fff700>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<#fff700>█<#ffae00>█<#ffae00>█<#fff700>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<#fff700>█<#ffae00>█<#ff6f00>█<#ff6f00>█<#ffae00>█<#fff700>█<alpha=#00>█<br><#fff700>█<#ffae00>█<#ff6f00>█<#ff1100>█<#ff1100>█<#ff6f00>█<#ffae00>█<#fff700>█<br><#fff700>█<#ffae00>█<#ff6f00>█<#ff1100>█<#ff1100>█<#ff6f00>█<#ffae00>█<#fff700>█<br><alpha=#00>█<#fff700>█<#ffae00>█<#ff6f00>█<#ff6f00>█<#ffae00>█<#fff700>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<#fff700>█<#ffae00>█<#ffae00>█<#fff700>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<#fff700>█<#fff700>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br></line-height></size>
 * Volcano Eruption:
 * 1. <size=150%><font="VCR SDF"><line-height=67%><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<#ff6200>█<#ff6200>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<#ff6200>█<#ff6200>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br></line-height></size>
 * 2. <size=150%><font="VCR SDF"><line-height=67%><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<alpha=#00>█<br><alpha=#00>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<alpha=#00>█<br><alpha=#00>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<alpha=#00>█<br><alpha=#00>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br></line-height></size>
 * 3. <size=150%><font="VCR SDF"><line-height=67%><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br></line-height></size>
 * 4. <size=150%><font="VCR SDF"><line-height=67%><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br><#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<#ff6200>█<br></line-height></size>
 * Tornado: <size=150%><font="VCR SDF"><line-height=67%><alpha=#00>█<alpha=#00>█<#dbdbdb>█<#dbdbdb>█<#dbdbdb>█<#dbdbdb>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<#dbdbdb>█<#b0b0b0>█<#b0b0b0>█<#b0b0b0>█<#b0b0b0>█<#dbdbdb>█<alpha=#00>█<br><#dbdbdb>█<#b0b0b0>█<#b0b0b0>█<#828282>█<#828282>█<#b0b0b0>█<#b0b0b0>█<#dbdbdb>█<br><#dbdbdb>█<#b0b0b0>█<#828282>█<#474747>█<#474747>█<#828282>█<#b0b0b0>█<#dbdbdb>█<br><#dbdbdb>█<#b0b0b0>█<#828282>█<#474747>█<#474747>█<#828282>█<#b0b0b0>█<#dbdbdb>█<br><#dbdbdb>█<#b0b0b0>█<#b0b0b0>█<#828282>█<#828282>█<#b0b0b0>█<#b0b0b0>█<#dbdbdb>█<br><alpha=#00>█<#dbdbdb>█<#b0b0b0>█<#b0b0b0>█<#b0b0b0>█<#b0b0b0>█<#dbdbdb>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<#dbdbdb>█<#dbdbdb>█<#dbdbdb>█<#dbdbdb>█<alpha=#00>█<alpha=#00>█<br></line-height></size>
 * Thunderstorm strike: <size=150%><font="VCR SDF"><line-height=67%><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#c6c7c3>█<br><alpha=#00>█<#c6c7c3>█<alpha=#00>█<alpha=#00>█<#c6c7c3>█<alpha=#00>█<br><#c6c7c3>█<alpha=#00>█<#fffb00>█<#fffb00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<#fffb00>█<#fffb00>█<alpha=#00>█<#c6c7c3>█<br><#c6c7c3>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<#c6c7c3>█<#c6c7c3>█<alpha=#00>█<alpha=#00>█<br></line-height></size>
 * Sandstorm: <size=150%><font="VCR SDF"><line-height=67%><alpha=#00>█<#cf935f>█<#ffd3c2>█<alpha=#00>█<alpha=#00>█<#cf935f>█<#ffd3c2>█<alpha=#00>█<br><alpha=#00>█<#ffd3c2>█<alpha=#00>█<#cf935f>█<#ffd3c2>█<alpha=#00>█<alpha=#00>█<#cf935f>█<br><#cf935f>█<alpha=#00>█<#ffd3c2>█<#ffd3c2>█<alpha=#00>█<#cf935f>█<alpha=#00>█<alpha=#00>█<br><#ffd3c2>█<#ffd3c2>█<#cf935f>█<#ffd3c2>█<alpha=#00>█<alpha=#00>█<#ffd3c2>█<#cf935f>█<br><alpha=#00>█<alpha=#00>█<#ffd3c2>█<alpha=#00>█<#cf935f>█<#ffd3c2>█<alpha=#00>█<#ffd3c2>█<br><#ffd3c2>█<#cf935f>█<alpha=#00>█<#ffd3c2>█<alpha=#00>█<#ffd3c2>█<#cf935f>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<#ffd3c2>█<#cf935f>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#ffd3c2>█<br><#cf935f>█<#ffd3c2>█<alpha=#00>█<alpha=#00>█<#ffd3c2>█<#cf935f>█<alpha=#00>█<#cf935f>█<br></line-height></size>
 * Tsunami:
 * Left to right: <size=150%><font="VCR SDF"><line-height=67%><#0073ff>█<#0095ff>█<#00ccff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<br><#0073ff>█<#0095ff>█<#00ccff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<br><#0073ff>█<#0095ff>█<#00ccff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<br><#0073ff>█<#0095ff>█<#00ccff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<br><#0073ff>█<#0095ff>█<#00ccff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<br><#0073ff>█<#0095ff>█<#00ccff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<br><#0073ff>█<#0095ff>█<#00ccff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<br><#0073ff>█<#0095ff>█<#00ccff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<br></line-height></size>
 * Right to left: <size=150%><font="VCR SDF"><line-height=67%><#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#00ccff>█<#0095ff>█<#0073ff>█<br><#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#00ccff>█<#0095ff>█<#0073ff>█<br><#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#00ccff>█<#0095ff>█<#0073ff>█<br><#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#00ccff>█<#0095ff>█<#0073ff>█<br><#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#00ccff>█<#0095ff>█<#0073ff>█<br><#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#00ccff>█<#0095ff>█<#0073ff>█<br><#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#00ccff>█<#0095ff>█<#0073ff>█<br><#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#9ce8ff>█<#00ccff>█<#0095ff>█<#0073ff>█<br></line-height></size>
 * Top to bottom: <size=150%><font="VCR SDF"><line-height=67%><#003cff>█<#003cff>█<#003cff>█<#003cff>█<#003cff>█<#003cff>█<#003cff>█<#003cff>█<br><#006aff>█<#006aff>█<#006aff>█<#006aff>█<#006aff>█<#006aff>█<#006aff>█<#006aff>█<br><#00bbff>█<#00bbff>█<#00bbff>█<#00bbff>█<#00bbff>█<#00bbff>█<#00bbff>█<#00bbff>█<br><#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<br><#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<br><#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<br><#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<br><#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<#b8e1ff>█<br></line-height></size>
 * Bottom to top: <size=150%><font="VCR SDF"><line-height=67%><#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<br><#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<br><#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<br><#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<br><#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<#c2e2ff>█<br><#00bfff>█<#00bfff>█<#00bfff>█<#00bfff>█<#00bfff>█<#00bfff>█<#00bfff>█<#00bfff>█<br><#007bff>█<#007bff>█<#007bff>█<#007bff>█<#007bff>█<#007bff>█<#007bff>█<#007bff>█<br><#0033ff>█<#0033ff>█<#0033ff>█<#0033ff>█<#0033ff>█<#0033ff>█<#0033ff>█<#0033ff>█<br></line-height></size>
 * Sinkhole: <size=150%><font="VCR SDF"><line-height=67%><#7d7d7d>█<#7d7d7d>█<#545454>█<#7d7d7d>█<#7d7d7d>█<#7d7d7d>█<#7d7d7d>█<#545454>█<br><#545454>█<#424242>█<#424242>█<#424242>█<#424242>█<#424242>█<#424242>█<#7d7d7d>█<br><#7d7d7d>█<#424242>█<#000000>█<#000000>█<#000000>█<#000000>█<#424242>█<#7d7d7d>█<br><#545454>█<#424242>█<#000000>█<#000000>█<#000000>█<#000000>█<#424242>█<#7d7d7d>█<br><#7d7d7d>█<#424242>█<#000000>█<#000000>█<#000000>█<#000000>█<#424242>█<#545454>█<br><#545454>█<#424242>█<#000000>█<#000000>█<#000000>█<#000000>█<#424242>█<#7d7d7d>█<br><#7d7d7d>█<#424242>█<#424242>█<#424242>█<#424242>█<#424242>█<#424242>█<#7d7d7d>█<br><#545454>█<#545454>█<#7d7d7d>█<#7d7d7d>█<#545454>█<#7d7d7d>█<#7d7d7d>█<#545454>█<br></line-height></size>
 */

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

        private static ((float Left, float Right) X, (float Top, float Bottom) Y) MapBounds;
        private static long GameStartTimeStamp;

        private static OptionItem DisasterFrequency;
        private static OptionItem DisasterWarningTime;

        public static void SetupCustomOption()
        {
            const int id = 69_216_001;
            Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

            DisasterFrequency = new IntegerOptionItem(id, "ND_DisasterFrequency", new(1, 20, 1), 2, TabGroup.GameSettings)
                .SetHeader(true)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            DisasterWarningTime = new IntegerOptionItem(id + 1, "ND_DisasterWarningTime", new(1, 30, 1), 5, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.NaturalDisasters)
                .SetColor(color)
                .SetValueFormat(OptionFormat.Seconds);

            AllDisasters = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(Disaster)))
                .ToList();

            AllDisasters.ForEach(x => x.GetMethod("SetupOwnCustomOption")?.Invoke(null, null));
        }

        public static void OnGameStart()
        {
            SurvivalTimes.Clear();
            ActiveDisasters.Clear();
            PreparingDisasters.Clear();
            Sinkhole.Sinkholes.Clear();
            BuildingCollapse.CollapsedRooms.Clear();
            BuildingCollapse.LastPosition.Clear();

            if (Options.CurrentGameMode != CustomGameMode.NaturalDisasters) return;

            var rooms = Main.CurrentMap switch
            {
                MapNames.Skeld => new RandomSpawn.SkeldSpawnMap().positions.Values,
                MapNames.Mira => new RandomSpawn.MiraHQSpawnMap().positions.Values,
                MapNames.Polus => new RandomSpawn.PolusSpawnMap().positions.Values,
                MapNames.Dleks => new RandomSpawn.DleksSpawnMap().positions.Values,
                MapNames.Airship => new RandomSpawn.AirshipSpawnMap().positions.Values,
                MapNames.Fungle => new RandomSpawn.FungleSpawnMap().positions.Values,
                _ => null
            };
            if (rooms == null) return;

            var x = rooms.Select(r => r.x).ToArray();
            var y = rooms.Select(r => r.y).ToArray();

            const float extend = 2.5f;
            MapBounds = ((x.Min() - extend, x.Max() + extend), (y.Min() - extend, y.Max() + extend));

            GameStartTimeStamp = Utils.TimeStamp + 8;
        }

        public static void ApplyGameOptions(IGameOptions opt, byte id)
        {
            ActiveDisasters.ForEach(x => x.ApplyOwnGameOptions(opt, id));
        }

        public static string SuffixText()
        {
            var cb = string.Format(Translator.GetString("CollapsedBuildings"), BuildingCollapse.CollapsedBuildingsString);
            var ts = ActiveDisasters.Any(x => x is Thunderstorm) ? $"\n{Translator.GetString("OngoingThunderstorm")}" : string.Empty;
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
            "Earthquake" => "<size=150%><font=\"VCR SDF\"><line-height=67%><#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<#adadad>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#5e5e5e>\u2588<#000000>\u2588<#5e5e5e>\u2588<#adadad>\u2588<#adadad>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#adadad>\u2588<#adadad>\u2588<alpha=#00>\u2588<br><#adadad>\u2588<#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#adadad>\u2588<br><alpha=#00>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#5e5e5e>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#adadad>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#5e5e5e>\u2588<#000000>\u2588<#000000>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#adadad>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#000000>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#adadad>\u2588<#5e5e5e>\u2588<#000000>\u2588<br></line-height></size>",
            "Meteor" => "<size=150%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<br><#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff1100>\u2588<#ff1100>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<br><#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff1100>\u2588<#ff1100>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<br><alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ff6f00>\u2588<#ff6f00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#ffae00>\u2588<#ffae00>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#fff700>\u2588<#fff700>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
            "VolcanoEruption" => "<size=150%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
            "Tornado" => "<size=150%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#828282>\u2588<#828282>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#828282>\u2588<#474747>\u2588<#474747>\u2588<#828282>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#828282>\u2588<#474747>\u2588<#474747>\u2588<#828282>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#828282>\u2588<#828282>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<br><alpha=#00>\u2588<#dbdbdb>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#b0b0b0>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<#dbdbdb>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
            "Sandstorm" => "<size=150%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<#cf935f>\u2588<#ffd3c2>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#cf935f>\u2588<#ffd3c2>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ffd3c2>\u2588<alpha=#00>\u2588<#cf935f>\u2588<#ffd3c2>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#cf935f>\u2588<br><#cf935f>\u2588<alpha=#00>\u2588<#ffd3c2>\u2588<#ffd3c2>\u2588<alpha=#00>\u2588<#cf935f>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#ffd3c2>\u2588<#ffd3c2>\u2588<#cf935f>\u2588<#ffd3c2>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#ffd3c2>\u2588<#cf935f>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ffd3c2>\u2588<alpha=#00>\u2588<#cf935f>\u2588<#ffd3c2>\u2588<alpha=#00>\u2588<#ffd3c2>\u2588<br><#ffd3c2>\u2588<#cf935f>\u2588<alpha=#00>\u2588<#ffd3c2>\u2588<alpha=#00>\u2588<#ffd3c2>\u2588<#cf935f>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ffd3c2>\u2588<#cf935f>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#ffd3c2>\u2588<br><#cf935f>\u2588<#ffd3c2>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#ffd3c2>\u2588<#cf935f>\u2588<alpha=#00>\u2588<#cf935f>\u2588<br></line-height></size>",
            "Tsunami" => Tsunami.Sprites[Enum.GetValues<Tsunami.MovingDirection>().RandomElement()],
            "Sinkhole" => "<size=150%><font=\"VCR SDF\"><line-height=67%><#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<br><#545454>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#7d7d7d>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#545454>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#7d7d7d>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#545454>\u2588<br><#545454>\u2588<#424242>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#000000>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#7d7d7d>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#424242>\u2588<#7d7d7d>\u2588<br><#545454>\u2588<#545454>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<#7d7d7d>\u2588<#7d7d7d>\u2588<#545454>\u2588<br></line-height></size>",
            _ => string.Empty
        };

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        static class FixedUpdatePatch
        {
            private static long LastDisaster = Utils.TimeStamp;

            [SuppressMessage("ReSharper", "UnusedMember.Local")]
            public static void Postfix(PlayerControl __instance)
            {
                if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.NaturalDisasters || Main.HasJustStarted || !__instance.IsHost()) return;

                foreach (NaturalDisaster naturalDisaster in PreparingDisasters.ToArray())
                {
                    naturalDisaster.Update();
                    if (float.IsNaN(naturalDisaster.SpawnTimer))
                    {
                        var type = AllDisasters.Find(d => d.Name == naturalDisaster.DisasterName);
                        Activator.CreateInstance(type, naturalDisaster.Position, naturalDisaster);
                        PreparingDisasters.Remove(naturalDisaster);
                    }
                }

                ActiveDisasters.ForEach(x => x.Update());
                Sinkhole.OnFixedUpdate();
                BuildingCollapse.OnFixedUpdate();

                if (Utils.TimeStamp - LastDisaster >= DisasterFrequency.GetInt())
                {
                    LastDisaster = Utils.TimeStamp;
                    var disaster = AllDisasters.RandomElement();
                    var position = IRandom.Instance.Next(2) == 0
                        ? Main.AllAlivePlayerControls.RandomElement().Pos()
                        : new(Random.Range(MapBounds.X.Left, MapBounds.X.Right), Random.Range(MapBounds.Y.Top, MapBounds.Y.Bottom));
                    PreparingDisasters.Add(new(position, DisasterWarningTime.GetFloat(), Sprite(disaster.Name), disaster.Name));
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

            private long StartTimeStamp { get; } = Utils.TimeStamp;
            protected Vector2 Position { get; set; }
            protected NaturalDisaster NetObject { get; init; } = null;
            protected virtual int Duration { get; set; } = 0;

            protected virtual bool RemoveIfExpired()
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

            public Earthquake(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
            {
                NetObject = naturalDisaster;
                Update();
            }

            protected override int Duration { get; set; } = DurationOpt.GetInt();

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
                    if (Vector2.Distance(pc.Pos(), this.Position) <= Range)
                    {
                        float speed = Speed.GetFloat();
                        if (Mathf.Approximately(Main.AllPlayerSpeed[pc.PlayerId], speed)) continue;
                        Main.AllPlayerSpeed[pc.PlayerId] = speed;
                        pc.MarkDirtySettings();
                    }
                    else
                    {
                        float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                        if (Mathf.Approximately(Main.AllPlayerSpeed[pc.PlayerId], speed)) continue;
                        Main.AllPlayerSpeed[pc.PlayerId] = speed;
                        pc.MarkDirtySettings();
                    }
                }
            }

            protected override bool RemoveIfExpired()
            {
                if (base.RemoveIfExpired())
                {
                    foreach (var pc in Main.AllAlivePlayerControls)
                    {
                        float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                        if (Mathf.Approximately(Main.AllPlayerSpeed[pc.PlayerId], speed)) continue;
                        Main.AllPlayerSpeed[pc.PlayerId] = speed;
                        pc.MarkDirtySettings();
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

            protected override int Duration { get; set; } = 5;

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

            protected override int Duration { get; set; } = (int)((Phases - 1) * FlowStepDelay.GetFloat()) + DurationAfterFlowComplete.GetInt();

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
                        2 => "<size=150%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>",
                        3 => "<size=150%><font=\"VCR SDF\"><line-height=67%><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br></line-height></size>",
                        4 => "<size=150%><font=\"VCR SDF\"><line-height=67%><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br><#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<#ff6200>\u2588<br></line-height></size>",
                        _ => string.Empty
                    };

                    this.NetObject.RpcChangeSprite(newSprite);
                }

                var range = Range - ((Phases - Phase) * 0.4f);
                KillNearbyPlayers(PlayerState.DeathReason.Lava, range);
            }
        }

        sealed class Tornado : Disaster
        {
            private static OptionItem DurationOpt;
            private float Angle = Random.Range(0, 2 * Mathf.PI);
            private int Count = 2;

            private long LastAngleChange = Utils.TimeStamp;

            public Tornado(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
            {
                NetObject = naturalDisaster;
                Update();
            }

            protected override int Duration { get; set; } = DurationOpt.GetInt();

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
            }

            public override void Update()
            {
                if (RemoveIfExpired()) return;

                const float eyeRange = Range / 3f;
                const float dragRange = Range * 1.5f;

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
                    angle = Random.Range(0, 2 * Mathf.PI);
                    LastAngleChange = now;
                    Angle = angle;
                }
                else angle = Angle;

                Vector2 newPos = this.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.1f;

                this.Position = newPos;
                this.NetObject.TP(this.Position);
            }
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
            }

            protected override int Duration { get; set; } = DurationOpt.GetInt();

            [SuppressMessage("ReSharper", "UnusedMember.Local")]
            public static void SetupOwnCustomOption()
            {
                const int id = 69_216_400;
                Color color = Utils.GetRoleColor(CustomRoles.NDPlayer);

                DurationOpt = new IntegerOptionItem(id, "ND_Thunderstorm.DurationOpt", new(1, 20, 1), 20, TabGroup.GameSettings)
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

            protected override int Duration { get; set; } = DurationOpt.GetInt();

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

            protected override bool RemoveIfExpired()
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
            public enum MovingDirection
            {
                LeftToRight,
                RightToLeft,
                TopToBottom,
                BottomToTop
            }

            private static OptionItem MovingSpeed;

            public static readonly Dictionary<MovingDirection, string> Sprites = new()
            {
                [MovingDirection.LeftToRight] = "<size=150%><font=\"VCR SDF\"><line-height=67%><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br><#0073ff>\u2588<#0095ff>\u2588<#00ccff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<br></line-height></size>",
                [MovingDirection.RightToLeft] = "<size=150%><font=\"VCR SDF\"><line-height=67%><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br><#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#9ce8ff>\u2588<#00ccff>\u2588<#0095ff>\u2588<#0073ff>\u2588<br></line-height></size>",
                [MovingDirection.TopToBottom] = "<size=150%><font=\"VCR SDF\"><line-height=67%><#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<#003cff>\u2588<br><#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<#006aff>\u2588<br><#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<#00bbff>\u2588<br><#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<br><#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<br><#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<br><#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<br><#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<#b8e1ff>\u2588<br></line-height></size>",
                [MovingDirection.BottomToTop] = "<size=150%><font=\"VCR SDF\"><line-height=67%><#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<br><#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<br><#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<br><#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<br><#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<#c2e2ff>\u2588<br><#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<#00bfff>\u2588<br><#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<#007bff>\u2588<br><#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<#0033ff>\u2588<br></line-height></size>"
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
        }

        sealed class Sinkhole : Disaster
        {
            public static readonly List<Vector2> Sinkholes = [];

            public Sinkhole(Vector2 position, NaturalDisaster naturalDisaster) : base(position)
            {
                NetObject = naturalDisaster;
                Update();

                KillNearbyPlayers(PlayerState.DeathReason.Sunken);

                Sinkholes.Add(position);
            }

            protected override int Duration { get; set; } = int.MaxValue;

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
                        if (Vector2.Distance(pos, Sinkholes[i]) <= Range)
                        {
                            pc.Suicide(PlayerState.DeathReason.Sunken);
                        }
                    }
                }
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

                var room = naturalDisaster.playerControl.GetPlainShipRoom();
                if (room == default(PlainShipRoom)) return;

                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    if (pc.GetPlainShipRoom() == room)
                    {
                        pc.Suicide(PlayerState.DeathReason.Collapsed);
                    }
                }

                CollapsedRooms.Add(room);
            }

            public static string CollapsedBuildingsString => CollapsedRooms.Count > 0
                ? CollapsedRooms.Join(x => Translator.GetString($"{x.RoomId}"))
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
                    if (room != default(PlainShipRoom) && CollapsedRooms.Any(x => x == room))
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
 * Sandstorm: Sand blows, making it hard to see.
 * Tsunami: A giant wave comes moving in a specific direction, drowning players that are on the way.
 * Sinkhole: The ground collapses, killing players in range and any players that enter this range in the future.
 * Building Collapse: A building on the ship collapses, killing everyone inside it. Collapsed rooms cannot be entered in the future.
 */