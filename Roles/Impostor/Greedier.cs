using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Neutral;

namespace TOHE.Roles.Impostor;

// 来源：https://github.com/Yumenopai/TownOfHost_Y
public class Greedier : RoleBase // Also used for Imitator as the NK version of this
{
    private const int Id = 1300;
    public static List<byte> playerIdList = [];

    private static OptionItem OddKillCooldown;
    private static OptionItem EvenKillCooldown;
    private static OptionItem AfterMeetingKillCooldown;

    private float OddKCD;
    private float EvenKCD;
    private float AfterMeetingKCD;
    private bool HasImpVision;

    private bool IsImitator;

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

        IsImitator = Main.PlayerStates[playerId].MainRole == CustomRoles.Imitator;
        if (IsImitator)
        {
            OddKCD = Imitator.OddKillCooldown.GetFloat();
            EvenKCD = Imitator.EvenKillCooldown.GetFloat();
            AfterMeetingKCD = Imitator.AfterMeetingKillCooldown.GetFloat();
            HasImpVision = Imitator.HasImpostorVision.GetBool();
        }
        else
        {
            OddKCD = OddKillCooldown.GetFloat();
            EvenKCD = EvenKillCooldown.GetFloat();
            AfterMeetingKCD = AfterMeetingKillCooldown.GetFloat();
            HasImpVision = true;
        }

        if (!AmongUsClient.Instance.AmHost) return;

        if (!Main.ResetCamPlayerList.Contains(playerId) && IsImitator)
        {
            Main.ResetCamPlayerList.Add(playerId);
        }
    }

    public override bool IsEnable => playerIdList.Count > 0;

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(HasImpVision);
    }

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
        Main.AllPlayerKillCooldown[id] = OddKCD;
    }

    public override void OnReportDeadBody()
    {
        foreach (var pc in Main.AllAlivePlayerControls.Where(x => playerIdList.Contains(x.PlayerId)))
        {
            IsOdd = true;
            SendRPC(pc.PlayerId);
            Main.AllPlayerKillCooldown[pc.PlayerId] = AfterMeetingKCD;
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        switch (IsOdd)
        {
            case true:
                Logger.Info($"{killer.Data?.PlayerName}: Odd-Kill", "Greedier");
                Main.AllPlayerKillCooldown[killer.PlayerId] = EvenKCD;
                break;
            case false:
                Logger.Info($"{killer.Data?.PlayerName}: Even-Kill", "Greedier");
                Main.AllPlayerKillCooldown[killer.PlayerId] = OddKCD;
                break;
        }

        IsOdd = !IsOdd;
        SendRPC(killer.PlayerId);
        killer.SyncSettings();
        return base.OnCheckMurder(killer, target);
    }
}