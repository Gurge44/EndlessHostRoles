namespace EHR.Roles.AddOns.Common
{
    internal class YouTuber : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(18800, CustomRoles.Youtuber, canSetNum: true, tab: TabGroup.Addons, teamSpawnOptions: true);
        }
    }
}