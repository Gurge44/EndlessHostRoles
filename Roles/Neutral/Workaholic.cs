using System.Linq;
using AmongUs.GameOptions;
using TOHE.Modules;
using static TOHE.Options;

namespace TOHE.Roles.Neutral
{
    internal class Workaholic : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(11700, TabGroup.NeutralRoles, CustomRoles.Workaholic); //TOH_Y
            WorkaholicCannotWinAtDeath = BooleanOptionItem.Create(11710, "WorkaholicCannotWinAtDeath", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
            WorkaholicVentCooldown = FloatOptionItem.Create(11711, "VentCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic])
                .SetValueFormat(OptionFormat.Seconds);
            WorkaholicVisibleToEveryone = BooleanOptionItem.Create(11712, "WorkaholicVisibleToEveryone", false, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
            WorkaholicGiveAdviceAlive = BooleanOptionItem.Create(11713, "WorkaholicGiveAdviceAlive", false, TabGroup.NeutralRoles, false)
                .SetParent(WorkaholicVisibleToEveryone);
            WorkaholicTasks = OverrideTasksData.Create(11714, TabGroup.NeutralRoles, CustomRoles.Workaholic);
            WorkaholicCanGuess = BooleanOptionItem.Create(11725, "CanGuess", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
        }

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
            AURoleOptions.EngineerCooldown = WorkaholicVentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 0f;
        }

        public override void OnTaskComplete(PlayerControl player, int CompletedTasksCount, int AllTasksCount)
        {
            var alive = player.IsAlive();
            if ((CompletedTasksCount + 1) >= AllTasksCount && !(WorkaholicCannotWinAtDeath.GetBool() && !alive))
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