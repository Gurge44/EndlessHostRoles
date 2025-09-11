using AmongUs.GameOptions;
using EHR.Modules;
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
    private long TimerEndTS;
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
        ResetTimer();
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
        KillingPhase = false;
        killer.RpcChangeRoleBasis(CanVent.GetBool() ? CustomRoles.EngineerEHR : CustomRoles.CrewmateEHR);
        killer.RpcResetTasks();
        ResetTimer();
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        KillingPhase = true;
        pc.RpcChangeRoleBasis(CustomRoles.Duality);
        ResetTimer();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
        
        long now = Utils.TimeStamp;
        if (LastUpdateTS == now) return;
        LastUpdateTS = now;

        if (now >= TimerEndTS)
        {
            pc.Suicide();
            if (pc.IsLocalPlayer()) Achievements.Type.OutOfTime.Complete();
            return;
        }
        
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override void AfterMeetingTasks()
    {
        if (TimerEndTS - Utils.TimeStamp < 8)
        {
            TimerEndTS = Utils.TimeStamp + 8;
            Utils.SendRPC(CustomRPC.SyncRoleData, DualityId, TimerEndTS, KillingPhase);
        }
    }

    void ResetTimer()
    {
        TimerEndTS = Utils.TimeStamp + Time.GetInt() + (Main.IntroDestroyed ? 1 : 12);
        Utils.SendRPC(CustomRPC.SyncRoleData, DualityId, TimerEndTS, KillingPhase);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        TimerEndTS = long.Parse(reader.ReadString());
        KillingPhase = reader.ReadBoolean();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != DualityId || seer.PlayerId != target.PlayerId || (!hud && seer.IsModdedClient()) || meeting || !seer.IsAlive()) return string.Empty;
        return $"<size=80%>{string.Format(Translator.GetString(KillingPhase ? "Duality.MustKill" : "Duality.MustDoTask"), TimerEndTS - Utils.TimeStamp)}</size>";
    }
}