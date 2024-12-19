using System.Collections.Generic;
using static EHR.Translator;

namespace EHR.Impostor;

public class Consort : RoleBase
{
    private const int Id = 642400;
    private static List<byte> PlayerIdList = [];

    private static OptionItem CD;
    private static OptionItem UseLimit;
    private static OptionItem Duration;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Consort);

        CD = new FloatOptionItem(Id + 10, "RoleBlockCooldown", new(2.5f, 60f, 2.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Consort])
            .SetValueFormat(OptionFormat.Seconds);

        UseLimit = new IntegerOptionItem(Id + 11, "AbilityUseLimit", new(1, 20, 1), 3, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Consort])
            .SetValueFormat(OptionFormat.Times);

        Duration = new FloatOptionItem(Id + 12, "RoleBlockDuration", new(1f, 60f, 1f), 15f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Consort])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(UseLimit.GetInt());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsEnable || killer == null || target == null) return false;

        if (killer.GetAbilityUseLimit() <= 0 || !killer.Is(CustomRoles.Consort)) return true;

        return killer.CheckDoubleTrigger(target, () =>
        {
            killer.RpcRemoveAbilityUse();
            target.BlockRole(Duration.GetFloat());
            killer.Notify(GetString("EscortTargetHacked"));
            killer.SetKillCooldown(CD.GetFloat());
        });
    }
}