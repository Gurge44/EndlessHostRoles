using AmongUs.GameOptions;

namespace EHR.Crewmate
{
    internal class Convener : RoleBase
    {
        public static OptionItem CD;
        public static OptionItem Limit;
        public static OptionItem ConvenerAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        public static bool On;
        private static int Id => 643350;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Convener);
            CD = Options.CreateCDSetting(Id + 2, TabGroup.CrewmateRoles, CustomRoles.Convener);
            Limit = new IntegerOptionItem(Id + 3, "AbilityUseLimit", new(0, 20, 1), 0, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Convener])
                .SetValueFormat(OptionFormat.Times);
            ConvenerAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 4, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.4f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Convener])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 5, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Convener])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(Limit.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool())
            {
                return;
            }

            AURoleOptions.EngineerCooldown = CD.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void OnPet(PlayerControl pc)
        {
            PullEveryone(pc, isPet: true);
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            PullEveryone(physics.myPlayer, ventId);
        }

        private static void PullEveryone(PlayerControl pc, int ventId = 0, bool isPet = false)
        {
            if (pc == null || pc.GetAbilityUseLimit() < 1f)
            {
                return;
            }

            if (isPet)
            {
                Utils.TPAll(pc.Pos());
            }
            else
            {
                LateTask.New(() => pc.MyPhysics.RpcBootFromVent(ventId), 0.5f, "Convener RpcBootFromVent");
                LateTask.New(() => Utils.TPAll(pc.Pos()), 1f, "Convener TP");
            }

            pc.RpcRemoveAbilityUse();
        }
    }
}