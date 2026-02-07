using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
    private static Stopwatch ShieldTimer;

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
        
        ShieldTimer = Stopwatch.StartNew();
        Utils.SendRPC(CustomRPC.Blessed, 1);
        Main.Instance.StartCoroutine(CoRoutine());
        return;

        IEnumerator CoRoutine()
        {
            int duration = ShieldDuration.GetInt();
            while (ShieldTimer.GetRemainingTime(duration) > 0 && !(GameStates.IsMeeting || ExileController.Instance || GameStates.IsEnded || !GameStates.InGame)) yield return null;
            
            ShieldTimer.Reset();
            Utils.SendRPC(CustomRPC.Blessed, 2);
            
            if (GameStates.IsInTask && !ExileController.Instance && !AntiBlackout.SkipTasks)
                ShieldActive.ToValidPlayers().Do(x => Utils.NotifyRoles(SpecifySeer: x, SpecifyTarget: x));
            
            ShieldActive = [];
        }
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                ShieldTimer = Stopwatch.StartNew();
                break;
            case 2:
                ShieldTimer.Reset();
                break;
        }
    }

    public static string GetSuffix(PlayerControl seer)
    {
        return !ShieldTimer.IsRunning ? string.Empty : $"<size=80%>{(seer.IsModdedClient() ? string.Format(Translator.GetString("SafeguardSuffixTimer"), ShieldTimer.GetRemainingTime(ShieldDuration.GetInt())) : Translator.GetString("SafeguardSuffix"))}</size>";
    }
}