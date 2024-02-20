using Hazel;
using System.Collections.Generic;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    public class EvilDiviner : RoleBase
    {
        private const int Id = 2700;
        public static List<byte> playerIdList = [];

        private static OptionItem KillCooldown;
        private static OptionItem DivinationMaxCount;
        public static OptionItem EDAbilityUseGainWithEachKill;

        public List<byte> DivinationTarget = [];

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.EvilDiviner);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.EvilDiviner])
                .SetValueFormat(OptionFormat.Seconds);
            DivinationMaxCount = IntegerOptionItem.Create(Id + 11, "DivinationMaxCount", new(0, 15, 1), 1, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.EvilDiviner])
                .SetValueFormat(OptionFormat.Times);
            EDAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.3f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilDiviner])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            playerIdList = [];
            DivinationTarget = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(DivinationMaxCount.GetInt());
            DivinationTarget = [];
        }

        static void SendRPC(byte playerId, byte targetId)
        {
            if (!Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetEvilDiviner, SendOption.Reliable);
            writer.Write(playerId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void ReceiveRPC(byte targetId)
        {
            DivinationTarget.Add(targetId);
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            return !(killer.GetAbilityUseLimit() >= 1) || killer.CheckDoubleTrigger(target, () => { SetDivination(killer, target); });
        }

        public bool IsDivination(byte target) => DivinationTarget.Contains(target);

        public void SetDivination(PlayerControl killer, PlayerControl target)
        {
            if (!IsDivination(target.PlayerId))
            {
                killer.RpcRemoveAbilityUse();
                DivinationTarget.Add(target.PlayerId);
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()}: Divination target → {target.GetNameWithRole().RemoveHtmlTags()} || Remaining: {killer.GetAbilityUseLimit()} uses", "EvilDiviner");
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);

                SendRPC(killer.PlayerId, target.PlayerId);
                killer.SetKillCooldown();
            }
        }
        public static bool IsShowTargetRole(PlayerControl seer, PlayerControl target)
        {
            return Main.PlayerStates[seer.PlayerId].Role is EvilDiviner ed && ed.DivinationTarget.Contains(target.PlayerId);
        }
    }
}