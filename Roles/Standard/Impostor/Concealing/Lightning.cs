using EHR.Modules;
using EHR.Patches;
using Hazel;
using Rewired.Utils.Classes.Data;
using System.Collections.Generic;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles;

public class Lightning : RoleBase
{
    private const int Id = 16700;
    public static readonly List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem ConvertTime;
    private static OptionItem KillerConvertGhost;

    private static readonly List<byte> DeList = [];
    private static readonly List<byte> GhostPlayer = [];
    private static readonly Dictionary<byte, PlayerControl> RealKiller = [];

    public override bool IsEnable => PlayerIdList.Count > 0 || Randomizer.Exists;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Lightning);

        KillCooldown = new FloatOptionItem(Id + 10, "LightningKillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lightning])
            .SetValueFormat(OptionFormat.Seconds);

        ConvertTime = new FloatOptionItem(Id + 12, "LightningConvertTime", new(0f, 180f, 0.5f), 10f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lightning])
            .SetValueFormat(OptionFormat.Seconds);

        KillerConvertGhost = new BooleanOptionItem(Id + 14, "LightningKillerConvertGhost", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lightning]);
    }

    public override void Init()
    {
        PlayerIdList.Clear();
        GhostPlayer.Clear();
        RealKiller.Clear();
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

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
            GhostPlayer.Clear();
            return;
        }

        if (isGhost)
        {
            if (!GhostPlayer.Contains(GhostId)) GhostPlayer.Add(GhostId);
        }
        else
        {
            if (GhostPlayer.Contains(GhostId)) GhostPlayer.Remove(GhostId);
        }
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public static bool IsGhost(PlayerControl player)
    {
        return GhostPlayer.Contains(player.PlayerId);
    }

    public static bool IsGhost(byte id)
    {
        return GhostPlayer.Contains(id);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return !CheckLightningMurder(killer, target);
    }

    public static bool CheckLightningMurder(PlayerControl killer, PlayerControl target, bool force = false)
    {
        if (killer == null || target == null || (!killer.Is(CustomRoles.Lightning) && !force)) return false;

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
        LateTask.New(() =>
        {
            if (GameStates.IsInGame && GameStates.IsInTask && !GameStates.IsMeeting && target.IsAliveWithConditions())
            {
                GhostPlayer.Add(target.PlayerId);
                SendRPC(target.PlayerId);
                RealKiller.TryAdd(target.PlayerId, killer);
                if (!killer.inVent) killer.SetKillCooldown();

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);
                Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} is now converted to a 'non-spherical lightning' XD", "Lightning");
            }
        }, ConvertTime.GetFloat(), "Lightning Convert Player To Ghost");
    }

    public static void MurderPlayer(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || !target.Is(CustomRoles.Lightning)) return;

        if (!KillerConvertGhost.GetBool() || IsGhost(killer)) return;

        RealKiller.TryAdd(killer.PlayerId, target);
        StartConvertCountDown(target, killer);
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (!GameStates.IsInTask) return;

        DeList.Clear();

        foreach (byte ghost in GhostPlayer)
        {
            PlayerControl gs = Utils.GetPlayerById(ghost);

            if (!gs.IsAlive() || gs.Data.Disconnected)
            {
                //DeList.Add(gs.PlayerId); // This will always result in a null reference exception
                continue;
            }

            if (pc.PlayerId != gs.PlayerId && pc.IsAliveWithConditions() && !pc.Is(CustomRoles.Lightning) && !IsGhost(pc))
            {
                Vector3 pos = gs.Pos();
                if (!FastVector2.DistanceWithinRange(pos, pc.Pos(), 0.3f)) continue;

                DeList.Add(gs.PlayerId);
                gs.Suicide(PlayerState.DeathReason.Quantization, RealKiller[gs.PlayerId]);
                break;
            }
        }

        if (DeList.Count > 0)
        {
            GhostPlayer.RemoveAll(DeList.Contains);

            foreach (byte gs in DeList)
            {
                SendRPC(gs);
                Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(gs));
            }
        }
    }

    public override void OnReportDeadBody()
    {
        if (!(IsEnable || CustomRoles.Lightning.IsEnable())) return;

        foreach (byte ghost in GhostPlayer)
        {
            PlayerControl gs = Utils.GetPlayerById(ghost);
            if (gs == null) continue;

            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Quantization, gs.PlayerId);
            gs.SetRealKiller(RealKiller[gs.PlayerId]);
            Utils.NotifyRoles(SpecifySeer: gs);
        }

        GhostPlayer.Clear();
        SendRPC(byte.MaxValue);
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("LightningButtonText"));
    }
}