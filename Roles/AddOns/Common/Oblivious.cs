using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Oblivious : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15400, CustomRoles.Oblivious, canSetNum: true);
            ImpCanBeOblivious = BooleanOptionItem.Create(15410, "ImpCanBeOblivious", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Oblivious]);
            CrewCanBeOblivious = BooleanOptionItem.Create(15411, "CrewCanBeOblivious", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Oblivious]);
            NeutralCanBeOblivious = BooleanOptionItem.Create(15412, "NeutralCanBeOblivious", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Oblivious]);
            ObliviousBaitImmune = BooleanOptionItem.Create(15413, "ObliviousBaitImmune", false, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Oblivious]);
        }
    }
}
