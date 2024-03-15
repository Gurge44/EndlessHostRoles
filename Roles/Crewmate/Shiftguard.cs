namespace TOHE.Roles.Crewmate
{
    internal class Shiftguard : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(5594, TabGroup.CrewmateRoles, CustomRoles.Shiftguard);
    }
}