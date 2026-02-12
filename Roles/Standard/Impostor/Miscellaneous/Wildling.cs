using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;
using static EHR.Options;

namespace EHR.Roles;

public class Wildling : RoleBase
{
    private const int Id = 4700;
    private static List<byte> PlayerIdList = [];

    private static OptionItem ProtectDurationOpt;
    private static OptionItem CanVentOpt;
    public static OptionItem CanShapeshiftOpt;
    private static OptionItem ShapeshiftCDOpt;
    private static OptionItem ShapeshiftDurOpt;

    private bool CanShapeshift;
    private bool CanVent;
    private bool HasImpostorVision;
    private float KillCooldown;

    private float ProtectionDuration;
    private float ShapeshiftCD;
    private float ShapeshiftDur;

    private CountdownTimer Timer;
    private byte WildlingId;

    private CustomRoles UsedRole;

    public override bool IsEnable => PlayerIdList.Count > 0;


    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Wildling);

        ProtectDurationOpt = new FloatOptionItem(Id + 14, "BKProtectDuration", new(1f, 30f, 1f), 15f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Wildling])
            .SetValueFormat(OptionFormat.Seconds);

        CanVentOpt = new BooleanOptionItem(Id + 15, "CanVent", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Wildling]);

        CanShapeshiftOpt = new BooleanOptionItem(Id + 16, "CanShapeshift", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Wildling]);

        ShapeshiftCDOpt = new FloatOptionItem(Id + 17, "ShapeshiftCooldown", new(1f, 60f, 1f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CanShapeshiftOpt)
            .SetValueFormat(OptionFormat.Seconds);

        ShapeshiftDurOpt = new FloatOptionItem(Id + 18, "ShapeshiftDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles)
            .SetParent(CanShapeshiftOpt)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        WildlingId = playerId;

        Timer = null;

        UsedRole = Main.PlayerStates[playerId].MainRole;

        switch (UsedRole)
        {
            case CustomRoles.Wildling:
                ProtectionDuration = ProtectDurationOpt.GetFloat();
                CanVent = CanVentOpt.GetBool();
                CanShapeshift = CanShapeshiftOpt.GetBool();
                ShapeshiftCD = ShapeshiftCDOpt.GetFloat();
                ShapeshiftDur = ShapeshiftDurOpt.GetFloat();
                HasImpostorVision = true;
                KillCooldown = AdjustedDefaultKillCooldown;
                break;
            case CustomRoles.BloodKnight:
                ProtectionDuration = BloodKnight.ProtectDuration.GetFloat();
                CanVent = BloodKnight.CanVent.GetBool();
                CanShapeshift = false;
                ShapeshiftCD = 0;
                ShapeshiftDur = 0;
                HasImpostorVision = BloodKnight.HasImpostorVision.GetBool();
                KillCooldown = BloodKnight.KillCooldown.GetFloat();
                break;
        }
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision);

        if (CanShapeshift)
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCD;
            AURoleOptions.ShapeshifterDuration = ShapeshiftDur;
        }
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Timer = new CountdownTimer(ProtectionDuration, () => Timer = null, onCanceled: () => Timer = null);
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return;

        Timer = new CountdownTimer(ProtectionDuration, () =>
        {
            Timer = null;
            killer.Notify(Translator.GetString("BKProtectOut"));
        }, onTick: () => Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, SendOption: SendOption.None), onCanceled: () => Timer = null);
        Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId);
        killer.Notify(Translator.GetString("BKInProtect"));
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (Timer != null)
        {
            killer.RpcGuardAndKill(target);
            target.Notify(Translator.GetString("BKOffsetKill"));
            return false;
        }

        return true;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != WildlingId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;

        var str = new StringBuilder();

        if (Timer != null)
        {
            int remainTime = (int)Math.Ceiling(Timer.Remaining.TotalSeconds);
            str.Append(string.Format(Translator.GetString("BKSkillTimeRemain"), remainTime));
        }
        else
            str.Append(Translator.GetString("BKSkillNotice"));

        return str.ToString();
    }
}