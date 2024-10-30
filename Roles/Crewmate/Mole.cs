using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Crewmate
{
    internal class Mole : RoleBase
    {
        public static bool On;

        public static OptionItem CD;
        public override bool IsEnable => On;

        private static int Id => 64400;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Mole);
            CD = new FloatOptionItem(Id + 2, "AbilityCooldown", new(0f, 120f, 0.5f), 15f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mole])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool())
            {
                return;
            }

            AURoleOptions.EngineerInVentMaxTime = 1f;
            AURoleOptions.EngineerCooldown = CD.GetFloat();
        }

        public override void OnExitVent(PlayerControl pc, Vent vent)
        {
            if (UsePets.GetBool())
            {
                return;
            }

            LateTask.New(() => { pc.TPToRandomVent(); }, 0.5f, "Mole TP");
        }

        public override void OnPet(PlayerControl pc)
        {
            pc.TPToRandomVent();
        }
    }
}