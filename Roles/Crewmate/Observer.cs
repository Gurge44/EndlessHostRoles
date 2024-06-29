namespace EHR.Crewmate
{
    internal class Observer : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(7500, TabGroup.CrewmateRoles, CustomRoles.Observer);
    }
}