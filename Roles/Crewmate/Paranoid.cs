using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Crewmate;

internal class Paranoid : RoleBase
{
    public static Dictionary<byte, int> ParaUsedButtonCount = [];

    
    public static OptionItem ParanoidAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    
    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(7800, TabGroup.CrewmateRoles, CustomRoles.Paranoid);

        ParanoidNumOfUseButton = new FloatOptionItem(7810, "ParanoidNumOfUseButton", new(0, 90, 1), 3, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Paranoid])
            .SetValueFormat(OptionFormat.Times);

        ParanoidVentCooldown = new FloatOptionItem(7811, "ParanoidVentCooldown", new(0, 180, 1), 10, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Paranoid])
            .SetValueFormat(OptionFormat.Seconds);
        
        ParanoidAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(7812, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.4f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Paranoid])
            .SetValueFormat(OptionFormat.Times);
        
        AbilityChargesWhenFinishedTasks = new FloatOptionItem(7813, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Paranoid])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Add(byte playerId)
    {
        On = true;
        ParaUsedButtonCount[playerId] = 0;
        playerId.SetAbilityUseLimit(ParanoidNumOfUseButton.GetFloat());
    }

    public override void Init()
    {
        On = false;
    }

    public override void Remove(byte playerId)
    {
        ParaUsedButtonCount.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown =
            !ParaUsedButtonCount.TryGetValue(playerId, out int count2) || count2 < ParanoidNumOfUseButton.GetInt()
                ? ParanoidVentCooldown.GetFloat()
                : 300f;

        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (UsePets.GetBool())
            hud.PetButton.buttonLabelText.text = Translator.GetString("ParanoidVentButtonText");
        else
            hud.AbilityButton.buttonLabelText.text = Translator.GetString("ParanoidVentButtonText");
    }

    public override void OnPet(PlayerControl pc)
    {
        Panic(pc);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (UsePets.GetBool()) return;
        pc.MyPhysics?.RpcBootFromVent(vent.Id);
        Panic(pc);
    }

    private static void Panic(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() >= 1)
        {
            pc.RpcRemoveAbilityUse();
            if (AmongUsClient.Instance.AmHost) LateTask.New(() => Utils.SendMessage(Translator.GetString("SkillUsedLeft") + (ParanoidNumOfUseButton.GetInt() - ParaUsedButtonCount[pc.PlayerId]), pc.PlayerId), 4f, "Paranoid Skill Remain Message");
            pc.NoCheckStartMeeting(pc.Data);
        }
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}