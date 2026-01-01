using System.Collections.Generic;
using static EHR.Options;

namespace EHR.Impostor;

public class Lurker : RoleBase
{
    private const int Id = 2100;
    public static List<byte> PlayerIdList = [];

    private static OptionItem DefaultKillCooldown;
    private static OptionItem ReduceKillCooldown;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Lurker);

        DefaultKillCooldown = new FloatOptionItem(Id + 10, "ArroganceDefaultKillCooldown", new(1f, 180f, 1f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lurker])
            .SetValueFormat(OptionFormat.Seconds);

        ReduceKillCooldown = new FloatOptionItem(Id + 11, "ArroganceReduceKillCooldown", new(1f, 10f, 1f), 1f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Lurker])
            .SetValueFormat(OptionFormat.Seconds);
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
        Main.AllPlayerKillCooldown[id] = DefaultKillCooldown.GetFloat();
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (!pc.Is(CustomRoles.Lurker)) return;

        float newCd = Main.AllPlayerKillCooldown[pc.PlayerId] - ReduceKillCooldown.GetFloat();
        if (newCd <= 0) return;

        Main.AllPlayerKillCooldown[pc.PlayerId] = newCd;
        pc.SyncSettings();
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        killer.ResetKillCooldown();
    }
}
