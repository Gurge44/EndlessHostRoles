using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Oblivious : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15400, CustomRoles.Oblivious, canSetNum: true, teamSpawnOptions: true);
            ObliviousBaitImmune = BooleanOptionItem.Create(15413, "ObliviousBaitImmune", false, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Oblivious]);
        }
    }
}