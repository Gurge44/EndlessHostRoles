using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.AddOns.GhostRoles;

internal class GA : IGhostRole
{
    private static OptionItem ProtectDuration;
    private static OptionItem CD;

    public HashSet<byte> ProtectionList = [];

    public Team Team => Team.Crewmate;
    public RoleTypes RoleTypes => RoleTypes.GuardianAngel;
    public int Cooldown => CD.GetInt();

    public void OnProtect(PlayerControl pc, PlayerControl target)
    {
        if (ProtectionList.Add(target.PlayerId)) LateTask.New(() => ProtectionList.Remove(target.PlayerId), ProtectDuration.GetFloat());
    }

    public void OnAssign(PlayerControl pc)
    {
        ProtectionList = [];
    }

    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(649600, TabGroup.OtherRoles, CustomRoles.GA);

        ProtectDuration = new IntegerOptionItem(649602, "BKProtectDuration", new(1, 90, 1), 5, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.GA])
            .SetValueFormat(OptionFormat.Seconds);

        CD = new IntegerOptionItem(649603, "AbilityCooldown", new(0, 120, 1), 30, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.GA])
            .SetValueFormat(OptionFormat.Seconds);
    }
}