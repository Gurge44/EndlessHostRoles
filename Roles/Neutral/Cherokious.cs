using EHR.Patches;

namespace EHR.Roles.Impostor
{
    internal class Cherokious : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static OptionItem KillCooldown;
        public static Options.OverrideTasksData Tasks;

        public static void SetupCustomOption()
        {
            const int id = 13860;
            Options.SetupRoleOptions(id, TabGroup.NeutralRoles, CustomRoles.Cherokious);
            KillCooldown = IntegerOptionItem.Create(id + 2, "KillCooldown", new(0, 60, 1), 15, TabGroup.NeutralRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Cherokious])
                .SetValueFormat(OptionFormat.Seconds);
            Tasks = Options.OverrideTasksData.Create(id + 3, TabGroup.NeutralRoles, CustomRoles.Cherokious);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(0);
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
        {
            pc.RpcIncreaseAbilityUseLimitBy(1);
        }

        public override void OnPet(PlayerControl pc)
        {
            if (pc.GetAbilityUseLimit() < 1) return;
            var target = ExternalRpcPetPatch.SelectKillButtonTarget(pc);
            if (target != null && pc.RpcCheckAndMurder(target)) pc.RpcRemoveAbilityUse();
        }

        public override bool CanUseKillButton(PlayerControl pc) => false;
    }
}
