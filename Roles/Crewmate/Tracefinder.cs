using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate;
public static class Tracefinder
{
    private static readonly int Id = 6100;
    private static List<byte> playerIdList = [];
    private static OptionItem VitalsDuration;
    private static OptionItem VitalsCooldown;
    private static OptionItem ArrowDelayMin;
    private static OptionItem ArrowDelayMax;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tracefinder);
        VitalsCooldown = FloatOptionItem.Create(Id + 10, "VitalsCooldown", new(1f, 60f, 1f), 25f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
            .SetValueFormat(OptionFormat.Seconds);
        VitalsDuration = FloatOptionItem.Create(Id + 11, "VitalsDuration", new(1f, 30f, 1f), 1f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
            .SetValueFormat(OptionFormat.Seconds);
        ArrowDelayMin = FloatOptionItem.Create(Id + 12, "ArrowDelayMin", new(1f, 30f, 1f), 2f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
            .SetValueFormat(OptionFormat.Seconds);
        ArrowDelayMax = FloatOptionItem.Create(Id + 13, "ArrowDelayMax", new(1f, 30f, 1f), 7f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        playerIdList = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Any();
    private static void SendRPC(byte playerId, bool add, Vector3 loc = new())
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTracefinderArrow, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(add);
        if (add)
        {
            writer.Write(loc.x);
            writer.Write(loc.y);
            writer.Write(loc.z);
        }
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ApplyGameOptions()
    {
        AURoleOptions.ScientistCooldown = VitalsCooldown.GetFloat();
        AURoleOptions.ScientistBatteryCharge = VitalsDuration.GetFloat();
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        bool add = reader.ReadBoolean();
        if (add)
            LocateArrow.Add(playerId, new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        else
            LocateArrow.RemoveAllTarget(playerId);
    }
    public static void OnReportDeadBody(/*PlayerControl pc, GameData.PlayerInfo target*/)
    {
        foreach (byte apc in playerIdList.ToArray())
        {
            LocateArrow.RemoveAllTarget(apc);
            SendRPC(apc, false);
        }

    }

    public static void OnPlayerDead(PlayerControl target)
    {
        var pos = target.GetTruePosition();
        float minDis = float.MaxValue;
        string minName = string.Empty;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == target.PlayerId) continue;
            var dis = Vector2.Distance(pc.GetTruePosition(), pos);
            if (dis < minDis && dis < 1.5f)
            {
                minDis = dis;
                minName = pc.GetRealName();
            }
        }

        float delay;
        if (ArrowDelayMax.GetFloat() < ArrowDelayMin.GetFloat()) delay = 0f;
        else delay = IRandom.Instance.Next((int)ArrowDelayMin.GetFloat(), (int)ArrowDelayMax.GetFloat() + 1);
        delay = Math.Max(delay, 0.15f);

        _ = new LateTask(() =>
        {
            if (GameStates.IsInTask)
            {
                foreach (byte pc in playerIdList.ToArray())
                {
                    var player = Utils.GetPlayerById(pc);
                    if (player == null || !player.IsAlive())
                        continue;
                    LocateArrow.Add(pc, target.transform.position);
                    SendRPC(pc, true, target.transform.position);
                    Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(pc));
                }
            }
        }, delay, "Tracefinder arrow delay");
    }
    public static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
    {
        if (!seer.Is(CustomRoles.Tracefinder)) return string.Empty;
        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
        return Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
    }
}