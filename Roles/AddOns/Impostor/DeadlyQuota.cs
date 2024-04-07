namespace EHR.Roles.AddOns.Impostor
{
    internal class DeadlyQuota : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(14650, CustomRoles.DeadlyQuota, canSetNum: true);
            Options.DQNumOfKillsNeeded = IntegerOptionItem.Create(14660, "DQNumOfKillsNeeded", new(1, 14, 1), 3, TabGroup.Addons)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.DeadlyQuota]);
        }
    }
}
