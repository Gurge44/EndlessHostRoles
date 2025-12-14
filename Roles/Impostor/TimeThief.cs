using System.Collections.Generic;
using System.Linq;

namespace EHR.Impostor;

public class TimeThief : RoleBase
{
    private const int Id = 3300;
    public static List<byte> PlayerIdList = [];
    public static OptionItem KillCooldown;
    public static OptionItem DecreaseMeetingTime;
    public static OptionItem LowerLimitVotingTime;
    public static OptionItem ReturnStolenTimeUponDeath;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.TimeThief);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeThief])
            .SetValueFormat(OptionFormat.Seconds);

        DecreaseMeetingTime = new IntegerOptionItem(Id + 11, "TimeThiefDecreaseMeetingTime", new(0, 100, 1), 10, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeThief])
            .SetValueFormat(OptionFormat.Seconds);

        LowerLimitVotingTime = new IntegerOptionItem(Id + 12, "TimeThiefLowerLimitVotingTime", new(0, 300, 5), 50, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeThief])
            .SetValueFormat(OptionFormat.Seconds);

        ReturnStolenTimeUponDeath = new BooleanOptionItem(Id + 13, "TimeThiefReturnStolenTimeUponDeath", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeThief]);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    private static int StolenTime(byte id)
    {
        return PlayerIdList.Contains(id) && (Utils.GetPlayerById(id).IsAlive() || !ReturnStolenTimeUponDeath.GetBool())
            ? DecreaseMeetingTime.GetInt() * Main.PlayerStates[id].GetKillCount()
            : 0;
    }

    public static int TotalDecreasedMeetingTime()
    {
        int sec = PlayerIdList.Aggregate(0, (current, playerId) => current - StolenTime(playerId));

        Logger.Info($"{sec} second", "TimeThief.TotalDecreasedMeetingTime");
        return sec;
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return StolenTime(playerId) > 0 ? Utils.ColorString(Palette.ImpostorRed.ShadeColor(0.5f), $"{-StolenTime(playerId)}s") : string.Empty;
    }
}