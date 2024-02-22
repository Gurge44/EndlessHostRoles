using System.Collections.Generic;
using System.Linq;
using Hazel;

namespace TOHE.Roles.Impostor;

// 来源：https://github.com/Yumenopai/TownOfHost_Y
public class Greedier : RoleBase // Also used for Imitator as the NK version of this
{
    private const int Id = 1300;
    public static List<byte> playerIdList = [];

    private static OptionItem OddKillCooldown;
    private static OptionItem EvenKillCooldown;
    private static OptionItem AfterMeetingKillCooldown;

    public bool IsOdd = true;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Greedier);
        OddKillCooldown = FloatOptionItem.Create(Id + 10, "OddKillCooldown", new(0f, 60f, 2.5f), 27.5f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Greedier])
            .SetValueFormat(OptionFormat.Seconds);
        EvenKillCooldown = FloatOptionItem.Create(Id + 11, "EvenKillCooldown", new(0f, 30f, 2.5f), 15f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Greedier])
            .SetValueFormat(OptionFormat.Seconds);
        AfterMeetingKillCooldown = FloatOptionItem.Create(Id + 12, "AfterMeetingKillCooldown", new(0f, 30f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Greedier])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        IsOdd = true;

        if (!AmongUsClient.Instance.AmHost) return;

        if (!Main.ResetCamPlayerList.Contains(playerId) && Main.PlayerStates[playerId].MainRole == CustomRoles.Imitator)
        {
            Main.ResetCamPlayerList.Add(playerId);
        }
    }

    public override bool IsEnable => playerIdList.Count > 0;

    void SendRPC(byte playerId)
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetGreedierOE, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(IsOdd);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(bool isOdd)
    {
        IsOdd = isOdd;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = OddKillCooldown.GetFloat();
    }

    public override void OnReportDeadBody()
    {
        foreach (var pc in Main.AllAlivePlayerControls.Where(x => playerIdList.Contains(x.PlayerId)).ToArray())
        {
            IsOdd = true;
            SendRPC(pc.PlayerId);
            Main.AllPlayerKillCooldown[pc.PlayerId] = AfterMeetingKillCooldown.GetFloat();
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        switch (IsOdd)
        {
            case true:
                Logger.Info($"{killer.Data?.PlayerName}: Odd-Kill", "Greedier");
                Main.AllPlayerKillCooldown[killer.PlayerId] = EvenKillCooldown.GetFloat();
                break;
            case false:
                Logger.Info($"{killer.Data?.PlayerName}: Even-Kill", "Greedier");
                Main.AllPlayerKillCooldown[killer.PlayerId] = OddKillCooldown.GetFloat();
                break;
        }

        IsOdd = !IsOdd;
        SendRPC(killer.PlayerId);
        killer.SyncSettings();
        return base.OnCheckMurder(killer, target);
    }
}