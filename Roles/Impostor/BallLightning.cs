using Hazel;
using System.Collections.Generic;
using TOHE.Modules;
using TOHE.Patches;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

public class BallLightning : RoleBase
{
    private const int Id = 16700;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem ConvertTime;
    private static OptionItem KillerConvertGhost;

    private static List<byte> GhostPlayer = [];
    private static Dictionary<byte, PlayerControl> RealKiller = [];

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.OtherRoles, CustomRoles.BallLightning);
        KillCooldown = FloatOptionItem.Create(Id + 10, "BallLightningKillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.BallLightning])
            .SetValueFormat(OptionFormat.Seconds);
        ConvertTime = FloatOptionItem.Create(Id + 12, "BallLightningConvertTime", new(0f, 180f, 2.5f), 10f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.BallLightning])
            .SetValueFormat(OptionFormat.Seconds);
        KillerConvertGhost = BooleanOptionItem.Create(Id + 14, "BallLightningKillerConvertGhost", true, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.BallLightning]);
    }

    public override void Init()
    {
        playerIdList = [];
        GhostPlayer = [];
        RealKiller = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0 || Randomizer.Exists;

    private static void SendRPC(byte playerId)
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetGhostPlayer, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(IsGhost(playerId));
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte GhostId = reader.ReadByte();
        bool isGhost = reader.ReadBoolean();
        if (GhostId == byte.MaxValue)
        {
            GhostPlayer = [];
            return;
        }

        if (isGhost)
        {
            if (!GhostPlayer.Contains(GhostId))
                GhostPlayer.Add(GhostId);
        }
        else
        {
            if (GhostPlayer.Contains(GhostId))
                GhostPlayer.Remove(GhostId);
        }
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static bool IsGhost(PlayerControl player) => GhostPlayer.Contains(player.PlayerId);
    public static bool IsGhost(byte id) => GhostPlayer.Contains(id);

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return CheckBallLightningMurder(killer, target);
    }

    public static bool CheckBallLightningMurder(PlayerControl killer, PlayerControl target, bool force = false)
    {
        if (killer == null || target == null || (!killer.Is(CustomRoles.BallLightning) && !force)) return false;
        if (IsGhost(target)) return false;
        if (!force)
        {
            killer.SetKillCooldown();
            killer.RPCPlayCustomSound("Shield");
        }

        StartConvertCountDown(killer, target);
        return true;
    }

    private static void StartConvertCountDown(PlayerControl killer, PlayerControl target)
    {
        _ = new LateTask(() =>
        {
            if (GameStates.IsInGame && GameStates.IsInTask && !GameStates.IsMeeting && target.IsAlive() && !Pelican.IsEaten(target.PlayerId))
            {
                GhostPlayer.Add(target.PlayerId);
                SendRPC(target.PlayerId);
                RealKiller.TryAdd(target.PlayerId, killer);
                if (!killer.inVent) killer.SetKillCooldown();
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);
                Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} is now converted to a 'non-spherical lightning' XD", "BallLightning");
            }
        }, ConvertTime.GetFloat(), "BallLightning Convert Player To Ghost");
    }

    public static void MurderPlayer(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || !target.Is(CustomRoles.BallLightning)) return;
        if (!KillerConvertGhost.GetBool() || IsGhost(killer)) return;
        RealKiller.TryAdd(killer.PlayerId, target);
        StartConvertCountDown(target, killer);
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (!GameStates.IsInTask) return;
        List<byte> deList = [];
        foreach (byte ghost in GhostPlayer.ToArray())
        {
            var gs = Utils.GetPlayerById(ghost);
            if (gs == null || !gs.IsAlive() || gs.Data.Disconnected)
            {
                //deList.Add(gs.PlayerId); // This will always result in a null reference exception
                continue;
            }

            if (pc.PlayerId != gs.PlayerId && pc.IsAlive() && !pc.Is(CustomRoles.BallLightning) && !IsGhost(pc) && !Pelican.IsEaten(pc.PlayerId))
            {
                var pos = gs.transform.position;
                var dis = Vector2.Distance(pos, pc.Pos());
                if (dis > 0.3f) continue;

                deList.Add(gs.PlayerId);
                gs.Suicide(PlayerState.DeathReason.Quantization, RealKiller[gs.PlayerId]);

                break;
            }
        }

        if (deList.Count > 0)
        {
            GhostPlayer.RemoveAll(deList.Contains);
            foreach (byte gs in deList.ToArray())
            {
                SendRPC(gs);
                Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(gs));
            }
        }
    }

    public override void OnReportDeadBody()
    {
        if (!(IsEnable || CustomRoles.BallLightning.IsEnable())) return;

        foreach (byte ghost in GhostPlayer.ToArray())
        {
            var gs = Utils.GetPlayerById(ghost);
            if (gs == null)
                continue;
            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Quantization, gs.PlayerId);
            gs.SetRealKiller(RealKiller[gs.PlayerId]);
            Utils.NotifyRoles(SpecifySeer: gs);
        }

        GhostPlayer = [];
        SendRPC(byte.MaxValue);
    }
}