using static EHR.Options;

namespace EHR.Neutral
{
    public class Juggernaut : RoleBase
    {
        private const int Id = 12300;

        public static OptionItem DefaultKillCooldown;
        public static OptionItem ReduceKillCooldown;
        public static OptionItem MinKillCooldown;
        public static OptionItem HasImpostorVision;
        public static OptionItem CanVent;

        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Juggernaut);

            DefaultKillCooldown = new FloatOptionItem(Id + 10, "SansDefaultKillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Juggernaut])
                .SetValueFormat(OptionFormat.Seconds);

            ReduceKillCooldown = new FloatOptionItem(Id + 11, "SansReduceKillCooldown", new(0f, 30f, 0.5f), 4f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Juggernaut])
                .SetValueFormat(OptionFormat.Seconds);

            MinKillCooldown = new FloatOptionItem(Id + 12, "SansMinKillCooldown", new(0f, 30f, 0.5f), 10f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Juggernaut])
                .SetValueFormat(OptionFormat.Seconds);

            HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Juggernaut]);
            CanVent = new BooleanOptionItem(Id + 14, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Juggernaut]);
        }

        public override void Init() { }

        public override void Add(byte playerId) { }
    }
}