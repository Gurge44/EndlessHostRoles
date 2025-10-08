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
    public static OptionItem ForensicCanknowColorType;
    public static OptionItem ForensicCanknowColorTypeMinBodyAge;

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

        ForensicCanknowColorType = new BooleanOptionItem(6614, "ForensicCanknowColorType", false, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Forensic]);
        
        ForensicCanknowColorTypeMinBodyAge = new IntegerOptionItem(6615, "ForensicCanknowColorTypeMinBodyAge", new(1, 60, 1), 10, TabGroup.CrewmateRoles)
            .SetValueFormat(OptionFormat.Seconds)
            .SetParent(ForensicCanknowColorType);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }

    public static void OnReportDeadBody(PlayerControl player, PlayerControl tpc)
    {
        string msg = string.Format(Translator.GetString("ForensicNoticeVictim"), tpc.GetRealName(), tpc.GetCustomRole().ToColoredString());

        PlayerControl realKiller = tpc.GetRealKiller();

        if (ForensicCanknowKiller.GetBool())
        {
            if (realKiller == null)
                msg += "；" + Translator.GetString("ForensicNoticeKillerNotFound");
            else
                msg += "；" + string.Format(Translator.GetString("ForensicNoticeKiller"), realKiller.GetCustomRole().ToColoredString());
        }

        if (ForensicCanknowDeathReason.GetBool())
            msg += "；" + string.Format(Translator.GetString("ForensicNoticeDeathReason"), Translator.GetString($"DeathReason.{Main.PlayerStates[tpc.PlayerId].deathReason}"));

        if (ForensicCanknowAddons.GetBool())
            msg += "；" + string.Format(Translator.GetString("ForensicNoticeAddons"), string.Join(", ", tpc.GetCustomSubRoles().Select(x => x.ToColoredString())));

        DateTime deathTimeStamp = Main.PlayerStates[tpc.PlayerId].RealKiller.TimeStamp;
        DateTime now = DateTime.Now;
        double timeSpanSeconds = (now - deathTimeStamp).TotalSeconds;
        
        if (ForensicCanknowKillTime.GetBool())
            msg += "；" + string.Format(Translator.GetString("ForensicNoticeKillTime"), (int)timeSpanSeconds);

        if (ForensicCanknowColorType.GetBool() && realKiller != null && timeSpanSeconds >= ForensicCanknowColorTypeMinBodyAge.GetInt())
        {
            var darker = new List<int> { 0, 1, 2, 6, 8, 9, 12, 15 };
            bool isDarker = darker.Contains(realKiller.CurrentOutfit.ColorId);
            Func<int, string> selector = x => Utils.ColorString(Palette.PlayerColors[x], Palette.GetColorName(x));
            var colors = isDarker ? string.Join('/', darker.Select(selector)) : string.Join('/', Enumerable.Range(0, 18).Except(darker).Select(selector));
            var str = Translator.GetString(isDarker ? "WhispererInfo.ColorDark" : "WhispererInfo.ColorLight");
            msg += "；" + string.Format(Translator.GetString("ForensicNoticeColorType"), str, colors);
        }

        ForensicNotify[player.PlayerId] = msg;
    }
}