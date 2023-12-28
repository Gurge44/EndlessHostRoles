using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal class Mole
    {
        private static int Id => 64400;
        public static void SetupCustomOption() => SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Mole);
        public static void OnCoEnterVent(PlayerPhysics physics)
        {
            var pc = physics.myPlayer;
            if (pc == null || !pc.Is(CustomRoles.Mole)) return;
            _ = new LateTask(() =>
            {
                var vents = UnityEngine.Object.FindObjectsOfType<Vent>();
                var vent = vents[IRandom.Instance.Next(0, vents.Count)];
                physics.RpcBootFromVent(vent.Id);
                _ = new LateTask(() =>
                {
                    pc.TPtoRndVent();
                }, 0.55f, "Mole TP");
            }, 0.5f, "Mole BootFromVent");
        }
    }
}
