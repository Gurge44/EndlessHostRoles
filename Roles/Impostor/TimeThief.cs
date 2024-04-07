using System.Collections.Generic;
using System.Linq;

namespace EHR.Roles.Impostor;

public class TimeThief : RoleBase
{
    private const int Id = 3300;
    public static List<byte> playerIdList = [];
    public static OptionItem KillCooldown;
    public static OptionItem DecreaseMeetingTime;
    public static OptionItem LowerLimitVotingTime;
    public static OptionItem ReturnStolenTimeUponDeath;
    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.TimeThief);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeThief])
            .SetValueFormat(OptionFormat.Seconds);
        DecreaseMeetingTime = IntegerOptionItem.Create(Id + 11, "TimeThiefDecreaseMeetingTime", new(0, 100, 1), 10, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeThief])
            .SetValueFormat(OptionFormat.Seconds);
        LowerLimitVotingTime = IntegerOptionItem.Create(Id + 12, "TimeThiefLowerLimitVotingTime", new(0, 300, 5), 50, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeThief])
            .SetValueFormat(OptionFormat.Seconds);
        ReturnStolenTimeUponDeath = BooleanOptionItem.Create(Id + 13, "TimeThiefReturnStolenTimeUponDeath", true, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeThief]);
    }

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();

    private static int StolenTime(byte id)
    {
        return playerIdList.Contains(id) && (Utils.GetPlayerById(id).IsAlive() || !ReturnStolenTimeUponDeath.GetBool())
            ? DecreaseMeetingTime.GetInt() * Main.PlayerStates[id].GetKillCount(true)
            : 0;
    }
    public static int TotalDecreasedMeetingTime()
    {
        int sec = playerIdList.ToArray().Aggregate(0, (current, playerId) => current - StolenTime(playerId));

        Logger.Info($"{sec} second", "TimeThief.TotalDecreasedMeetingTime");
        return sec;
    }

    public override string GetProgressText(byte playerId, bool comms)
        => StolenTime(playerId) > 0 ? Utils.ColorString(Palette.ImpostorRed.ShadeColor(0.5f), $"{-StolenTime(playerId)}s") : string.Empty;
}