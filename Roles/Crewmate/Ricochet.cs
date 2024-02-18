namespace TOHE.Roles.Crewmate
{
    using Hazel;
    using System.Collections.Generic;
    using System.Text;
    using static TOHE.Options;

    public static class Ricochet
    {
        private static readonly int Id = 640100;
        public static List<byte> playerIdList = [];
        public static byte ProtectAgainst = byte.MaxValue;

        public static OptionItem UseLimitOpt;
        public static OptionItem RicochetAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        public static OptionItem CancelVote;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Ricochet, 1);
            UseLimitOpt = IntegerOptionItem.Create(Id + 10, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ricochet])
                .SetValueFormat(OptionFormat.Times);
            RicochetAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 11, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ricochet])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ricochet])
                .SetValueFormat(OptionFormat.Times);
            CancelVote = CreateVoteCancellingUseSetting(Id + 12, CustomRoles.Ricochet, TabGroup.CrewmateRoles);
        }
        public static void Init()
        {
            playerIdList = [];
            ProtectAgainst = byte.MaxValue;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static void SendRPCSyncTarget(byte targetId)
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRicochetTarget, SendOption.Reliable);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCSyncTarget(MessageReader reader)
        {
            if (AmongUsClient.Instance.AmHost) return;

            ProtectAgainst = reader.ReadByte();
        }
        public static bool OnKillAttempt(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;
            if (!target.Is(CustomRoles.Ricochet)) return true;

            if (ProtectAgainst == killer.PlayerId)
            {
                killer.SetKillCooldown(time: 5f);
                return false;
            }

            return true;
        }
        public static bool OnVote(PlayerControl pc, PlayerControl target)
        {
            if (target == null || pc == null || pc.PlayerId == target.PlayerId || !pc.Is(CustomRoles.Ricochet) || Main.DontCancelVoteList.Contains(pc.PlayerId)) return false;

            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();
                ProtectAgainst = target.PlayerId;
                SendRPCSyncTarget(ProtectAgainst);
                Main.DontCancelVoteList.Add(pc.PlayerId);
                return true;
            }
            return false;
        }
        public static void OnReportDeadBody()
        {
            ProtectAgainst = byte.MaxValue;
            SendRPCSyncTarget(ProtectAgainst);
        }
        public static string GetProgressText(byte playerId, bool comms)
        {
            if (Utils.GetPlayerById(playerId) == null) return string.Empty;

            var sb = new StringBuilder();

            sb.Append(Utils.GetTaskCount(playerId, comms));
            sb.Append(Utils.GetAbilityUseLimitDisplay(playerId, ProtectAgainst != byte.MaxValue));

            return sb.ToString();
        }
        public static string TargetText => ProtectAgainst != byte.MaxValue ? $"<color=#00ffa5>Target:</color> <color=#ffffff>{Utils.GetPlayerById(ProtectAgainst).GetRealName()}</color>" : string.Empty;
    }
}