using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.AddOns.GhostRoles;

internal class Minion : IGhostRole
{
    public static HashSet<byte> BlindPlayers = [];

    private static OptionItem BlindDuration;
    private static OptionItem CD;

    public Team Team => Team.Impostor;
    public RoleTypes RoleTypes => RoleTypes.GuardianAngel;
    public int Cooldown => CD.GetInt();

    public void OnProtect(PlayerControl pc, PlayerControl target)
    {
        if (!BlindPlayers.Add(target.PlayerId)) return;

        target.RPCPlayCustomSound("FlashBang");
        target.MarkDirtySettings();

        LateTask.New(() =>
        {
            if (BlindPlayers.Remove(target.PlayerId)) target.MarkDirtySettings();
            RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
        }, BlindDuration.GetFloat(), "Remove Minion Blindness");
    }

    public void OnAssign(PlayerControl pc) { }

    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(649000, TabGroup.OtherRoles, CustomRoles.Minion);

        BlindDuration = new IntegerOptionItem(649002, "MinionBlindDuration", new(1, 90, 1), 5, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Minion])
            .SetValueFormat(OptionFormat.Seconds);

        CD = new IntegerOptionItem(649003, "AbilityCooldown", new(0, 120, 1), 30, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Minion])
            .SetValueFormat(OptionFormat.Seconds);
    }
}
