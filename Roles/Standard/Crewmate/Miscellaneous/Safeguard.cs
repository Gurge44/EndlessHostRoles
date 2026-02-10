using System;
using EHR.Modules.Extensions;

namespace EHR.Roles;

public class Safeguard : RoleBase
{
    public static bool On;

    private static OptionItem ShieldDuration;
    private static OptionItem MinTasks;

    private byte SafeguardId;
    private CountdownTimer Timer;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        const TabGroup tab = TabGroup.CrewmateRoles;
        const CustomRoles role = CustomRoles.Safeguard;
        var id = 645500;

        Options.SetupRoleOptions(id++, tab, role);

        ShieldDuration = new FloatOptionItem(++id, "AidDur", new(0.5f, 60f, 0.5f), 5f, tab)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Seconds);

        MinTasks = new IntegerOptionItem(++id, "MinTasksToActivateAbility", new(1, 10, 1), 3, tab)
            .SetParent(Options.CustomRoleSpawnChances[role]);

        Options.OverrideTasksData.Create(++id, tab, role);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Timer = null;
        SafeguardId = playerId;
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (!pc.IsAlive()) return;

        if (completedTaskCount + 1 >= MinTasks.GetInt())
        {
            bool shielded = Timer != null;
            Timer = new CountdownTimer(ShieldDuration.GetFloat() + (shielded ? (float)Timer.Remaining.TotalSeconds : 0), () =>
            {
                Timer = null;
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }, onCanceled: () => Timer = null);
            if (!shielded) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return Timer == null;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != SafeguardId || meeting || (seer.IsModdedClient() && !hud) || Timer == null) return string.Empty;
        return seer.IsHost() ? string.Format(Translator.GetString("SafeguardSuffixTimer"), (int)Math.Ceiling(Timer.Remaining.TotalSeconds)) : Translator.GetString("SafeguardSuffix");
    }
}