using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Utils;
using static TOHE.Translator;
using Hazel;

namespace TOHE.Roles.Crewmate
{
    public static class DonutDelivery
    {
        private static readonly int Id = 642400;
        private static List<byte> playerIdList = new();

        private static OptionItem CD;
        private static OptionItem UseLimit;

        public static int DeliverLimit;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.DonutDelivery, 1);
            CD = FloatOptionItem.Create(Id + 10, "DonutDeliverCD", new(2.5f, 60f, 2.5f), 30f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(1, 20, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery])
                .SetValueFormat(OptionFormat.Times);
        }

        public static void Init()
        {
            playerIdList = new();
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
        public static void SendRPC()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDonutLimit, SendOption.Reliable, -1);
            writer.Write(DeliverLimit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            DeliverLimit = reader.ReadInt32();
        }
        public static void SetKillCooldown(byte playerId)
        {
            Main.AllPlayerKillCooldown[playerId] = CD.GetFloat();
        }
        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return;
            if (target == null) return;
            if (DeliverLimit <= 0) return;
            if (!killer.Is(CustomRoles.DonutDelivery)) return;

            DeliverLimit--;
            Glitch.hackedIdList.TryAdd(target.PlayerId, GetTimeStamp());
            var num1 = IRandom.Instance.Next(0, 19);
            var num2 = IRandom.Instance.Next(0, 15);
            killer.Notify(GetString($"DonutDelivered-{num1}"));
            target.Notify(GetString($"DonutGot-{num2}"));
        }
        public static string GetProgressText() => $"<color=#777777>-</color> <color=#ffffff>{DeliverLimit}</color>";
    }
}
