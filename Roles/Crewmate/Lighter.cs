using AmongUs.GameOptions;
using System.Text;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal class Lighter : RoleBase
    {
        private bool IsAbilityActive;
        private long ActivateTimeStamp;

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(6850, TabGroup.CrewmateRoles, CustomRoles.Lighter, 1);
            LighterSkillCooldown = FloatOptionItem.Create(6852, "LighterSkillCooldown", new(0f, 180f, 1f), 25f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
                .SetValueFormat(OptionFormat.Seconds);
            LighterSkillDuration = FloatOptionItem.Create(6853, "LighterSkillDuration", new(0f, 180f, 1f), 10f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
                .SetValueFormat(OptionFormat.Seconds);
            LighterVisionNormal = FloatOptionItem.Create(6854, "LighterVisionNormal", new(0f, 5f, 0.05f), 0.9f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
                .SetValueFormat(OptionFormat.Multiplier);
            LighterVisionOnLightsOut = FloatOptionItem.Create(6855, "LighterVisionOnLightsOut", new(0f, 5f, 0.05f), 0.35f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
                .SetValueFormat(OptionFormat.Multiplier);
            LighterSkillMaxOfUseage = IntegerOptionItem.Create(6856, "AbilityUseLimit", new(0, 180, 1), 2, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
                .SetValueFormat(OptionFormat.Times);
            LighterAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(6857, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1.5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
                .SetValueFormat(OptionFormat.Times);
            LighterAbilityChargesWhenFinishedTasks = FloatOptionItem.Create(6858, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(LighterSkillMaxOfUseage.GetInt());
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
            if (!UsePets.GetBool())
            {
                AURoleOptions.EngineerInVentMaxTime = 1;
                AURoleOptions.EngineerCooldown = LighterSkillCooldown.GetFloat();
            }

            if (IsAbilityActive)
            {
                opt.SetVision(false);
                if (Utils.IsActive(SystemTypes.Electrical)) opt.SetFloat(FloatOptionNames.CrewLightMod, LighterVisionOnLightsOut.GetFloat() * 5);
                else opt.SetFloat(FloatOptionNames.CrewLightMod, LighterVisionNormal.GetFloat());
            }
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var ProgressText = new StringBuilder();

            ProgressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, IsAbilityActive));
            ProgressText.Append(Utils.GetTaskCount(playerId, comms));

            return ProgressText.ToString();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool())
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

                pc.Notify(Translator.GetString("LighterSkillInUse"), LighterSkillDuration.GetFloat());
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

            if (IsAbilityActive && ActivateTimeStamp + LighterSkillDuration.GetInt() < Utils.TimeStamp)
            {
                IsAbilityActive = false;
                player.RpcResetAbilityCooldown();
                player.Notify(Translator.GetString("LighterSkillStop"));
                player.MarkDirtySettings();
            }
        }
    }
}