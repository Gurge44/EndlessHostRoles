using AmongUs.GameOptions;
using System.Collections.Generic;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Roles;

public class Scanner : RoleBase
{
    private const int Id = 679900;

    private static OptionItem Cooldown;
    private static OptionItem Duration;
    private static OptionItem Range;
    private static OptionItem TimeLimit;
    private static bool On;

    public bool AbilityActive;
    public static readonly Dictionary<byte, float> InRangeTimers = [];
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(Id)
            .AutoSetupOption(ref Cooldown, 30f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds, overrideName: "ScannerCooldown")
            .AutoSetupOption(ref Duration, 10f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds, overrideName: "ScannerDuration")
            .AutoSetupOption(ref Range, 2.5f, new FloatValueRule(0.25f, 5f, 0.25f), OptionFormat.Multiplier, overrideName: "ScannerAbilityRange")
            .AutoSetupOption(ref TimeLimit, 5f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds, overrideName: "ScannerTimeLimit");
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        AURoleOptions.PhantomCooldown = Cooldown.GetFloat();
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
        if (!IsEnable || pc == null)
            return false;

        if (!AbilityActive)
        {
            AbilityActive = true;
            LateTask.New(() => AbilityActive = false, Duration.GetFloat());
        }
        return false;
    }
    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (pc != PlayerControl.LocalPlayer || !AbilityActive || !AmongUsClient.Instance.AmHost) return;

        foreach (var apc in Main.AllAlivePlayerControls)
        {
            if (apc == null || apc.PlayerId == pc.PlayerId) continue;
            if (!Main.Invisible.Contains(apc.PlayerId)) continue;

            float distance = Vector2.Distance(pc.GetTruePosition(), apc.GetTruePosition());

            if (distance <= Range.GetFloat())
            {
                if (!InRangeTimers.ContainsKey(apc.PlayerId))
                    InRangeTimers[apc.PlayerId] = TimeLimit.GetFloat();

                InRangeTimers[apc.PlayerId] -= Time.fixedDeltaTime / Time.timeScale;
                apc.Notify(string.Format(GetString("ScannerWarningInRange"), Mathf.Round(InRangeTimers[apc.PlayerId])), time: 1f, overrideAll: true);

                if (InRangeTimers[apc.PlayerId] <= 0f)
                {
                    apc.RpcMakeVisible();
                    apc.Notify(GetString("ScannerNowVisible"), overrideAll: true);

                    InRangeTimers[apc.PlayerId] = TimeLimit.GetFloat();

                    return;
                }
            }
            else
            {
                InRangeTimers[apc.PlayerId] = TimeLimit.GetFloat();
            }
        }
    }
}