using System;
using System.Collections.Generic;
using System.Linq;

namespace EHR.Roles;

public class Tired : IAddon
{
    public AddonTypes Type => AddonTypes.ImpOnly;

    private static Dictionary<byte, int> KillsThisRound;

    private static OptionItem MaxKillsPerRound;
    
    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(656300, CustomRoles.Tired, canSetNum: true);
        
        MaxKillsPerRound = new IntegerOptionItem(656310, "TiredMaxKillsPerRound", new(1, 10, 1), 3, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tired]);
    }

    public static void Reset()
    {
        try { KillsThisRound = Main.CachedAlivePlayerControls().Where(x => x.Is(CustomRoles.Tired)).ToDictionary(x => x.PlayerId, _ => 0); }
        catch (Exception e) { Utils.ThrowException(e); }

        if (KillsThisRound.Count == 0)
            KillsThisRound = null;
    }
    
    public static void OnMurder(byte killerId)
    {
        if (KillsThisRound == null || !KillsThisRound.ContainsKey(killerId)) return;
        KillsThisRound[killerId]++;
    }
    
    public static bool CheckMurderLimit(byte killerId)
    {
        if (KillsThisRound == null || !KillsThisRound.TryGetValue(killerId, out int kills)) return true;
        return kills < MaxKillsPerRound.GetInt();
    }
}