using Hazel;
using System.Collections.Generic;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    public static class Escort
    {
        private static readonly int Id = 642300;
        private static List<byte> playerIdList = [];

        private static OptionItem CD;
        private static OptionItem UseLimit;
        public static OptionItem UsePet;

        public static int BlockLimit;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Escort, 1);
            CD = FloatOptionItem.Create(Id + 10, "EscortCD", new(2.5f, 60f, 2.5f), 30f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Escort])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Escort])
                .SetValueFormat(OptionFormat.Times);
            UsePet = CreatePetUseSetting(Id + 12, CustomRoles.Escort);
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

            if (!AmongUsClient.Instance.AmHost || (UsePets.GetBool() && UsePet.GetBool())) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static void SendRPC()
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetEscortLimit, SendOption.Reliable, -1);
            writer.Write(BlockLimit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;
            BlockLimit = reader.ReadInt32();
        }
        public static void SetKillCooldown(byte playerId)
        {
            Main.AllPlayerKillCooldown[playerId] = BlockLimit > 0 ? CD.GetFloat() : 300f;
        }
        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable || killer == null || target == null || BlockLimit <= 0 || !killer.Is(CustomRoles.Escort)) return;

            BlockLimit--;
            killer.SetKillCooldown();
            Glitch.hackedIdList.TryAdd(target.PlayerId, GetTimeStamp());
            killer.Notify(GetString("EscortTargetHacked"));
        }
        public static string GetProgressText() => $"<color=#777777>-</color> <color=#ffffff>{BlockLimit}</color>";
    }
}
