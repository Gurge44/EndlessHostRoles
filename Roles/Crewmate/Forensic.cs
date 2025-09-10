using System;
using System.Collections.Generic;
using System.Linq;

namespace EHR.Crewmate;

internal class Forensic : RoleBase
{
    public static OptionItem ForensicCanknowKiller;
    public static OptionItem ForensicCanknowDeathReason;
    public static OptionItem ForensicCanknowAddons;
    public static OptionItem ForensicCanknowKillTime;

    public static Dictionary<byte, string> ForensicNotify = [];

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(6600, TabGroup.CrewmateRoles, CustomRoles.Forensic);

        ForensicCanknowKiller = new BooleanOptionItem(6610, "ForensicCanknowKiller", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Forensic]);

        ForensicCanknowDeathReason = new BooleanOptionItem(6611, "ForensicCanknowDeathReason", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Forensic]);

        ForensicCanknowAddons = new BooleanOptionItem(6612, "ForensicCanknowAddons", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Forensic]);

        ForensicCanknowKillTime = new BooleanOptionItem(6613, "ForensicCanknowKillTime", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Forensic]);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }

    public static void OnReportDeadBody(PlayerControl player, PlayerControl tpc)
    {
        string msg = string.Format(Translator.GetString("ForensicNoticeVictim"), tpc.GetRealName(), tpc.GetCustomRole().ToColoredString());

        if (ForensicCanknowKiller.GetBool())
        {
            PlayerControl realKiller = tpc.GetRealKiller();

            if (realKiller == null)
                msg += "；" + Translator.GetString("ForensicNoticeKillerNotFound");
            else
                msg += "；" + string.Format(Translator.GetString("ForensicNoticeKiller"), realKiller.GetCustomRole().ToColoredString());
        }

        if (ForensicCanknowDeathReason.GetBool()) msg += "；" + string.Format(Translator.GetString("ForensicNoticeDeathReason"), Translator.GetString($"DeathReason.{Main.PlayerStates[tpc.PlayerId].deathReason}"));

        if (ForensicCanknowAddons.GetBool()) msg += "；" + string.Format(Translator.GetString("ForensicNoticeAddons"), string.Join(", ", tpc.GetCustomSubRoles().Select(x => x.ToColoredString())));

        if (ForensicCanknowKillTime.GetBool())
        {
            DateTime deathTimeStamp = Main.PlayerStates[tpc.PlayerId].RealKiller.TimeStamp;
            DateTime now = DateTime.Now;
            double timeSpanSeconds = (now - deathTimeStamp).TotalSeconds;
            msg += "；" + string.Format(Translator.GetString("ForensicNoticeKillTime"), (int)timeSpanSeconds);
        }

        ForensicNotify[player.PlayerId] = msg;
    }
}