using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public class Vulture : RoleBase
{
    private const int Id = 11600;
    private static List<byte> playerIdList = [];

    public static List<byte> UnreportablePlayers = [];
    public static Dictionary<byte, int> BodyReportCount = [];
    public static Dictionary<byte, int> AbilityLeftInRound = [];
    public static Dictionary<byte, long> LastReport = [];

    public static OptionItem ArrowsPointingToDeadBody;
    public static OptionItem NumberOfReportsToWin;
    public static OptionItem CanVent;
    public static OptionItem VultureReportCD;
    public static OptionItem MaxEaten;
    public static OptionItem HasImpVision;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Vulture);
        ArrowsPointingToDeadBody = BooleanOptionItem.Create(Id + 10, "VultureArrowsPointingToDeadBody", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
        NumberOfReportsToWin = IntegerOptionItem.Create(Id + 11, "VultureNumberOfReportsToWin", new(1, 10, 1), 4, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
        CanVent = BooleanOptionItem.Create(Id + 12, "CanVent", true, TabGroup.NeutralRoles, true).SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
        VultureReportCD = FloatOptionItem.Create(Id + 13, "VultureReportCooldown", new(0f, 180f, 2.5f), 15f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vulture])
            .SetValueFormat(OptionFormat.Seconds);
        MaxEaten = IntegerOptionItem.Create(Id + 14, "VultureMaxEatenInOneRound", new(1, 10, 1), 2, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
        HasImpVision = BooleanOptionItem.Create(Id + 15, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
    }

    public override void Init()
    {
        playerIdList = [];
        UnreportablePlayers = [];
        BodyReportCount = [];
        AbilityLeftInRound = [];
        LastReport = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        BodyReportCount[playerId] = 0;
        AbilityLeftInRound[playerId] = MaxEaten.GetInt();
        LastReport[playerId] = Utils.TimeStamp;
        _ = new LateTask(() =>
        {
            if (GameStates.IsInTask)
            {
                Utils.GetPlayerById(playerId).Notify(GetString("VultureCooldownUp"));
            }
        }, VultureReportCD.GetFloat() + 8f, "Vulture CD"); //for some reason that idk vulture cd completes 8s faster when the game starts, so I added 8f for now 
    }

    public override bool IsEnable => playerIdList.Count > 0;

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpVision.GetBool());
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    private static void SendRPC(byte playerId, bool add, Vector3 loc = new())
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetVultureArrow, SendOption.Reliable);
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

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        bool add = reader.ReadBoolean();
        if (add)
            LocateArrow.Add(playerId, new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        else
            LocateArrow.RemoveAllTarget(playerId);
    }

    public static void Clear()
    {
        foreach (byte apc in playerIdList)
        {
            LocateArrow.RemoveAllTarget(apc);
            SendRPC(apc, false);
        }
    }

    public override void AfterMeetingTasks()
    {
        foreach (byte apc in playerIdList)
        {
            var player = Utils.GetPlayerById(apc);
            if (player.IsAlive())
            {
                AbilityLeftInRound[apc] = MaxEaten.GetInt();
                LastReport[apc] = Utils.TimeStamp;
                _ = new LateTask(() =>
                {
                    if (GameStates.IsInTask)
                    {
                        //Utils.GetPlayerById(apc).RpcGuardAndKill(Utils.GetPlayerById(apc));
                        Utils.GetPlayerById(apc).Notify(GetString("VultureCooldownUp"));
                    }
                }, VultureReportCD.GetFloat(), "Vulture CD");
                SendRPC(apc, false);
            }
        }
    }

    public static void OnPlayerDead(PlayerControl target)
    {
        if (!ArrowsPointingToDeadBody.GetBool()) return;

        var pos = target.Pos();
        float minDis = float.MaxValue;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == target.PlayerId) continue;
            var dis = Vector2.Distance(pc.Pos(), pos);
            if (dis < minDis && dis < 1.5f)
            {
                minDis = dis;
            }
        }

        foreach (byte pc in playerIdList)
        {
            var player = Utils.GetPlayerById(pc);
            if (player == null || !player.IsAlive()) continue;
            LocateArrow.Add(pc, target.transform.position);
            SendRPC(pc, true, target.transform.position);
        }
    }

    public override bool CheckReportDeadBody(PlayerControl pc, GameData.PlayerInfo target, PlayerControl killer)
    {
        BodyReportCount[pc.PlayerId]++;
        AbilityLeftInRound[pc.PlayerId]--;
        Logger.Msg($"target.object {target.Object}, is null? {target.Object == null}", "VultureNull");
        if (target.Object != null)
        {
            foreach (byte apc in playerIdList)
            {
                LocateArrow.Remove(apc, target.Object.transform.position);
                SendRPC(apc, false);
            }
        }

        pc.Notify(GetString("VultureBodyReported"));
        UnreportablePlayers.Remove(target.PlayerId);
        UnreportablePlayers.Add(target.PlayerId);
        //playerIdList.Remove(target.PlayerId);
        return false;
    }

    public static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
    {
        if (!seer.Is(CustomRoles.Vulture)) return string.Empty;
        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
        return GameStates.IsMeeting ? string.Empty : Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        var playerId = pc.PlayerId;
        if (BodyReportCount[playerId] >= NumberOfReportsToWin.GetInt() && GameStates.IsInTask)
        {
            BodyReportCount[playerId] = NumberOfReportsToWin.GetInt();
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Vulture);
            CustomWinnerHolder.WinnerIds.Add(playerId);
        }
    }
}