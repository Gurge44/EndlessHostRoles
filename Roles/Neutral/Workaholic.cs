using System.Linq;
using AmongUs.GameOptions;
using TOHE.Modules;

namespace TOHE.Roles.Neutral
{
    internal class Workaholic : RoleBase
    {
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

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = Options.WorkaholicVentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 0f;
        }

        public override void OnTaskComplete(PlayerControl player, int CompletedTasksCount, int AllTasksCount)
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