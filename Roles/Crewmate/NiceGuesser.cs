using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate;

internal class NiceGuesser : RoleBase
{
    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(8600, TabGroup.CrewmateRoles, CustomRoles.NiceGuesser);

        GGCanGuessTime = new IntegerOptionItem(8610, "GuesserCanGuessTimes", new(0, 15, 1), 15, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser])
            .SetValueFormat(OptionFormat.Times);

        GGCanGuessCrew = new BooleanOptionItem(8611, "GGCanGuessCrew", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser]);

        GGCanGuessAdt = new BooleanOptionItem(8612, "GGCanGuessAdt", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser]);

        GGTryHideMsg = new BooleanOptionItem(8613, "GuesserTryHideMsg", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser])
            .SetColor(Color.green);
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