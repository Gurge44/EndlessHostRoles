using AmongUs.GameOptions;

namespace EHR.Crewmate
{
    public class Clairvoyant : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption() => Options.SetupRoleOptions(644970, TabGroup.CrewmateRoles, CustomRoles.Clairvoyant);

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = 1f;
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        static void UseAbility(PlayerControl pc) => pc.Notify(Utils.GetRemainingKillers(notify: true));
        public override void OnPet(PlayerControl pc) => UseAbility(pc);
        public override void OnEnterVent(PlayerControl pc, Vent vent) => UseAbility(pc);
    }
}