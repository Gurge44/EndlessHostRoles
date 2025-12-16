using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Crewmate;

internal class Veteran : RoleBase
{
    public static Dictionary<byte, long> VeteranInProtect = [];

    public static bool On;
    public override bool IsEnable => On;
    
    public static OptionItem VeteranSkillCooldown;
    public static OptionItem VeteranSkillDuration;
    public static OptionItem VeteranSkillMaxOfUsage;
    public static OptionItem VeteranAbilityUseGainWithEachTaskCompleted;
    public static OptionItem VeteranAbilityChargesWhenFinishedTasks;
    public static OptionItem VeteranAlertActivatesOnNonKillingInteractions;

    public override void SetupCustomOption()
    {
        const int id = 652200;
        SetupRoleOptions(id, TabGroup.CrewmateRoles, CustomRoles.Veteran);

        VeteranSkillCooldown = new FloatOptionItem(id + 2, "VeteranSkillCooldown", new(0f, 180f, 1f), 20f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
            .SetValueFormat(OptionFormat.Seconds);

        VeteranSkillDuration = new FloatOptionItem(id + 3, "VeteranSkillDuration", new(0f, 180f, 1f), 10f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
            .SetValueFormat(OptionFormat.Seconds);

        VeteranSkillMaxOfUsage = new IntegerOptionItem(id + 4, "VeteranSkillMaxOfUsage", new(0, 30, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
            .SetValueFormat(OptionFormat.Times);

        VeteranAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(id + 5, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
            .SetValueFormat(OptionFormat.Times);

        VeteranAbilityChargesWhenFinishedTasks = new FloatOptionItem(id + 6, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
            .SetValueFormat(OptionFormat.Times);
        
        VeteranAlertActivatesOnNonKillingInteractions = new BooleanOptionItem(id + 7, "VeteranAlertActivatesOnNonKillingInteractions", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(VeteranSkillMaxOfUsage.GetFloat());
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = VeteranSkillCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var progressText = new StringBuilder();

        progressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, VeteranInProtect.ContainsKey(playerId)));
        progressText.Append(Utils.GetTaskCount(playerId, comms));

        return progressText.ToString();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (UsePets.GetBool())
            hud.PetButton.buttonLabelText.text = Translator.GetString("VeteranVentButtonText");
        else
            hud.AbilityButton.buttonLabelText.text = Translator.GetString("VeteranVentButtonText");
    }

    public override void OnPet(PlayerControl pc)
    {
        Alert(pc);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (UsePets.GetBool()) return;
        Alert(pc);
    }

    private static void Alert(PlayerControl pc)
    {
        if (VeteranInProtect.ContainsKey(pc.PlayerId)) return;

        if (pc.GetAbilityUseLimit() >= 1)
        {
            VeteranInProtect[pc.PlayerId] = Utils.TimeStamp;
            pc.RpcRemoveAbilityUse();
            pc.RPCPlayCustomSound("Gunload");
            pc.Notify(Translator.GetString("VeteranOnGuard"), VeteranSkillDuration.GetFloat());
            pc.MarkDirtySettings();
        }
        else
            pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (!killer.IsAlive()) return false;
        
        if (VeteranInProtect.ContainsKey(target.PlayerId) && killer.PlayerId != target.PlayerId)
        {
            if (!killer.Is(CustomRoles.Pestilence))
            {
                killer.SetRealKiller(target);
                target.Kill(killer);
                Logger.Info($"{target.GetRealName()} reverse killed: {killer.GetRealName()}", "Veteran Kill");
                return false;
            }

            target.SetRealKiller(killer);
            killer.Kill(target);
            Logger.Info($"{target.GetRealName()} reverse reverse killed: {target.GetRealName()}", "Pestilence Reflect");

            if (killer.AmOwner)
                Achievements.Type.YoureTooLate.Complete();

            return false;
        }

        return true;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        byte playerId = player.PlayerId;

        if (VeteranInProtect.TryGetValue(playerId, out long vtime) && vtime + VeteranSkillDuration.GetInt() < Utils.TimeStamp)
        {
            VeteranInProtect.Remove(playerId);
            player.RpcResetAbilityCooldown();
            player.Notify(string.Format(Translator.GetString("VeteranOffGuard"), (int)player.GetAbilityUseLimit()));
        }
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = true;
        countsAs = 1;
    }
}