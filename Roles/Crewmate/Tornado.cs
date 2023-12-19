using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    internal class Tornado
    {
        private static int Id => 64420;
        private static readonly List<byte> playerIdList = [];

        public static OptionItem TornadoCooldown;
        private static OptionItem TornadoDuration;
        private static OptionItem TornadoRange;

        private static RandomSpawn.SpawnMap Map;
        private static readonly Dictionary<(Vector2 LOCATION, string ROOM_NAME), long> Tornados = [];
        private static long LastNotify = GetTimeStamp();

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tornado);
            TornadoCooldown = IntegerOptionItem.Create(Id + 2, "TornadoCooldown", new(1, 90, 1), 15, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
                .SetValueFormat(OptionFormat.Seconds);
            TornadoDuration = IntegerOptionItem.Create(Id + 3, "TornadoDuration", new(1, 90, 1), 25, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
                .SetValueFormat(OptionFormat.Seconds);
            TornadoRange = FloatOptionItem.Create(Id + 4, "TornadoRange", new(0.5f, 25f, 0.5f), 3f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
                .SetValueFormat(OptionFormat.Multiplier);
        }
        public static void Init()
        {
            playerIdList.Clear();
            Tornados.Clear();
            LastNotify = GetTimeStamp();

            Map = (MapNames)Main.NormalOptions.MapId switch
            {
                MapNames.Skeld => new RandomSpawn.SkeldSpawnMap(),
                MapNames.Mira => new RandomSpawn.MiraHQSpawnMap(),
                MapNames.Polus => new RandomSpawn.PolusSpawnMap(),
                MapNames.Airship => new RandomSpawn.AirshipSpawnMap(),
                MapNames.Fungle => new RandomSpawn.FungleSpawnMap(),
                _ => throw new NotImplementedException(),
            };
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static void SpawnTornado(PlayerControl pc)
        {
            if (pc == null) return;
            Tornados.Add(pc.GetPositionInfo(), GetTimeStamp());
        }
        public static void OnFixedUpdate(PlayerControl tornadoPc)
        {
            if (!IsEnable || !GameStates.IsInTask || !Tornados.Any()) return;

            var Random = IRandom.Instance;
            var NotifyString = GetString("TeleportedByTornado");
            var now = GetTimeStamp();
            var tornadoRange = TornadoRange.GetFloat();
            var tornadoDuration = TornadoDuration.GetInt();

            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (playerIdList.Contains(pc.PlayerId)) continue;

                foreach (var tornado in Tornados)
                {
                    if (Vector2.Distance(tornado.Key.LOCATION, pc.Pos()) <= tornadoRange)
                    {
                        if (Random.Next(0, 100) < 50)
                        {
                            pc.TPtoRndVent();
                        }
                        else
                        {
                            Map.RandomTeleport(pc);
                        }
                        pc.Notify(NotifyString);
                    }

                    if (tornado.Value + tornadoDuration < now)
                    {
                        Tornados.Remove(tornado.Key);
                    }
                }
            }

            if (tornadoPc == null || LastNotify >= now || !tornadoPc.Is(CustomRoles.Tornado) || tornadoPc.HasAbilityCD()) return;
            NotifyRoles(SpecifySeer: tornadoPc, SpecifyTarget: tornadoPc);
            LastNotify = now;
        }
        public static string GetSuffixText(byte playerId, bool isHUD = false) => string.Join(isHUD ? "\n" : ", ", Tornados.Select(x => $"Tornado {GetFormattedRoomName(x.Key.ROOM_NAME)} {GetFormattedVectorText(x.Key.LOCATION)} ({(int)(TornadoDuration.GetInt() - (GetTimeStamp() - x.Value))}s)"));
    }
}
