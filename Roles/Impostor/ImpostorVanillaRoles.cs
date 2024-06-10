using static EHR.Options;

namespace EHR.Roles.Impostor
{
    internal class ImpostorVanillaRoles : IVanillaSettingHolder
    {
        public TabGroup Tab => TabGroup.ImpostorRoles;

        public void SetupCustomOption()
        {
            SetupRoleOptions(300, Tab, CustomRoles.ImpostorEHR);
            SetupRoleOptions(400, Tab, CustomRoles.ShapeshifterEHR);
            ShapeshiftCD = new FloatOptionItem(402, "ShapeshiftCooldown", new(1f, 180f, 1f), 30f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ShapeshifterEHR])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftDur = new FloatOptionItem(403, "ShapeshiftDuration", new(1f, 60f, 1f), 10f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ShapeshifterEHR])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}