using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Roles;

public class Obstructer : RoleBase
{
    public static bool On;

    private static OptionItem SpeedReductionPerSecond;
    private static OptionItem DecreasedKillCooldown;

    public override bool IsEnable => On;

    private bool DidCauseSabotage;

    public override void SetupCustomOption()
    {
        StartSetup(658200)
            .AutoSetupOption(ref SpeedReductionPerSecond, 0.03f, new FloatValueRule(0.01f, 0.5f, 0.01f), OptionFormat.Multiplier)
            .AutoSetupOption(ref DecreasedKillCooldown, 15f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        DidCauseSabotage = false;
    }

    public override void SetKillCooldown(byte id)
    {
        if (DidCauseSabotage)
            Main.AllPlayerKillCooldown[id] = DecreasedKillCooldown.GetFloat();
        else
            base.SetKillCooldown(id);
    }

    public override bool OnSabotage(PlayerControl pc)
    {
        DidCauseSabotage = true;
        pc.MarkDirtySettings();
        Main.Instance.StartCoroutine(Coroutine());
        return true;
    }

    private System.Collections.IEnumerator Coroutine()
    {
        do
        {
            yield return new WaitForSecondsRealtime(1f);

            if (ReportDeadBodyPatch.MeetingStarted || GameStates.IsEnded || !GameStates.IsInTask) break;

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                if (!Main.AllPlayerSpeed.ContainsKey(pc.PlayerId)) continue;
                Main.AllPlayerSpeed[pc.PlayerId] -= SpeedReductionPerSecond.GetFloat();
                pc.MarkDirtySettings();
            }
        } while (Utils.IsAnySabotageActive());

        DidCauseSabotage = false;
        Main.AllPlayerSpeed.SetAllValues(Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
        
        if (ReportDeadBodyPatch.MeetingStarted || GameStates.IsEnded || !GameStates.IsInTask) yield break;
        
        Utils.MarkEveryoneDirtySettings();
    }
}