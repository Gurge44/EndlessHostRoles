using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Diseased : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            const int id = 648600;
            SetupAdtRoleOptions(111420, CustomRoles.Diseased, canSetNum: true);
            ImpCanBeDiseased = BooleanOptionItem.Create(id + 3, "ImpCanBeDiseased", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
            CrewCanBeDiseased = BooleanOptionItem.Create(id + 4, "CrewCanBeDiseased", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
            NeutralCanBeDiseased = BooleanOptionItem.Create(id + 5, "NeutralCanBeDiseased", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
            DiseasedCDOpt = FloatOptionItem.Create(id + 6, "DiseasedCDOpt", new(0f, 180f, 1f), 25f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased])
                .SetValueFormat(OptionFormat.Seconds);
            DiseasedCDReset = BooleanOptionItem.Create(id + 7, "DiseasedCDReset", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
        }
    }
}