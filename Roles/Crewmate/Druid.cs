using Hazel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    public class Druid : RoleBase
    {
        private const int Id = 642800;
        private static List<byte> playerIdList = [];

        public static OptionItem VentCooldown;
        private static OptionItem TriggerPlaceDelay;
        private static OptionItem UseLimitOpt;
        public static OptionItem DruidAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

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
            UseLimitOpt = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(0, 20, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Times);
            DruidAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 14, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            playerIdList = [];
            TriggerDelays = [];
            Triggers = [];
            lastUpdate = TimeStamp;
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
            lastUpdate = TimeStamp;
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetInt();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public static void SendRPCAddTrigger(bool add, byte playerId, Vector2 position, string roomName = "")
        {
            if (!DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.DruidAddTrigger, SendOption.Reliable);
            writer.Write(add);
            writer.Write(playerId);
            writer.Write(position.x);
            writer.Write(position.y);
            if (add) writer.Write(roomName);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPCAddTrigger(MessageReader reader)
        {
            bool add = reader.ReadBoolean();
            byte playerId = reader.ReadByte();

            if (Main.PlayerStates[playerId].Role is not Druid { IsEnable: true }) return;

            float x = reader.ReadSingle();
            float y = reader.ReadSingle();

            Vector2 position = new(x, y);

            if (add)
            {
                string roomName = reader.ReadString();

                TriggerDelays.Remove(playerId);
                if (!Triggers.ContainsKey(playerId)) Triggers.Add(playerId, []);
                Triggers[playerId].TryAdd(position, roomName);
            }
            else
            {
                Triggers[playerId].Remove(position);
            }
        }

        public override void OnPet(PlayerControl pc)
        {
            PlaceTrigger(pc, true);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            PlaceTrigger(pc);
        }

        void PlaceTrigger(PlayerControl pc, bool isPet = false)
        {
            if (!IsEnable) return;
            if (pc == null || !GameStates.IsInTask) return;
            if (pc.GetAbilityUseLimit() < 1) return;

            long now = TimeStamp;

            if (isPet)
            {
                (Vector2 LOCATION, string ROOM_NAME) = pc.GetPositionInfo();
                if (!Triggers.ContainsKey(pc.PlayerId)) Triggers.Add(pc.PlayerId, []);
                Triggers[pc.PlayerId].TryAdd(LOCATION, ROOM_NAME);
                SendRPCAddTrigger(true, pc.PlayerId, LOCATION, ROOM_NAME);
            }
            else
            {
                TriggerDelays.TryAdd(pc.PlayerId, now);
            }

            pc.RpcRemoveAbilityUse();
        }

        public override void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!GameStates.IsInTask || Triggers.Count <= 0 || playerIdList.Contains(pc.PlayerId)) return;

            foreach (var triggers in Triggers.ToArray()) // Check for all Druids' traps - Most likely just 1 loop
            {
                foreach (var trigger in triggers.Value.Where(trigger => Vector2.Distance(trigger.Key, pc.Pos()) <= 1.5f)) // Check for all traps of the current Druid - Most likely 0-3 loops
                {
                    GetPlayerById(triggers.Key).Notify(string.Format(GetString("DruidTriggerTriggered"), GetFormattedRoomName(trigger.Value), GetFormattedVectorText(trigger.Key)));
                    Triggers[triggers.Key].Remove(trigger.Key);
                    SendRPCAddTrigger(false, triggers.Key, trigger.Key);
                }
            }
        }

        public override void OnFixedUpdate(PlayerControl _)
        {
            if (!IsEnable || !GameStates.IsInTask || TriggerDelays.Count <= 0) return;

            long now = TimeStamp;

            foreach ((byte id, long ts) in TriggerDelays)
            {
                var pc = GetPlayerById(id);
                if (pc == null) continue;

                if (ts + TriggerPlaceDelay.GetInt() < now)
                {
                    TriggerDelays.Remove(id);
                    (Vector2 LOCATION, string ROOM_NAME) = pc.GetPositionInfo();
                    if (!Triggers.ContainsKey(id)) Triggers.Add(id, []);
                    Triggers[id].TryAdd(LOCATION, ROOM_NAME);
                    SendRPCAddTrigger(true, id, LOCATION, ROOM_NAME);
                    continue;
                }

                var timeLeft = TriggerPlaceDelay.GetInt() - (now - ts);
                if (lastUpdate < now) pc.Notify(string.Format(GetString("DruidTimeLeft"), timeLeft, 2f));
            }

            lastUpdate = now;
        }

        public static string GetSuffixText(byte playerId)
        {
            if (GetPlayerById(playerId) == null) return string.Empty;

            if (!Triggers.TryGetValue(playerId, out var triggers)) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("\n<size=1.7>");

            sb.AppendLine($"<#00ffa5>{triggers.Count}</color> trigger{(triggers.Count == 1 ? string.Empty : 's')} active");
            sb.Append(string.Join(", ", triggers.Select(trigger => $"Trigger {GetFormattedRoomName(trigger.Value)} {GetFormattedVectorText(trigger.Key)}")));

            sb.Append("</size>");
            return sb.ToString();
        }

        public static string GetHUDText(PlayerControl pc)
        {
            if (pc == null) return string.Empty;

            var id = pc.PlayerId;
            var sb = new StringBuilder();

            sb.AppendLine(GetCD_HUDText());

            if (!Triggers.TryGetValue(id, out var triggers)) return sb.ToString();

            string GetCD_HUDText() => !UsePets.GetBool() || !Main.AbilityCD.TryGetValue(id, out var CD)
                ? string.Empty
                : string.Format(GetString("CDPT"), CD.TOTALCD - (TimeStamp - CD.START_TIMESTAMP) + 1);

            sb.AppendLine($"<#00ffa5>{triggers.Count}</color> trigger{(triggers.Count == 1 ? string.Empty : 's')} active");
            sb.Append(string.Join('\n', triggers.Select(trigger => $"Trigger {GetFormattedRoomName(trigger.Value)} {GetFormattedVectorText(trigger.Key)}")));

            return sb.ToString();
        }
    }
}