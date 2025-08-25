using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Patches;

namespace EHR.Neutral;

internal class Cherokious : RoleBase
{
    public static bool On;

    public static OptionItem KillCooldown;
    public static OptionItem ImpostorVision;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        const int id = 13860;
        Options.SetupRoleOptions(id, TabGroup.NeutralRoles, CustomRoles.Cherokious);

        KillCooldown = new IntegerOptionItem(id + 2, "KillCooldown", new(0, 60, 1), 15, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Cherokious])
            .SetValueFormat(OptionFormat.Seconds);

        ImpostorVision = new BooleanOptionItem(id + 3, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Cherokious]);

        Options.OverrideTasksData.Create(id + 4, TabGroup.NeutralRoles, CustomRoles.Cherokious);
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(0);
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(ImpostorVision.GetBool());
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        pc.RpcIncreaseAbilityUseLimitBy(1);
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1) return;

        PlayerControl target = ExternalRpcPetPatch.SelectKillButtonTarget(pc);
        if (target.Is(CustomRoles.Spy) && !Spy.OnKillAttempt(pc, target)) return;
        if (target != null && pc.RpcCheckAndMurder(target)) pc.RpcRemoveAbilityUse();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return false;
    }
}