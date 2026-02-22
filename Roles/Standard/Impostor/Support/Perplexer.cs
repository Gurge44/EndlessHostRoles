using AmongUs.GameOptions;
using EHR.Modules;
using System.Linq;

namespace EHR.Roles;

public class Perplexer : RoleBase
{
    public static bool On;
    private const int Id = 698000;

    public override bool IsEnable => On;

    private static OptionItem KillCooldown;
    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    private byte MarkedId;

    public override void SetupCustomOption()
    {
        StartSetup(Id)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 3f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.5f, new FloatValueRule(0f, 5f, 0.25f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        MarkedId = byte.MaxValue;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetInt();
        AURoleOptions.ShapeshifterDuration = 0.1f;
    }
    
    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (shapeshifting && MarkedId == byte.MaxValue && target.IsAlive() && shapeshifter.GetAbilityUseLimit() >= 1f)
        {
            MarkedId = target.PlayerId;
            Main.AllPlayerSpeed[MarkedId] *= -1;
            target.MarkDirtySettings();
            shapeshifter.RpcRemoveAbilityUse();
            
            LateTask.New(() =>
            {
                if (Main.AllPlayerSpeed[MarkedId] < 0f) Main.AllPlayerSpeed[MarkedId] *= -1;
                MarkedId = byte.MaxValue;
                if (target && target.IsAlive()) target.MarkDirtySettings();
            }, AbilityDuration.GetFloat(), "Perplexer Revert Control Invert");
        }
        return false;
    }
}