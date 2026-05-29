using System.Collections.Generic;

namespace EHR.Roles;

public class Framer : RoleBase
{
    public static bool On;
    public static readonly HashSet<byte> FramedPlayers = [];

    private static OptionItem KillCooldown;
    private static OptionItem AbilityUseLimit;
    public static OptionItem FramerAbilityUseGainWithEachKill;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(647195, TabGroup.ImpostorRoles, CustomRoles.Framer);
        
        KillCooldown = new FloatOptionItem(647197, "KillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Framer])
            .SetValueFormat(OptionFormat.Seconds);

        AbilityUseLimit = new FloatOptionItem(647198, "AbilityUseLimit", new(0, 20, 0.05f), 0, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Framer])
            .SetValueFormat(OptionFormat.Times);

        FramerAbilityUseGainWithEachKill = new FloatOptionItem(647199, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.5f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Framer])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        FramedPlayers.Clear();
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1f) return true;

        return killer.CheckDoubleTrigger(target, () =>
        {
            FramedPlayers.Add(target.PlayerId);
            killer.SetKillCooldown();
            killer.Notify(string.Format(Translator.GetString("Framer.TargetFramedNotify"), target.PlayerId.ColoredPlayerName()));
            killer.RpcRemoveAbilityUse();
        });
    }
}