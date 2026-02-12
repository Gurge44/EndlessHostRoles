using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Diagnostics;
using EHR.Modules.Extensions;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Roles;

public class Scanner : RoleBase
{
    private static bool On;
    private const int Id = 679900;

    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem Range;
    private static OptionItem TimeLimit;
    
    private static Dictionary<byte, Stopwatch> InRangeTimers = [];

    private bool AbilityActive;
    
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(Id)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref Range, 2.5f, new FloatValueRule(0.25f, 5f, 0.25f), OptionFormat.Multiplier, overrideName: "ScannerAbilityRange")
            .AutoSetupOption(ref TimeLimit, 5f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds, overrideName: "ScannerTimeLimit");
    }

    public override void Init()
    {
        On = false;
        InRangeTimers = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (!IsEnable)
            return false;

        if (!AbilityActive)
        {
            AbilityActive = true;
            LateTask.New(() => AbilityActive = false, AbilityDuration.GetFloat());
        }
        
        return false;
    }
    
    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!AbilityActive) return;

        foreach (PlayerControl apc in Main.EnumerateAlivePlayerControls())
        {
            if (apc.PlayerId == pc.PlayerId) continue;
            if (!Main.Invisible.Contains(apc.PlayerId)) continue;

            if (FastVector2.DistanceWithinRange(pc.Pos(), apc.Pos(), Range.GetFloat()))
            {
                if (!InRangeTimers.ContainsKey(apc.PlayerId))
                    InRangeTimers[apc.PlayerId] = Stopwatch.StartNew();

                float time = TimeLimit.GetFloat();
                apc.Notify(string.Format(GetString("ScannerWarningInRange"), Mathf.Round(InRangeTimers[apc.PlayerId].GetRemainingTime((int)time))), time: 1f, overrideAll: true);

                if (InRangeTimers[apc.PlayerId].Elapsed.TotalSeconds >= time)
                {
                    apc.RpcMakeVisible();
                    apc.Notify(GetString("ScannerNowVisible"), overrideAll: true);

                    InRangeTimers[apc.PlayerId] = Stopwatch.StartNew();

                    return;
                }
            }
            else
            {
                InRangeTimers[apc.PlayerId] = Stopwatch.StartNew();
            }
        }
    }
}
