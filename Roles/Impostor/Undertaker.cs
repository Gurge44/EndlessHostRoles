using static EHR.Options;

namespace EHR.Impostor
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
            UndertakerMarkCooldown = new FloatOptionItem(Id + 30, "UndertakerMarkCooldown", new(0f, 180f, 0.5f), 1f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker])
                .SetValueFormat(OptionFormat.Seconds);
            UndertakerAssassinateCooldown = new FloatOptionItem(Id + 31, "UndertakerAssassinateCooldown", new(0f, 180f, 0.5f), 18.5f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker])
                .SetValueFormat(OptionFormat.Seconds);
            UndertakerCanKillAfterAssassinate = new BooleanOptionItem(Id + 32, "UndertakerCanKillAfterAssassinate", true, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker]);
        }
    }
}