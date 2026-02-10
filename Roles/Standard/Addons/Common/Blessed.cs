using System;
using System.Collections.Generic;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class Blessed : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    private static OptionItem ShieldDuration;
    private static OptionItem MinLivingPlayersToActivateShield;

    public static HashSet<byte> ShieldActive = [];
    private static CountdownTimer ShieldTimer;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(657900, CustomRoles.Blessed, canSetNum: true, teamSpawnOptions: true);

        ShieldDuration = new IntegerOptionItem(657910, "ShieldDuration", new(1, 120, 1), 30, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Blessed])
            .SetValueFormat(OptionFormat.Seconds);
        
        MinLivingPlayersToActivateShield = new IntegerOptionItem(657911, "Blessed.MinLivingPlayersToActivateShield", new(1, 15, 1), 6, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Blessed])
            .SetValueFormat(OptionFormat.Players);
    }

    public static void AfterMeetingTasks()
    {
        var aapc = Main.AllAlivePlayerControls;
        if (aapc.Count < MinLivingPlayersToActivateShield.GetInt()) return;
        
        foreach (PlayerControl pc in aapc)
        {
            if (!pc.Is(CustomRoles.Blessed)) continue;
            ShieldActive.Add(pc.PlayerId);
        }
        
        ShieldTimer = new CountdownTimer(ShieldDuration.GetInt(), () =>
        {
            ShieldTimer = null;
            ShieldActive.ToValidPlayers().Do(x => Utils.NotifyRoles(SpecifySeer: x, SpecifyTarget: x));
            ShieldActive.Clear();
        }, onCanceled: () =>
        {
            ShieldTimer = null;
            ShieldActive.Clear();
        });
        Utils.SendRPC(CustomRPC.Blessed);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        ShieldTimer = new CountdownTimer(ShieldDuration.GetInt(), () => ShieldTimer = null, onCanceled: () => ShieldTimer = null);
    }

    public static string GetSuffix(PlayerControl seer)
    {
        return ShieldTimer == null ? string.Empty : $"<size=80%>{(seer.IsModdedClient() ? string.Format(Translator.GetString("SafeguardSuffixTimer"), (int)Math.Ceiling(ShieldTimer.Remaining.TotalSeconds)) : Translator.GetString("SafeguardSuffix"))}</size>";
    }
}