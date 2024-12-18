using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Crewmate;

public class SwordsMan : RoleBase
{
    private const int Id = 9000;
    private static List<byte> PlayerIdList = [];
    public static List<byte> Killed = [];
    private static OptionItem CanVent;
    private static OptionItem KCD;
    public static OptionItem UsePet;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.SwordsMan);

        CanVent = new BooleanOptionItem(Id + 11, "CanVent", false, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SwordsMan]);

        KCD = new FloatOptionItem(Id + 9, "KillCooldown", new(0f, 120f, 0.5f), 15f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SwordsMan])
            .SetValueFormat(OptionFormat.Seconds);

        UsePet = Options.CreatePetUseSetting(Id + 10, CustomRoles.SwordsMan);
    }

    public override void Init()
    {
        Killed = [];
        PlayerIdList = [];
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = IsKilled(id) ? 300f : KCD.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override string GetProgressText(byte id, bool comms)
    {
        return Utils.ColorString(!IsKilled(id) ? Utils.GetRoleColor(CustomRoles.SwordsMan).ShadeColor(0.25f) : Color.gray, !IsKilled(id) ? "(1)" : "(0)");
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return !Main.PlayerStates[pc.PlayerId].IsDead
               && !IsKilled(pc.PlayerId);
    }

    private static bool IsKilled(byte playerId)
    {
        return Killed.Contains(playerId);
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

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
        if (!Killed.Contains(SwordsManId)) Killed.Add(SwordsManId);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return CanUseKillButton(killer);
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        SendRPC(killer.PlayerId);
        Killed.Add(killer.PlayerId);
        SetKillCooldown(killer.PlayerId);
        killer.RpcChangeRoleBasis(CustomRoles.CrewmateEHR);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
    }
}