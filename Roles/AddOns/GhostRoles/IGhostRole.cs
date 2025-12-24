using AmongUs.GameOptions;

namespace EHR.AddOns.GhostRoles;

public interface IGhostRole
{
    public Team Team { get; }
    public RoleTypes RoleTypes { get; }
    public int Cooldown { get; }
    public void OnProtect(PlayerControl pc, PlayerControl target);
    public void OnAssign(PlayerControl pc);
    public void SetupCustomOption();
}