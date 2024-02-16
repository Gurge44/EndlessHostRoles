using Hazel;
using System.Collections.Generic;
using TOHE.Roles.Neutral;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor
{
    public static class Consort
    {
        private static readonly int Id = 642400;
        private static List<byte> playerIdList = [];

        private static OptionItem CD;
        private static OptionItem UseLimit;

        public static Dictionary<byte, int> BlockLimit;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Consort);
            CD = FloatOptionItem.Create(Id + 10, "EscortCD", new(2.5f, 60f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Consort])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 3, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Consort])
                .SetValueFormat(OptionFormat.Times);
        }

        public static void Init()
        {
            playerIdList = [];
            BlockLimit = [];
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            BlockLimit[playerId] = UseLimit.GetInt();
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static void SendRPC(byte playerId)
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetConsortLimit, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(BlockLimit[playerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;
            byte playerId = reader.ReadByte();
            int limit = reader.ReadInt32();
            BlockLimit[playerId] = limit;
        }
        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable || killer == null || target == null) return false;
            if (BlockLimit[killer.PlayerId] <= 0 || !killer.Is(CustomRoles.Consort)) return true;

            return killer.CheckDoubleTrigger(target, () =>
            {
                BlockLimit[killer.PlayerId]--;
                Glitch.hackedIdList.TryAdd(target.PlayerId, Utils.GetTimeStamp());
                killer.Notify(GetString("EscortTargetHacked"));
                killer.SetKillCooldown(CD.GetFloat());
                SendRPC(killer.PlayerId);
            });
        }
        public static string GetProgressText(byte id) => BlockLimit.TryGetValue(id, out var limit) ? $"<color=#777777>-</color> <color=#ffffff>{limit}</color>" : string.Empty;
    }
}
