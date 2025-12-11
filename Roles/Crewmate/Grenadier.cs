using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Crewmate;

internal class Grenadier : RoleBase
{
    public static Dictionary<byte, long> GrenadierBlinding = [];
    public static Dictionary<byte, long> MadGrenadierBlinding = [];

    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(6800, TabGroup.CrewmateRoles, CustomRoles.Grenadier);

        GrenadierSkillCooldown = new FloatOptionItem(6810, "GrenadierSkillCooldown", new(0f, 180f, 1f), 25f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Seconds);

        GrenadierSkillDuration = new FloatOptionItem(6811, "GrenadierSkillDuration", new(0f, 180f, 1f), 10f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Seconds);

        GrenadierCauseVision = new FloatOptionItem(6812, "GrenadierCauseVision", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Multiplier);

        GrenadierCanAffectNeutral = new BooleanOptionItem(6813, "GrenadierCanAffectNeutral", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier]);

        GrenadierSkillMaxOfUsage = new IntegerOptionItem(6814, "GrenadierSkillMaxOfUsage", new(0, 30, 1), 2, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Times);

        GrenadierAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(6815, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Times);

        GrenadierAbilityChargesWhenFinishedTasks = new FloatOptionItem(6816, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(GrenadierSkillMaxOfUsage.GetFloat());
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = GrenadierSkillCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var progressText = new StringBuilder();

        progressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, GrenadierBlinding.ContainsKey(playerId)));
        progressText.Append(Utils.GetTaskCount(playerId, comms));

        return progressText.ToString();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (UsePets.GetBool())
            hud.PetButton.buttonLabelText.text = Translator.GetString("GrenadierVentButtonText");
        else
            hud.AbilityButton.buttonLabelText.text = Translator.GetString("GrenadierVentButtonText");
    }

    public override void OnPet(PlayerControl pc)
    {
        BlindPlayers(pc);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (UsePets.GetBool()) return;
        BlindPlayers(pc);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask) return;

        byte playerId = player.PlayerId;
        long now = Utils.TimeStamp;

        if (GrenadierBlinding.TryGetValue(playerId, out long gtime) && gtime + GrenadierSkillDuration.GetInt() < now)
        {
            GrenadierBlinding.Remove(playerId);
            player.RpcResetAbilityCooldown();
            player.Notify(string.Format(Translator.GetString("GrenadierSkillStop"), (int)player.GetAbilityUseLimit()));
            Utils.MarkEveryoneDirtySettingsV3();
        }

        if (MadGrenadierBlinding.TryGetValue(playerId, out long mgtime) && mgtime + GrenadierSkillDuration.GetInt() < now)
        {
            MadGrenadierBlinding.Remove(playerId);
            player.RpcResetAbilityCooldown();
            player.Notify(string.Format(Translator.GetString("GrenadierSkillStop"), (int)player.GetAbilityUseLimit()));
            Utils.MarkEveryoneDirtySettingsV3();
        }
    }

    private static void BlindPlayers(PlayerControl pc)
    {
        if (GrenadierBlinding.ContainsKey(pc.PlayerId) || MadGrenadierBlinding.ContainsKey(pc.PlayerId)) return;

        if (pc.GetAbilityUseLimit() >= 1)
        {
            if (pc.Is(CustomRoles.Madmate))
            {
                MadGrenadierBlinding[pc.PlayerId] = Utils.TimeStamp;
                Main.AllPlayerControls.Where(x => x.IsModdedClient()).Where(x => !x.GetCustomRole().IsImpostorTeam() && !x.Is(CustomRoles.Madmate)).Do(x => x.RPCPlayCustomSound("FlashBang"));
            }
            else
            {
                GrenadierBlinding[pc.PlayerId] = Utils.TimeStamp;
                Main.AllPlayerControls.Where(x => x.IsModdedClient()).Where(x => x.IsImpostor() || (x.GetCustomRole().IsNeutral() && GrenadierCanAffectNeutral.GetBool())).Do(x => x.RPCPlayCustomSound("FlashBang"));
            }

            pc.RPCPlayCustomSound("FlashBang");
            pc.Notify(Translator.GetString("GrenadierSkillInUse"), GrenadierSkillDuration.GetFloat());
            pc.RpcRemoveAbilityUse();
            Utils.MarkEveryoneDirtySettingsV3();
        }
        else
            pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}