namespace EHR.AddOns.Impostor;

public class Underdog : IAddon
{
    public AddonTypes Type => AddonTypes.ImpOnly;

    public static OptionItem UnderdogMaximumPlayersNeededToKill;
    public static OptionItem UnderdogKillCooldownWithMorePlayersAlive;
    public static OptionItem UnderdogKillCooldownWithLessPlayersAlive;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(651080, CustomRoles.Underdog, canSetNum: true);

        UnderdogMaximumPlayersNeededToKill = new IntegerOptionItem(651090, "UnderdogMaximumPlayersNeededToKill", new(1, 15, 1), 8, TabGroup.Addons)
            .SetValueFormat(OptionFormat.Players)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Underdog]);
        
        UnderdogKillCooldownWithMorePlayersAlive = new IntegerOptionItem(651091, "UnderdogKillCooldownWithMorePlayersAlive", new(0, 90, 1), 30, TabGroup.Addons)
            .SetValueFormat(OptionFormat.Seconds)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Underdog]);
        
        UnderdogKillCooldownWithLessPlayersAlive = new IntegerOptionItem(651092, "HitmanLowKCD", new(0, 90, 1), 15, TabGroup.Addons)
            .SetValueFormat(OptionFormat.Seconds)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Underdog]);
    }
}