using Hazel;
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

        private static readonly Dictionary<string, string> replacementDict = new() { { "Tornado", ColorString(GetRoleColor(CustomRoles.Tornado), "Tornado") } };

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

            TornadoCooldown.ReplacementDictionary = replacementDict;
            TornadoDuration.ReplacementDictionary = replacementDict;
            TornadoRange.ReplacementDictionary = replacementDict;
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
        private static void SendRPCAddTornado(Vector2 pos, string roomname, long timestamp)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AddTornado, SendOption.Reliable, -1);
            writer.Write(pos.x);
            writer.Write(pos.y);
            writer.Write(roomname);
            writer.Write(timestamp.ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        private static void SendRPCRemoveTornado(Vector2 pos, string roomname)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RemoveTornado, SendOption.Reliable, -1);
            writer.Write(pos.x);
            writer.Write(pos.y);
            writer.Write(roomname);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCAddTornado(MessageReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            string roomname = reader.ReadString();
            long timestamp = long.Parse(reader.ReadString());
            Tornados.Add((new(x, y), roomname), timestamp);
        }
        public static void ReceiveRPCRemoveTornado(MessageReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            string roomname = reader.ReadString();
            Tornados.Remove((new(x, y), roomname));
        }
        public static void SpawnTornado(PlayerControl pc)
        {
            if (pc == null) return;
            var info = pc.GetPositionInfo();
            var now = GetTimeStamp();
            Tornados.Add(info, now);
            SendRPCAddTornado(info.LOCATION, info.ROOM_NAME, now);
        }
        public static void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!IsEnable || !GameStates.IsInTask || Tornados.Count == 0) return;

            var Random = IRandom.Instance;
            var NotifyString = GetString("TeleportedByTornado");
            var now = GetTimeStamp();
            var tornadoRange = TornadoRange.GetFloat();
            var tornadoDuration = TornadoDuration.GetInt();

            foreach (var tornadoPc in playerIdList.Select(x => GetPlayerById(x)).Where(x => x.PlayerId != pc.PlayerId).ToArray())
            {
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
                        SendRPCRemoveTornado(tornado.Key.LOCATION, tornado.Key.ROOM_NAME);
                    }
                }

                if (tornadoPc == null || LastNotify >= now || !tornadoPc.Is(CustomRoles.Tornado) || tornadoPc.HasAbilityCD()) return;
                NotifyRoles(SpecifySeer: tornadoPc, SpecifyTarget: tornadoPc);
                LastNotify = now;
            }
        }
        public static string GetSuffixText(byte playerId, bool isHUD = false) => string.Join(isHUD ? "\n" : ", ", Tornados.Select(x => $"Tornado {GetFormattedRoomName(x.Key.ROOM_NAME)} {GetFormattedVectorText(x.Key.LOCATION)} ({(int)(TornadoDuration.GetInt() - (GetTimeStamp() - x.Value) + 1)}s)"));
    }
}
