using static EHR.Options;

namespace EHR.Impostor
{
    internal class Vindicator : RoleBase
    {
        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(3400, TabGroup.ImpostorRoles, CustomRoles.Vindicator);
            VindicatorAdditionalVote = new IntegerOptionItem(3410, "MayorAdditionalVote", new(1, 30, 1), 1, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Vindicator])
                .SetValueFormat(OptionFormat.Votes);
            VindicatorHideVote = new BooleanOptionItem(3411, "MayorHideVote", false, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Vindicator]);
        }

        public override void Init()
        {
        }

        public override void Add(byte playerId)
        {
        }
    }
}