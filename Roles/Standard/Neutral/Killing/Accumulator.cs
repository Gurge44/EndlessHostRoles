using System;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

public class Accumulator : RoleBase
{
    public static bool On;

    private static OptionItem StartingKillCooldown;
    private static OptionItem KillCooldownDecrementWithEachTask;
    public static OptionItem CanVentBeforeKilling;
    private static OptionItem VentCooldown;
    private static OptionItem MaxInVentTime;
    private static OptionItem CanVentAfterKilling;
    private static OptionItem ImpostorVisionBeforeKilling;
    private static OptionItem ImpostorVisionAfterKilling;

    public override bool IsEnable => On;

    private bool Killing;
    private float KCD;
    private byte AccumulatorId;

    public override void SetupCustomOption()
    {
        StartSetup(657700)
            .AutoSetupOption(ref StartingKillCooldown, 30f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldownDecrementWithEachTask, 3f, new FloatValueRule(0f, 30f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVentBeforeKilling, false)
            .AutoSetupOption(ref VentCooldown, 0f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds, overrideParent: CanVentBeforeKilling)
            .AutoSetupOption(ref MaxInVentTime, 0f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds, overrideParent: CanVentBeforeKilling)
            .AutoSetupOption(ref CanVentAfterKilling, true)
            .AutoSetupOption(ref ImpostorVisionBeforeKilling, false)
            .AutoSetupOption(ref ImpostorVisionAfterKilling, true)
            .CreateOverrideTasksData();
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Killing = false;
        KCD = StartingKillCooldown.GetFloat();
        AccumulatorId = playerId;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KCD;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return Killing;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(Killing ? ImpostorVisionAfterKilling.GetBool() : ImpostorVisionBeforeKilling.GetBool());

        if (!Killing && CanVentBeforeKilling.GetBool())
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetFloat();
        }
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return Killing && CanVentAfterKilling.GetBool();
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        KCD -= KillCooldownDecrementWithEachTask.GetFloat();
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, KCD, Killing);
        
        if (completedTaskCount + 1 >= totalTaskCount)
            OnPet(pc);
    }

    public override void OnPet(PlayerControl pc)
    {
        if (Killing) return;
        Killing = true;
        pc.RpcSetRoleDesync(RoleTypes.Impostor, pc.OwnerId, setRoleMap: true);
        LateTask.New(() => pc.SetKillCooldown(KCD), 0.2f);
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, KCD, Killing);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        KCD = reader.ReadSingle();
        Killing = reader.ReadBoolean();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != AccumulatorId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || Killing) return string.Empty;
        return string.Format(Translator.GetString("KCD"), Math.Round(Math.Max(0, KCD), 1));
    }
}