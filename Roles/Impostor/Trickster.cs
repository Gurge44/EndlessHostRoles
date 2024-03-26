namespace EHR.Roles.Impostor
{
    internal class Trickster : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(4300, TabGroup.ImpostorRoles, CustomRoles.Trickster);
    }
}