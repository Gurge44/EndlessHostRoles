using System.Collections.Generic;

namespace TOHE.Roles.Impostor
{
    internal class Visionary : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public List<byte> RevealedPlayerIds = [];

        public static void SetupCustomOption() => Options.SetupRoleOptions(16150, TabGroup.ImpostorRoles, CustomRoles.Visionary);

        public override void Add(byte playerId)
        {
            On = true;
            RevealedPlayerIds = [];
            playerId.SetAbilityUseLimit(0);
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            killer.RpcIncreaseAbilityUseLimitBy(1);
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!base.OnCheckMurder(killer, target)) return false;

            if (killer.GetAbilityUseLimit() < 1) return true;

            return killer.CheckDoubleTrigger(target, () =>
            {
                RevealedPlayerIds.Add(target.PlayerId);
                killer.RpcRemoveAbilityUse();
            });
        }
    }
}