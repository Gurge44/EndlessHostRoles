using System.Collections.Generic;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

public class Deadlined : IAddon
{
    private static HashSet<byte> DidTask = [];
    private static long MeetingEndTS;
    private static OptionItem InactiveTime;
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(649291, CustomRoles.Deadlined, canSetNum: true, teamSpawnOptions: true);

        InactiveTime = new IntegerOptionItem(649299, "Deadlined.InactiveTime", new(0, 60, 1), 15, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Deadlined])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public static void SetDone(PlayerControl pc)
    {
        DidTask.Add(pc.PlayerId);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        Utils.SendRPC(CustomRPC.Deadlined, 1, pc.PlayerId);
    }

    public static void AfterMeetingTasks()
    {
        DidTask = [];
        MeetingEndTS = Utils.TimeStamp;
        Utils.SendRPC(CustomRPC.Deadlined, 2);

        foreach (PlayerControl pc in Main.EnumeratePlayerControls())
        {
            TaskState ts = pc.GetTaskState();
            if (pc.Is(CustomRoles.Deadlined) && (!pc.IsAlive() || ts.IsTaskFinished || (!ts.HasTasks && !pc.CanUseKillButton())))
                Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Deadlined);
        }
    }

    public static void OnMeetingStart()
    {
        if (MeetingStates.FirstMeeting) return;

        if (MeetingEndTS + InactiveTime.GetInt() >= Utils.TimeStamp) return;

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
        {
            if (!pc.Is(CustomRoles.Deadlined)) continue;

            if (!DidTask.Contains(pc.PlayerId))
                pc.Suicide();
        }
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                DidTask.Add(reader.ReadByte());
                break;
            case 2:
                DidTask = [];
                MeetingEndTS = Utils.TimeStamp - 1;
                break;
        }
    }

    public static string GetSuffix(PlayerControl seer, bool hud = false)
    {
        if (!seer.Is(CustomRoles.Deadlined) || (seer.IsModdedClient() && !hud)) return string.Empty;

        if (DidTask.Contains(seer.PlayerId) || MeetingStates.FirstMeeting) return "<#00ff00>\u2713</color>";

        long now = Utils.TimeStamp;

        return MeetingEndTS + InactiveTime.GetInt() <= now
            ? Translator.GetString("Deadlined.MustDoTask")
            : string.Format(Translator.GetString("Deadlined.SafeTime"), InactiveTime.GetInt() - (now - MeetingEndTS));
    }
}