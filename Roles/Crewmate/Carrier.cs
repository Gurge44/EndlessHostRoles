using AmongUs.GameOptions;

namespace EHR.Crewmate;

public class Carrier : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    private Vector2? Location;
    private bool TaskMode;
    private int Count;

    public override void SetupCustomOption()
    {
        StartSetup(657000)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.3f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Location = null;
        TaskMode = false;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetInt();
        opt.SetVision(false);
    }

    public override void OnPet(PlayerControl pc)
    {
        Location = pc.Pos();
        pc.Notify(Translator.GetString("MarkDone"));
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting || !Location.HasValue) return true;
        target.TP(Location.Value);
        target.Notify(Translator.GetString("Carrier.TargetNotify"));
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!Main.IntroDestroyed || !GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;

        if (Count++ < 40) return;

        Count = 0;

        switch (TaskMode)
        {
            case true when (pc.GetAbilityUseLimit() >= 1 || pc.GetTaskState().IsTaskFinished) && pc.IsAlive():
                pc.RpcChangeRoleBasis(CustomRoles.Carrier);
                TaskMode = false;
                break;
            case false when !pc.IsAlive():
                pc.RpcSetRoleGlobal(RoleTypes.CrewmateGhost);
                TaskMode = true;
                break;
            case false when pc.GetAbilityUseLimit() < 1 && pc.IsAlive():
                pc.RpcSetRoleGlobal(RoleTypes.Crewmate, setRoleMap: true);
                pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
                TaskMode = true;
                break;
        }
    }
}