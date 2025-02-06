using System.Collections.Generic;

namespace EHR.AddOns.Common;

internal class Stained : IAddon
{
    public static readonly List<byte> VioletNameList = [];
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(15180, CustomRoles.Stained, canSetNum: true, teamSpawnOptions: true);
    }

    public static void OnDeath(PlayerControl pc, PlayerControl killer)
    {
        if (killer == null || pc == null || pc.PlayerId == killer.PlayerId || !killer.IsAlive() || !GameStates.IsInTask) return;

        VioletNameList.Add(killer.PlayerId);
        Utils.NotifyRoles(SpecifyTarget: killer);

        LateTask.New(() =>
        {
            VioletNameList.Remove(killer.PlayerId);
            if (!GameStates.IsMeeting) Utils.NotifyRoles(SpecifyTarget: killer);
        }, 3f, "Stained Killer Violet Name");
    }
}