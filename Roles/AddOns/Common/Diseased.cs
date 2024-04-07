using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Diseased : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(111420, CustomRoles.Diseased, canSetNum: true);
            ImpCanBeDiseased = BooleanOptionItem.Create(111426, "ImpCanBeDiseased", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
            CrewCanBeDiseased = BooleanOptionItem.Create(111427, "CrewCanBeDiseased", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
            NeutralCanBeDiseased = BooleanOptionItem.Create(111423, "NeutralCanBeDiseased", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
            DiseasedCDOpt = FloatOptionItem.Create(111424, "DiseasedCDOpt", new(0f, 180f, 1f), 25f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased])
                .SetValueFormat(OptionFormat.Seconds);
            DiseasedCDReset = BooleanOptionItem.Create(111425, "DiseasedCDReset", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
        }
    }
}
