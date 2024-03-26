namespace EHR.Roles.AddOns.Impostor
{
    internal class TicketsStealer : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(16100, CustomRoles.TicketsStealer, canSetNum: true, tab: TabGroup.Addons);
            Options.TicketsPerKill = FloatOptionItem.Create(16110, "TicketsPerKill", new(0.1f, 90f, 0.1f), 0.5f, TabGroup.Addons, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.TicketsStealer]);
        }
    }
}
