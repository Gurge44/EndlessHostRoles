using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class DualPersonality : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14700, CustomRoles.DualPersonality, canSetNum: true, teamSpawnOptions: true);
            DualVotes = BooleanOptionItem.Create(14712, "DualVotes", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DualPersonality]);
        }
    }
}