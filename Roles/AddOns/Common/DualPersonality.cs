using static TOHE.Options;

namespace TOHE.Roles.AddOns.Common
{
    internal class DualPersonality : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14700, CustomRoles.DualPersonality, canSetNum: true);
            ImpCanBeDualPersonality = BooleanOptionItem.Create(14710, "ImpCanBeDualPersonality", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DualPersonality]);
            CrewCanBeDualPersonality = BooleanOptionItem.Create(14711, "CrewCanBeDualPersonality", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DualPersonality]);
            DualVotes = BooleanOptionItem.Create(14712, "DualVotes", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DualPersonality]);
        }
    }
}
