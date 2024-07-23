using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Crewmate
{
    internal class Tornado : RoleBase
    {
        private static readonly List<byte> PlayerIdList = [];

        public static OptionItem TornadoCooldown;
        public static OptionItem TornadoDuration;
        private static OptionItem TornadoRange;

        private static readonly Dictionary<string, string> ReplacementDict = new() { { "Tornado", ColorString(GetRoleColor(CustomRoles.Tornado), "Tornado") } };

        private static RandomSpawn.SpawnMap Map;
        private static readonly Dictionary<(Vector2 LOCATION, string ROOM_NAME), long> Tornados = [];
        private static long LastNotify = TimeStamp;
        private static bool CanUseMap;
        private PlayerControl TornadoPC;
        private static int Id => 64420;

        public override bool IsEnable => PlayerIdList.Count > 0 || Randomizer.Exists;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tornado);
            TornadoCooldown = new IntegerOptionItem(Id + 2, "TornadoCooldown", new(1, 90, 1), 15, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
                .SetValueFormat(OptionFormat.Seconds);
            TornadoDuration = new IntegerOptionItem(Id + 3, "TornadoDuration", new(1, 90, 1), 25, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
                .SetValueFormat(OptionFormat.Seconds);
            TornadoRange = new FloatOptionItem(Id + 4, "TornadoRange", new(0.5f, 25f, 0.5f), 3f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
                .SetValueFormat(OptionFormat.Multiplier);

            TornadoCooldown.ReplacementDictionary = ReplacementDict;
            TornadoDuration.ReplacementDictionary = ReplacementDict;
            TornadoRange.ReplacementDictionary = ReplacementDict;
        }

        public override void Init()
        {
            PlayerIdList.Clear();
            Tornados.Clear();
            LastNotify = TimeStamp;

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
                    _ => throw new NotImplementedException()
                };
                CanUseMap = true;
            }
            catch (NotImplementedException)
            {
                Logger.CurrentMethod();
                Logger.Error("Unsupported Map", "Torando");
                CanUseMap = false;
            }
        }

        public override void Add(byte playerId)
        {
            TornadoPC = GetPlayerById(playerId);
            PlayerIdList.Add(playerId);
        }

        private static void SendRPCAddTornado(bool add, Vector2 pos, string roomname, long timestamp = 0)
        {
            if (!DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AddTornado, SendOption.Reliable);
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
                Tornados.TryAdd((new(x, y), roomname), timestamp);
            }
            else
            {
                Tornados.Remove((new(x, y), roomname));
            }
        }

        public override void OnPet(PlayerControl pc)
        {
            SpawnTornado(pc);
        }

        public static void SpawnTornado(PlayerControl pc)
        {
            if (pc == null) return;
            var info = pc.GetPositionInfo();
            var now = TimeStamp;
            Tornados.TryAdd(info, now);
            SendRPCAddTornado(true, info.Location, info.RoomName, now);
            _ = new TornadoObject(info.Location, [pc.PlayerId]);
        }

        public override void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!IsEnable || !GameStates.IsInTask || Tornados.Count == 0 || pc == null) return;

            var now = TimeStamp;

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
                        NotifyRoles(SpecifySeer: TornadoPC, SpecifyTarget: TornadoPC);
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

        public override string GetSuffix(PlayerControl s, PlayerControl t, bool h = false, bool m = false)
        {
            if (s.PlayerId != t.PlayerId || !IsEnable || (s.IsModClient() && !h) || s.PlayerId != TornadoPC.PlayerId) return string.Empty;
            return string.Join(h ? "\n" : ", ", Tornados.Select(x => $"Tornado {GetFormattedRoomName(x.Key.ROOM_NAME)} {GetFormattedVectorText(x.Key.LOCATION)} ({(int)(TornadoDuration.GetInt() - (TimeStamp - x.Value) + 1)}s)"));
        }
    }
}