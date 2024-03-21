using static EHR.Options;

namespace EHR.Roles.Impostor
{
    internal class Undertaker : ISettingHolder
    {
        private const int Id = 720;
        public static OptionItem UndertakerMarkCooldown;
        public static OptionItem UndertakerAssassinateCooldown;
        public static OptionItem UndertakerCanKillAfterAssassinate;

        public void SetupCustomOption()
        {
            SetupRoleOptions(Id + 20, TabGroup.ImpostorRoles, CustomRoles.Undertaker);
            UndertakerMarkCooldown = FloatOptionItem.Create(Id + 30, "UndertakerMarkCooldown", new(0f, 180f, 0.5f), 1f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker])
                .SetValueFormat(OptionFormat.Seconds);
            UndertakerAssassinateCooldown = FloatOptionItem.Create(Id + 31, "UndertakerAssassinateCooldown", new(0f, 180f, 0.5f), 18.5f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker])
                .SetValueFormat(OptionFormat.Seconds);
            UndertakerCanKillAfterAssassinate = BooleanOptionItem.Create(Id + 32, "UndertakerCanKillAfterAssassinate", true, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker]);
        }
    }
}