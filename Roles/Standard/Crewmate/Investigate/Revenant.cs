namespace EHR.Roles;

internal class Revenant : RoleBase
{
    public static bool On;
    public static OptionItem KnowInfo;

    private static readonly string[] KnowInfoMode =
    [
        "Alignments",
        "Roles"
    ];

    private byte RevenantId;
    private bool ShouldBeRevived;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(659100)
            .AutoSetupOption(ref KnowInfo, 1, KnowInfoMode)
            .CreateOverrideTasksData();
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        RevenantId = playerId;
        ShouldBeRevived = false;
    }

    public override void OnReportDeadBody()
    {
        PlayerControl pc = RevenantId.GetPlayer();

        if (pc && pc.IsAlive() && !pc.AllTasksCompleted())
        {
            ShouldBeRevived = true;
            pc.RpcExiled();
            PlayerState state = Main.PlayerStates[RevenantId];
            state.deathReason = PlayerState.DeathReason.Suicide;
            state.SetDead();
        }
    }

    public override void AfterMeetingTasks()
    {
        if (!ShouldBeRevived) return;
        
        LateTask.New(() =>
        {
            if (GameStates.IsEnded || !GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;

            PlayerControl pc = RevenantId.GetPlayer();

            if (pc)
            {
                pc.RpcRevive();
                pc.TPToRandomVent();
            }
        }, 2f, "Revenant Revive Delay");
    }
}