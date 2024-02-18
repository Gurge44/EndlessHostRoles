using System.Collections.Generic;
using System.Linq;

namespace TOHE.Roles.Crewmate
{
    internal class Insight
    {
        public static List<byte> KnownRolesOfPlayerIds = [];

        public static void OnTaskComplete(PlayerControl player)
        {
            var list = Main.AllPlayerControls.Where(x => !KnownRolesOfPlayerIds.Contains(x.PlayerId) && !x.Is(CountTypes.OutOfGame) && !x.Is(CustomRoles.Insight) && !x.Is(CustomRoles.GM) && !x.Is(CustomRoles.NotAssigned))?.ToList();
            if (list != null && list.Count != 0)
            {
                var target = list[IRandom.Instance.Next(0, list.Count)];
                KnownRolesOfPlayerIds.Add(target.PlayerId);
                player.Notify(string.Format(Utils.ColorString(target.GetRoleColor(), Translator.GetString("InsightNotify")), target.GetDisplayRoleName(pure: true)));
            }
        }
    }
}