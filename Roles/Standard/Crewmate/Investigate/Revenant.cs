using EHR.Modules;
using Hazel;

namespace EHR.Roles;

using static Options;
using static Utils;

internal class Revenant : RoleBase
{
    public static bool On;
    public static OptionItem KnowInfo;
    private static OptionItem RemainingTasksToBeFound;

    private static readonly string[] KnowInfoMode =
    [
        "Alignments",
        "Roles"
    ];

    public bool TaskDone;
    private bool StillAlive;
    private bool IsExposed;
    private byte RevenantId;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        int id = 659500;
        SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Revenant);

        KnowInfo = new StringOptionItem(id++, "Revenant.KnowInfo", KnowInfoMode, 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revenant]);

        RemainingTasksToBeFound = new IntegerOptionItem(id++, "SnitchRemainingTaskFound", new(0, 10, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revenant]);

        Options.OverrideTasksData.Create(id++, TabGroup.CrewmateRoles, CustomRoles.Revenant);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        TaskDone = false;
        StillAlive = false;
        IsExposed = false;
        RevenantId = playerId;
    }

    public override void OnReportDeadBody()
    {
        if (!TaskDone && RevenantId.GetPlayer().IsAlive())
        {
            PlayerState state = Main.PlayerStates[RevenantId];
            state.deathReason = PlayerState.DeathReason.Suicide;
            state.SetDead();
            StillAlive = true;
            SendRPC(CustomRPC.SyncRoleData, RevenantId, TaskDone, StillAlive, IsExposed);
        }
    }

    public override void AfterMeetingTasks()
    {
        LateTask.New(() =>
        {
            if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
            if (!Main.PlayerStates.TryGetValue(RevenantId, out var state)) return;

            PlayerControl pc = RevenantId.GetPlayer();

            if (pc && StillAlive)
            {
                RPC.PlaySoundRPC(RevenantId, Sounds.SpawnSound);
                GhostRolesManager.RemoveGhostRole(RevenantId);
                pc.RpcRevive();
                pc.TPToRandomVent();
                StillAlive = false;
                SendRPC(CustomRPC.SyncRoleData, RevenantId, TaskDone, StillAlive, IsExposed);
            }
        }, 2f, "Revive Delay");
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (!pc.IsAlive() && !GameStates.IsMeeting) return;

        if (totalTaskCount - (completedTaskCount + 1) <= RemainingTasksToBeFound.GetInt() && !IsExposed)
        {
            foreach (PlayerControl target in Main.CachedAlivePlayerControls())
            {
                TargetArrow.Add(target.PlayerId, pc.PlayerId);
                NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
            }
            IsExposed = true;
        }

        if (completedTaskCount + 1 >= totalTaskCount)
        {
            TaskDone = true;
            pc.Notify(Translator.GetString("RevenantDoneTasks"));
        }
        SendRPC(CustomRPC.SyncRoleData, RevenantId, TaskDone, StillAlive, IsExposed);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        TaskDone = reader.ReadBoolean();
        StillAlive = reader.ReadBoolean();
        IsExposed = reader.ReadBoolean();
    }
}