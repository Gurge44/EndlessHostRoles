using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Roles.AddOns.GhostRoles
{
    internal class Haunter : IGhostRole, ISettingHolder
    {
        public Team Team => Team.Crewmate | Team.Neutral;

        public byte HauntedPlayer = byte.MaxValue;
        public HashSet<byte> WarnedImps = [];
        public static HashSet<byte> AllHauntedPlayers = [];

        public void OnProtect(PlayerControl pc, PlayerControl target)
        {
        }

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(649300, TabGroup.OtherRoles, CustomRoles.Haunter);
        }

        public void OnAssign(PlayerControl pc)
        {
            HauntedPlayer = byte.MaxValue;
            _ = new LateTask(() =>
            {
                var taskState = pc.GetTaskState();
                if (taskState == null) return;

                taskState.hasTasks = true;
                taskState.CompletedTasksCount = 0;
                taskState.AllTasksCount = Utils.TotalTaskCount - Main.RealOptionsData.GetInt(Int32OptionNames.NumCommonTasks);

                GameData.Instance.RpcSetTasks(pc.PlayerId, Array.Empty<byte>());
                pc.SyncSettings();
                pc.RpcResetAbilityCooldown();
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }, 1f, "Haunter Assign");
        }

        public void OnOneTaskLeft(PlayerControl pc)
        {
            WarnedImps = [];
            foreach (var imp in Main.AllAlivePlayerControls.Where(x => x.Is(Team.Impostor)))
            {
                TargetArrow.Add(imp.PlayerId, pc.PlayerId);
                imp.Notify(Translator.GetString("Haunter1TaskLeft"), 300f);
                WarnedImps.Add(imp.PlayerId);
            }
        }

        public void OnFinishedTasks(PlayerControl pc)
        {
            if (WarnedImps.Count == 0) return;

            var index = IRandom.Instance.Next(WarnedImps.Count);
            var target = WarnedImps.ElementAt(index);
            HauntedPlayer = target;
            AllHauntedPlayers.Add(HauntedPlayer);

            var targetPc = Utils.GetPlayerById(target);
            targetPc?.Notify(Translator.GetString("HaunterRevealedYou"), 7f);

            foreach (var imp in WarnedImps)
            {
                TargetArrow.Remove(imp, pc.PlayerId);
                if (imp == target) continue;
                Utils.GetPlayerById(imp)?.Notify(Translator.GetString("HaunterFinishedTasks"), 7f);
            }

            Utils.NotifyRoles(SpecifyTarget: targetPc);
        }

        public void Update(PlayerControl pc)
        {
            if (WarnedImps.Count == 0) return;

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
    }
}
