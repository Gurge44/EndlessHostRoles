using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Crewmate;

public class Vigilante : RoleBase
{
    private const int Id = 652300;
    private static List<byte> PlayerIdList = [];
    public static List<byte> Killed = [];
    private static OptionItem CanVent;
    private static OptionItem KCD;
    public static OptionItem UsePet;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Vigilante);

        CanVent = new BooleanOptionItem(Id + 11, "CanVent", false, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vigilante]);

        KCD = new FloatOptionItem(Id + 9, "KillCooldown", new(0f, 120f, 0.5f), 15f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vigilante])
            .SetValueFormat(OptionFormat.Seconds);

        UsePet = Options.CreatePetUseSetting(Id + 10, CustomRoles.Vigilante);
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
        Main.AllPlayerKillCooldown[id] = KCD.GetFloat();
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
        return Utils.ColorString(!IsKilled(id) ? Utils.GetRoleColor(CustomRoles.Vigilante).ShadeColor(0.25f) : Color.gray, !IsKilled(id) ? "(1)" : "(0)");
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

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.VigilanteKill, SendOption.Reliable);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte vigilanteId = reader.ReadByte();
        if (!Killed.Contains(vigilanteId)) Killed.Add(vigilanteId);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return CanUseKillButton(killer);
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        LateTask.New(() =>
        {
            SendRPC(killer.PlayerId);
            Killed.Add(killer.PlayerId);
            SetKillCooldown(killer.PlayerId);
            killer.RpcChangeRoleBasis(CustomRoles.CrewmateEHR);
            killer.RpcResetTasks();
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
        }, 0.2f, log: false);
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = PlayerIdList.Exists(x => !Killed.Contains(x));
        countsAs = 1;
    }
}