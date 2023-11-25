using Hazel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Roles.Impostor;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    public static class Druid
    {
        private static readonly int Id = 642800;
        private static List<byte> playerIdList = [];
        public static Dictionary<byte, float> UseLimit = [];

        public static OptionItem VentCooldown;
        private static OptionItem TriggerPlaceDelay;
        private static OptionItem UseLimitOpt;
        public static OptionItem DruidAbilityUseGainWithEachTaskCompleted;

        private static Dictionary<byte, long> TriggerDelays = [];
        private static Dictionary<byte, Dictionary<Vector2, string>> Triggers = [];
        private static long lastUpdate;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Druid);
            VentCooldown = IntegerOptionItem.Create(Id + 10, "VentCooldown", new(0, 60, 1), 15, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Seconds);
            TriggerPlaceDelay = IntegerOptionItem.Create(Id + 11, "DruidTriggerPlaceDelayOpt", new(1, 20, 1), 7, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Seconds);
            DruidAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
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
        public static void SendRPCRemoveTrigger(byte playerId, Vector2 position)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.DruidAddTriggerDelay, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(position.x);
            writer.Write(position.y);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCRemoveTrigger(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();

            Vector2 position = new(x, y);
            Triggers[playerId].Remove(position);
        }
        public static void SendRPCSyncLastUpdate()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.DruidAddTriggerDelay, SendOption.Reliable, -1);
            writer.Write(lastUpdate);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCSyncLastUpdate(MessageReader reader)
        {
            lastUpdate = long.Parse(reader.ReadString());
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

                var (LOCATION, ROOM_NAME) = GetTriggerInfo(pc);
                if (!Triggers.ContainsKey(pc.PlayerId)) Triggers.Add(pc.PlayerId, []);
                Triggers[pc.PlayerId].TryAdd(LOCATION, ROOM_NAME);
                SendRPCAddTrigger(pc.PlayerId, LOCATION, ROOM_NAME);
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
            lastUpdate = now;
            SendRPCSyncLastUpdate();

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
                        var (LOCATION, ROOM_NAME) = GetTriggerInfo(pc);
                        if (!Triggers.ContainsKey(id)) Triggers.Add(id, []);
                        Triggers[id].TryAdd(LOCATION, ROOM_NAME);
                        SendRPCAddTrigger(id, LOCATION, ROOM_NAME);
                        continue;
                    }

                    var timeLeft = TriggerPlaceDelay.GetInt() - (now - x.Value);
                    if (lastUpdate < now) pc.Notify(string.Format(GetString("DruidTimeLeft"), timeLeft));
                }
            }
            if (Triggers.Any())
            {
                foreach (var pc in Main.AllAlivePlayerControls.Where(pc => !playerIdList.Contains(pc.PlayerId)).ToArray()) // Check for all alive players except Druids - Worst case scenario it's 14 loops
                {
                    foreach (var triggers in Triggers.ToArray()) // Check for all Druids' traps - Most likely just 1 loop
                    {
                        foreach (var trigger in triggers.Value.ToArray()) // Check for all traps of the current Druid - Most likely 0-2 loops
                        {
                            if (Vector2.Distance(trigger.Key, pc.GetTruePosition()) <= 2f)
                            {
                                GetPlayerById(triggers.Key).Notify(string.Format(GetString("DruidTriggerTriggered"), GetFormattedRoomName(trigger.Value), GetFormattedVectorText(trigger.Key)));
                                Triggers[triggers.Key].Remove(trigger.Key);
                                SendRPCRemoveTrigger(triggers.Key, trigger.Key);
                            }
                        }
                    }
                }
            }
        }

        private static (Vector2 LOCATION, string ROOM_NAME) GetTriggerInfo(PlayerControl pc)
        {
            PlainShipRoom room = pc.GetPlainShipRoom();
            string roomName = room == null ? "Outside" : room.name;
            Vector2 pos = pc.GetTruePosition();

            return (pos, roomName);
        }
        private static string GetFormattedRoomName(string roomName) => roomName == "Outside" ? "<color=#00ffa5>Outside</color>" : $"In <color=#00ffa5>{roomName}</color>";
        private static string GetFormattedVectorText(Vector2 pos) => $"<color=#777777>(at {pos.ToString().Replace("(", string.Empty).Replace(")", string.Empty)})</color>";

        public static string GetSuffixText(byte playerId)
        {
            if (GetPlayerById(playerId) == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("\n<size=1.7>");

            sb.AppendLine($"<color=#00ffa5>{Triggers[playerId].Count}</color> triggers active");
            sb.Append(string.Join(", ", Triggers[playerId].Select(trigger => $"Trigger {GetFormattedRoomName(trigger.Value)} {GetFormattedVectorText(trigger.Key)}")));

            sb.Append("</size>");
            return sb.ToString();
        }

        public static string GetHUDText(PlayerControl pc)
        {
            if (pc == null) return string.Empty;

            var id = pc.PlayerId;
            var sb = new StringBuilder();

            sb.AppendLine(HudManagerPatch.GetCD_HUDText(Main.DruidCD, VentCooldown.GetInt(), id));

            sb.AppendLine($"<color=#00ffa5>{Triggers[id].Count}</color> triggers active");
            sb.Append(string.Join('\n', Triggers[id].Select(trigger => $"Trigger {GetFormattedRoomName(trigger.Value)} {GetFormattedVectorText(trigger.Key)}")));

            return sb.ToString();
        }
    }
}
