using AmongUs.GameOptions;
using System.Text;

namespace TOHE.Roles.Crewmate
{
    internal class Lighter : RoleBase
    {
        private bool IsAbilityActive;
        private long ActivateTimeStamp;

        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(Options.LighterSkillMaxOfUseage.GetInt());
            IsAbilityActive = false;
            ActivateTimeStamp = 0;
        }

        public override void Init()
        {
            On = false;
            IsAbilityActive = false;
            ActivateTimeStamp = 0;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerInVentMaxTime = 1;
            AURoleOptions.EngineerCooldown = Options.LighterSkillCooldown.GetFloat();

            if (IsAbilityActive)
            {
                opt.SetVisionV2();
                if (Utils.IsActive(SystemTypes.Electrical)) opt.SetFloat(FloatOptionNames.CrewLightMod, Options.LighterVisionOnLightsOut.GetFloat() * 5);
                else opt.SetFloat(FloatOptionNames.CrewLightMod, Options.LighterVisionNormal.GetFloat());
            }
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var ProgressText = new StringBuilder();

            ProgressText.Append(Utils.GetTaskCount(playerId, comms));
            ProgressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, IsAbilityActive));

            return ProgressText.ToString();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool())
                hud.PetButton.buttonLabelText.text = Translator.GetString("LighterVentButtonText");
            else
                hud.AbilityButton.buttonLabelText.text = Translator.GetString("LighterVentButtonText");
        }

        public override void OnPet(PlayerControl pc)
        {
            Light(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            Light(pc);
        }

        void Light(PlayerControl pc)
        {
            if (IsAbilityActive) return;
            if (pc.GetAbilityUseLimit() >= 1)
            {
                IsAbilityActive = true;
                ActivateTimeStamp = Utils.TimeStamp;

                pc.Notify(Translator.GetString("LighterSkillInUse"), Options.LighterSkillDuration.GetFloat());
                pc.RpcRemoveAbilityUse();
                pc.MarkDirtySettings();
            }
            else
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                    pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }

        public override void OnReportDeadBody()
        {
            IsAbilityActive = false;
            ActivateTimeStamp = 0;
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask) return;

            if (IsAbilityActive && ActivateTimeStamp + Options.LighterSkillDuration.GetInt() < Utils.TimeStamp)
            {
                IsAbilityActive = false;
                player.RpcResetAbilityCooldown();
                player.Notify(Translator.GetString("LighterSkillStop"));
                player.MarkDirtySettings();
            }
        }
    }
}
