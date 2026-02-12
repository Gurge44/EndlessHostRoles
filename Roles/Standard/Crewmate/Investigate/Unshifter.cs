using AmongUs.GameOptions;
using Hazel;
using InnerNet;
using static EHR.Translator;

namespace EHR.Roles;

public class Unshifter : RoleBase
{
    private const int Id = 646900;
    private static OptionItem Cooldown;
    private static OptionItem UseLimit;
    private static OptionItem TargetKnows;
    private static bool On;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(Id)
            .AutoSetupOption(ref Cooldown, 25f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds, overrideName: "UnshifterCooldown")
            .AutoSetupOption(ref UseLimit, 3, new IntegerValueRule(0, 20, 1), OptionFormat.Times, overrideName: "UnshifterUseLimit")
            .AutoSetupOption(ref TargetKnows, true, overrideName: "UnshifterTargetKnows");
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(UseLimit.GetFloat());
    }

    public override void SetKillCooldown(byte playerId)
    {
        Main.AllPlayerKillCooldown[playerId] = Cooldown.GetFloat();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() >= 1;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsEnable || killer.GetAbilityUseLimit() <= 0)
            return false;

        if (!target.IsShifted())
        {
            killer.Notify(GetString("UnshifterTargetNotShifted"));
            return false;
        }

        target.RpcShapeshift(target, true);
        target.RpcResetAbilityCooldown();

        killer.RpcRemoveAbilityUse();
        killer.SetKillCooldown();

        killer.Notify(GetString("UnshifterSuccess"));

        if (TargetKnows.GetBool())
            target.Notify(GetString("UnshifterTargetNotify"));

        Logger.Info($"Unshifter: {killer.GetNameWithRole()} unshifted {target.GetNameWithRole()}", "Unshifter");
        return false;
    }
}