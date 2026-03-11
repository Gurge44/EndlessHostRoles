namespace EHR.Roles;

internal class Detour : RoleBase
{
    public static int TotalRedirections;
    public override bool IsEnable => false;

    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    public override void SetupCustomOption()
    {
        StartSetup(5590)
            .AutoSetupOption(ref AbilityUseLimit, 0f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.4f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times);
    }

    public override void Init()
    {
        TotalRedirections = 0;
    }

    public override void Add(byte playerId)
    {
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }
}