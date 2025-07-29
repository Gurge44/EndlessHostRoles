using System;
using System.Collections.Generic;
using System.Linq;

namespace EHR.Impostor;

public class ClockBlocker : RoleBase
{
    public static bool On;
    private static List<ClockBlocker> Instances = [];

    public override bool IsEnable => On;

    private static OptionItem KillCooldown;
    private static OptionItem EmergencyCooldownIncreasePerKill;
    private static OptionItem MaxEmergencyCooldown;

    private byte ClockBlockerId;

    public override void SetupCustomOption()
    {
        StartSetup(653300)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0.5f, 120f, 0.5f))
            .AutoSetupOption(ref EmergencyCooldownIncreasePerKill, 5, new IntegerValueRule(1, 30, 1))
            .AutoSetupOption(ref MaxEmergencyCooldown, 90, new IntegerValueRule(5, 300, 5));
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        ClockBlockerId = playerId;
        Instances.Add(this);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    private int GetAddedTime()
    {
        return !Main.PlayerStates.TryGetValue(ClockBlockerId, out PlayerState state) ? 0 : EmergencyCooldownIncreasePerKill.GetInt() * state.GetKillCount(true);
    }

    public static int GetTotalTime(int originalTime)
    {
        int maxTime = MaxEmergencyCooldown.GetInt();
        return originalTime >= maxTime ? originalTime : Math.Min(maxTime, originalTime + Instances.Sum(x => x.GetAddedTime()));
    }
}