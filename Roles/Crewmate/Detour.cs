namespace EHR.Roles.Crewmate
{
    internal class Detour : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(5590, TabGroup.CrewmateRoles, CustomRoles.Detour);
    }
}