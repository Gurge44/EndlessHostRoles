namespace EHR.Crewmate;

internal class Dictator : RoleBase
{
    public override bool IsEnable => false;

    public static OptionItem MinTasksToDictate;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(652400, TabGroup.CrewmateRoles, CustomRoles.Dictator);

        MinTasksToDictate = new IntegerOptionItem(652410, "Dictator.MinTasksToDictate", new(0, 10, 1), 3, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dictator]);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = true;
        countsAs = 1;
    }
}