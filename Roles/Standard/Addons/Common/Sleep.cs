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
        byte pcId = pc.PlayerId;
        var alivePlayers = Main.CachedAlivePlayerControls();

        for (int index = 0; index < alivePlayers.Count; index++)
        {
            PlayerControl target = alivePlayers[index];
            if (!target.Is(CustomRoles.Glow) || !FastVector2.DistanceWithinRange(target.Pos(), pos, 1.5f)) continue;

            Main.PlayerStates[pcId].RemoveSubRole(CustomRoles.Sleep);
            pc.MarkDirtySettings();

            if (pc.AmOwner) Achievements.Type.AlarmClock.Complete();

            return;
        }
    }
}