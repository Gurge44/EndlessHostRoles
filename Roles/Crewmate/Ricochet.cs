using Hazel;
using System.Collections.Generic;
using TOHE.Modules;

namespace TOHE.Roles.Crewmate
{
    using static Options;

    public class Ricochet : RoleBase
    {
        private const int Id = 640100;
        public static List<byte> playerIdList = [];

        public byte ProtectAgainst = byte.MaxValue;
        private byte RicochetId;

        public static OptionItem UseLimitOpt;
        public static OptionItem RicochetAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        public static OptionItem CancelVote;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Ricochet);
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

        public override void Init()
        {
            playerIdList = [];
            ProtectAgainst = byte.MaxValue;
            RicochetId = byte.MaxValue;
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
            RicochetId = playerId;
        }

        public override bool IsEnable => playerIdList.Count > 0;

        void SendRPCSyncTarget(byte targetId)
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRicochetTarget, SendOption.Reliable);
            writer.Write(RicochetId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCSyncTarget(MessageReader reader)
        {
            byte id = reader.ReadByte();
            if (Main.PlayerStates[id].Role is not Ricochet rc) return;

            rc.ProtectAgainst = reader.ReadByte();
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;

            if (ProtectAgainst == killer.PlayerId)
            {
                killer.SetKillCooldown(time: 5f);
                return false;
            }

            return true;
        }
        public static bool OnVote(PlayerControl pc, PlayerControl target)
        {
            if (target == null || pc == null || pc.PlayerId == target.PlayerId || Main.PlayerStates[pc.PlayerId].Role is not Ricochet rc || Main.DontCancelVoteList.Contains(pc.PlayerId)) return false;

            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();
                rc.ProtectAgainst = target.PlayerId;
                rc.SendRPCSyncTarget(rc.ProtectAgainst);
                Main.DontCancelVoteList.Add(pc.PlayerId);
                return true;
            }
            return false;
        }

        public override void OnReportDeadBody()
        {
            ProtectAgainst = byte.MaxValue;
            SendRPCSyncTarget(ProtectAgainst);
        }

        public static string TargetText(byte id) => Main.PlayerStates[id].Role is Ricochet rc && rc.ProtectAgainst != byte.MaxValue ? $"<color=#00ffa5>Target:</color> <color=#ffffff>{Utils.GetPlayerById(rc.ProtectAgainst).GetRealName()}</color>" : string.Empty;
    }
}