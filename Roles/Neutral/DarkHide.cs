using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using InnerNet;

namespace EHR.Roles.Neutral;

// 来源：https://github.com/Yumenopai/TownOfHost_Y
public class DarkHide : RoleBase
{
    public static readonly int Id = 12900;
    public static List<byte> playerIdList = [];

    public static OptionItem KillCooldown;
    public static OptionItem HasImpostorVision;
    public static OptionItem CanCountNeutralKiller;
    public static OptionItem CanVent;
    public static OptionItem SnatchesWin;

    public float CurrentKillCooldown = Options.DefaultKillCooldown;
    public bool IsWinKill;

    public static void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.DarkHide);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.DarkHide])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 14, "CanVent", true, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.DarkHide]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 11, "ImpostorVision", false, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.DarkHide]);
        CanCountNeutralKiller = BooleanOptionItem.Create(Id + 12, "CanCountNeutralKiller", false, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.DarkHide]);
        SnatchesWin = BooleanOptionItem.Create(Id + 13, "SnatchesWin", false, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.DarkHide]);
    }

    public override void Init()
    {
        playerIdList = [];
        CurrentKillCooldown = KillCooldown.GetFloat();
        IsWinKill = false;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        CurrentKillCooldown = KillCooldown.GetFloat();
        IsWinKill = false;

        DRpcSetKillCount(Utils.GetPlayerById(playerId));

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;

    public static void ReceiveRPC(MessageReader msg)
    {
        byte DarkHiderId = msg.ReadByte();
        if (Main.PlayerStates[DarkHiderId].Role is not DarkHide { IsEnable: true } dh) return;
        bool IsKillerKill = msg.ReadBoolean();
        dh.IsWinKill = IsKillerKill;
    }

    void DRpcSetKillCount(PlayerControl player)
    {
        if (!IsEnable || !Utils.DoRPC || !AmongUsClient.Instance.AmHost) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDarkHiderKillCount, SendOption.Reliable);
        writer.Write(player.PlayerId);
        writer.Write(IsWinKill);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CurrentKillCooldown;
    public override bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl Ktarget)
    {
        var targetRole = Ktarget.GetCustomRole();
        var succeeded = targetRole.IsImpostor();
        if (CanCountNeutralKiller.GetBool() && !Ktarget.Is(CustomRoles.Arsonist) && !Ktarget.Is(CustomRoles.Revolutionist))
        {
            succeeded = succeeded || Ktarget.IsNeutralKiller();
        }

        if (succeeded && SnatchesWin.GetBool())
            IsWinKill = true;

        DRpcSetKillCount(killer);
        MessageWriter SabotageFixWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, killer.GetClientId());
        SabotageFixWriter.Write((byte)SystemTypes.Electrical);
        SabotageFixWriter.WriteNetObject(killer);
        AmongUsClient.Instance.FinishRpcImmediately(SabotageFixWriter);

        foreach (PlayerControl target in Main.AllPlayerControls)
        {
            if (target.PlayerId == killer.PlayerId || target.Data.Disconnected) continue;
            SabotageFixWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, target.GetClientId());
            SabotageFixWriter.Write((byte)SystemTypes.Electrical);
            SabotageFixWriter.WriteNetObject(target);
            AmongUsClient.Instance.FinishRpcImmediately(SabotageFixWriter);
        }

        return true;
    }
}