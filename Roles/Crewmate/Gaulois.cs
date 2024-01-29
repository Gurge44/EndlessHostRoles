using Hazel;
using System.Collections.Generic;
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
        public static OptionItem UsePet;

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
            UsePet = CreatePetUseSetting(Id + 8, CustomRoles.Gaulois);
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

            if (!AmongUsClient.Instance.AmHost || (UsePets.GetBool() && UsePet.GetBool())) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public static bool IsEnable => playerIdList.Count > 0;

        public static void SendRPC(byte playerId)
        {
            if (!IsEnable || !Utils.DoRPC) return;
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

            killer.SetKillCooldown();
            SendRPC(killer.PlayerId);
        }

        public static string GetProgressText(byte playerId)
        {
            if (!IsEnable) return string.Empty;
            if (!UseLimit.TryGetValue(playerId, out var limit)) return string.Empty;
            return $" <color=#777777>-</color> <color=#ffffff>{limit}</color>";
        }
    }
}
