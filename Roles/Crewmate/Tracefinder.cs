using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate;

public class Tracefinder : RoleBase
{
    private const int Id = 6100;
    private static List<byte> playerIdList = [];
    private static OptionItem VitalsDuration;
    private static OptionItem VitalsCooldown;
    private static OptionItem ArrowDelayMin;
    private static OptionItem ArrowDelayMax;

    public static bool On;

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

    public override void Init()
    {
        playerIdList = [];
        On = false;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        On = true;
    }

    public override bool IsEnable => playerIdList.Count > 0;

    private static void SendRPC(byte playerId, bool add, Vector3 loc = new())
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTracefinderArrow, SendOption.Reliable);
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

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        AURoleOptions.ScientistCooldown = VitalsCooldown.GetFloat();
        AURoleOptions.ScientistBatteryCharge = VitalsDuration.GetFloat();
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        bool add = reader.ReadBoolean();
        if (add)
            LocateArrow.Add(playerId, new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        else
            LocateArrow.RemoveAllTarget(playerId);
    }

    public override void OnReportDeadBody( /*PlayerControl pc, GameData.PlayerInfo target*/)
    {
        foreach (byte apc in playerIdList.ToArray())
        {
            LocateArrow.RemoveAllTarget(apc);
            SendRPC(apc, false);
        }
    }

    public static void OnPlayerDead(PlayerControl target)
    {
        if (!On || !GameStates.IsInTask || target == null) return;

        var pos = target.Pos();
        float minDis = float.MaxValue;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == target.PlayerId) continue;
            var dis = Vector2.Distance(pc.Pos(), pos);
            if (dis < minDis && dis < 1.5f)
            {
                minDis = dis;
                pc.GetRealName();
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
                foreach (byte id in playerIdList)
                {
                    PlayerControl pc = Utils.GetPlayerById(id);
                    if (pc == null || !pc.IsAlive())
                        continue;
                    LocateArrow.Add(id, target.transform.position);
                    SendRPC(id, true, target.transform.position);
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
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