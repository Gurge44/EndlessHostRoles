using static EHR.Options;

namespace EHR.Neutral
{
    internal class God : RoleBase
    {
        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(18200, TabGroup.NeutralRoles, CustomRoles.God);
            NotifyGodAlive = new BooleanOptionItem(18210, "NotifyGodAlive", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
            GodCanGuess = new BooleanOptionItem(18211, "CanGuess", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
        }

        public override void Init() => throw new System.NotImplementedException();
        public override void Add(byte playerId) => throw new System.NotImplementedException();
    }
}