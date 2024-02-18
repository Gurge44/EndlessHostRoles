using System.Linq;
using UnityEngine;

namespace TOHE.Roles.Crewmate
{
    public static class Transmitter
    {
        public static void SetupCustomOption() => Options.SetupRoleOptions(642610, TabGroup.CrewmateRoles, CustomRoles.Transmitter);

        public static void OnTaskComplete(PlayerControl pc) => pc.TP(Main.AllAlivePlayerControls.OrderBy(x => Vector2.Distance(x.Pos(), pc.Pos())).FirstOrDefault(x => x.PlayerId != pc.PlayerId) ?? pc);
    }
}
