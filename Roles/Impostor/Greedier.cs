using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;

namespace EHR.Impostor;

// Reference: https://github.com/Yumenopai/TownOfHost_Y
public class Greedier : RoleBase // Also used for Imitator as the NK version of this
{
    private const int Id = 1300;
    public static List<byte> PlayerIdList = [];

    private static OptionItem OddKillCooldown;
    private static OptionItem EvenKillCooldown;
    private static OptionItem AfterMeetingKillCooldown;
    private float AfterMeetingKCD;
    private float EvenKCD;
    private bool HasImpVision;

    private bool IsImitator;

    public bool IsOdd = true;

    private float OddKCD;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Greedier);
        OddKillCooldown = new FloatOptionItem(Id + 10, "OddKillCooldown", new(0f, 60f, 2.5f), 27.5f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Greedier])
            .SetValueFormat(OptionFormat.Seconds);
        EvenKillCooldown = new FloatOptionItem(Id + 11, "EvenKillCooldown", new(0f, 30f, 2.5f), 15f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Greedier])
            .SetValueFormat(OptionFormat.Seconds);
        AfterMeetingKillCooldown = new FloatOptionItem(Id + 12, "AfterMeetingKillCooldown", new(0f, 30f, 2.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Greedier])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
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
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(HasImpVision);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc) => !IsImitator || Imitator.CanVent.GetBool();

    void SendRPC(byte playerId)
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetGreedierOe, SendOption.Reliable);
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
        foreach (var pc in Main.AllAlivePlayerControls.Where(x => PlayerIdList.Contains(x.PlayerId)))
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