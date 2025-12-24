using AmongUs.GameOptions;

namespace EHR.AddOns.GhostRoles;

public class Facilitator : IGhostRole
{
    private static OptionItem CD;

    public Team Team => Team.Coven;
    public RoleTypes RoleTypes => RoleTypes.GuardianAngel;
    public int Cooldown => CD.GetInt();

    public void OnProtect(PlayerControl pc, PlayerControl target)
    {
        if (!Main.PlayerStates.TryGetValue(target.PlayerId, out PlayerState state) || state.Role is not Coven.Coven covenRole) return;

        covenRole.HasNecronomicon = true;
        covenRole.OnReceiveNecronomicon();
    }

    public void OnAssign(PlayerControl pc) { }

    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(654600, TabGroup.OtherRoles, CustomRoles.Facilitator);

        CD = new IntegerOptionItem(649702, "AbilityCooldown", new(0, 120, 1), 60, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Facilitator])
            .SetValueFormat(OptionFormat.Seconds);
    }
}