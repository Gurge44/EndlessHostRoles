using System.Collections.Generic;

namespace EHR.Roles.Crewmate
{
    internal class Detective : ISettingHolder
    {
        public static Dictionary<byte, string> DetectiveNotify = [];

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(6600, TabGroup.CrewmateRoles, CustomRoles.Detective);
            Options.DetectiveCanknowKiller = new BooleanOptionItem(6610, "DetectiveCanknowKiller", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detective]);
        }

        public static void OnReportDeadBody(PlayerControl player, PlayerControl tpc)
        {
            string msg = string.Format(Translator.GetString("DetectiveNoticeVictim"), tpc.GetRealName(), tpc.GetDisplayRoleName());
            if (Options.DetectiveCanknowKiller.GetBool())
            {
                var realKiller = tpc.GetRealKiller();
                if (realKiller == null) msg += "；" + Translator.GetString("DetectiveNoticeKillerNotFound");
                else msg += "；" + string.Format(Translator.GetString("DetectiveNoticeKiller"), realKiller.GetDisplayRoleName());
            }

            DetectiveNotify.Add(player.PlayerId, msg);
        }
    }
}