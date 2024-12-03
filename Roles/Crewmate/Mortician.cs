using System.Collections.Generic;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate;

public class Mortician : RoleBase
{
    private const int Id = 7400;
    public static List<byte> PlayerIdList = [];

    private static OptionItem ShowArrows;

    private static Dictionary<byte, string> LastPlayerName = [];
    public static Dictionary<byte, string> MsgToSend = [];

    private byte MorticianId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Mortician);

        ShowArrows = new BooleanOptionItem(Id + 2, "ShowArrows", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mortician]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        LastPlayerName = [];
        MsgToSend = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        MorticianId = playerId;
    }

    public static void OnPlayerDead(PlayerControl target)
    {
        Vector2 pos = target.Pos();
        var minDis = float.MaxValue;
        var minName = string.Empty;

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == target.PlayerId) continue;

            float dis = Vector2.Distance(pc.Pos(), pos);

            if (dis < minDis && dis < 1.5f)
            {
                minDis = dis;
                minName = pc.GetRealName();
            }
        }

        LastPlayerName.TryAdd(target.PlayerId, minName);

        foreach (byte pc in PlayerIdList)
        {
            PlayerControl player = Utils.GetPlayerById(pc);
            if (player == null || !player.IsAlive()) continue;

            LocateArrow.Add(pc, target.transform.position);
        }
    }

    public static void OnReportDeadBody(PlayerControl pc, NetworkedPlayerInfo target)
    {
        foreach (byte apc in PlayerIdList) LocateArrow.RemoveAllTarget(apc);

        if (!pc.Is(CustomRoles.Mortician) || target == null || pc.PlayerId == target.PlayerId) return;

        LastPlayerName.TryGetValue(target.PlayerId, out string name);
        MsgToSend.Add(pc.PlayerId, name == "" ? string.Format(Translator.GetString("MorticianGetNoInfo"), target.PlayerName) : string.Format(Translator.GetString("MorticianGetInfo"), target.PlayerName, name));
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (ShowArrows.GetBool())
        {
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;

            if (seer.PlayerId != MorticianId) return string.Empty;

            return GameStates.IsMeeting ? string.Empty : Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
        }

        return string.Empty;
    }
}