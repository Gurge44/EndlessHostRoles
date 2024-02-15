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
        private static bool CanUseMap = true;

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

            try
            {
                Map = Main.CurrentMap switch
                {
                    MapNames.Skeld => new RandomSpawn.SkeldSpawnMap(),
                    MapNames.Mira => new RandomSpawn.MiraHQSpawnMap(),
                    MapNames.Polus => new RandomSpawn.PolusSpawnMap(),
                    MapNames.Dleks => new RandomSpawn.DleksSpawnMap(),
                    MapNames.Airship => new RandomSpawn.AirshipSpawnMap(),
                    MapNames.Fungle => new RandomSpawn.FungleSpawnMap(),
                    _ => throw new NotImplementedException(),
                };
                CanUseMap = true;
            }
            catch (NotImplementedException)
            {
                Logger.CurrentMethod(lineNumber: 60);
                Logger.Error("Unsupported Map", "Torando");
                CanUseMap = false;
            }
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }
        public static bool IsEnable => playerIdList.Count > 0 || Randomizer.IsEnable;
        private static void SendRPCAddTornado(bool add, Vector2 pos, string roomname, long timestamp = 0)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AddTornado, SendOption.Reliable, -1);
            writer.Write(add);
            writer.Write(pos.x);
            writer.Write(pos.y);
            writer.Write(roomname);
            if (add) writer.Write(timestamp.ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCAddTornado(MessageReader reader)
        {
            bool add = reader.ReadBoolean();
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            string roomname = reader.ReadString();

            if (add)
            {
                long timestamp = long.Parse(reader.ReadString());
                Tornados.Add((new(x, y), roomname), timestamp);
            }
            else
            {
                Tornados.Remove((new(x, y), roomname));
            }
        }
        public static void SpawnTornado(PlayerControl pc)
        {
            if (pc == null) return;
            var info = pc.GetPositionInfo();
            var now = GetTimeStamp();
            Tornados.Add(info, now);
            SendRPCAddTornado(true, info.LOCATION, info.ROOM_NAME, now);
        }
        public static void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!IsEnable || !GameStates.IsInTask || Tornados.Count == 0 || pc == null) return;

            var now = GetTimeStamp();

            if (!pc.Is(CustomRoles.Tornado))
            {
                var Random = IRandom.Instance;
                var NotifyString = GetString("TeleportedByTornado");
                var tornadoRange = TornadoRange.GetFloat();
                var tornadoDuration = TornadoDuration.GetInt();

                foreach (var tornado in Tornados)
                {
                    if (Vector2.Distance(tornado.Key.LOCATION, pc.Pos()) <= tornadoRange)
                    {
                        if (!CanUseMap || Random.Next(0, 100) < 50)
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
                        SendRPCAddTornado(false, tornado.Key.LOCATION, tornado.Key.ROOM_NAME);
                    }
                }
            }
            else
            {
                if (LastNotify >= now || pc.HasAbilityCD()) return;
                NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                LastNotify = now;
            }
        }
        public static string GetSuffixText(bool isHUD = false) => string.Join(isHUD ? "\n" : ", ", Tornados.Select(x => $"Tornado {GetFormattedRoomName(x.Key.ROOM_NAME)} {GetFormattedVectorText(x.Key.LOCATION)} ({(int)(TornadoDuration.GetInt() - (GetTimeStamp() - x.Value) + 1)}s)"));
    }
}
