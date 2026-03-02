using AmongUs.GameOptions;
using System.Linq;

namespace EHR.Roles;

public class Fakeshifter : RoleBase
{
    public static bool On;
    private const int Id = 697000;

    public override bool IsEnable => On;

    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    
    private byte MarkedId;

    public override void SetupCustomOption()
    {
        StartSetup(Id)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds, overrideName: "FakeshifterAbilityDuration")
            .AutoSetupOption(ref AbilityUseLimit, 3f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times);
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

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (MarkedId == byte.MaxValue && target.IsAlive())
        {
            MarkedId = target.PlayerId;
            PlayerControl randomPlayer = Main.EnumerateAlivePlayerControls().Without(target).RandomElement();
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (pc.PlayerId == target.PlayerId) continue;
                target.RpcShapeshiftDesync(randomPlayer, pc, false);
            }
            shapeshifter.RpcRemoveAbilityUse();
            LateTask.New(() =>
            {
                if (target != null && target.IsAlive())
                {
                    target.RpcShapeshift(target, true);
                    MarkedId = byte.MaxValue;
                }
            }, AbilityDuration.GetFloat(), "Fakeshifter Ability Finish");
        }
        return false;
    }
}