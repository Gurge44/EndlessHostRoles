namespace TOHE.Roles.Crewmate
{
    internal class Detective
    {
        public static void OnReportDeadBody(PlayerControl player, PlayerControl tpc)
        {
            string msg = string.Format(Translator.GetString("DetectiveNoticeVictim"), tpc.GetRealName(), tpc.GetDisplayRoleName());
            if (Options.DetectiveCanknowKiller.GetBool())
            {
                var realKiller = tpc.GetRealKiller();
                if (realKiller == null) msg += "；" + Translator.GetString("DetectiveNoticeKillerNotFound");
                else msg += "；" + string.Format(Translator.GetString("DetectiveNoticeKiller"), realKiller.GetDisplayRoleName());
            }

            Main.DetectiveNotify.Add(player.PlayerId, msg);
        }
    }
}
