using static EHR.Options;

namespace EHR.Roles.AddOns.Crewmate
{
    internal class Bloodlust : IAddon
    {
        private const int Id = 15790;
        public static OptionItem KCD;
        public static OptionItem CanVent;
        public static OptionItem HasImpVision;
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(Id, CustomRoles.Bloodlust);
            KCD = new FloatOptionItem(Id + 3, "KillCooldown", new(0f, 60f, 2.5f), 30f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodlust])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = new BooleanOptionItem(Id + 4, "CanVent", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodlust]);
            HasImpVision = new BooleanOptionItem(Id + 5, "ImpostorVision", false, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodlust]);
        }
    }
}