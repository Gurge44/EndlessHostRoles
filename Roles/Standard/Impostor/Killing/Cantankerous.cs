using System.Collections.Generic;
using static EHR.Options;

namespace EHR.Roles;

public class Cantankerous : RoleBase
{
    private const int Id = 642860;
    private static List<byte> PlayerIdList;

    private static OptionItem PointsGainedPerEjection;
    private static OptionItem StartingPoints;
    private static OptionItem KCD;

    public override bool IsEnable => PlayerIdList is { Count: > 0 };

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Cantankerous);

        KCD = new FloatOptionItem(Id + 5, "KillCooldown", new(0f, 60f, 0.5f), 15f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
            .SetValueFormat(OptionFormat.Seconds);

        PointsGainedPerEjection = new IntegerOptionItem(Id + 6, "CantankerousPointsGainedPerEjection", new(1, 5, 1), 2, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
            .SetValueFormat(OptionFormat.Times);

        StartingPoints = new IntegerOptionItem(Id + 7, "CantankerousStartingPoints", new(0, 5, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = null;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList ??= [];
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(StartingPoints.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList?.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KCD.GetFloat();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() > 0;
    }

    public static void OnCrewmateEjected()
    {
        if (PlayerIdList == null) return;
        
        int value = PointsGainedPerEjection.GetInt();

        foreach (byte id in PlayerIdList)
        {
            var pc = id.GetPlayer();
            if (!pc || !pc.IsAlive()) continue;
            
            pc.RpcIncreaseAbilityUseLimitBy(value);
        }
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        killer.RpcRemoveAbilityUse();
    }
}