using static EHR.Options;

namespace EHR.Neutral;

internal class Specter : RoleBase
{
    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(11400, TabGroup.NeutralRoles, CustomRoles.Specter);

        PhantomCanVent = new BooleanOptionItem(11410, "CanVent", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Specter]);

        PhantomSnatchesWin = new BooleanOptionItem(11411, "SnatchesWin", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Specter]);

        PhantomCanGuess = new BooleanOptionItem(11412, "CanGuess", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Specter]);

        OverrideTasksData.Create(11413, TabGroup.NeutralRoles, CustomRoles.Specter);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (completedTaskCount + 1 >= totalTaskCount && !pc.IsAlive() && PhantomSnatchesWin.GetBool())
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Specter);
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
        }
    }
}