using System.Diagnostics;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Neutral;

public class Duality : RoleBase
{
    public static bool On;

    private static OptionItem Time;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;

    public override bool IsEnable => On;

    public bool KillingPhase;
    private Stopwatch Timer;
    private long LastUpdateTS;
    private byte DualityId;

    public override void SetupCustomOption()
    {
        StartSetup(654700)
            .AutoSetupOption(ref Time, 40, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        KillingPhase = true;
        DualityId = playerId;
        Timer = new();
        LateTask.New(() => Timer = Stopwatch.StartNew(), 10f, log: false);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return KillingPhase;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = 3f;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());

        if (CanVent.GetBool() && !KillingPhase)
        {
            AURoleOptions.EngineerCooldown = 0f;
            AURoleOptions.EngineerInVentMaxTime = 0f;
        }
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        LateTask.New(() =>
        {
            KillingPhase = false;
            killer.RpcSetRoleGlobal(CanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate, setRoleMap: true);
            killer.RpcResetTasks();
            ResetTimer();
        }, 0.2f, log: false);
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        KillingPhase = true;
        pc.RpcSetRoleDesync(RoleTypes.Impostor, pc.OwnerId, setRoleMap: true);
        ResetTimer();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
        
        long now = Utils.TimeStamp;
        if (LastUpdateTS == now) return;
        LastUpdateTS = now;

        if (Timer.GetRemainingTime(Time.GetInt()) <= 0)
        {
            pc.Suicide();
            if (pc.AmOwner) Achievements.Type.OutOfTime.Complete();
            return;
        }
        
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override void OnReportDeadBody()
    {
        Timer.Reset();
    }

    public override void AfterMeetingTasks()
    {
        ResetTimer();
    }

    void ResetTimer()
    {
        Timer = Stopwatch.StartNew();
        Utils.SendRPC(CustomRPC.SyncRoleData, DualityId, KillingPhase);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        KillingPhase = reader.ReadBoolean();
        Timer = Stopwatch.StartNew();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != DualityId || seer.PlayerId != target.PlayerId || (!hud && seer.IsModdedClient()) || meeting || !seer.IsAlive()) return string.Empty;
        return $"<size=80%>{string.Format(Translator.GetString(KillingPhase ? "Duality.MustKill" : "Duality.MustDoTask"), Timer.GetRemainingTime(Time.GetInt()) - 1)}</size>";
    }
}