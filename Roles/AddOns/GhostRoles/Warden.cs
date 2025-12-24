using System;
using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.AddOns.GhostRoles;

internal class Warden : IGhostRole
{
    private static OptionItem ExtraSpeed;
    private static OptionItem ExtraSpeedDuration;
    private static OptionItem NotifyTarget;
    private static OptionItem CD;

    private readonly Dictionary<byte, (long StartTimeStamp, float OriginalSpeed)> SpeedList = [];

    public Team Team => Team.Crewmate;
    public RoleTypes RoleTypes => RoleTypes.GuardianAngel;
    public int Cooldown => CD.GetInt();

    public void OnProtect(PlayerControl pc, PlayerControl target)
    {
        if (SpeedList.ContainsKey(target.PlayerId)) return;

        float speed = Main.AllPlayerSpeed[target.PlayerId];
        float targetSpeed = speed + ExtraSpeed.GetFloat();
        if (Math.Abs(speed - targetSpeed) < 0.1f || speed > targetSpeed) return;

        Main.AllPlayerSpeed[target.PlayerId] += ExtraSpeed.GetFloat();
        target.MarkDirtySettings();
        if (NotifyTarget.GetBool()) target.Notify(Translator.GetString("WardenNotify"));

        SpeedList[target.PlayerId] = (Utils.TimeStamp, speed);
    }

    public void OnAssign(PlayerControl pc) { }

    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(649200, TabGroup.OtherRoles, CustomRoles.Warden);

        ExtraSpeedDuration = new IntegerOptionItem(649202, "ExpressSpeedDur", new(1, 90, 1), 5, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden])
            .SetValueFormat(OptionFormat.Seconds);

        ExtraSpeed = new FloatOptionItem(649203, "WardenAdditionalSpeed", new(0f, 3f, 0.05f), 0.25f, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden])
            .SetValueFormat(OptionFormat.Multiplier);

        NotifyTarget = new BooleanOptionItem(649204, "WardenNotifyTarget", true, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden]);

        CD = new IntegerOptionItem(649205, "AbilityCooldown", new(0, 120, 1), 30, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public void Update(PlayerControl pc)
    {
        SpeedList.DoIf(x => x.Value.StartTimeStamp + ExtraSpeedDuration.GetInt() <= Utils.TimeStamp, x =>
        {
            Main.AllPlayerSpeed[x.Key] = x.Value.OriginalSpeed;
            pc.MarkDirtySettings();
            SpeedList.Remove(x.Key);
        }, false);
    }
}