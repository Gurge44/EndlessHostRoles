using System;
using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using Hazel;
using TOHE.Roles.Neutral;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

public class Wildling : RoleBase
{
    private const int Id = 4700;
    public static List<byte> playerIdList = [];

    public static OptionItem ProtectDurationOpt;
    public static OptionItem CanVentOpt;
    public static OptionItem CanShapeshiftOpt;
    public static OptionItem ShapeshiftCDOpt;
    public static OptionItem ShapeshiftDurOpt;

    private float ProtectionDuration;
    private bool CanVent;
    private bool CanShapeshift;
    private float ShapeshiftCD;
    private float ShapeshiftDur;
    private bool HasImpostorVision;
    private float KillCooldown;

    private CustomRoles UsedRole;

    private long TimeStamp;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Wildling, 1, zeroOne: false);
        ProtectDurationOpt = FloatOptionItem.Create(Id + 14, "BKProtectDuration", new(1f, 30f, 1f), 15f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Wildling])
            .SetValueFormat(OptionFormat.Seconds);
        CanVentOpt = BooleanOptionItem.Create(Id + 15, "CanVent", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Wildling]);
        CanShapeshiftOpt = BooleanOptionItem.Create(Id + 16, "CanShapeshift", false, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Wildling]);
        ShapeshiftCDOpt = FloatOptionItem.Create(Id + 17, "ShapeshiftCooldown", new(1f, 60f, 1f), 30f, TabGroup.ImpostorRoles, false).SetParent(CanShapeshiftOpt)
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftDurOpt = FloatOptionItem.Create(Id + 18, "ShapeshiftDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles, false).SetParent(CanShapeshiftOpt)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        playerIdList = [];
        TimeStamp = 0;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
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

                if (!AmongUsClient.Instance.AmHost) return;
                if (!Main.ResetCamPlayerList.Contains(playerId))
                    Main.ResetCamPlayerList.Add(playerId);
                break;
        }
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown;

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision);
        if (CanShapeshift)
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCD;
            AURoleOptions.ShapeshifterDuration = ShapeshiftDur;
        }
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent;

    public override bool IsEnable => playerIdList.Count > 0;

    void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBKTimer, SendOption.Reliable);
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

    bool InProtect => TimeStamp > Utils.TimeStamp;

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
    public static string GetHudText(PlayerControl pc)
    {
        if (pc == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive()) return string.Empty;
        if (Main.PlayerStates[pc.PlayerId].Role is not Wildling wl) return string.Empty;

        var str = new StringBuilder();
        if (wl.InProtect)
        {
            var remainTime = wl.TimeStamp - Utils.GetTimeStamp(DateTime.Now);
            str.Append(string.Format(Translator.GetString("BKSkillTimeRemain"), remainTime));
        }
        else
        {
            str.Append(Translator.GetString("BKSkillNotice"));
        }
        return str.ToString();
    }
}