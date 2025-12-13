using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Crewmate;

internal class SecurityGuard : RoleBase
{
    public static Dictionary<byte, long> BlockSabo = [];

    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(6860, TabGroup.CrewmateRoles, CustomRoles.SecurityGuard);

        SecurityGuardSkillCooldown = new FloatOptionItem(6862, "SecurityGuardSkillCooldown", new(0f, 180f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
            .SetValueFormat(OptionFormat.Seconds);

        SecurityGuardSkillDuration = new FloatOptionItem(6863, "SecurityGuardSkillDuration", new(0f, 180f, 1f), 10f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
            .SetValueFormat(OptionFormat.Seconds);

        SecurityGuardSkillMaxOfUsage = new IntegerOptionItem(6866, "AbilityUseLimit", new(0, 30, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
            .SetValueFormat(OptionFormat.Times);

        SecurityGuardAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(6867, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.4f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
            .SetValueFormat(OptionFormat.Times);

        SecurityGuardAbilityChargesWhenFinishedTasks = new FloatOptionItem(6868, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SecurityGuard])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(SecurityGuardSkillMaxOfUsage.GetFloat());
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsePets.GetBool()) return;

        AURoleOptions.EngineerInVentMaxTime = 1f;
        AURoleOptions.EngineerCooldown = SecurityGuardSkillCooldown.GetFloat();
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var progressText = new StringBuilder();

        progressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, BlockSabo.ContainsKey(playerId)));
        progressText.Append(Utils.GetTaskCount(playerId, comms));

        return progressText.ToString();
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

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (UsePets.GetBool()) return;
        Guard(pc);
    }

    private static void Guard(PlayerControl pc)
    {
        if (BlockSabo.ContainsKey(pc.PlayerId)) return;

        if (pc.GetAbilityUseLimit() >= 1)
        {
            BlockSabo[pc.PlayerId] = Utils.TimeStamp;
            pc.Notify(Translator.GetString("SecurityGuardSkillInUse"), SecurityGuardSkillDuration.GetFloat());
            pc.RpcRemoveAbilityUse();
        }
        else
            pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        byte playerId = player.PlayerId;

        if (BlockSabo.TryGetValue(playerId, out long stime) && stime + SecurityGuardSkillDuration.GetInt() < Utils.TimeStamp)
        {
            BlockSabo.Remove(playerId);
            player.RpcResetAbilityCooldown();
            player.Notify(Translator.GetString("SecurityGuardSkillStop"));
        }
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}