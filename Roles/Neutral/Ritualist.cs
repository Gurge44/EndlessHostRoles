using static EHR.Options;

namespace EHR.Neutral
{
    public class Ritualist : RoleBase
    {
        private const int Id = 13000;

        public static OptionItem KillCooldown;
        public static OptionItem RitualMaxCount;
        public static OptionItem CanVent;
        public static OptionItem HasImpostorVision;

        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Ritualist);
            KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Ritualist])
                .SetValueFormat(OptionFormat.Seconds);
            RitualMaxCount = new IntegerOptionItem(Id + 11, "RitualMaxCount", new(0, 15, 1), 1, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Ritualist])
                .SetValueFormat(OptionFormat.Times);
            CanVent = new BooleanOptionItem(Id + 12, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Ritualist]);
            HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Ritualist]);
        }

        public override void Init()
        {
        }

        public override void Add(byte playerId)
        {
        }
    }
}