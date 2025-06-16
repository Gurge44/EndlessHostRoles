using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Astral : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    public static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;
    private byte AstralId;

    private long BackTS;
    private long LastNotifyTS;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(651400)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10, new IntegerValueRule(0, 30, 1), OptionFormat.Seconds)
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
        BackTS = 0;
        AstralId = playerId;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = AbilityCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 0.1f;

        try { AURoleOptions.GuardianAngelCooldown = 900f; }
        catch { }
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        LateTask.New(() => BecomeGhostTemporarily(pc), 2f, log: false);
    }

    public override void OnPet(PlayerControl pc)
    {
        BecomeGhostTemporarily(pc);
    }

    void BecomeGhostTemporarily(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1f) return;
        pc.RpcRemoveAbilityUse();

        pc.Exiled();
        CustomRpcSender.Create("Astral", (SendOption)1).AutoStartRpc(pc.NetId, 4).EndRpc().SendMessage();
        LateTask.New(() => pc.RpcSetRoleDesync(RoleTypes.GuardianAngel, pc.OwnerId), 0.2f, log: false);
        LateTask.New(pc.RpcResetAbilityCooldown, 0.4f, log: false);
        pc.MarkDirtySettings();

        BackTS = Utils.TimeStamp + AbilityDuration.GetInt() + 1;
        Utils.SendRPC(CustomRPC.SyncRoleData, AstralId, BackTS);
    }

    void BecomeAliveAgain(PlayerControl pc, bool onMeeting = false)
    {
        BackTS = 0;
        Utils.SendRPC(CustomRPC.SyncRoleData, AstralId, BackTS);

        GhostRolesManager.RemoveGhostRole(pc.PlayerId);
        pc.RpcChangeRoleBasis(pc.GetRoleMap().CustomRole);
        Camouflage.RpcSetSkin(pc);

        if (onMeeting) return;

        pc.SyncSettings();

        if (!Options.UsePets.GetBool())
            pc.RpcResetAbilityCooldown();

        Utils.NotifyRoles(SpecifySeer: pc);
        Utils.NotifyRoles(SpecifyTarget: pc);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.IsAlive() || BackTS == 0) return;

        long now = Utils.TimeStamp;

        if (BackTS > now)
        {
            if (LastNotifyTS != now)
            {
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                LastNotifyTS = now;
            }

            return;
        }

        BecomeAliveAgain(pc);
    }

    public override void OnReportDeadBody()
    {
        if (BackTS != 0) BecomeAliveAgain(AstralId.GetPlayer(), true);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        BackTS = long.Parse(reader.ReadString());
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != AstralId || seer.PlayerId != target.PlayerId || hud || meeting || BackTS == 0) return string.Empty;
        return string.Format(Translator.GetString("AstralSuffix"), BackTS - Utils.TimeStamp);
    }
}