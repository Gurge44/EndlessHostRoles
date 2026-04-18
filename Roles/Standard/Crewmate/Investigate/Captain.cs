using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

public class Captain : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private static OptionItem CancelVote;

    private string LastNotify;
    private byte CaptainId;
    private int Count;
    private byte TargetId;
    private List<SystemTypes> TargetRooms;

    public override void SetupCustomOption()
    {
        StartSetup(655300)
            .CreateVoteCancellingUseSetting(ref CancelVote);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        LastNotify = string.Empty;
        CaptainId = playerId;
        Count = 0;
        TargetId = byte.MaxValue;
        TargetRooms = [];
    }

    public override bool OnVote(PlayerControl voter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return false;
        if (voter == null || target == null || voter.PlayerId == target.PlayerId || Main.DontCancelVoteList.Contains(voter.PlayerId)) return false;

        TargetId = target.PlayerId;

        Main.DontCancelVoteList.Add(voter.PlayerId);
        return true;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        OnVote(shapeshifter, target);
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (lowLoad || pc.PlayerId != TargetId || !GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
        
        var room = pc.GetPlainShipRoom();
        
        if (room != null && room.RoomId != SystemTypes.Hallway && (TargetRooms.Count == 0 || TargetRooms[^1] != room.RoomId))
            TargetRooms.Add(room.RoomId);
    }

    public override void OnReportDeadBody()
    {
        if (TargetRooms.Count == 0) return;
        
        LateTask.New(() =>
        {
            string msg = string.Format(Translator.GetString("Captain.TargetInfo"), TargetId.ColoredPlayerName(), string.Join(" ➡ ", TargetRooms.ConvertAll(x => Translator.GetString(x.ToString()))));
            Utils.SendMessage(msg, CaptainId, CustomRoles.Captain.ToColoredString(), importance: MessageImportance.High);
            TargetRooms = [];
        }, 10f, "Captain Message");
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || Count++ < 20) return;
        Count = 0;

        var room = pc.GetPlainShipRoom();
        
        if (room == null)
        {
            if (string.IsNullOrEmpty(LastNotify)) return;
            LastNotify = string.Empty;
            Utils.SendRPC(CustomRPC.SyncRoleData, CaptainId, LastNotify);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            return;
        }

        string notify = string.Join(", ", Main.EnumerateAlivePlayerControls().Without(pc).Where(x => x.IsInRoom(room)).Select(x => x.PlayerId.ColoredPlayerName()));
        if (notify == LastNotify) return;
        LastNotify = notify;
        Utils.SendRPC(CustomRPC.SyncRoleData, CaptainId, LastNotify);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        LastNotify = reader.ReadString();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != CaptainId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;
        return string.Format(Translator.GetString("Captain.Suffix"), string.IsNullOrEmpty(LastNotify) ? Translator.GetString("None") : LastNotify);
    }
}