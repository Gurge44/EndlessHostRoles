using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Crewmate
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

        private PlayerControl DruidPC;
        private long lastUpdate;
        private long TriggerDelay;
        private Dictionary<Vector2, int> TriggerIds = [];
        private Dictionary<Vector2, string> Triggers = [];

        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Druid);
            VentCooldown = new IntegerOptionItem(Id + 10, "VentCooldown", new(0, 60, 1), 15, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Seconds);
            TriggerPlaceDelay = new IntegerOptionItem(Id + 11, "DruidTriggerPlaceDelayOpt", new(1, 20, 1), 7, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = new IntegerOptionItem(Id + 12, "AbilityUseLimit", new(0, 20, 1), 3, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Times);
            DruidAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 14, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Druid])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            playerIdList = [];
            TriggerDelay = 0;
            Triggers = [];
            lastUpdate = TimeStamp;
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            DruidPC = GetPlayerById(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
            lastUpdate = TimeStamp;
            TriggerIds = [];
        }

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

            if (Main.PlayerStates[playerId].Role is not Druid { IsEnable: true } di) return;

            float x = reader.ReadSingle();
            float y = reader.ReadSingle();

            Vector2 position = new(x, y);

            if (add)
            {
                string roomName = reader.ReadString();

                di.TriggerDelay = 0;
                di.Triggers.TryAdd(position, roomName);
            }
            else
            {
                di.Triggers.Remove(position);
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
                (Vector2 location, string roomName) = pc.GetPositionInfo();
                Triggers.TryAdd(location, roomName);
                SendRPCAddTrigger(true, pc.PlayerId, location, roomName);
                _ = new PlayerDetector(location, [pc.PlayerId], out int id);
                TriggerIds.TryAdd(location, id);
            }
            else
            {
                TriggerDelay = now;
            }

            pc.RpcRemoveAbilityUse();
        }

        public override void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!GameStates.IsInTask || Triggers.Count <= 0 || playerIdList.Contains(pc.PlayerId)) return;

            foreach (var trigger in Triggers.Where(trigger => Vector2.Distance(trigger.Key, pc.Pos()) <= 1.5f))
            {
                DruidPC.Notify(string.Format(GetString("DruidTriggerTriggered"), GetFormattedRoomName(trigger.Value), GetFormattedVectorText(trigger.Key)));
                Triggers.Remove(trigger.Key);
                SendRPCAddTrigger(false, DruidPC.PlayerId, trigger.Key);
                CustomNetObject.Get(TriggerIds[trigger.Key])?.Despawn();
            }
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!IsEnable || !GameStates.IsInTask || TriggerDelay == 0 || pc == null) return;

            long now = TimeStamp;

            if (TriggerDelay + TriggerPlaceDelay.GetInt() < now)
            {
                var id = pc.PlayerId;
                TriggerDelay = 0;
                (Vector2 location, string roomName) = pc.GetPositionInfo();
                Triggers.TryAdd(location, roomName);
                SendRPCAddTrigger(true, id, location, roomName);
                _ = new PlayerDetector(location, [pc.PlayerId], out int oid);
                TriggerIds.TryAdd(location, oid);
                return;
            }

            var timeLeft = TriggerPlaceDelay.GetInt() - (now - TriggerDelay);
            if (lastUpdate < now) pc.Notify(string.Format(GetString("DruidTimeLeft"), timeLeft, 2f));

            lastUpdate = now;
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (hud) return GetHUDText(seer);
            if (seer == null || seer.IsModClient() || seer.PlayerId != target.PlayerId || seer.PlayerId != DruidPC.PlayerId) return string.Empty;

            if (Triggers.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("\n<size=1.7>");

            sb.AppendLine($"<#00ffa5>{Triggers.Count}</color> trigger{(Triggers.Count == 1 ? string.Empty : 's')} active");
            sb.Append(string.Join(", ", Triggers.Select(trigger => $"Trigger {GetFormattedRoomName(trigger.Value)} {GetFormattedVectorText(trigger.Key)}")));

            sb.Append("</size>");
            return sb.ToString();
        }

        string GetHUDText(PlayerControl pc)
        {
            if (pc == null || Triggers.Count == 0) return string.Empty;

            var sb = new StringBuilder();

            sb.AppendLine($"<#00ffa5>{Triggers.Count}</color> trigger{(Triggers.Count == 1 ? string.Empty : 's')} active");
            sb.Append(string.Join('\n', Triggers.Select(trigger => $"Trigger {GetFormattedRoomName(trigger.Value)} {GetFormattedVectorText(trigger.Key)}")));

            return sb.ToString();
        }
    }
}