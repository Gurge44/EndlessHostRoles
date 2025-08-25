using System.Collections.Generic;
using System.Linq;

namespace EHR.AddOns.Common;

public class Commited : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    private static OptionItem PermanentReduction;
    private static OptionItem Reduction;
    private static OptionItem IgnoreSkips;

    private static Dictionary<byte, byte> Target = [];
    public static Dictionary<byte, float> ReduceKCD = [];

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(653600, CustomRoles.Commited, canSetNum: true, teamSpawnOptions: true);

        PermanentReduction = new BooleanOptionItem(653610, "Commited.PermanentReduction", false, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Commited]);

        Reduction = new FloatOptionItem(653611, "Commited.Reduction", new(0.5f, 30f, 0.5f), 5f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Commited])
            .SetValueFormat(OptionFormat.Seconds);

        IgnoreSkips = new BooleanOptionItem(653612, "Commited.IgnoreSkips", true, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Commited]);
    }

    public static void Init()
    {
        Target = [];
        ReduceKCD = [];
    }

    public static void OnMeetingStart()
    {
        Target = [];

        if (!PermanentReduction.GetBool())
            ReduceKCD = [];

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.Is(CustomRoles.Commited))
                Target[pc.PlayerId] = Main.AllAlivePlayerControls.Where(x => !x.Is(CustomRoles.Commited)).RandomElement().PlayerId;
        }
    }

    public static void OnVotingResultsShown(List<MeetingHud.VoterState> vs)
    {
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (Target.TryGetValue(pc.PlayerId, out byte target) && vs.FindFirst(x => x.VoterId == pc.PlayerId, out MeetingHud.VoterState s) && vs.FindFirst(x => x.VoterId == target, out MeetingHud.VoterState ts) && !(IgnoreSkips.GetBool() && s.SkippedVote) && s.VotedForId == ts.VotedForId)
            {
                if (ReduceKCD.ContainsKey(pc.PlayerId))
                    ReduceKCD[pc.PlayerId] += Reduction.GetFloat();
                else
                    ReduceKCD[pc.PlayerId] = Reduction.GetFloat();
            }
        }
    }

    public static string GetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Commited) || !Target.TryGetValue(seer.PlayerId, out byte t) || t != target.PlayerId) return string.Empty;
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Commited), "⌆");
    }
}