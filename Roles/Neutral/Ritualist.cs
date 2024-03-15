using static TOHE.Options;

namespace TOHE.Roles.Neutral
{
    public class Ritualist : ISettingHolder
    {
        private const int Id = 13000;

        public static OptionItem KillCooldown;
        public static OptionItem RitualMaxCount;
        public static OptionItem CanVent;
        public static OptionItem HasImpostorVision;

        public void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Ritualist);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ritualist])
                .SetValueFormat(OptionFormat.Seconds);
            RitualMaxCount = IntegerOptionItem.Create(Id + 11, "RitualMaxCount", new(0, 15, 1), 1, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ritualist])
                .SetValueFormat(OptionFormat.Times);
            CanVent = BooleanOptionItem.Create(Id + 12, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ritualist]);
            HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ritualist]);
        }
    }
}