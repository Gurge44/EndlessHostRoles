using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;

// From: TOH_Y
namespace EHR.Roles.Neutral
{
    internal class Workaholic : RoleBase
    {
        public static bool On;

        public static List<byte> WorkaholicAlive = [];

        public static OptionItem WorkaholicVentCooldown;
        public static OptionItem WorkaholicCannotWinAtDeath;
        public static OptionItem WorkaholicVisibleToEveryone;
        public static OptionItem WorkaholicGiveAdviceAlive;
        public static OptionItem WorkaholicCanGuess;
        public static OptionItem WorkaholicSpeed;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(11700, TabGroup.NeutralRoles, CustomRoles.Workaholic);
            WorkaholicCannotWinAtDeath = BooleanOptionItem.Create(11710, "WorkaholicCannotWinAtDeath", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
            WorkaholicVentCooldown = FloatOptionItem.Create(11711, "VentCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic])
                .SetValueFormat(OptionFormat.Seconds);
            WorkaholicVisibleToEveryone = BooleanOptionItem.Create(11712, "WorkaholicVisibleToEveryone", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
            WorkaholicGiveAdviceAlive = BooleanOptionItem.Create(11713, "WorkaholicGiveAdviceAlive", false, TabGroup.NeutralRoles)
                .SetParent(WorkaholicVisibleToEveryone);
            OverrideTasksData.Create(11714, TabGroup.NeutralRoles, CustomRoles.Workaholic);
            WorkaholicCanGuess = BooleanOptionItem.Create(11725, "CanGuess", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
            WorkaholicSpeed = FloatOptionItem.Create(11726, "WorkaholicSpeed", new(0.1f, 3f, 0.1f), 1.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic])
                .SetValueFormat(OptionFormat.Multiplier);
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
            Main.AllPlayerSpeed[playerId] = WorkaholicSpeed.GetFloat();
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