namespace EHR.Roles.Crewmate
{
    internal class Needy : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(5700, TabGroup.CrewmateRoles, CustomRoles.Needy);
    }
}