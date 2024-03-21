using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Antidote : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(222420, CustomRoles.Antidote, canSetNum: true);
            ImpCanBeAntidote = BooleanOptionItem.Create(222426, "ImpCanBeAntidote", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
            CrewCanBeAntidote = BooleanOptionItem.Create(222427, "CrewCanBeAntidote", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
            NeutralCanBeAntidote = BooleanOptionItem.Create(222423, "NeutralCanBeAntidote", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
            AntidoteCDOpt = FloatOptionItem.Create(222424, "AntidoteCDOpt", new(0f, 180f, 1f), 5f, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote])
                .SetValueFormat(OptionFormat.Seconds);
            AntidoteCDReset = BooleanOptionItem.Create(222425, "AntidoteCDReset", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
        }
    }
}