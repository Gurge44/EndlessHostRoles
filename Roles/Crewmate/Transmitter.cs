using System.Linq;

namespace TOHE.Roles.Crewmate
{
    public static class Transmitter
    {
        public static void SetupCustomOption() => Options.SetupRoleOptions(642610, TabGroup.CrewmateRoles, CustomRoles.Transmitter);

        public static void OnTaskComplete(PlayerControl pc) => pc.TP(Main.AllAlivePlayerControls.OrderBy(x => UnityEngine.Vector2.Distance(x.Pos(), pc.Pos())).FirstOrDefault(x => x.PlayerId != pc.PlayerId).Pos());
    }
}
