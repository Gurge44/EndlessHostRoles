using System;
using System.Collections.Generic;
using System.Text;
using Hazel;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

public class Wildling : RoleBase
{
    private const int Id = 4700;
    public static List<byte> playerIdList = [];

    private static OptionItem ProtectDuration;
    public static OptionItem CanVent;
    public static OptionItem CanShapeshift;
    public static OptionItem ShapeshiftCD;
    public static OptionItem ShapeshiftDur;

    private long TimeStamp;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Wildling, 1, zeroOne: false);
        ProtectDuration = FloatOptionItem.Create(Id + 14, "BKProtectDuration", new(1f, 30f, 1f), 15f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Wildling])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 15, "CanVent", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Wildling]);
        CanShapeshift = BooleanOptionItem.Create(Id + 16, "CanShapeshift", false, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Wildling]);
        ShapeshiftCD = FloatOptionItem.Create(Id + 17, "ShapeshiftCooldown", new(1f, 60f, 1f), 30f, TabGroup.ImpostorRoles, false).SetParent(CanShapeshift)
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftDur = FloatOptionItem.Create(Id + 18, "ShapeshiftDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles, false).SetParent(CanShapeshift)
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
    }

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

    bool InProtect => TimeStamp > Utils.GetTimeStamp(DateTime.Now);

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return;
        TimeStamp = Utils.GetTimeStamp(DateTime.Now) + (long)ProtectDuration.GetFloat();
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
        if (!GameStates.IsInTask || !pc.Is(CustomRoles.Wildling)) return;
        if (TimeStamp < Utils.GetTimeStamp(DateTime.Now) && TimeStamp != 0)
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