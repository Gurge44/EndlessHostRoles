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
    public static class Escort
    {
        private static readonly int Id = 642300;
        private static List<byte> playerIdList = new();

        private static OptionItem CD;
        private static OptionItem UseLimit;

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
        }

        public static void Init()
        {
            playerIdList = new();
            BlockLimit = 0;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);

            BlockLimit = UseLimit.GetInt();

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static void SendRPC()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetEscortLimit, SendOption.Reliable, -1);
            writer.Write(BlockLimit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            BlockLimit = reader.ReadInt32();
        }
        public static void SetKillCooldown(byte playerId)
        {
            Main.AllPlayerKillCooldown[playerId] = CD.GetFloat();
        }
        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return;
            if (target == null) return;
            if (BlockLimit <= 0) return;
            if (!killer.Is(CustomRoles.Escort)) return;

            BlockLimit--;
            killer.SetKillCooldown();
            Glitch.hackedIdList.TryAdd(target.PlayerId, GetTimeStamp());
            killer.Notify(GetString("EscortTargetHacked"));
        }
        public static string GetProgressText() => $"<color=#777777>-</color> <color=#ffffff>{BlockLimit}</color>";
    }
}
