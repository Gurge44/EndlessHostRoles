using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Bewilder : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15200, CustomRoles.Bewilder, canSetNum: true);
            BewilderVision = FloatOptionItem.Create(15210, "BewilderVision", new(0f, 5f, 0.05f), 0.6f, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bewilder])
                .SetValueFormat(OptionFormat.Multiplier);
            ImpCanBeBewilder = BooleanOptionItem.Create(15211, "ImpCanBeBewilder", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bewilder]);
            CrewCanBeBewilder = BooleanOptionItem.Create(15212, "CrewCanBeBewilder", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bewilder]);
            NeutralCanBeBewilder = BooleanOptionItem.Create(15213, "NeutralCanBeBewilder", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bewilder]);
        }
    }
}
