using System.Collections.Generic;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Crewmate;

public class Mortician : RoleBase
{
    private const int Id = 7400;
    private static List<byte> playerIdList = [];

    private static OptionItem ShowArrows;

    private static Dictionary<byte, string> lastPlayerName = [];
    public static Dictionary<byte, string> msgToSend = [];

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Mortician);
        ShowArrows = new BooleanOptionItem(Id + 2, "ShowArrows", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Mortician]);
    }

    public override void Init()
    {
        playerIdList = [];
        lastPlayerName = [];
        msgToSend = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }

    public static void OnPlayerDead(PlayerControl target)
    {
        var pos = target.Pos();
        float minDis = float.MaxValue;
        string minName = string.Empty;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == target.PlayerId) continue;
            var dis = Vector2.Distance(pc.Pos(), pos);
            if (dis < minDis && dis < 1.5f)
            {
                minDis = dis;
                minName = pc.GetRealName();
            }
        }

        lastPlayerName.TryAdd(target.PlayerId, minName);
        foreach (byte pc in playerIdList)
        {
            var player = Utils.GetPlayerById(pc);
            if (player == null || !player.IsAlive()) continue;
            LocateArrow.Add(pc, target.transform.position);
        }
    }

    public static void OnReportDeadBody(PlayerControl pc, GameData.PlayerInfo target)
    {
        foreach (byte apc in playerIdList)
        {
            LocateArrow.RemoveAllTarget(apc);
        }

        if (!pc.Is(CustomRoles.Mortician) || target == null || pc.PlayerId == target.PlayerId) return;
        lastPlayerName.TryGetValue(target.PlayerId, out var name);
        msgToSend.Add(pc.PlayerId, name == "" ? string.Format(Translator.GetString("MorticianGetNoInfo"), target.PlayerName) : string.Format(Translator.GetString("MorticianGetInfo"), target.PlayerName, name));
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool m = false)
    {
        if (ShowArrows.GetBool())
        {
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
            return GameStates.IsMeeting ? string.Empty : Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
        }

        return string.Empty;
    }
}