using Hazel;
using System.Collections.Generic;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class Consort
    {
        private static readonly int Id = 642400;
        private static List<byte> playerIdList = [];

        private static OptionItem CD;
        private static OptionItem UseLimit;

        public static int BlockLimit;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Consort);
            CD = FloatOptionItem.Create(Id + 10, "EscortCD", new(2.5f, 60f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Consort])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 3, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Consort])
                .SetValueFormat(OptionFormat.Times);
        }

        public static void Init()
        {
            playerIdList = [];
            BlockLimit = 0;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            BlockLimit = UseLimit.GetInt();
        }
        public static void SendRPC()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetConsortLimit, SendOption.Reliable, -1);
            writer.Write(BlockLimit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            BlockLimit = reader.ReadInt32();
        }
        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;
            if (BlockLimit <= 0) return true;
            if (!killer.Is(CustomRoles.Consort)) return false;

            return killer.CheckDoubleTrigger(target, () =>
            {
                BlockLimit--;
                Glitch.hackedIdList.TryAdd(target.PlayerId, GetTimeStamp());
                killer.Notify(GetString("EscortTargetHacked"));
                killer.SetKillCooldown(CD.GetFloat());
                SendRPC();
            });
        }
        public static string GetProgressText() => $"<color=#777777>-</color> <color=#ffffff>{BlockLimit}</color>";
    }
}
