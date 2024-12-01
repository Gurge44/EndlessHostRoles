using System.Linq;
using AmongUs.GameOptions;
using EHR.Impostor;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Crewmate;

internal class DovesOfNeace : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(7700, TabGroup.CrewmateRoles, CustomRoles.DovesOfNeace);

        DovesOfNeaceCooldown = new FloatOptionItem(7710, "DovesOfNeaceCooldown", new(0f, 180f, 1f), 7f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
            .SetValueFormat(OptionFormat.Seconds);

        DovesOfNeaceMaxOfUseage = new IntegerOptionItem(7711, "DovesOfNeaceMaxOfUseage", new(0, 180, 1), 0, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
            .SetValueFormat(OptionFormat.Times);

        DovesOfNeaceAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(7712, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
            .SetValueFormat(OptionFormat.Times);

        DovesOfNeaceAbilityChargesWhenFinishedTasks = new FloatOptionItem(7713, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
            .SetValueFormat(OptionFormat.Times);
    }


    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(DovesOfNeaceMaxOfUseage.GetInt());
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = DovesOfNeaceCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (UsePets.GetBool())
            hud.PetButton.buttonLabelText.text = Translator.GetString("DovesOfNeaceVentButtonText");
        else
            hud.AbilityButton.buttonLabelText.text = Translator.GetString("DovesOfNeaceVentButtonText");
    }

    public override void OnPet(PlayerControl pc)
    {
        ResetCooldowns(pc);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        ResetCooldowns(pc);
    }

    private static void ResetCooldowns(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1)
        {
            pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            return;
        }

        pc.RpcRemoveAbilityUse();
        bool isMadMate = pc.Is(CustomRoles.Madmate);

        Main.AllAlivePlayerControls
            .Where(x => isMadMate ? x.CanUseKillButton() && x.IsCrewmate() : x.CanUseKillButton())
            .Do(x =>
            {
                x.RPCPlayCustomSound("Dove");
                x.ResetKillCooldown();
                x.SetKillCooldown();
                if (Main.PlayerStates[x.PlayerId].Role is SerialKiller sk) sk.OnReportDeadBody();

                x.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.DovesOfNeace), Translator.GetString("DovesOfNeaceSkillNotify")));
            });

        pc.RPCPlayCustomSound("Dove");
        pc.Notify(string.Format(Translator.GetString("DovesOfNeaceOnGuard"), pc.GetAbilityUseLimit()));
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.GetClosestVent()?.Id == ventId;
    }
}