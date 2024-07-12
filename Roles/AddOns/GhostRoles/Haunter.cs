using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.AddOns.GhostRoles
{
    internal class Haunter : IGhostRole, ISettingHolder
    {
        public static HashSet<byte> AllHauntedPlayers = [];

        public static OptionItem TasksBeforeBeingKnown;
        private static OptionItem RevealNeutralKillers;
        private static OptionItem RevealMadmates;
        private static OptionItem NumberOfReveals;
        private byte HaunterId;

        private List<byte> WarnedImps = [];

        private long WarnTimeStamp = 0;
        public Team Team => Team.Crewmate | Team.Neutral;
        public int Cooldown => 900;
        public bool ChangeToGA => false;

        public void OnProtect(PlayerControl pc, PlayerControl target)
        {
        }

        public void OnAssign(PlayerControl pc)
        {
            HaunterId = pc.PlayerId;
            LateTask.New(() =>
            {
                var taskState = pc.GetTaskState();
                if (taskState == null) return;

                taskState.hasTasks = true;
                taskState.CompletedTasksCount = 0;
                taskState.AllTasksCount = Utils.TotalTaskCount - Main.RealOptionsData.GetInt(Int32OptionNames.NumCommonTasks);

                pc.Data.RpcSetTasks(Array.Empty<byte>());
                pc.SyncSettings();
                pc.RpcResetAbilityCooldown();
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }, 1f, "Haunter Assign");
        }

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(649300, TabGroup.OtherRoles, CustomRoles.Haunter, zeroOne: true);
            TasksBeforeBeingKnown = new IntegerOptionItem(649302, "Haunter.TasksBeforeBeingKnown", new(1, 10, 1), 1, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Haunter]);
            RevealNeutralKillers = new BooleanOptionItem(649303, "Haunter.RevealNeutralKillers", true, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Haunter]);
            RevealMadmates = new BooleanOptionItem(649304, "Haunter.RevealMadmates", true, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Haunter]);
            NumberOfReveals = new IntegerOptionItem(649305, "Haunter.NumberOfReveals", new(1, 10, 1), 1, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Haunter]);
            Options.OverrideTasksData.Create(649306, TabGroup.OtherRoles, CustomRoles.Haunter);
        }

        public void OnOneTaskLeft(PlayerControl pc)
        {
            if (WarnedImps.Count > 0) return;

            WarnedImps = [];
            var filtered = Main.AllAlivePlayerControls.Where(x =>
            {
                return x.GetTeam() switch
                {
                    Team.Impostor when x.GetCustomRole().IsMadmate() || x.Is(CustomRoles.Madmate) => RevealMadmates.GetBool(),
                    Team.Impostor => true,
                    Team.Neutral when x.IsNeutralKiller() => RevealNeutralKillers.GetBool(),
                    _ => false
                };
            });
            foreach (var imp in filtered)
            {
                TargetArrow.Add(imp.PlayerId, pc.PlayerId);
                imp.Notify(Translator.GetString("Haunter1TaskLeft"), 300f);
                WarnedImps.Add(imp.PlayerId);
            }

            WarnTimeStamp = Utils.TimeStamp;
        }

        public void OnFinishedTasks(PlayerControl pc)
        {
            if (WarnedImps.Count == 0) return;

            List<byte> targets = [];
            int numOfReveals = NumberOfReveals.GetInt();
            for (int i = 0; i < numOfReveals; i++)
            {
                var index = IRandom.Instance.Next(WarnedImps.Count);
                var target = WarnedImps[index];
                targets.Add(target);
                WarnedImps.Remove(target);
                TargetArrow.Remove(target, pc.PlayerId);
            }

            AllHauntedPlayers.UnionWith(targets);

            targets.ForEach(x => Utils.GetPlayerById(x)?.Notify(Translator.GetString("HaunterRevealedYou"), 7f));
            WarnedImps.ForEach(x => Utils.GetPlayerById(x)?.Notify(Translator.GetString("HaunterFinishedTasks"), 7f));

            Utils.NotifyRoles(ForceLoop: true);
        }

        public void Update(PlayerControl pc)
        {
            if (WarnedImps.Count == 0 || WarnTimeStamp == Utils.TimeStamp) return;

            if (WarnedImps.Any(imp => TargetArrow.GetArrows(Utils.GetPlayerById(imp), pc.PlayerId) == "・"))
            {
                foreach (var imp in WarnedImps)
                {
                    TargetArrow.Remove(imp, pc.PlayerId);
                    Utils.GetPlayerById(imp)?.Notify(Translator.GetString("HaunterStopped"), 7f);
                }

                WarnedImps = [];
                Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Haunter);
                GhostRolesManager.AssignedGhostRoles.Remove(pc.PlayerId);
                pc.Notify(Translator.GetString("HaunterStoppedSelf"), 7f);
            }
        }

        public static string GetSuffix(PlayerControl seer)
        {
            foreach (var role in GhostRolesManager.AssignedGhostRoles.Values)
            {
                if (role.Instance is not Haunter haunter) continue;
                if (!haunter.WarnedImps.Contains(seer.PlayerId)) continue;
                return TargetArrow.GetArrows(seer, haunter.HaunterId);
            }

            return string.Empty;
        }
    }
}