using System.Linq;
using EHR.Modules;

namespace EHR.Roles;

public class Sleep : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(644292, CustomRoles.Sleep, canSetNum: true, teamSpawnOptions: true);
    }

    public static void CheckGlowNearby(PlayerControl pc)
    {
        if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

        Vector2 pos = pc.Pos();

        if (Main.EnumerateAlivePlayerControls().Any(x => x.Is(CustomRoles.Glow) && FastVector2.DistanceWithinRange(x.Pos(), pos, 1.5f)))
        {
            Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Sleep);
            pc.MarkDirtySettings();
            
            if (pc.AmOwner) Achievements.Type.AlarmClock.Complete();
        }
    }
}