using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate
{
    public static class DonutDelivery
    {
        private static readonly int Id = 642700;
        private static List<byte> playerIdList = [];

        private static OptionItem CD;
        private static OptionItem UseLimit;

        public static int DeliverLimit;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.DonutDelivery, 1);
            CD = FloatOptionItem.Create(Id + 10, "DonutDeliverCD", new(2.5f, 60f, 2.5f), 30f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(1, 20, 1), 5, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery])
                .SetValueFormat(OptionFormat.Times);
        }

        public static void Init()
        {
            playerIdList = [];
            DeliverLimit = 0;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);

            DeliverLimit = UseLimit.GetInt();

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static bool IsEnable => playerIdList.Any();
        public static void SendRPC()
        {
            if (!IsEnable) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDonutLimit, SendOption.Reliable, -1);
            writer.Write(DeliverLimit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;
            DeliverLimit = reader.ReadInt32();
        }
        public static void SetKillCooldown(byte playerId)
        {
            Main.AllPlayerKillCooldown[playerId] = DeliverLimit > 0 ? CD.GetFloat() : 300f;
        }
        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable || killer == null || target == null || DeliverLimit <= 0 || !killer.Is(CustomRoles.DonutDelivery)) return;

            DeliverLimit--;

            var num1 = IRandom.Instance.Next(0, 19);
            var num2 = IRandom.Instance.Next(0, 15);

            killer.Notify(GetString($"DonutDelivered-{num1}"));
            target.Notify(GetString($"DonutGot-{num2}"));

            killer.SetKillCooldown();
        }
        public static string GetProgressText() => $"<color=#777777>-</color> <color=#ffffff>{DeliverLimit}</color>";
    }
}
