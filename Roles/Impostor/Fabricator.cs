using EHR.Modules;

namespace EHR.Impostor;

public class Fabricator : RoleBase
{
    public static bool On;

    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    public override bool IsEnable => On;

    public PlayerState.DeathReason NextDeathReason;

    public override void SetupCustomOption()
    {
        StartSetup(655900)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.5f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        NextDeathReason = PlayerState.DeathReason.Kill;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (NextDeathReason == PlayerState.DeathReason.Kill || killer.GetAbilityUseLimit() < 1 || !Main.PlayerStates.TryGetValue(target.PlayerId, out var state)) return;
        state.deathReason = NextDeathReason;
        RPC.SendDeathReason(target.PlayerId, state.deathReason);
        killer.RpcRemoveAbilityUse();
    }
}