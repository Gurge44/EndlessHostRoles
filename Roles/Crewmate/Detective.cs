using System.Collections.Generic;

namespace EHR.Crewmate
{
    internal class Detective : RoleBase
    {
        public static Dictionary<byte, string> DetectiveNotify = [];

        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(6600, TabGroup.CrewmateRoles, CustomRoles.Detective);
            Options.DetectiveCanknowKiller = new BooleanOptionItem(6610, "DetectiveCanknowKiller", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detective]);
        }

        public override void Init() => throw new System.NotImplementedException();
        public override void Add(byte playerId) => throw new System.NotImplementedException();

        public static void OnReportDeadBody(PlayerControl player, PlayerControl tpc)
        {
            string msg = string.Format(Translator.GetString("DetectiveNoticeVictim"), tpc.GetRealName(), tpc.GetCustomRole().ToColoredString());
            if (Options.DetectiveCanknowKiller.GetBool())
            {
                var realKiller = tpc.GetRealKiller();
                if (realKiller == null) msg += "；" + Translator.GetString("DetectiveNoticeKillerNotFound");
                else msg += "；" + string.Format(Translator.GetString("DetectiveNoticeKiller"), realKiller.GetCustomRole().ToColoredString());
            }

            DetectiveNotify.Add(player.PlayerId, msg);
        }
    }
}