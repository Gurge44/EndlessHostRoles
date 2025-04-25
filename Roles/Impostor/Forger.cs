using System.Collections.Generic;

namespace EHR.Impostor;

public class Forger : RoleBase
{
    public static bool On;

    public static Dictionary<byte, CustomRoles> Forges = [];

    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(651700)
            .AutoSetupOption(ref AbilityUseLimit, 1, new IntegerValueRule(0, 20, 1), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.4f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        Forges = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
    }
}