using System;
using System.Collections.Generic;

namespace TOHE.Roles.Impostor
{
    internal class Capitalism : RoleBase
    {
        public static Dictionary<byte, int> CapitalismAddTask = [];
        public static Dictionary<byte, int> CapitalismAssignTask = [];
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.KillButton?.OverrideText(Translator.GetString("CapitalismButtonText"));
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Options.CapitalismKillCooldown.GetFloat();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            return killer.CheckDoubleTrigger(target, () =>
            {
                CapitalismAddTask.TryAdd(target.PlayerId, 0);
                CapitalismAddTask[target.PlayerId]++;
                CapitalismAssignTask.TryAdd(target.PlayerId, 0);
                CapitalismAssignTask[target.PlayerId]++;
                Logger.Info($"{killer.GetRealName()} added a task for：{target.GetRealName()}", "Capitalism Add Task");
                //killer.RpcGuardAndKill(killer);
                killer.SetKillCooldown(Options.CapitalismSkillCooldown.GetFloat());
            });
        }

        public static bool AddTaskForPlayer(PlayerControl player)
        {
            if (CapitalismAddTask.TryGetValue(player.PlayerId, out var amount))
            {
                var taskState = player.GetTaskState();
                taskState.AllTasksCount += amount;
                CapitalismAddTask.Remove(player.PlayerId);
                taskState.CompletedTasksCount++;
                GameData.Instance.RpcSetTasks(player.PlayerId, Array.Empty<byte>()); // Redistribute tasks
                player.SyncSettings();
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                return false;
            }

            return true;
        }
    }
}
