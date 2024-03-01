using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TOHE.Modules;
using UnityEngine;

namespace TOHE.Roles.Crewmate;

public class SwordsMan : RoleBase
{
    private const int Id = 9000;
    public static List<byte> playerIdList = [];
    public static List<byte> killed = [];
    public static OptionItem CanVent;
    public static OptionItem UsePet;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.SwordsMan);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", false, TabGroup.CrewmateRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SwordsMan]);
        UsePet = Options.CreatePetUseSetting(Id + 10, CustomRoles.SwordsMan);
    }

    public override void Init()
    {
        killed = [];
        playerIdList = [];
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = IsKilled(id) ? 300f : Options.DefaultKillCooldown;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
    public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);
    public override string GetProgressText(byte id, bool comms) => Utils.ColorString(!IsKilled(id) ? Utils.GetRoleColor(CustomRoles.SwordsMan).ShadeColor(0.25f) : Color.gray, !IsKilled(id) ? "(1)" : "(0)");

    public override bool CanUseKillButton(PlayerControl pc)
        => !Main.PlayerStates[pc.PlayerId].IsDead
           && !IsKilled(pc.PlayerId);

    public static bool IsKilled(byte playerId) => killed.Contains(playerId);

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost || (Options.UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SendRPC(byte playerId)
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SwordsManKill, SendOption.Reliable);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte SwordsManId = reader.ReadByte();
        if (!killed.Contains(SwordsManId))
            killed.Add(SwordsManId);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target) => CanUseKillButton(killer);

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        SendRPC(killer.PlayerId);
        killed.Add(killer.PlayerId);
        SetKillCooldown(killer.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
    }
}