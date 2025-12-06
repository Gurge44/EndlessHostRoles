using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Crewmate;

internal class Lighter : RoleBase
{
    public static bool On;
    private long ActivateTimeStamp;
    private bool IsAbilityActive;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(6850, TabGroup.CrewmateRoles, CustomRoles.Lighter);

        LighterSkillCooldown = new FloatOptionItem(6852, "LighterSkillCooldown", new(0f, 180f, 1f), 25f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Seconds);

        LighterSkillDuration = new FloatOptionItem(6853, "LighterSkillDuration", new(0f, 180f, 1f), 10f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Seconds);

        LighterVisionNormal = new FloatOptionItem(6854, "LighterVisionNormal", new(0f, 5f, 0.05f), 0.9f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Multiplier);

        LighterVisionOnLightsOut = new FloatOptionItem(6855, "LighterVisionOnLightsOut", new(0f, 5f, 0.05f), 0.35f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Multiplier);

        LighterSkillMaxOfUsage = new IntegerOptionItem(6856, "AbilityUseLimit", new(0, 30, 1), 2, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Times);

        LighterAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(6857, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1.5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Times);

        LighterAbilityChargesWhenFinishedTasks = new FloatOptionItem(6858, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lighter])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(LighterSkillMaxOfUsage.GetFloat());
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
            AURoleOptions.EngineerInVentMaxTime = 1f;
            AURoleOptions.EngineerCooldown = LighterSkillCooldown.GetFloat();
        }

        if (IsAbilityActive)
        {
            opt.SetVision(false);

            if (Utils.IsActive(SystemTypes.Electrical))
                opt.SetFloat(FloatOptionNames.CrewLightMod, LighterVisionOnLightsOut.GetFloat() * 5);
            else
                opt.SetFloat(FloatOptionNames.CrewLightMod, LighterVisionNormal.GetFloat());
        }
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var progressText = new StringBuilder();

        progressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, IsAbilityActive));
        progressText.Append(Utils.GetTaskCount(playerId, comms));

        return progressText.ToString();
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
        if (UsePets.GetBool()) return;
        Light(pc);
    }

    private void Light(PlayerControl pc)
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
            pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
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

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}