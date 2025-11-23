using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

public class Scout : RoleBase
{
    private const int Id = 8300;
    private static List<byte> PlayerIdList = [];

    private static OptionItem TrackLimitOpt;
    private static OptionItem OptionCanSeeLastRoomInMeeting;
    private static OptionItem CanGetColoredArrow;
    public static OptionItem HideVote;
    public static OptionItem ScoutAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    public static OptionItem CancelVote;

    public static bool CanSeeLastRoomInMeeting;

    private static Dictionary<byte, List<byte>> TrackerTarget = [];
    private byte TrackerId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Scout);

        TrackLimitOpt = new IntegerOptionItem(Id + 5, "FortuneTellerSkillLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Scout])
            .SetValueFormat(OptionFormat.Times);

        CanGetColoredArrow = new BooleanOptionItem(Id + 6, "TrackerCanGetArrowColor", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Scout]);

        OptionCanSeeLastRoomInMeeting = new BooleanOptionItem(Id + 7, "EvilTrackerCanSeeLastRoomInMeeting", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Scout]);

        HideVote = new BooleanOptionItem(Id + 8, "TrackerHideVote", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Scout]);

        ScoutAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 9, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Scout])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 3, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Scout])
            .SetValueFormat(OptionFormat.Times);

        CancelVote = CreateVoteCancellingUseSetting(Id + 4, CustomRoles.Scout, TabGroup.CrewmateRoles);
    }

    public override void Init()
    {
        PlayerIdList = [];
        TrackerTarget = [];
        CanSeeLastRoomInMeeting = OptionCanSeeLastRoomInMeeting.GetBool();
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(TrackLimitOpt.GetFloat());
        TrackerTarget.Add(playerId, []);
        TrackerId = playerId;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public static void SendRPC(byte trackerId = byte.MaxValue, byte targetId = byte.MaxValue)
    {
        if (!Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTrackerTarget, SendOption.Reliable);
        writer.Write(trackerId);
        writer.Write(targetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte trackerId = reader.ReadByte();
        byte targetId = reader.ReadByte();

        Utils.GetPlayerById(trackerId).RpcRemoveAbilityUse();

        TrackerTarget[trackerId].Add(targetId);
    }

    public static string GetTargetMark(PlayerControl seer, PlayerControl target)
    {
        return !(seer == null || target == null) && TrackerTarget.ContainsKey(seer.PlayerId) && TrackerTarget[seer.PlayerId].Contains(target.PlayerId) ? Utils.ColorString(seer.GetRoleColor(), "◀") : string.Empty;
    }

    public override bool OnVote(PlayerControl player, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return false;
        if (player == null || target == null || player.GetAbilityUseLimit() < 1f || player.PlayerId == target.PlayerId || TrackerTarget[player.PlayerId].Contains(target.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;

        player.RpcRemoveAbilityUse();

        TrackerTarget[player.PlayerId].Add(target.PlayerId);
        TargetArrow.Add(player.PlayerId, target.PlayerId);

        SendRPC(player.PlayerId, target.PlayerId);

        Main.DontCancelVoteList.Add(player.PlayerId);
        return true;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        OnVote(shapeshifter, target);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer == null || seer.PlayerId != TrackerId || target != null && seer.PlayerId != target.PlayerId || !TrackerTarget.ContainsKey(seer.PlayerId) || meeting || hud) return string.Empty;
        return TrackerTarget[seer.PlayerId].Aggregate(string.Empty, (current, trackTarget) => current + Utils.ColorString(CanGetColoredArrow.GetBool() ? Main.PlayerColors[trackTarget] : Color.white, TargetArrow.GetArrows(seer, trackTarget))) + LocateArrow.GetArrows(seer);
    }

    public static bool IsTrackTarget(PlayerControl seer, PlayerControl target)
    {
        return seer.IsAlive() && PlayerIdList.Contains(seer.PlayerId)
                              && TrackerTarget[seer.PlayerId].Contains(target.PlayerId)
                              && target.IsAlive();
    }

    public static string GetArrowAndLastRoom(PlayerControl seer, PlayerControl target)
    {
        if (seer == null || target == null) return string.Empty;

        Color roleColor = Utils.GetRoleColor(CustomRoles.Scout);
        string text = Utils.ColorString(roleColor, TargetArrow.GetArrows(seer, target.PlayerId));
        text += Utils.ColorString(roleColor, LocateArrow.GetArrows(seer));

        PlainShipRoom room = Main.PlayerStates[target.PlayerId].LastRoom;

        if (room == null)
            text += Utils.ColorString(Color.gray, "@" + GetString("FailToTrack"));
        else
            text += Utils.ColorString(roleColor, "@" + GetString(room.RoomId.ToString()));

        return text;
    }

    public static void OnPlayerDeath(PlayerControl player)
    {
        if (player == null) return;

        foreach (KeyValuePair<byte, List<byte>> kvp in TrackerTarget)
        {
            if (kvp.Value.Contains(player.PlayerId))
            {
                kvp.Value.Remove(player.PlayerId);
                TargetArrow.Remove(kvp.Key, player.PlayerId);
                LocateArrow.Add(kvp.Key, player.Pos());
            }
        }
    }

    public override void AfterMeetingTasks()
    {
        foreach (KeyValuePair<byte, List<byte>> kvp in TrackerTarget)
        {
            LocateArrow.RemoveAllTarget(kvp.Key);

            foreach (byte id in kvp.Value.ToArray())
            {
                PlayerControl pc = Utils.GetPlayerById(id);

                if (pc == null || !pc.IsAlive())
                {
                    kvp.Value.Remove(id);
                    TargetArrow.Remove(kvp.Key, id);
                }
            }
        }
    }
}