namespace EHR.Roles;

public class Transmitter : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
    }

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(642610, TabGroup.CrewmateRoles, CustomRoles.Transmitter);
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        pc.TP(FastVector2.TryGetClosestPlayerTo(pc, out PlayerControl closest) ? closest : pc);
    }
}