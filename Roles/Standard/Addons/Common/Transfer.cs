using System.Collections.Generic;
using System.Linq;
using EHR.Modules;

namespace EHR.Roles;

public class Transfer : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    private static Dictionary<byte, long> LastTP;
    private static Vector2[] VentLocations;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(659700, CustomRoles.Transfer, canSetNum: true, teamSpawnOptions: true);
    }

    public static void Init()
    {
        LastTP = null;
        LateTask.New(() => VentLocations = ShipStatus.Instance && CustomRoles.Transfer.RoleExist() ? ShipStatus.Instance.AllVents.Select(x => new Vector2(x.transform.position.x, x.transform.position.y + 0.3636f)).ToArray() : null, 25f);
    }

    public static void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (VentLocations == null || !PerSecondUpdateScheduler.ShouldRunUpdate(pc.PlayerId) || !pc.Is(CustomRoles.Transfer)) return;

        long now = Utils.TimeStamp;
        LastTP ??= [];
        
        if (LastTP.TryGetValue(pc.PlayerId, out long ts) && ts + 3 >= now) return;
        if (!FastVector2.TryGetClosestInRange(pc.Pos(), VentLocations, 0.8f, out _)) return;

        pc.TPToRandomVent();
        LastTP[pc.PlayerId] = now;
    }
}