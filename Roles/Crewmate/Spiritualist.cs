using System.Collections.Generic;
using System.Linq;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

internal class Spiritualist : RoleBase
{
    private const int Id = 8100;

    public static List<byte> PlayerIdList = [];

    private static OptionItem ShowGhostArrowEverySeconds;
    private static OptionItem ShowGhostArrowForSeconds;

    public static byte SpiritualistTarget;
    private long LastGhostArrowShowTime;
    private long ShowGhostArrowUntil;
    private byte SpiritualistId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    private bool ShowArrow
    {
        get
        {
            long timestamp = Utils.TimeStamp;

            if (LastGhostArrowShowTime == 0 || LastGhostArrowShowTime + (long)ShowGhostArrowEverySeconds.GetFloat() <= timestamp)
            {
                LastGhostArrowShowTime = timestamp;
                ShowGhostArrowUntil = timestamp + (long)ShowGhostArrowForSeconds.GetFloat();
                return true;
            }

            return ShowGhostArrowUntil >= timestamp;
        }
    }

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Spiritualist);

        ShowGhostArrowEverySeconds = new FloatOptionItem(Id + 10, "SpiritualistShowGhostArrowEverySeconds", new(1f, 60f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritualist])
            .SetValueFormat(OptionFormat.Seconds);

        ShowGhostArrowForSeconds = new FloatOptionItem(Id + 11, "SpiritualistShowGhostArrowForSeconds", new(1f, 60f, 1f), 2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritualist])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
        SpiritualistTarget = 0;
        LastGhostArrowShowTime = 0;
        ShowGhostArrowUntil = 0;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        SpiritualistTarget = byte.MaxValue;
        LastGhostArrowShowTime = 0;
        ShowGhostArrowUntil = 0;
        SpiritualistId = playerId;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public static void OnReportDeadBody(NetworkedPlayerInfo target)
    {
        if (target == null) return;

        if (SpiritualistTarget != byte.MaxValue) RemoveTarget();

        SpiritualistTarget = target.PlayerId;
    }

    public override void AfterMeetingTasks()
    {
        foreach (byte spiritualist in PlayerIdList)
        {
            PlayerControl player = spiritualist.GetPlayer();
            if (!player.IsAlive()) continue;

            LastGhostArrowShowTime = 0;
            ShowGhostArrowUntil = 0;

            PlayerControl target = Main.AllPlayerControls.FirstOrDefault(a => a.PlayerId == SpiritualistTarget);
            if (target == null) continue;

            target.Notify(GetString("SpiritualistTargetMessage"));

            TargetArrow.Add(spiritualist, target.PlayerId);

            var writer = CustomRpcSender.Create("SpiritualistSendMessage");
            writer.StartMessage(target.GetClientId());

            writer.StartRpc(target.NetId, (byte)RpcCalls.SetName)
                .Write(target.Data.NetId)
                .Write(GetString("SpiritualistNoticeTitle"))
                .EndRpc();

            writer.StartRpc(target.NetId, (byte)RpcCalls.SendChat)
                .Write(GetString("SpiritualistNoticeMessage"))
                .EndRpc();

            writer.StartRpc(target.NetId, (byte)RpcCalls.SetName)
                .Write(target.Data.NetId)
                .Write(target.Data.PlayerName)
                .EndRpc();

            writer.EndMessage();
            writer.SendMessage();
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!seer.IsAlive() || seer.PlayerId != SpiritualistId) return string.Empty;

        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;

        if (GameStates.IsMeeting) return string.Empty;

        return SpiritualistTarget != byte.MaxValue && ShowArrow ? Utils.ColorString(seer.GetRoleColor(), TargetArrow.GetArrows(seer, SpiritualistTarget)) : string.Empty;
    }

    public static void RemoveTarget()
    {
        foreach (byte spiritualist in PlayerIdList) TargetArrow.Remove(spiritualist, SpiritualistTarget);

        SpiritualistTarget = byte.MaxValue;
    }
}