using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Brakar : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14900, CustomRoles.Brakar, canSetNum: true);
            ImpCanBeTiebreaker = BooleanOptionItem.Create(14910, "ImpCanBeTiebreaker", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Brakar]);
            CrewCanBeTiebreaker = BooleanOptionItem.Create(14911, "CrewCanBeTiebreaker", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Brakar]);
            NeutralCanBeTiebreaker = BooleanOptionItem.Create(14912, "NeutralCanBeTiebreaker", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Brakar]);
        }
    }
}
