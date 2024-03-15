using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    internal class ImpostorVanillaRoles : IVanillaSettingHolder
    {
        public TabGroup Tab => TabGroup.ImpostorRoles;

        public void SetupCustomOption()
        {
            SetupRoleOptions(300, Tab, CustomRoles.ImpostorTOHE);
            SetupRoleOptions(400, Tab, CustomRoles.ShapeshifterTOHE);
            ShapeshiftCD = FloatOptionItem.Create(402, "ShapeshiftCooldown", new(1f, 180f, 1f), 30f, Tab, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ShapeshifterTOHE])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftDur = FloatOptionItem.Create(403, "ShapeshiftDuration", new(1f, 60f, 1f), 10f, Tab, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ShapeshifterTOHE])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}
