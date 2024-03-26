﻿using static EHR.Options;

namespace EHR.Roles.Impostor
{
    internal class Vindicator : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(3400, TabGroup.ImpostorRoles, CustomRoles.Vindicator);
            VindicatorAdditionalVote = IntegerOptionItem.Create(3410, "MayorAdditionalVote", new(1, 30, 1), 1, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Vindicator])
                .SetValueFormat(OptionFormat.Votes);
            VindicatorHideVote = BooleanOptionItem.Create(3411, "MayorHideVote", false, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Vindicator]);
        }
    }
}
