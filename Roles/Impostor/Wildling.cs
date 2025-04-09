using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using static EHR.Options;

namespace EHR.Impostor;

public class Wildling : RoleBase
{
    private const int Id = 4700;
    public static List<byte> PlayerIdList = [];

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

    private long TimeStamp;

    private CustomRoles UsedRole;

    public override bool IsEnable => PlayerIdList.Count > 0;

    private bool InProtect => TimeStamp > Utils.TimeStamp;

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
        TimeStamp = 0;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        TimeStamp = 0;

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
                KillCooldown = DefaultKillCooldown;
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

    private void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBkTimer, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(TimeStamp.ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        string Time = reader.ReadString();

        if (Main.PlayerStates[PlayerId].Role is not Wildling wl) return;

        wl.TimeStamp = long.Parse(Time);
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return;

        TimeStamp = Utils.TimeStamp + (long)ProtectionDuration;
        SendRPC(killer.PlayerId);
        killer.Notify(Translator.GetString("BKInProtect"));
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (InProtect)
        {
            killer.RpcGuardAndKill(target);
            target.Notify(Translator.GetString("BKOffsetKill"));
            return false;
        }

        return true;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask) return;

        if (TimeStamp < Utils.TimeStamp && TimeStamp != 0)
        {
            TimeStamp = 0;
            pc.Notify(Translator.GetString("BKProtectOut"));
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!hud || seer == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive()) return string.Empty;

        if (Main.PlayerStates[seer.PlayerId].Role is not Wildling wl) return string.Empty;

        var str = new StringBuilder();

        if (wl.InProtect)
        {
            long remainTime = wl.TimeStamp - Utils.GetTimeStamp(DateTime.Now);
            str.Append(string.Format(Translator.GetString("BKSkillTimeRemain"), remainTime));
        }
        else
            str.Append(Translator.GetString("BKSkillNotice"));

        return str.ToString();
    }
}