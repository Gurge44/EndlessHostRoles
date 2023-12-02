using System.Linq;

namespace TOHE.Roles.Crewmate
{
    internal class Autocrat
    {
        public static void SetupCustomOption() => Options.SetupRoleOptions(642620, TabGroup.CrewmateRoles, CustomRoles.Autocrat);

        public static void OnTaskComplete(PlayerControl pc) => Main.AllAlivePlayerControls.OrderBy(x => UnityEngine.Vector2.Distance(x.Pos(), pc.Pos())).FirstOrDefault(x => x.PlayerId != pc.PlayerId).TP(pc.Pos());
    }
}
