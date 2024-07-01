namespace EHR.AddOns.Crewmate
{
    public class Finder : IAddon
    {
        public static OptionItem FinderCD;
        public static OptionItem FinderDuration;
        public static OptionItem FinderDelay;
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(15660, CustomRoles.Finder, canSetNum: true);
            FinderCD = new FloatOptionItem(15665, "TrackerCooldown", new(0f, 180f, 1f), 25f, TabGroup.Addons)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Finder])
                .SetValueFormat(OptionFormat.Seconds);
            FinderDuration = new FloatOptionItem(15666, "TrackerDuration", new(0f, 180f, 1f), 10f, TabGroup.Addons)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Finder])
                .SetValueFormat(OptionFormat.Seconds);
            FinderDelay = new FloatOptionItem(15667, "TrackerDelay", new(0f, 180f, 1f), 5f, TabGroup.Addons)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Finder])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}