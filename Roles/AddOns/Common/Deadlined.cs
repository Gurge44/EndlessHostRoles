using System.Collections.Generic;

namespace EHR.AddOns.Common
{
    public class Deadlined : IAddon
    {
        private static HashSet<byte> DidTask = [];
        private static long MeetingEndTS;
        private static OptionItem InactiveTime;
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(649293, CustomRoles.Deadlined, canSetNum: true, teamSpawnOptions: true);
            InactiveTime = new IntegerOptionItem(649299, "Deadlined.InactiveTime", new(0, 60, 1), 15, TabGroup.Addons)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Deadlined])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void SetDone(PlayerControl pc)
        {
            DidTask.Add(pc.PlayerId);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public static void AfterMeetingTasks()
        {
            DidTask = [];
            MeetingEndTS = Utils.TimeStamp;

            foreach (var pc in Main.AllPlayerControls)
            {
                TaskState ts = pc.GetTaskState();
                if (pc.Is(CustomRoles.Deadlined) && (!pc.IsAlive() || ts.IsTaskFinished || !ts.hasTasks))
                    Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Deadlined);
            }
        }

        public static void OnMeetingStart()
        {
            if (MeetingEndTS + InactiveTime.GetInt() > Utils.TimeStamp) return;
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (!pc.Is(CustomRoles.Deadlined)) continue;
                if (!DidTask.Contains(pc.PlayerId))
                    pc.Suicide();
            }
        }

        public static string GetSuffix(PlayerControl seer, bool hud = false)
        {
            if (!seer.Is(CustomRoles.Deadlined) || (seer.IsModClient() && !hud)) return string.Empty;
            if (DidTask.Contains(seer.PlayerId)) return "<#00ff00>\u2713</color>";
            long now = Utils.TimeStamp;
            return MeetingEndTS + InactiveTime.GetInt() <= now
                ? Translator.GetString("Deadlined.MustDoTask")
                : string.Format(Translator.GetString("Deadlined.SafeTime"), InactiveTime.GetInt() - (now - MeetingEndTS));
        }
    }
}