using static EHR.Options;

namespace EHR.Neutral
{
    internal class Phantasm : RoleBase
    {
        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(11400, TabGroup.NeutralRoles, CustomRoles.Phantasm);
            PhantomCanVent = new BooleanOptionItem(11410, "CanVent", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Phantasm]);
            PhantomSnatchesWin = new BooleanOptionItem(11411, "SnatchesWin", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Phantasm]);
            PhantomCanGuess = new BooleanOptionItem(11412, "CanGuess", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Phantasm]);
            OverrideTasksData.Create(11413, TabGroup.NeutralRoles, CustomRoles.Phantasm);
        }

        public override void Init() => throw new System.NotImplementedException();
        public override void Add(byte playerId) => throw new System.NotImplementedException();
    }
}