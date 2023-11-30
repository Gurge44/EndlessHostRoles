using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    public static class Gaulois
    {
        private static readonly int Id = 643070;
        private static List<byte> playerIdList = [];
        public static Dictionary<byte, int> UseLimit = [];

        private static OptionItem CD;
        private static OptionItem AdditionalSpeed;
        private static OptionItem UseLimitOpt;

        public static List<byte> IncreasedSpeedPlayerList = [];

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Gaulois);
            CD = FloatOptionItem.Create(Id + 5, "AbilityCooldown", new(0f, 60f, 2.5f), 30f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Gaulois])
                .SetValueFormat(OptionFormat.Seconds);
            AdditionalSpeed = FloatOptionItem.Create(Id + 6, "GauloisSpeedBoost", new(0f, 2f, 0.05f), 0.5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Gaulois])
                .SetValueFormat(OptionFormat.Multiplier);
            UseLimitOpt = IntegerOptionItem.Create(Id + 7, "AbilityUseLimit", new(1, 14, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Gaulois])
                .SetValueFormat(OptionFormat.Times);
        }

        public static void Init()
        {
            playerIdList = [];
            UseLimit = [];
            IncreasedSpeedPlayerList = [];
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
        }

        public static bool IsEnable => playerIdList.Count > 0;

        public static void SendRPC(byte playerId)
        {
            if (!IsEnable) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetGauloisLimit, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(UseLimit[playerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;
            byte playerId = reader.ReadByte();
            int useLimit = reader.ReadInt32();
            UseLimit[playerId] = useLimit;
        }

        private static void SendRPCAddPlayerToList(byte playerId)
        {
            if (!IsEnable) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.GaulousAddPlayerToList, SendOption.Reliable, -1);
            writer.Write(playerId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPCAddPlayerToList(MessageReader reader)
        {
            if (!IsEnable) return;
            byte playerId = reader.ReadByte();
            IncreasedSpeedPlayerList.Add(playerId);
        }

        public static void SetKillCooldown(byte playerId)
        {
            if (!IsEnable) return;
            Main.AllPlayerKillCooldown[playerId] = UseLimit[playerId] > 0 ? CD.GetFloat() : 300f;
        }

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable || killer == null || target == null || UseLimit[killer.PlayerId] <= 0) return;

            Main.AllPlayerSpeed[target.PlayerId] += AdditionalSpeed.GetFloat();
            IncreasedSpeedPlayerList.Add(target.PlayerId);
            UseLimit[killer.PlayerId]--;

            SendRPC(killer.PlayerId);
            SendRPCAddPlayerToList(target.PlayerId);
        }

        public static string GetProgressText(byte playerId)
        {
            if (!IsEnable) return string.Empty;
            if (!UseLimit.TryGetValue(playerId, out var limit)) return string.Empty;
            return $" <color=#777777>-</color> <color=#ffffff>{limit}</color>";
        }
    }
}
