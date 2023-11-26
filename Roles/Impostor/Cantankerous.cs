using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    public static class Cantankerous
    {
        private static readonly int Id = 642860;
        private static List<byte> playerIdList = [];
        private static Dictionary<byte, int> Points = [];

        private static OptionItem PointsGainedPerEjection;
        private static OptionItem StartingPoints;
        private static OptionItem KCD;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Cantankerous);
            KCD = FloatOptionItem.Create(Id + 5, "KillCooldown", new(0f, 60f, 2.5f), 22.5f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
                .SetValueFormat(OptionFormat.Seconds);
            PointsGainedPerEjection = IntegerOptionItem.Create(Id + 6, "CantankerousPointsGainedPerEjection", new(1, 5, 1), 2, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
                .SetValueFormat(OptionFormat.Times);
            StartingPoints = IntegerOptionItem.Create(Id + 7, "CantankerousStartingPoints", new(0, 5, 1), 1, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
                .SetValueFormat(OptionFormat.Times);
        }

        public static void Init()
        {
            playerIdList = [];
            Points = [];
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            Points.Add(playerId, StartingPoints.GetInt());
        }

        public static bool IsEnable => playerIdList.Any();

        public static bool CanUseKillButton(byte playerId) => Points.TryGetValue(playerId, out var point) && point > 0;

        private static void SendRPC(byte playerId, bool isPlus)
        {
            if (!IsEnable) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncCantankerousLimit, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(isPlus);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;
            byte playerId = reader.ReadByte();
            bool isPlus = reader.ReadBoolean();

            if (isPlus) Points[playerId] += PointsGainedPerEjection.GetInt();
            else Points[playerId]--;
        }

        public static void OnCrewmateEjected()
        {
            if (!IsEnable) return;
            var value = PointsGainedPerEjection.GetInt();
            foreach (var x in Points.Keys.ToArray())
            {
                Points[x] += value;
                SendRPC(x, true);
            }
        }

        public static bool OnCheckMurder(PlayerControl killer)
        {
            if (!IsEnable) return false;
            if (killer == null) return false;

            if (Points[killer.PlayerId] <= 0) return false;

            Points[killer.PlayerId]--;
            SendRPC(killer.PlayerId, false);

            return true;
        }
    }
}
