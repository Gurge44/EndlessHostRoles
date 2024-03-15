using static TOHE.Options;

namespace TOHE.Roles.AddOns.Crewmate
{
    internal class Bloodlust : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        private const int Id = 15790;
        public static OptionItem KCD;
        public static OptionItem CanVent;
        public static OptionItem HasImpVision;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(Id, CustomRoles.Bloodlust);
            KCD = FloatOptionItem.Create(Id + 3, "KillCooldown", new(0f, 60f, 2.5f), 30f, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodlust])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(Id + 4, "CanVent", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodlust]);
            HasImpVision = BooleanOptionItem.Create(Id + 5, "ImpostorVision", false, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodlust]);
        }
    }
}
