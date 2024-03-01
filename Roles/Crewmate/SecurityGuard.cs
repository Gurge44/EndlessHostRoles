using System.Text;
using AmongUs.GameOptions;

namespace TOHE.Roles.Crewmate
{
    internal class SecurityGuard : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(Options.SecurityGuardSkillMaxOfUseage.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerInVentMaxTime = 1;
            AURoleOptions.EngineerCooldown = Options.SecurityGuardSkillCooldown.GetFloat();
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var ProgressText = new StringBuilder();

            ProgressText.Append(Utils.GetTaskCount(playerId, comms));
            ProgressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, Main.BlockSabo.ContainsKey(playerId)));

            return ProgressText.ToString();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool())
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
                pc.Notify(Translator.GetString("SecurityGuardSkillInUse"), Options.SecurityGuardSkillDuration.GetFloat());
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
            if (Main.BlockSabo.TryGetValue(playerId, out var stime) && stime + Options.SecurityGuardSkillDuration.GetInt() < Utils.TimeStamp)
            {
                Main.BlockSabo.Remove(playerId);
                player.RpcResetAbilityCooldown();
                player.Notify(Translator.GetString("SecurityGuardSkillStop"));
            }
        }
    }
}
