using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

// Credit: https://github.com/Yumenopai/TownOfHost_Y
public class Stalker : RoleBase
{
    private const int Id = 12900;
    private static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem HasImpostorVision;
    private static OptionItem CanCountNeutralKiller;
    private static OptionItem CanVent;
    public static OptionItem SnatchesWin;

    private float CurrentKillCooldown = Options.AdjustedDefaultKillCooldown;
    public bool IsWinKill;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Stalker);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stalker])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 14, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stalker]);

        HasImpostorVision = new BooleanOptionItem(Id + 11, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stalker]);

        CanCountNeutralKiller = new BooleanOptionItem(Id + 12, "CanCountNeutralKiller", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stalker]);

        SnatchesWin = new BooleanOptionItem(Id + 13, "SnatchesWin", false, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stalker]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        CurrentKillCooldown = KillCooldown.GetFloat();
        IsWinKill = false;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        CurrentKillCooldown = KillCooldown.GetFloat();
        IsWinKill = false;

        DRpcSetKillCount(Utils.GetPlayerById(playerId));
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public static void ReceiveRPC(MessageReader msg)
    {
        byte StalkerId = msg.ReadByte();
        if (Main.PlayerStates[StalkerId].Role is not Stalker { IsEnable: true } dh) return;

        bool IsKillerKill = msg.ReadBoolean();
        dh.IsWinKill = IsKillerKill;
    }

    private void DRpcSetKillCount(PlayerControl player, CustomRpcSender sender = null)
    {
        if (!IsEnable || !Utils.DoRPC || !AmongUsClient.Instance.AmHost) return;

        if (sender == null)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetStalkerKillCount, SendOption.Reliable);
            writer.Write(player.PlayerId);
            writer.Write(IsWinKill);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        else
        {
            sender.AutoStartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetStalkerKillCount);
            sender.Write(player.PlayerId);
            sender.Write(IsWinKill);
            sender.EndRpc();
        }
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CurrentKillCooldown;
    }

    public override bool CanUseKillButton(PlayerControl player)
    {
        return player.IsAlive();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl Ktarget)
    {
        CustomRoles targetRole = Ktarget.GetCustomRole();
        bool succeeded = targetRole.IsImpostor();
        if (CanCountNeutralKiller.GetBool()) succeeded |= Ktarget.IsNeutralKiller();

        if (succeeded && SnatchesWin.GetBool()) IsWinKill = true;

        var sender = CustomRpcSender.Create("Stalker.OnCheckMurder", SendOption.Reliable);
        DRpcSetKillCount(killer, sender);
        sender.AutoStartRpc(ShipStatus.Instance.NetId, RpcCalls.UpdateSystem, killer.OwnerId);
        sender.Write((byte)SystemTypes.Electrical);
        sender.WriteNetObject(killer);
        sender.EndRpc();

        foreach (PlayerControl target in Main.AllPlayerControls)
        {
            if (target.PlayerId == killer.PlayerId || target.Data.Disconnected) continue;

            sender.AutoStartRpc(ShipStatus.Instance.NetId, RpcCalls.UpdateSystem, target.OwnerId);
            sender.Write((byte)SystemTypes.Electrical);
            sender.WriteNetObject(target);
            sender.EndRpc();
        }

        sender.SendMessage();
        return true;
    }
}