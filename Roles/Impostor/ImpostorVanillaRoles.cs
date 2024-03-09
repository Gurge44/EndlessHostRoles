using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    internal static class ImpostorVanillaRoles
    {
        public static void SetupCustomOption()
        {
            SetupRoleOptions(300, TabGroup.ImpostorRoles, CustomRoles.ImpostorTOHE);
            SetupRoleOptions(400, TabGroup.ImpostorRoles, CustomRoles.ShapeshifterTOHE);
            ShapeshiftCD = FloatOptionItem.Create(402, "ShapeshiftCooldown", new(1f, 180f, 1f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ShapeshifterTOHE])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftDur = FloatOptionItem.Create(403, "ShapeshiftDuration", new(1f, 60f, 1f), 10f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ShapeshifterTOHE])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}
