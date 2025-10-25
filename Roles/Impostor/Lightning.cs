using System.Collections.Generic;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using EHR.Patches;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Impostor;

public class Lightning : RoleBase
{
    private const int Id = 16700;
    public static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem ConvertTime;
    private static OptionItem KillerConvertGhost;

    private static List<byte> GhostPlayer = [];
    private static Dictionary<byte, PlayerControl> RealKiller = [];

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
        PlayerIdList = [];
        GhostPlayer = [];
        RealKiller = [];
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
            GhostPlayer = [];
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
            if (GameStates.IsInGame && GameStates.IsInTask && !GameStates.IsMeeting && target.IsAlive() && !Pelican.IsEaten(target.PlayerId))
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

        List<byte> deList = [];

        foreach (byte ghost in GhostPlayer.ToArray())
        {
            PlayerControl gs = Utils.GetPlayerById(ghost);

            if (gs == null || !gs.IsAlive() || gs.Data.Disconnected)
            {
                //deList.Add(gs.PlayerId); // This will always result in a null reference exception
                continue;
            }

            if (pc.PlayerId != gs.PlayerId && pc.IsAlive() && !pc.Is(CustomRoles.Lightning) && !IsGhost(pc) && !Pelican.IsEaten(pc.PlayerId))
            {
                Vector3 pos = gs.Pos();
                float dis = Vector2.Distance(pos, pc.Pos());
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
        if (!(IsEnable || CustomRoles.Lightning.IsEnable())) return;

        foreach (byte ghost in GhostPlayer.ToArray())
        {
            PlayerControl gs = Utils.GetPlayerById(ghost);
            if (gs == null) continue;

            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Quantization, gs.PlayerId);
            gs.SetRealKiller(RealKiller[gs.PlayerId]);
            Utils.NotifyRoles(SpecifySeer: gs);
        }

        GhostPlayer = [];
        SendRPC(byte.MaxValue);
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("LightningButtonText"));
    }
}