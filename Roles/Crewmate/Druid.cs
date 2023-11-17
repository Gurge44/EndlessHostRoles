using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Utils;
using static TOHE.Translator;
using Hazel;

namespace TOHE.Roles.Crewmate
{
    public static class Druid
    {
        private static readonly int Id = 642800;
        private static List<byte> playerIdList = [];
        public static Dictionary<byte, float> UseLimit = [];

        private static OptionItem TriggerPlaceDelay;
        private static OptionItem UseLimitOpt;
        private static OptionItem DruidAbilityUseGainWithEachTaskCompleted;

        private static Dictionary<byte, long> TriggerDelays = [];
        private static Dictionary<byte, Dictionary<Vector2, string>> Triggers = [];
        private static long lastUpdate;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Druid);
            TriggerPlaceDelay = IntegerOptionItem.Create(Id + 10, "DruidTriggerPlaceDelayOpt", new(1, 20, 1), 7, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Seconds);
            DruidAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Init()
        {
            playerIdList = [];
            UseLimit = [];
            TriggerDelays = [];
            Triggers = [];
            lastUpdate = GetTimeStamp();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetFloat());
            lastUpdate = GetTimeStamp();
        }

        public static void SendRPCSyncAbilityUse(byte playerId)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDruidLimit, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(UseLimit[playerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCSyncAbilityUse(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            UseLimit[playerId] = reader.ReadSingle();
        }
        public static void SendRPCAddTriggerDelay(byte playerId, long timestamp)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.DruidAddTriggerDelay, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(timestamp);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCAddTriggerDelay(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            long timestamp = long.Parse(reader.ReadString());

            TriggerDelays.Remove(playerId);
            TriggerDelays.Add(playerId, timestamp);
        }
        public static void SendRPCAddTrigger(byte playerId, Vector2 position, string roomName)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.DruidAddTriggerDelay, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(roomName);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCAddTrigger(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            string roomName = reader.ReadString();

            Vector2 position = new(x, y);
            TriggerDelays.Remove(playerId);
            if (!Triggers.ContainsKey(playerId)) Triggers.Add(playerId, []);
            Triggers[playerId].TryAdd(position, roomName);
        }

        public static void OnEnterVent(PlayerControl pc, bool isPet = false)
        {
            if (pc == null || !GameStates.IsInTask) return;
            if (!pc.Is(CustomRoles.Druid) || UseLimit[pc.PlayerId] < 1) return;

            long now = GetTimeStamp();

            if (isPet)
            {
                if (Main.DruidCD.ContainsKey(pc.PlayerId)) return;
                Main.DruidCD.TryAdd(pc.PlayerId, now);

                var trigger = GetTriggerInfo(pc);
                if (!Triggers.ContainsKey(pc.PlayerId)) Triggers.Add(pc.PlayerId, []);
                Triggers[pc.PlayerId].TryAdd(trigger.Item1, trigger.Item2);
                SendRPCAddTrigger(pc.PlayerId, trigger.Item1, trigger.Item2);
            }
            else
            {
                TriggerDelays.TryAdd(pc.PlayerId, now);
                SendRPCAddTriggerDelay(pc.PlayerId, now);
            }

            UseLimit[pc.PlayerId]--;
            SendRPCSyncAbilityUse(pc.PlayerId);
        }

        public static void OnFixedUpdate()
        {
            if (!GameStates.IsInTask) return;
            if (!Triggers.Any() && !TriggerDelays.Any()) return;

            long now = GetTimeStamp();
            if (lastUpdate >= now) return;
            lastUpdate = now;

            if (TriggerDelays.Any())
            {
                foreach (var x in TriggerDelays.ToArray())
                {
                    var id = x.Key;
                    var pc = GetPlayerById(id);
                    if (pc == null) continue;

                    if (x.Value + TriggerPlaceDelay.GetInt() < now)
                    {
                        TriggerDelays.Remove(id);
                        var trigger = GetTriggerInfo(pc);
                        if (!Triggers.ContainsKey(id)) Triggers.Add(id, []);
                        Triggers[id].TryAdd(trigger.Item1, trigger.Item2);
                        SendRPCAddTrigger(id, trigger.Item1, trigger.Item2);
                        continue;
                    }

                    var timeLeft = TriggerPlaceDelay.GetInt() - (now - x.Value);
                    pc.Notify(string.Format(GetString("DruidTimeLeft"), timeLeft));

                }
            }
            if (Triggers.Any())
            {
                foreach (var pc in Main.AllAlivePlayerControls) // Check for all alive players
                {
                    foreach (var triggers in Triggers.ToArray()) // Check for all Druids' traps
                    {
                        foreach (var trigger in triggers.Value.ToArray()) // Check for all traps of the current Druid
                        {
                            if (Vector2.Distance(trigger.Key, pc.GetTruePosition()) <= 2f)
                            {
                                var druid = GetPlayerById(triggers.Key);
                                druid.Notify(string.Format(GetString("DruidTriggerTriggered"), trigger.Value));
                            }
                        }
                    }
                }
            }
        }

        private static (Vector2, string) GetTriggerInfo(PlayerControl pc)
        {
            PlainShipRoom room = pc.GetPlainShipRoom();
            string roomName = room == null ? "Outside" : room.name;
            Vector2 pos = pc.GetTruePosition();

            return (pos, roomName);
        }
    }
}
