namespace EHR.AddOns.Impostor
{
    internal class TicketsStealer : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(16100, CustomRoles.TicketsStealer, canSetNum: true, tab: TabGroup.Addons);
            Options.TicketsPerKill = new FloatOptionItem(16110, "TicketsPerKill", new(0.1f, 90f, 0.1f), 0.5f, TabGroup.Addons)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.TicketsStealer]);
        }
    }
}