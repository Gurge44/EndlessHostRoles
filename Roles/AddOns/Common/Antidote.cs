using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Antidote : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            const int id = 648500;
            SetupAdtRoleOptions(id, CustomRoles.Antidote, canSetNum: true);
            ImpCanBeAntidote = BooleanOptionItem.Create(id + 3, "ImpCanBeAntidote", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
            CrewCanBeAntidote = BooleanOptionItem.Create(id + 4, "CrewCanBeAntidote", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
            NeutralCanBeAntidote = BooleanOptionItem.Create(id + 5, "NeutralCanBeAntidote", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
            AntidoteCDOpt = FloatOptionItem.Create(id + 6, "AntidoteCDOpt", new(0f, 180f, 1f), 5f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote])
                .SetValueFormat(OptionFormat.Seconds);
            AntidoteCDReset = BooleanOptionItem.Create(id + 7, "AntidoteCDReset", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
        }
    }
}