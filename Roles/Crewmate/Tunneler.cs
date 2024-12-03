using System.Collections.Generic;

namespace EHR.Crewmate;

internal class Tunneler : RoleBase
{
    public static Dictionary<byte, Vector2> TunnelerPositions = [];
    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(5592, TabGroup.CrewmateRoles, CustomRoles.Tunneler);
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
        TunnelerPositions = [];
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var ProgressText = new StringBuilder();

        ProgressText.Append(base.GetProgressText(playerId, comms));
        if (TunnelerPositions.ContainsKey(playerId)) ProgressText.Append('●');

        return ProgressText.ToString();
    }

    public override void OnPet(PlayerControl pc)
    {
        if (TunnelerPositions.TryGetValue(pc.PlayerId, out Vector2 ps))
        {
            pc.TP(ps);
            TunnelerPositions.Remove(pc.PlayerId);
        }
        else
            TunnelerPositions[pc.PlayerId] = pc.Pos();
    }
}