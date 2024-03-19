using System;

namespace TOHE.Roles.AddOns.GhostRoles
{
    // TOU-R Phantom
    internal class Specter : IGhostRole, ISettingHolder
    {
        public Team Team => Team.Neutral;

        private static OptionItem SnatchWin;

        public bool IsWon;

        public void OnAssign(PlayerControl pc)
        {
            _ = new LateTask(() =>
            {
                IsWon = false;
                var taskState = pc.GetTaskState();
                if (taskState == null) return;
                taskState.hasTasks = true;
                taskState.CompletedTasksCount = 0;
                GameData.Instance.RpcSetTasks(pc.PlayerId, Array.Empty<byte>());
                pc.SyncSettings();
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }, 1f, "Specter Assign");
        }

        public void OnProtect(PlayerControl pc, PlayerControl target)
        {
        }

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(649100, TabGroup.OtherRoles, CustomRoles.Specter);
            SnatchWin = BooleanOptionItem.Create(649102, "SnatchWin", false, TabGroup.OtherRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Specter]);
        }

        public void OnFinishedTasks(PlayerControl pc)
        {
            if (!SnatchWin.GetBool())
            {
                IsWon = true;
                return;
            }

            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Specter);
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
        }
    }
}
