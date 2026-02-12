using System.Collections.Generic;

namespace EHR.Roles;

public class Sonar : IAddon
{
    private static readonly Dictionary<byte, byte> Target = [];
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(13642, CustomRoles.Sonar, canSetNum: true, teamSpawnOptions: true);
    }

    public static string GetSuffix(PlayerControl seer, bool meeting)
    {
        if (meeting || !seer.Is(CustomRoles.Sonar) || !Target.TryGetValue(seer.PlayerId, out byte targetId)) return string.Empty;

        return TargetArrow.GetArrows(seer, targetId);
    }

    public static void OnFixedUpdate(PlayerControl seer)
    {
        if (!seer.Is(CustomRoles.Sonar) || !GameStates.IsInTask || seer.inVent) return;
        
        if (!FastVector2.TryGetClosestPlayerTo(seer, out PlayerControl closest)) return;

        if (Target.TryGetValue(seer.PlayerId, out byte targetId))
        {
            if (targetId != closest.PlayerId)
            {
                Target[seer.PlayerId] = closest.PlayerId;
                TargetArrow.Remove(seer.PlayerId, targetId);
                TargetArrow.Add(seer.PlayerId, closest.PlayerId);
                Utils.NotifyRoles(SpecifySeer: seer, SpecifyTarget: closest);
            }
        }
        else
        {
            Target[seer.PlayerId] = closest.PlayerId;
            TargetArrow.Add(seer.PlayerId, closest.PlayerId);
            Utils.NotifyRoles(SpecifySeer: seer, SpecifyTarget: closest);
        }
    }
}