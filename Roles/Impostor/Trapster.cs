using System.Collections.Generic;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Impostor;

internal class Trapster : RoleBase
{
    private static List<byte> TrapsterBody = [];
    private static Dictionary<byte, byte> KillerOfTrapsterBody = [];

    public static bool On;

    private static OptionItem TrapsterKillCooldown;
    private static OptionItem TrapOnlyWorksOnTheBodyTrapster;
    private static OptionItem TrapConsecutiveBodies;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(16500, TabGroup.ImpostorRoles, CustomRoles.Trapster);

        TrapsterKillCooldown = new FloatOptionItem(16510, "KillCooldown", new(2.5f, 180f, 0.5f), 20f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Trapster])
            .SetValueFormat(OptionFormat.Seconds);

        TrapOnlyWorksOnTheBodyTrapster = new BooleanOptionItem(16511, "TrapOnlyWorksOnTheBodyTrapster", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Trapster]);

        TrapConsecutiveBodies = new BooleanOptionItem(16512, "TrapConsecutiveBodies", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Trapster]);
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
        TrapsterBody = [];
        KillerOfTrapsterBody = [];
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = TrapsterKillCooldown.GetFloat();
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (TrapOnlyWorksOnTheBodyTrapster.GetBool() && !GameStates.IsMeeting)
            TrapsterBody.Add(target.PlayerId);

        return true;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (!TrapOnlyWorksOnTheBodyTrapster.GetBool() && killer != target)
        {
            if (!TrapsterBody.Contains(target.PlayerId))
                TrapsterBody.Add(target.PlayerId);

            KillerOfTrapsterBody.TryAdd(target.PlayerId, killer.PlayerId);

            killer.Suicide();
        }
    }

    public static bool OnAnyoneCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (TrapsterBody.Contains(target.PlayerId) && reporter.IsAlive() && !target.Object.Is(CustomRoles.Unreportable))
        {
            if (!TrapOnlyWorksOnTheBodyTrapster.GetBool())
            {
                byte killerID = KillerOfTrapsterBody[target.PlayerId];

                reporter.Suicide(PlayerState.DeathReason.Trapped, Utils.GetPlayerById(killerID));
                RPC.PlaySoundRPC(killerID, Sounds.KillSound);

                if (!TrapsterBody.Contains(reporter.PlayerId) && TrapConsecutiveBodies.GetBool())
                    TrapsterBody.Add(reporter.PlayerId);

                KillerOfTrapsterBody.TryAdd(reporter.PlayerId, killerID);
                return false;
            }

            byte killerID2 = target.PlayerId;

            reporter.Suicide(PlayerState.DeathReason.Trapped, Utils.GetPlayerById(killerID2));
            RPC.PlaySoundRPC(killerID2, Sounds.KillSound);
            return false;
        }

        return true;
    }
}