using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class Duality : RoleBase
{
    public static bool On;

    private static OptionItem Time;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;

    public override bool IsEnable => On;

    public bool KillingPhase;
    private CountdownTimer Timer;
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
        if (!AmongUsClient.Instance.AmHost) return;
        ResetTimer(8);
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

    public override void AfterMeetingTasks()
    {
        ResetTimer();
    }

    void ResetTimer(int add = 0)
    {
        var pc = DualityId.GetPlayer();
        if (pc == null || !pc.IsAlive()) return;
        Timer?.Dispose();
        Timer = new CountdownTimer(Time.GetInt() + add, () =>
        {
            if (pc == null || !pc.IsAlive()) return;
            pc.Suicide();
            if (pc.AmOwner) Achievements.Type.OutOfTime.Complete();
            Timer = null;
        }, onTick: () =>
        {
            if (pc == null || !pc.IsAlive())
            {
                Timer.Dispose();
                return;
            }

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, onCanceled: () => Timer = null);
        Utils.SendRPC(CustomRPC.SyncRoleData, DualityId, KillingPhase);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        KillingPhase = reader.ReadBoolean();
        Timer?.Dispose();
        Timer = new CountdownTimer(Time.GetInt(), () => Timer = null, onCanceled: () => Timer = null);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != DualityId || seer.PlayerId != target.PlayerId || (!hud && seer.IsModdedClient()) || meeting || !seer.IsAlive()) return string.Empty;
        return $"<size=80%>{string.Format(Translator.GetString(KillingPhase ? "Duality.MustKill" : "Duality.MustDoTask"), (int)Timer.Remaining.TotalSeconds)}</size>";
    }
}