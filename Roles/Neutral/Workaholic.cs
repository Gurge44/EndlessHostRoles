using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Neutral
{
    internal class Workaholic
    {
        public static void OnTaskComplte(PlayerControl player, int CompletedTasksCount, int AllTasksCount)
        {
            var alive = player.IsAlive();
            if ((CompletedTasksCount + 1) >= AllTasksCount && !(Options.WorkaholicCannotWinAtDeath.GetBool() && !alive))
            {
                Logger.Info("Workaholic Tasks Finished", "Workaholic");
                RPC.PlaySoundRPC(player.PlayerId, Sounds.KillSound);
                foreach (var pc in Main.AllAlivePlayerControls.Where(pc => pc.PlayerId != player.PlayerId).ToArray())
                {
                    pc.Suicide(pc.PlayerId == player.PlayerId ? PlayerState.DeathReason.Overtired : PlayerState.DeathReason.Ashamed, player);
                }

                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Workaholic);
                CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
            }
        }
    }
}
