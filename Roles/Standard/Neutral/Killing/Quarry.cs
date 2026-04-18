using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class Quarry : RoleBase
{
    public static bool On;
    private static List<Quarry> Instances = [];

    private static OptionItem TargetHasArrowToQuarry;
    private static OptionItem CanVentDuringSeekTime;
    private static OptionItem TargetDiesOnMeetingCall;
    private static OptionItem SeekTime;
    private static OptionItem ShapeshiftCooldown;
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;
    private static OptionItem CooldownsResetEachOther;

    public override bool IsEnable => On;

    private byte QuarryId;
    public byte TargetId;
    private float TargetKCD;
    private CountdownTimer SeekTimer;

    public override void SetupCustomOption()
    {
        StartSetup(657800)
            .AutoSetupOption(ref TargetHasArrowToQuarry, true)
            .AutoSetupOption(ref CanVentDuringSeekTime, false)
            .AutoSetupOption(ref TargetDiesOnMeetingCall, false)
            .AutoSetupOption(ref SeekTime, 15, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref ShapeshiftCooldown, 15f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true)
            .AutoSetupOption(ref AbilityUseLimit, 2f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 1f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times)
            .AutoSetupOption(ref CooldownsResetEachOther, true);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        QuarryId = playerId;
        TargetId = byte.MaxValue;
        TargetKCD = 0f;
        SeekTimer = null;
        Instances.Add(this);
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());
        AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool() && (TargetId == byte.MaxValue || CanVentDuringSeekTime.GetBool());
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return target.PlayerId != TargetId;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (CooldownsResetEachOther.GetBool())
            killer.RpcResetAbilityCooldown();
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        if (shapeshifter.GetAbilityUseLimit() < 1) return false;
        shapeshifter.RpcRemoveAbilityUse();

        LateTask.New(() =>
        {
            Vector2 pos = shapeshifter.Pos();
            shapeshifter.TP(target);
            target.TP(pos);
        }, 0.2f);

        SeekTimer = new CountdownTimer(SeekTime.GetInt(), () =>
        {
            target.Suicide();
            TargetId = byte.MaxValue;
            SeekTimer = null;
            Utils.SendRPC(CustomRPC.SyncRoleData, QuarryId, 2);
        }, onTick: () =>
        {
            if (target == null || !target.IsAlive())
            {
                TargetId = byte.MaxValue;
                SeekTimer = null;
                Utils.SendRPC(CustomRPC.SyncRoleData, QuarryId, 2);
                return;
            }
            
            Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: shapeshifter);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
        }, cancelOnMeeting: false, onCanceled: () => SeekTimer = null);
        TargetId = target.PlayerId;
        TargetKCD = Main.KillTimers[TargetId];
        Utils.SendRPC(CustomRPC.SyncRoleData, QuarryId, 1, TargetId);
        Main.Instance.StartCoroutine(ContinuouslyResetAbilityCooldown());
        if (TargetHasArrowToQuarry.GetBool()) TargetArrow.Add(target.PlayerId, shapeshifter.PlayerId);

        var sender = CustomRpcSender.Create("QuarryTarget", SendOption.Reliable);
        var hasValue = false;
        hasValue |= sender.Notify(target, string.Format(Translator.GetString("Quarry.TargetSeekBeginNotify"), CustomRoles.Quarry.ToColoredString()));
        hasValue |= sender.RpcSetRole(target, RoleTypes.Impostor, target.OwnerId);
        hasValue |= sender.RpcSetRole(shapeshifter, RoleTypes.Crewmate, target.OwnerId);
        hasValue |= sender.SetKillCooldown(target, 0.01f);
        hasValue |= sender.NotifyRolesSpecific(target, shapeshifter, out sender, out bool cleared);
        if (cleared) hasValue = false;
        sender.SendMessage(dispose: !hasValue);
        
        if (CooldownsResetEachOther.GetBool())
            shapeshifter.SetKillCooldown();
        
        return false;

        IEnumerator ContinuouslyResetAbilityCooldown()
        {
            while (TargetId != byte.MaxValue)
            {
                int interval = ShapeshiftCooldown.GetInt() - 2;
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed.Seconds < interval) yield return null;
                if (TargetId == byte.MaxValue) yield break;
                shapeshifter.RpcResetAbilityCooldown();
            }
        }
    }

    public static bool OnAnyoneCheckMurderStart(PlayerControl killer, PlayerControl target)
    {
        foreach (Quarry instance in Instances)
        {
            if (instance.TargetId == killer.PlayerId && instance.QuarryId == target.PlayerId)
            {
                killer.Kill(target);
                killer.RpcSetRoleDesync(killer.GetRoleTypes(), killer.OwnerId);
                killer.SetKillCooldown(instance.TargetKCD);
                instance.QuarryId.GetPlayer()?.RpcResetAbilityCooldown();
                instance.TargetId = byte.MaxValue;
                instance.TargetKCD = 0f;
                instance.SeekTimer?.Dispose();
                instance.SeekTimer = null;
                Utils.SendRPC(CustomRPC.SyncRoleData, instance.QuarryId, 2);
                return true;
            }
        }

        return false;
    }

    public static bool OnAnyoneCheckReportDeadBody(PlayerControl reporter)
    {
        return !Instances.Exists(x => x.TargetId == reporter.PlayerId);
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return pc.PlayerId != TargetId;
    }

    public override void OnReportDeadBody()
    {
        if (TargetId == byte.MaxValue) return;
        
        TargetId = byte.MaxValue;
        SeekTimer?.Dispose();
        SeekTimer = null;
        Utils.SendRPC(CustomRPC.SyncRoleData, QuarryId, 2);
        
        PlayerControl target = TargetId.GetPlayer();
        if (target == null || !target.IsAlive()) return;

        if (TargetDiesOnMeetingCall.GetBool())
            target.Suicide();
        else
        {
            PlayerControl pc = QuarryId.GetPlayer();
            if (pc == null || !pc.IsAlive()) return;
            
            pc.RpcIncreaseAbilityUseLimitBy(1);
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                TargetId = reader.ReadByte();
                SeekTimer = new CountdownTimer(SeekTime.GetInt(), () => SeekTimer = null, onCanceled: () => SeekTimer = null);
                break;
            case 2:
                TargetId = byte.MaxValue;
                SeekTimer?.Dispose();
                SeekTimer = null;
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if ((seer.PlayerId != QuarryId && seer.PlayerId != TargetId) || seer.PlayerId != target.PlayerId || meeting || hud || TargetId == byte.MaxValue) return string.Empty;
        string time = ((int)SeekTimer.Remaining.TotalSeconds).ToString();
        return seer.PlayerId == TargetId ? $"{TargetArrow.GetAllArrows(TargetId)}\n{string.Format(Translator.GetString("Quarry.TimeLeftSuffix"), time, CustomRoles.Quarry.ToColoredString())}" : time;
    }
}