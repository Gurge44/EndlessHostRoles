using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Fool : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(19200, CustomRoles.Fool, canSetNum: true, tab: TabGroup.Addons);
            ImpCanBeFool = BooleanOptionItem.Create(19210, "ImpCanBeFool", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Fool]);
            CrewCanBeFool = BooleanOptionItem.Create(19211, "CrewCanBeFool", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Fool]);
            NeutralCanBeFool = BooleanOptionItem.Create(19212, "NeutralCanBeFool", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Fool]);
        }
    }
}
