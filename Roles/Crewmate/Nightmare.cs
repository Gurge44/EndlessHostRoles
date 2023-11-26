namespace TOHE.Roles.Crewmate
{
    public static class Nightmare
    {
        public static void SetupCustomOption() => Options.SetupRoleOptions(642630, TabGroup.CrewmateRoles, CustomRoles.Nightmare);

        public static bool CanBeKilled => !Utils.IsActive(SystemTypes.Electrical);
    }
}
