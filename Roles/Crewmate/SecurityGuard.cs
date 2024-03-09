using AmongUs.GameOptions;
using System.Text;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal class SecurityGuard : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(6860, TabGroup.CrewmateRoles, CustomRoles.SecurityGuard);
            SecurityGuardSkillCooldown = FloatOptionItem.Create(6862, "SecurityGuardSkillCooldown", new(0f, 180f, 1f), 15f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
                .SetValueFormat(OptionFormat.Seconds);
            SecurityGuardSkillDuration = FloatOptionItem.Create(6863, "SecurityGuardSkillDuration", new(0f, 180f, 1f), 10f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
                .SetValueFormat(OptionFormat.Seconds);
            SecurityGuardSkillMaxOfUseage = IntegerOptionItem.Create(6866, "AbilityUseLimit", new(0, 180, 1), 1, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
                .SetValueFormat(OptionFormat.Times);
            SecurityGuardAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(6867, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.4f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
                .SetValueFormat(OptionFormat.Times);
            SecurityGuardAbilityChargesWhenFinishedTasks = FloatOptionItem.Create(6868, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(SecurityGuardSkillMaxOfUseage.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.EngineerInVentMaxTime = 1;
            AURoleOptions.EngineerCooldown = SecurityGuardSkillCooldown.GetFloat();
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var ProgressText = new StringBuilder();

            ProgressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, Main.BlockSabo.ContainsKey(playerId)));
            ProgressText.Append(Utils.GetTaskCount(playerId, comms));

            return ProgressText.ToString();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool())
                hud.PetButton.buttonLabelText.text = Translator.GetString("SecurityGuardVentButtonText");
            else
                hud.AbilityButton.buttonLabelText.text = Translator.GetString("SecurityGuardVentButtonText");
        }

        public override void OnPet(PlayerControl pc)
        {
            Guard(pc);
        }

        static void Guard(PlayerControl pc)
        {
            if (Main.BlockSabo.ContainsKey(pc.PlayerId)) return;
            if (pc.GetAbilityUseLimit() >= 1)
            {
                Main.BlockSabo.Remove(pc.PlayerId);
                Main.BlockSabo.Add(pc.PlayerId, Utils.TimeStamp);
                pc.Notify(Translator.GetString("SecurityGuardSkillInUse"), SecurityGuardSkillDuration.GetFloat());
                pc.RpcRemoveAbilityUse();
            }
            else
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                    pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            var playerId = player.PlayerId;
            if (Main.BlockSabo.TryGetValue(playerId, out var stime) && stime + SecurityGuardSkillDuration.GetInt() < Utils.TimeStamp)
            {
                Main.BlockSabo.Remove(playerId);
                player.RpcResetAbilityCooldown();
                player.Notify(Translator.GetString("SecurityGuardSkillStop"));
            }
        }
    }
}