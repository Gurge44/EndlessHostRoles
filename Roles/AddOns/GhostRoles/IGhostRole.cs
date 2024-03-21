namespace EHR.Roles.AddOns.GhostRoles
{
    public interface IGhostRole
    {
        public Team Team { get; }
        public void OnProtect(PlayerControl pc, PlayerControl target);
        public void OnAssign(PlayerControl pc);
    }
}
