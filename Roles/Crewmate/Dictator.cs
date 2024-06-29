namespace EHR.Crewmate
{
    internal class Dictator : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(9100, TabGroup.CrewmateRoles, CustomRoles.Dictator);
    }
}