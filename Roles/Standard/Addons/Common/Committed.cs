using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

public class Committed : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    private static OptionItem PermanentReduction;
    private static OptionItem Reduction;
    private static OptionItem IgnoreSkips;

    private static Dictionary<byte, byte> Target;
    public static Dictionary<byte, float> ReduceKCD;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(653600, CustomRoles.Committed, canSetNum: true, teamSpawnOptions: true);

        PermanentReduction = new BooleanOptionItem(653610, "Committed.PermanentReduction", false, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Committed]);

        Reduction = new FloatOptionItem(653611, "Committed.Reduction", new(0.5f, 30f, 0.5f), 5f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Committed])
            .SetValueFormat(OptionFormat.Seconds);

        IgnoreSkips = new BooleanOptionItem(653612, "Committed.IgnoreSkips", true, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Committed]);
    }

    public static void Init()
    {
        Target = null;
        ReduceKCD = null;
    }

    public static void OnMeetingStart()
    {
        Target = null;

        if (!PermanentReduction.GetBool())
            ReduceKCD = null;

        var aapc = Main.CachedAlivePlayerControls();
        var doRPC = false;

        foreach (PlayerControl pc in aapc)
        {
            if (pc.Is(CustomRoles.Committed))
            {
                Target ??= [];
                Target[pc.PlayerId] = aapc.Where(x => !x.Is(CustomRoles.Committed)).RandomElement().PlayerId;
                if (pc.IsNonHostModdedClient()) doRPC = true;
            }
        }

        if (doRPC)
        {
            var writer = Utils.CreateRPC(CustomRPC.Committed);
            writer.WritePacked(Target.Count);

            foreach ((byte key, byte value) in Target)
            {
                writer.Write(key);
                writer.Write(value);
            }
            
            Utils.EndRPC(writer);
        }
    }

    public static void OnVotingResultsShown(List<MeetingHud.VoterState> vs)
    {
        if (Target == null) return;
        
        foreach (PlayerControl pc in Main.CachedAlivePlayerControls())
        {
            if (Target.TryGetValue(pc.PlayerId, out byte target) && vs.FindFirst(x => x.VoterId == pc.PlayerId, out MeetingHud.VoterState s) && vs.FindFirst(x => x.VoterId == target, out MeetingHud.VoterState ts) && !(IgnoreSkips.GetBool() && s.SkippedVote) && s.VotedForId == ts.VotedForId)
            {
                ReduceKCD ??= [];
                
                if (ReduceKCD.ContainsKey(pc.PlayerId))
                    ReduceKCD[pc.PlayerId] += Reduction.GetFloat();
                else
                    ReduceKCD[pc.PlayerId] = Reduction.GetFloat();
            }
        }
    }

    public static string GetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Committed) || Target == null || !Target.TryGetValue(seer.PlayerId, out byte t) || t != target.PlayerId) return string.Empty;
        return CustomRoles.Committed.ColoredTextByRole("⌆");
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        Target = [];
        Loop.Times(reader.ReadPackedInt32(), _ => Target[reader.ReadByte()] = reader.ReadByte());
    }
}